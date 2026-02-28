namespace Tracker.Domain.DTOs;

public record JobApplicationEventDto(
    Guid Id,
    Guid JobApplicationId,
    string EventType,
    DateTimeOffset EventAt,
    string? Notes,
    string? Channel,
    bool PositiveOutcome,
    DateTimeOffset CreatedAt
);

public record JobApplicationDto(
    Guid Id,
    Guid JobId,
    Guid ResumeId,
    string JobTitle,
    string Company,
    string ResumeName,
    string Status,
    DateTimeOffset AppliedAt,
    DateTimeOffset? ClosedAt,
    string? ApplicationUrl,
    string? Notes,
    bool IsTestData,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    List<JobApplicationEventDto> Events
);
