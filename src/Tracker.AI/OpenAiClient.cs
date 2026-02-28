using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Tracker.AI;

/// <summary>
/// OpenAI implementation of ILlmClient with Polly resilience.
/// </summary>
public class OpenAiClient : ILlmClient
{
    private readonly OpenAIClient _client;
    private readonly string _providerName;
    private readonly string _chatModel;
    private readonly string _embeddingModel;
    private readonly int _maxInputTokens;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly IAsyncPolicy _resiliencePolicy;

    public OpenAiClient(
        OpenAIClient client,
        ILogger<OpenAiClient> logger,
        IAsyncPolicy resiliencePolicy,
        string providerName = "openai",
        string chatModel = "",
        string embeddingModel = "text-embedding-3-small",
        int maxInputTokens = 8192)
    {
        if (string.IsNullOrWhiteSpace(chatModel))
        {
            throw new ArgumentException("Chat model is required.", nameof(chatModel));
        }

        _client = client;
        _providerName = providerName;
        _chatModel = chatModel;
        _embeddingModel = embeddingModel;
        _maxInputTokens = Math.Max(1, maxInputTokens);
        _logger = logger;
        _resiliencePolicy = resiliencePolicy;
    }

    public async Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        string? providerOverride = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var sw = Stopwatch.StartNew();
        var providerName = string.IsNullOrWhiteSpace(providerOverride)
            ? _providerName
            : providerOverride.Trim().ToLowerInvariant();
        var estimatedInputTokens = CountTokens(systemPrompt) + CountTokens(userPrompt);
        if (estimatedInputTokens > _maxInputTokens)
        {
            throw new LlmException(
                $"Input exceeds configured context window for provider '{providerName}' ({estimatedInputTokens} > {_maxInputTokens} tokens).",
                400,
                "context_window_exceeded");
        }

        var responseFormat = BuildResponseFormat<T>();
        var options = new ChatCompletionOptions
        {
            ResponseFormat = responseFormat,
            Temperature = 0.1f,
            MaxOutputTokenCount = 4096
        };

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        var repairAttempted = false;
        var initialParseSuccess = true;

        try
        {
            var completion = await _resiliencePolicy.ExecuteAsync(async ct =>
                await _client.GetChatClient(_chatModel).CompleteChatAsync(messages, options, ct),
                cancellationToken);
            var content = ExtractContent(completion.Value);

            totalInputTokens += completion.Value.Usage.InputTokenCount;
            totalOutputTokens += completion.Value.Usage.OutputTokenCount;

            var parseSuccess = TryDeserialize<T>(content, out var value);
            initialParseSuccess = parseSuccess;

            if (!parseSuccess)
            {
                repairAttempted = true;
                _logger.LogWarning(
                    "Initial structured parse failed. Attempting one JSON repair pass. Initial response snippet: {Snippet}",
                    FormatSnippet(content));

                var repairMessages = new List<ChatMessage>
                {
                    new SystemChatMessage("""
                        You repair JSON outputs. Return ONLY valid JSON.
                        Do not include markdown, prose, comments, or code fences.
                        Preserve the intended schema and snake_case keys.
                        """),
                    new UserChatMessage($"""
                        Original system prompt:
                        {systemPrompt}

                        Original user prompt:
                        {userPrompt}

                        Invalid JSON output:
                        {content}

                        Return corrected JSON only.
                        """)
                };

                var repairCompletion = await _resiliencePolicy.ExecuteAsync(async ct =>
                    await _client.GetChatClient(_chatModel).CompleteChatAsync(repairMessages, options, ct),
                    cancellationToken);
                content = ExtractContent(repairCompletion.Value);
                totalInputTokens += repairCompletion.Value.Usage.InputTokenCount;
                totalOutputTokens += repairCompletion.Value.Usage.OutputTokenCount;

                parseSuccess = TryDeserialize<T>(content, out value);

                if (!parseSuccess || value is null)
                {
                    _logger.LogError(
                        "Failed to parse structured output after repair attempt. Latest response snippet: {Snippet}",
                        FormatSnippet(content));
                    throw new LlmException("Failed to parse structured output after repair attempt.");
                }
            }

            sw.Stop();

            return new LlmResult<T>
            {
                Value = value!,
                Usage = new LlmUsage
                {
                    InputTokens = totalInputTokens,
                    OutputTokens = totalOutputTokens
                },
                Provider = providerName,
                Model = _chatModel,
                LatencyMs = (int)sw.ElapsedMilliseconds,
                ParseSuccess = initialParseSuccess,
                RepairAttempted = repairAttempted,
                RawResponse = content
            };
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "LLM circuit breaker is open");
            throw new LlmException("LLM provider circuit breaker is open. Please retry shortly.", 503, "llm_circuit_open");
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "LLM request timed out");
            throw new LlmException("LLM request timed out.", 504, "llm_timeout");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "LLM request timed out");
            throw new LlmException("LLM request timed out.", 504, "llm_timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM API call failed");
            throw new LlmException($"LLM API call failed: {ex.Message}", ex);
        }
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        string? providerOverride = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _resiliencePolicy.ExecuteAsync(async ct =>
                await _client.GetEmbeddingClient(_embeddingModel)
                    .GenerateEmbeddingAsync(text, cancellationToken: ct),
                cancellationToken);
            
            return response.Value.ToFloats().ToArray();
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Embedding request blocked by circuit breaker");
            throw new LlmException("LLM provider circuit breaker is open. Please retry shortly.", 503, "llm_circuit_open");
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(ex, "Embedding request timed out");
            throw new LlmException("Embedding request timed out.", 504, "llm_timeout");
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Embedding request timed out");
            throw new LlmException("Embedding request timed out.", 504, "llm_timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Embedding API call failed");
            throw new LlmException($"Embedding API call failed: {ex.Message}", ex);
        }
    }

    public int CountTokens(string text)
    {
        return text.Length / 4;
    }

    private static string ExtractContent(ChatCompletion completion)
    {
        if (completion.Content.Count == 0)
        {
            return string.Empty;
        }

        return string.Concat(completion.Content.Select(c => c.Text));
    }

    private static ChatResponseFormat BuildResponseFormat<T>() where T : class
    {
        var schema = StructuredOutputSchemas.GetForType<T>();
        if (schema is null)
        {
            return ChatResponseFormat.CreateJsonObjectFormat();
        }

        return ChatResponseFormat.CreateJsonSchemaFormat(
            schema.Name,
            BinaryData.FromString(schema.SchemaJson),
            schema.Description,
            jsonSchemaIsStrict: true);
    }

    private bool TryDeserialize<T>(string content, out T? value) where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            if (value is null)
            {
                _logger.LogWarning("Deserialization returned null for response");
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM response as JSON");
            value = default;
            return false;
        }
    }

    private static string FormatSnippet(string content)
    {
        const int maxLength = 512;
        if (string.IsNullOrEmpty(content))
        {
            return "<empty response>";
        }

        return content.Length <= maxLength
            ? content
            : content.Substring(0, maxLength) + "...";
    }
}
