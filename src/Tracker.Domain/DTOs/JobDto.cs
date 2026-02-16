namespace Tracker.Domain.DTOs;

public record JobDto(
    Guid Id,
    string Title,
    string Company,
    string? DescriptionText,
    string? SourceUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);