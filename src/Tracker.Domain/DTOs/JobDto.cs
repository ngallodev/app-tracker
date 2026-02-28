namespace Tracker.Domain.DTOs;

public record JobDto(
    Guid Id,
    string Title,
    string Company,
    string? DescriptionText,
    string? SourceUrl,
    string? WorkType,
    string? EmploymentType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string? RecruiterName,
    string? RecruiterEmail,
    string? RecruiterPhone,
    string? RecruiterLinkedIn,
    string? CompanyCareersUrl,
    bool IsTestData,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
