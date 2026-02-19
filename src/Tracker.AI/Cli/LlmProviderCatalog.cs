namespace Tracker.AI.Cli;

public static class LlmProviderCatalog
{
    public const string Claude = "claude";
    public const string Codex = "codex";
    public const string Gemini = "gemini";
    public const string Qwen = "qwen";
    public const string Kilocode = "kilocode";
    public const string Opencode = "opencode";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Claude,
        Codex,
        Gemini,
        Qwen,
        Kilocode,
        Opencode
    };

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Allowed.Contains(value);

    public static string NormalizeOrThrow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LlmException("Provider is required.", 400, "invalid_provider");
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (!Allowed.Contains(normalized))
        {
            throw new LlmException(
                $"Unsupported provider '{value}'. Allowed: {string.Join(", ", Allowed.OrderBy(x => x))}.",
                400,
                "invalid_provider");
        }

        return normalized;
    }

    public static IReadOnlyCollection<string> List() => Allowed;
}
