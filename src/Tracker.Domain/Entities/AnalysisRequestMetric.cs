namespace Tracker.Domain.Entities;

public class AnalysisRequestMetric
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ResumeId { get; set; }
    public required string JobHash { get; set; }
    public required string ResumeHash { get; set; }
    public bool CacheHit { get; set; }
    public required string RequestMode { get; set; }
    public required string Outcome { get; set; }
    public bool UsedGapLlmFallback { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int LatencyMs { get; set; }
    public string? Provider { get; set; }
    public string? ErrorCategory { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
