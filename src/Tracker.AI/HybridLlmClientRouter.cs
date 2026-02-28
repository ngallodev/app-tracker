using Microsoft.Extensions.Options;
using Tracker.AI.Cli;

namespace Tracker.AI;

public sealed class HybridLlmClientRouter : ILlmClient
{
    private readonly CliLlmClientRouter _cliRouter;
    private readonly OpenAiClient _lmStudioClient;
    private readonly IOptionsMonitor<LlmCliOptions> _llmOptions;
    private readonly IOptionsMonitor<LmStudioOptions> _lmStudioOptions;

    public HybridLlmClientRouter(
        CliLlmClientRouter cliRouter,
        OpenAiClient lmStudioClient,
        IOptionsMonitor<LlmCliOptions> llmOptions,
        IOptionsMonitor<LmStudioOptions> lmStudioOptions)
    {
        _cliRouter = cliRouter;
        _lmStudioClient = lmStudioClient;
        _llmOptions = llmOptions;
        _lmStudioOptions = lmStudioOptions;
    }

    public Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        string? providerOverride = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var provider = ResolveProvider(providerOverride);
        if (provider == LlmProviderCatalog.Lmstudio)
        {
            EnsureLmStudioEnabled();
            return _lmStudioClient.CompleteStructuredAsync<T>(systemPrompt, userPrompt, provider, cancellationToken);
        }

        return _cliRouter.CompleteStructuredAsync<T>(systemPrompt, userPrompt, provider, cancellationToken);
    }

    public Task<float[]> GetEmbeddingAsync(
        string text,
        string? providerOverride = null,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(providerOverride);
        if (provider == LlmProviderCatalog.Lmstudio)
        {
            EnsureLmStudioEnabled();
            return _lmStudioClient.GetEmbeddingAsync(text, provider, cancellationToken);
        }

        return _cliRouter.GetEmbeddingAsync(text, provider, cancellationToken);
    }

    public int CountTokens(string text) => Math.Max(0, text.Length / 4);

    private string ResolveProvider(string? providerOverride)
    {
        var configuredDefault = _llmOptions.CurrentValue.DefaultProvider;
        var chosen = string.IsNullOrWhiteSpace(providerOverride) ? configuredDefault : providerOverride;
        return LlmProviderCatalog.NormalizeOrThrow(chosen);
    }

    private void EnsureLmStudioEnabled()
    {
        if (_lmStudioOptions.CurrentValue.Enabled)
        {
            return;
        }

        throw new LlmException(
            "Provider 'lmstudio' is disabled. Set Llm:LmStudio:Enabled=true.",
            503,
            "provider_unavailable");
    }
}
