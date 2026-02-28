namespace Tracker.AI;

public sealed class LmStudioOptions
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "http://127.0.0.1:1234/v1";
    public string? ApiKeyFile { get; set; }
    public string ApiKey { get; set; } = "lm-studio";
    public string ChatModel { get; set; } = "neuraldaredevil-8b-abliterated";
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int MaxInputTokens { get; set; } = 8192;
}
