using System.Text.Json.Serialization;

namespace Tracker.AI.Models;

/// <summary>
/// Result of comparing JD requirements against a resume.
/// </summary>
public record GapAnalysis
{
    [JsonPropertyName("matches")]
    public List<SkillMatch> Matches { get; init; } = [];
    
    [JsonPropertyName("missing_required")]
    public List<SkillWithEvidence> MissingRequired { get; init; } = [];
    
    [JsonPropertyName("missing_preferred")]
    public List<SkillWithEvidence> MissingPreferred { get; init; } = [];
    
    [JsonPropertyName("experience_gaps")]
    public List<ExperienceGap> ExperienceGaps { get; init; } = [];
}

/// <summary>
/// A skill that was found in the resume with evidence.
/// </summary>
public record SkillMatch
{
    [JsonPropertyName("skill_name")]
    public required string SkillName { get; init; }
    
    [JsonPropertyName("jd_evidence")]
    public required string JdEvidence { get; init; }
    
    [JsonPropertyName("resume_evidence")]
    public required string ResumeEvidence { get; init; }
    
    [JsonPropertyName("is_required")]
    public bool IsRequired { get; init; }
}

/// <summary>
/// An experience gap (e.g., years of experience).
/// </summary>
public record ExperienceGap
{
    [JsonPropertyName("requirement")]
    public required string Requirement { get; init; }
    
    [JsonPropertyName("jd_evidence")]
    public required string JdEvidence { get; init; }
    
    [JsonPropertyName("gap_description")]
    public required string GapDescription { get; init; }
}
