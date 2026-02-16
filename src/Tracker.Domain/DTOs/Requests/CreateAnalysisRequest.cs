using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record CreateAnalysisRequest
{
    [Required]
    public required Guid JobId { get; init; }
    
    [Required]
    public required Guid ResumeId { get; init; }
}