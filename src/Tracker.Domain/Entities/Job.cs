namespace Tracker.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Company { get; set; }
    public string? DescriptionText { get; set; }
    public string? DescriptionHash { get; set; }
    public string? SourceUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public ICollection<Analysis> Analyses { get; set; } = [];
}