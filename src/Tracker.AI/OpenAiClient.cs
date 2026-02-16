using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace Tracker.AI;

/// <summary>
/// OpenAI implementation of ILlmClient with Polly resilience.
/// </summary>
public class OpenAiClient : ILlmClient
{
    private readonly OpenAIClient _client;
    private readonly string _chatModel;
    private readonly string _embeddingModel;
    private readonly ILogger<OpenAiClient> _logger;

    public OpenAiClient(
        OpenAIClient client,
        ILogger<OpenAiClient> logger,
        string chatModel = "gpt-4o-mini",
        string embeddingModel = "text-embedding-3-small")
    {
        _client = client;
        _chatModel = chatModel;
        _embeddingModel = embeddingModel;
        _logger = logger;
    }

    public async Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) where T : class
    {
        var sw = Stopwatch.StartNew();
        var options = new ChatCompletionOptions
        {
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat(),
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
            var completion = await _client.GetChatClient(_chatModel)
                .CompleteChatAsync(messages, options, cancellationToken);
            var content = ExtractContent(completion.Value);

            totalInputTokens += completion.Value.Usage.InputTokenCount;
            totalOutputTokens += completion.Value.Usage.OutputTokenCount;

            var parseSuccess = TryDeserialize<T>(content, out var value);
            initialParseSuccess = parseSuccess;

            if (!parseSuccess)
            {
                repairAttempted = true;
                _logger.LogWarning("Initial structured parse failed. Attempting one JSON repair pass.");

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

                var repairCompletion = await _client.GetChatClient(_chatModel)
                    .CompleteChatAsync(repairMessages, options, cancellationToken);
                content = ExtractContent(repairCompletion.Value);
                totalInputTokens += repairCompletion.Value.Usage.InputTokenCount;
                totalOutputTokens += repairCompletion.Value.Usage.OutputTokenCount;

                parseSuccess = TryDeserialize<T>(content, out value);

                if (!parseSuccess || value is null)
                {
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
                Model = _chatModel,
                LatencyMs = (int)sw.ElapsedMilliseconds,
                ParseSuccess = initialParseSuccess,
                RepairAttempted = repairAttempted,
                RawResponse = content
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM API call failed");
            throw new LlmException($"LLM API call failed: {ex.Message}", ex);
        }
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetEmbeddingClient(_embeddingModel)
                .GenerateEmbeddingAsync(text, cancellationToken: cancellationToken);
            
            return response.Value.ToFloats().ToArray();
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
}
