using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record UpdateResumeRequest
{
    [StringLength(100)]
    public string? Name { get; init; }
    
    public string? Content { get; init; }
}