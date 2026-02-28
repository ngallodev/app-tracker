namespace Tracker.AI.Cli;

public static class LlmProviderCatalog
{
    public const string Kilo = "kilo";
    public const string Claude = "claude";
    public const string Codex = "codex";
    public const string Gemini = "gemini";
    public const string Qwen = "qwen";
    public const string Kilocode = "kilocode";
    public const string Opencode = "opencode";
    public const string Lmstudio = "lmstudio";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Claude,
        Codex,
        Gemini,
        Qwen,
        Kilocode,
        Opencode,
        Lmstudio
    };

    public static bool IsSupported(string? value)
    {
        try
        {
            NormalizeOrThrow(value);
            return true;
        }
        catch (LlmException)
        {
            return false;
        }
    }

    public static string NormalizeOrThrow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new LlmException("Provider is required.", 400, "invalid_provider");
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == Kilo)
        {
            normalized = Kilocode;
        }

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

    public static IReadOnlyList<string> GetDefaultCommandCandidates(string provider)
    {
        var normalized = NormalizeOrThrow(provider);
        return normalized switch
        {
            Kilocode => [Kilo, Kilocode],
            _ => [normalized]
        };
    }
}
