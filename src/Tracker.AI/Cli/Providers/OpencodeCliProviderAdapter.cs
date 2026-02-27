using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Tracker.AI.Cli.Providers;

public sealed class OpencodeCliProviderAdapter : CliProviderAdapterBase
{
    public OpencodeCliProviderAdapter(
        IHeadlessCliExecutor executor,
        IOptionsMonitor<LlmCliOptions> optionsMonitor,
        ILogger<OpencodeCliProviderAdapter> logger) : base(executor, optionsMonitor, logger)
    {
    }

    public override string ProviderName => LlmProviderCatalog.Opencode;
}
