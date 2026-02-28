using Microsoft.Extensions.Options;
using Tracker.AI;
using Tracker.AI.Cli;
using Xunit;

namespace Tracker.AI.Tests;

public sealed class HybridLlmClientRouterTests
{
    [Fact]
    public async Task CompleteStructuredAsync_WhenLmstudioDisabled_ThrowsProviderUnavailable()
    {
        var llmOptions = new LlmCliOptions
        {
            DefaultProvider = LlmProviderCatalog.Lmstudio
        };
        var lmStudioOptions = new LmStudioOptions
        {
            Enabled = false
        };

        var router = new HybridLlmClientRouter(
            cliRouter: null!,
            lmStudioClient: null!,
            llmOptions: new StaticOptionsMonitor<LlmCliOptions>(llmOptions),
            lmStudioOptions: new StaticOptionsMonitor<LmStudioOptions>(lmStudioOptions));

        var ex = await Assert.ThrowsAsync<LlmException>(() =>
            router.CompleteStructuredAsync<DummyResponse>("sys", "user"));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal("provider_unavailable", ex.ErrorCode);
        Assert.Contains("disabled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DummyResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue => currentValue;

        public T Get(string? name) => currentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
