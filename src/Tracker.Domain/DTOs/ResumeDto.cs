namespace Tracker.Domain.DTOs;

public record ResumeDto(
    Guid Id,
    string Name,
    string? ContentPreview,
    decimal? DesiredSalaryMin,
    decimal? DesiredSalaryMax,
    string? SalaryCurrency,
    bool IsTestData,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
