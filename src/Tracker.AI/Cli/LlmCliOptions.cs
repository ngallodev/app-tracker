namespace Tracker.AI.Cli;

public sealed class LlmCliOptions
{
    public string DefaultProvider { get; set; } = LlmProviderCatalog.Claude;
    public Dictionary<string, CliProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CliProviderOptions
{
    public bool Enabled { get; set; } = true;
    public string? Command { get; set; }
    public string? Arguments { get; set; }
    public List<string> ExtraFlags { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 60;
}
