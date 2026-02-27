using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Tracker.AI.Cli.Providers;

public sealed class GeminiCliProviderAdapter : CliProviderAdapterBase
{
    public GeminiCliProviderAdapter(
        IHeadlessCliExecutor executor,
        IOptionsMonitor<LlmCliOptions> optionsMonitor,
        ILogger<GeminiCliProviderAdapter> logger) : base(executor, optionsMonitor, logger)
    {
    }

    public override string ProviderName => LlmProviderCatalog.Gemini;
}
