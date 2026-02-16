using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record UpdateJobRequest
{
    [StringLength(200)]
    public string? Title { get; init; }
    
    [StringLength(200)]
    public string? Company { get; init; }
    
    public string? DescriptionText { get; init; }
    public string? SourceUrl { get; init; }
}