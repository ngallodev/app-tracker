using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record CreateJobRequest : IValidatableObject
{
    [StringLength(200)]
    public string? Title { get; init; }
    
    [StringLength(200)]
    public string? Company { get; init; }
    
    public string? DescriptionText { get; init; }
    public string? SourceUrl { get; init; }
    public bool IsTestData { get; init; }
    public string? WorkType { get; init; }
    public string? EmploymentType { get; init; }
    public decimal? SalaryMin { get; init; }
    public decimal? SalaryMax { get; init; }
    public string? SalaryCurrency { get; init; }
    public string? RecruiterName { get; init; }
    public string? RecruiterEmail { get; init; }
    public string? RecruiterPhone { get; init; }
    public string? RecruiterLinkedIn { get; init; }
    public string? CompanyCareersUrl { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasSourceUrl = !string.IsNullOrWhiteSpace(SourceUrl);
        if (!hasSourceUrl)
        {
            if (string.IsNullOrWhiteSpace(Title))
            {
                yield return new ValidationResult(
                    "Title is required when sourceUrl is not supplied.",
                    [nameof(Title)]);
            }

            if (string.IsNullOrWhiteSpace(Company))
            {
                yield return new ValidationResult(
                    "Company is required when sourceUrl is not supplied.",
                    [nameof(Company)]);
            }
        }
    }
}
