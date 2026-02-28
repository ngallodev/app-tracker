using System.ComponentModel.DataAnnotations;

namespace Tracker.Domain.DTOs.Requests;

public record ImportJobFromUrlRequest
{
    [Required]
    public required string SourceUrl { get; init; }
}
