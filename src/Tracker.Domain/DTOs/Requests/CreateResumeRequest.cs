using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record CreateResumeRequest
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }
    
    [Required]
    public required string Content { get; init; }

    public decimal? DesiredSalaryMin { get; init; }
    public decimal? DesiredSalaryMax { get; init; }
    public string? SalaryCurrency { get; init; }
    public bool IsTestData { get; init; }
}
