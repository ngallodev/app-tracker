using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

public class Analysis
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ResumeId { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    
    // Model tracking
    public string? Model { get; set; }
    public string? PromptVersion { get; set; }
    public string? SchemaVersion { get; set; }
    
    // Token tracking
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int LatencyMs { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public Job Job { get; set; } = null!;
    public Resume Resume { get; set; } = null!;
    public AnalysisResult? Result { get; set; }
    public ICollection<LlmLog> Logs { get; set; } = [];
}