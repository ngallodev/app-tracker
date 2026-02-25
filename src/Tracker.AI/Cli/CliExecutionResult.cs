namespace Tracker.AI.Cli;

public sealed record CliExecutionResult(
    int ExitCode,
    string StdOut,
    string StdErr,
    int LatencyMs,
    string? ResolvedCommandPath
);

public sealed record ProviderAvailability(
    string Provider,
    bool Enabled,
    bool Available,
    string? Command,
    string? ResolvedCommandPath,
    string? Message
);
