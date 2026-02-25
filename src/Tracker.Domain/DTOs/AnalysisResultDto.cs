namespace Tracker.Domain.DTOs;

public record AnalysisResultDto(
    Guid AnalysisId,
    Guid JobId,
    Guid ResumeId,
    string Status,
    string GapAnalysisMode,
    bool UsedGapLlmFallback,
    decimal CoverageScore,
    decimal GroundednessScore,
    string? RequiredSkillsJson,
    string? MissingRequiredJson,
    string? MissingPreferredJson,
    int InputTokens,
    int OutputTokens,
    int LatencyMs,
    string Provider,
    string ExecutionMode,
    DateTimeOffset CreatedAt
);

public record AnalysisDetailDto(
    Guid Id,
    Guid JobId,
    Guid ResumeId,
    string Status,
    decimal CoverageScore,
    decimal GroundednessScore,
    List<SkillMatchDto> Matches,
    List<SkillDto> MissingRequired,
    List<SkillDto> MissingPreferred,
    int InputTokens,
    int OutputTokens,
    int LatencyMs,
    DateTimeOffset CreatedAt
);

public record SkillMatchDto(
    string SkillName,
    string JdEvidence,
    string ResumeEvidence,
    bool IsRequired
);

public record SkillDto(
    string SkillName,
    string EvidenceQuote
);
