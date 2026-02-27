namespace Tracker.AI.Cli;

public interface IHeadlessCliExecutor
{
    ProviderAvailability CheckAvailability(string provider, CliProviderOptions options);

    Task<CliExecutionResult> ExecuteAsync(
        string provider,
        CliProviderOptions options,
        string input,
        CancellationToken cancellationToken = default);
}
