namespace Tracker.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Company { get; set; }
    public string? DescriptionText { get; set; }
    public string? DescriptionHash { get; set; }
    public string? SourceUrl { get; set; }
    public string? WorkType { get; set; }
    public string? EmploymentType { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? SalaryCurrency { get; set; }
    public string? RecruiterName { get; set; }
    public string? RecruiterEmail { get; set; }
    public string? RecruiterPhone { get; set; }
    public string? RecruiterLinkedIn { get; set; }
    public string? CompanyCareersUrl { get; set; }
    public bool IsTestData { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    
    // Navigation properties
    public ICollection<Analysis> Analyses { get; set; } = [];
    public ICollection<JobApplication> Applications { get; set; } = [];
}
