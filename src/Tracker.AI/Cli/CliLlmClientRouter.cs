using Microsoft.Extensions.Options;

namespace Tracker.AI.Cli;

public sealed class CliLlmClientRouter : ILlmClient
{
    private readonly IReadOnlyDictionary<string, ICliProviderAdapter> _adapters;
    private readonly IOptionsMonitor<LlmCliOptions> _options;

    public CliLlmClientRouter(
        IEnumerable<ICliProviderAdapter> adapters,
        IOptionsMonitor<LlmCliOptions> options)
    {
        _adapters = adapters.ToDictionary(a => a.ProviderName, StringComparer.OrdinalIgnoreCase);
        _options = options;
    }

    public async Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        string? providerOverride = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var provider = ResolveProvider(providerOverride);
        var adapter = ResolveAdapter(provider);
        return await adapter.CompleteStructuredAsync<T>(systemPrompt, userPrompt, cancellationToken);
    }

    public Task<float[]> GetEmbeddingAsync(
        string text,
        string? providerOverride = null,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(providerOverride);
        throw new LlmException(
            $"Embeddings are not supported in CLI mode for provider '{provider}'.",
            501,
            "embeddings_not_supported_in_cli_mode");
    }

    public int CountTokens(string text) => Math.Max(0, text.Length / 4);

    private string ResolveProvider(string? providerOverride)
    {
        var configuredDefault = _options.CurrentValue.DefaultProvider;
        var chosen = string.IsNullOrWhiteSpace(providerOverride) ? configuredDefault : providerOverride;
        return LlmProviderCatalog.NormalizeOrThrow(chosen);
    }

    private ICliProviderAdapter ResolveAdapter(string provider)
    {
        if (!_adapters.TryGetValue(provider, out var adapter))
        {
            throw new LlmException($"Provider '{provider}' is not registered.", 503, "provider_unavailable");
        }

        var availability = adapter.GetAvailability();
        if (!availability.Available)
        {
            throw new LlmException(
                $"Provider '{provider}' is unavailable ({availability.Message}).",
                503,
                "provider_unavailable");
        }

        return adapter;
    }
}
