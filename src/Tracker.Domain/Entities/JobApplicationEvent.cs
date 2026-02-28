using Tracker.Domain.Enums;

namespace Tracker.Domain.Entities;

public class JobApplicationEvent
{
    public Guid Id { get; set; }
    public Guid JobApplicationId { get; set; }
    public JobApplicationEventType EventType { get; set; } = JobApplicationEventType.Note;
    public DateTimeOffset EventAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
    public string? Channel { get; set; }
    public bool PositiveOutcome { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public JobApplication JobApplication { get; set; } = null!;
}
