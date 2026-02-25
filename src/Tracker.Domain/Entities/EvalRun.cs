namespace Tracker.Domain.Entities;

public class EvalRun
{
    public Guid Id { get; set; }
    public string Mode { get; set; } = "deterministic";
    public int FixtureCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public decimal SchemaPassRate { get; set; }
    public decimal GroundednessRate { get; set; }
    public decimal CoverageStabilityDiff { get; set; }
    public decimal AvgLatencyMs { get; set; }
    public decimal AvgCostPerRunUsd { get; set; }
    public string ResultsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
