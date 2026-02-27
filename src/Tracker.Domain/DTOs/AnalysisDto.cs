using Tracker.Domain.Enums;

namespace Tracker.Domain.DTOs;

public record AnalysisDto(
    Guid Id,
    Guid JobId,
    Guid ResumeId,
    AnalysisStatus Status,
    string? Model,
    int InputTokens,
    int OutputTokens,
    int LatencyMs,
    DateTimeOffset CreatedAt
);