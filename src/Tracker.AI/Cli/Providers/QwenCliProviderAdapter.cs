using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Tracker.AI.Cli.Providers;

public sealed class QwenCliProviderAdapter : CliProviderAdapterBase
{
    public QwenCliProviderAdapter(
        IHeadlessCliExecutor executor,
        IOptionsMonitor<LlmCliOptions> optionsMonitor,
        ILogger<QwenCliProviderAdapter> logger) : base(executor, optionsMonitor, logger)
    {
    }

    public override string ProviderName => LlmProviderCatalog.Qwen;
}
