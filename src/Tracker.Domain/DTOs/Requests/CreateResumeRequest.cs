using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record CreateResumeRequest
{
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }
    
    [Required]
    public required string Content { get; init; }
}