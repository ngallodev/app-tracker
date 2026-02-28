using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record CreateJobApplicationRequest
{
    [Required]
    public required Guid JobId { get; init; }

    [Required]
    public required Guid ResumeId { get; init; }

    public DateTimeOffset? AppliedAt { get; init; }
    public string? ApplicationUrl { get; init; }
    public string? Notes { get; init; }
    public bool IsTestData { get; init; }
}

public record UpdateJobApplicationRequest
{
    public string? Status { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public string? ApplicationUrl { get; init; }
    public string? Notes { get; init; }
}

public record AddJobApplicationEventRequest
{
    [Required]
    public required string EventType { get; init; }

    public DateTimeOffset? EventAt { get; init; }
    public string? Notes { get; init; }
    public string? Channel { get; init; }
    public bool PositiveOutcome { get; init; }
}
