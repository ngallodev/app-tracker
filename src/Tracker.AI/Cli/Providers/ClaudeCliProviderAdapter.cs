using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Tracker.AI.Cli.Providers;

public sealed class ClaudeCliProviderAdapter : CliProviderAdapterBase
{
    public ClaudeCliProviderAdapter(
        IHeadlessCliExecutor executor,
        IOptionsMonitor<LlmCliOptions> optionsMonitor,
        ILogger<ClaudeCliProviderAdapter> logger) : base(executor, optionsMonitor, logger)
    {
    }

    public override string ProviderName => LlmProviderCatalog.Claude;
}
