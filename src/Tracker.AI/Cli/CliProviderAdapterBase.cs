using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Tracker.AI.Cli;

public abstract class CliProviderAdapterBase : ICliProviderAdapter
{
    private readonly IHeadlessCliExecutor _executor;
    private readonly IOptionsMonitor<LlmCliOptions> _optionsMonitor;
    private readonly ILogger _logger;

    protected CliProviderAdapterBase(
        IHeadlessCliExecutor executor,
        IOptionsMonitor<LlmCliOptions> optionsMonitor,
        ILogger logger)
    {
        _executor = executor;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public abstract string ProviderName { get; }

    public ProviderAvailability GetAvailability() =>
        _executor.CheckAvailability(ProviderName, GetProviderOptions());

    public async Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) where T : class
    {
        var options = GetProviderOptions();
        var prompt = BuildStructuredPrompt(systemPrompt, userPrompt);
        var execution = await _executor.ExecuteAsync(ProviderName, options, prompt, cancellationToken);

        var parseSuccess = TryDeserialize<T>(execution.StdOut, out var value);
        var repairAttempted = false;
        var finalOutput = execution.StdOut;
        var totalLatency = execution.LatencyMs;
        var totalInputTokens = CountTokens(prompt);
        var totalOutputTokens = CountTokens(execution.StdOut);

        if (!parseSuccess)
        {
            repairAttempted = true;
            var repairPrompt = BuildRepairPrompt(systemPrompt, userPrompt, execution.StdOut);
            var repairExecution = await _executor.ExecuteAsync(ProviderName, options, repairPrompt, cancellationToken);
            totalLatency += repairExecution.LatencyMs;
            totalInputTokens += CountTokens(repairPrompt);
            totalOutputTokens += CountTokens(repairExecution.StdOut);
            finalOutput = repairExecution.StdOut;
            parseSuccess = TryDeserialize<T>(finalOutput, out value);
        }

        if (!parseSuccess || value is null)
        {
            throw new LlmException(
                $"Provider '{ProviderName}' returned invalid structured output after repair attempt.",
                502,
                "invalid_provider_output");
        }

        return new LlmResult<T>
        {
            Value = value,
            Usage = new LlmUsage
            {
                InputTokens = totalInputTokens,
                OutputTokens = totalOutputTokens
            },
            Provider = ProviderName,
            Model = $"{ProviderName}-cli",
            LatencyMs = totalLatency,
            ParseSuccess = !repairAttempted,
            RepairAttempted = repairAttempted,
            RawResponse = finalOutput
        };
    }

    private CliProviderOptions GetProviderOptions()
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.Providers.TryGetValue(ProviderName, out var providerOptions))
        {
            throw new LlmException(
                $"Provider '{ProviderName}' is missing configuration under Llm:Providers:{ProviderName}.",
                503,
                "provider_unavailable");
        }

        return providerOptions;
    }

    private static string BuildStructuredPrompt(string systemPrompt, string userPrompt) =>
        $"""
        SYSTEM INSTRUCTIONS:
        {systemPrompt}

        USER REQUEST:
        {userPrompt}

        RESPONSE CONTRACT:
        Return ONLY one valid JSON object.
        Do not include markdown, code fences, comments, or prose before/after JSON.
        """;

    private static string BuildRepairPrompt(string systemPrompt, string userPrompt, string invalidOutput) =>
        $"""
        You must repair invalid structured output into valid JSON.

        ORIGINAL SYSTEM INSTRUCTIONS:
        {systemPrompt}

        ORIGINAL USER REQUEST:
        {userPrompt}

        INVALID OUTPUT:
        {invalidOutput}

        Return ONLY one valid JSON object with no additional text.
        """;

    private static int CountTokens(string text) => Math.Max(0, text.Length / 4);

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
                _logger.LogWarning("Provider {Provider} deserialized null output.", ProviderName);
                return false;
            }

            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Provider {Provider} returned non-JSON output.", ProviderName);
            value = default;
            return false;
        }
    }
}
