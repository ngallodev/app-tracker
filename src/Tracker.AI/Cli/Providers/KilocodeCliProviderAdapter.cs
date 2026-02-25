using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Tracker.AI.Cli.Providers;

public sealed class KilocodeCliProviderAdapter : CliProviderAdapterBase
{
    public KilocodeCliProviderAdapter(
        IHeadlessCliExecutor executor,
        IOptionsMonitor<LlmCliOptions> optionsMonitor,
        ILogger<KilocodeCliProviderAdapter> logger) : base(executor, optionsMonitor, logger)
    {
    }

    public override string ProviderName => LlmProviderCatalog.Kilocode;
}
