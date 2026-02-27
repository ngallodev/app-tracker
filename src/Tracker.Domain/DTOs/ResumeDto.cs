namespace Tracker.Domain.DTOs;

public record ResumeDto(
    Guid Id,
    string Name,
    string? ContentPreview,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);