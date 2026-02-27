namespace Tracker.Domain.Entities;

public class AnalysisResult
{
    public Guid AnalysisId { get; set; }
    
    // JSON columns for flexible storage
    public string? RequiredSkillsJson { get; set; }
    public string? MissingRequiredJson { get; set; }
    public string? MissingPreferredJson { get; set; }
    
    // Deterministic scores (0-100)
    public decimal CoverageScore { get; set; }
    public decimal GroundednessScore { get; set; }
    
    // Navigation
    public Analysis Analysis { get; set; } = null!;
}