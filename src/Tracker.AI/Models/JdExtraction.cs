using System.Text.Json.Serialization;

namespace Tracker.AI.Models;

/// <summary>
/// Structured extraction from a job description.
/// Every skill must have an evidence quote from the source text.
/// </summary>
public record JdExtraction
{
    [JsonPropertyName("role_title")]
    public required string RoleTitle { get; init; }
    
    [JsonPropertyName("seniority_level")]
    public string? SeniorityLevel { get; init; }
    
    [JsonPropertyName("required_skills")]
    public List<SkillWithEvidence> RequiredSkills { get; init; } = [];
    
    [JsonPropertyName("preferred_skills")]
    public List<SkillWithEvidence> PreferredSkills { get; init; } = [];
    
    [JsonPropertyName("responsibilities")]
    public List<ResponsibilityWithEvidence> Responsibilities { get; init; } = [];
    
    [JsonPropertyName("years_experience")]
    public string? YearsExperience { get; init; }
    
    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; init; } = [];
}

/// <summary>
/// A skill with evidence quote from the source text.
/// </summary>
public record SkillWithEvidence
{
    [JsonPropertyName("skill_name")]
    public required string SkillName { get; init; }
    
    [JsonPropertyName("evidence_quote")]
    public required string EvidenceQuote { get; init; }
    
    [JsonPropertyName("category")]
    public string? Category { get; init; } // "technical", "soft", "tool", "domain"
}

/// <summary>
/// A responsibility with evidence quote.
/// </summary>
public record ResponsibilityWithEvidence
{
    [JsonPropertyName("description")]
    public required string Description { get; init; }
    
    [JsonPropertyName("evidence_quote")]
    public required string EvidenceQuote { get; init; }
}
