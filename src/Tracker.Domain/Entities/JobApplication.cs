using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

public class JobApplication
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid ResumeId { get; set; }
    public JobApplicationStatus Status { get; set; } = JobApplicationStatus.Applied;
    public DateTimeOffset AppliedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ApplicationUrl { get; set; }
    public string? Notes { get; set; }
    public bool IsTestData { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Job Job { get; set; } = null!;
    public Resume Resume { get; set; } = null!;
    public ICollection<JobApplicationEvent> Events { get; set; } = [];
}
