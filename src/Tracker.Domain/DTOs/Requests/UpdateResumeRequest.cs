using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record UpdateResumeRequest
{
    [StringLength(100)]
    public string? Name { get; init; }
    
    public string? Content { get; init; }
    public decimal? DesiredSalaryMin { get; init; }
    public decimal? DesiredSalaryMax { get; init; }
    public string? SalaryCurrency { get; init; }
    public bool? IsTestData { get; init; }
}
