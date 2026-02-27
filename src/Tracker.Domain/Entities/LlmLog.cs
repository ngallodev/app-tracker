namespace Tracker.Domain.Entities;

public class LlmLog
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public required string StepName { get; set; }
    public string? RawResponse { get; set; }
    public bool ParseSuccess { get; set; }
    public bool RepairAttempted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation
    public Analysis Analysis { get; set; } = null!;
}