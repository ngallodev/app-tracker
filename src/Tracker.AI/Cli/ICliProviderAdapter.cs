namespace Tracker.AI.Cli;

public interface ICliProviderAdapter
{
    string ProviderName { get; }
    ProviderAvailability GetAvailability();
    Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) where T : class;
}
