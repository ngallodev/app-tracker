using System.Text.Json.Serialization;

namespace Tracker.AI;

/// <summary>
/// Abstraction for LLM operations. Use KeyedServices for multi-provider support.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Get a structured completion with JSON schema validation.
    /// </summary>
    Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) where T : class;
    
    /// <summary>
    /// Get text embedding for semantic similarity.
    /// </summary>
    Task<float[]> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count tokens in text (approximate).
    /// </summary>
    int CountTokens(string text);
}

/// <summary>
/// Result of an LLM call with metadata.
/// </summary>
public record LlmResult<T>
{
    public required T Value { get; init; }
    public required LlmUsage Usage { get; init; }
    public required string Model { get; init; }
    public required int LatencyMs { get; init; }
    public bool ParseSuccess { get; init; } = true;
    public bool RepairAttempted { get; init; } = false;
    public string? RawResponse { get; init; }
}

/// <summary>
/// Token usage information.
/// </summary>
public record LlmUsage
{
    public required int InputTokens { get; init; }
    public required int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// Exception thrown when LLM call fails after retries.
/// </summary>
public class LlmException : Exception
{
    public int? StatusCode { get; }
    public string? ErrorCode { get; }
    
    public LlmException(string message, int? statusCode = null, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
    
    public LlmException(string message, Exception innerException)
        : base(message, innerException) { }
}
