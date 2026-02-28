namespace Tracker.Domain.Entities;

public class Resume
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Content { get; set; }
    public string? ContentHash { get; set; }
    public decimal? DesiredSalaryMin { get; set; }
    public decimal? DesiredSalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public bool IsTestData { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public ICollection<Analysis> Analyses { get; set; } = [];
    public ICollection<JobApplication> Applications { get; set; } = [];
}
