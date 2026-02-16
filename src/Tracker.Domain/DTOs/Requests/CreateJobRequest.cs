using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record CreateJobRequest
{
    [Required]
    [StringLength(200)]
    public required string Title { get; init; }
    
    [Required]
    [StringLength(200)]
    public required string Company { get; init; }
    
    public string? DescriptionText { get; init; }
    public string? SourceUrl { get; init; }
}