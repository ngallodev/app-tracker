using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tracker.AI.Models;
using Tracker.AI.Prompts;

namespace Tracker.AI.Services;

/// <summary>
/// Orchestrates the JD analysis pipeline.
/// </summary>
public interface IAnalysisService
{
    /// <summary>
    /// Run full analysis pipeline: extract JD -> gap analysis -> calculate scores.
    /// </summary>
    Task<AnalysisPipelineResult> AnalyzeAsync(
        string jobDescription,
        string resumeText,
        CancellationToken cancellationToken = default);
}

public record AnalysisPipelineResult
{
    public required JdExtraction JdExtraction { get; init; }
    public required GapAnalysis GapAnalysis { get; init; }
    public required AnalysisScores Scores { get; init; }
    public required AnalysisMetadata Metadata { get; init; }
}

public record AnalysisScores
{
    /// <summary>
    /// Percentage of required skills matched (0-100).
    /// </summary>
    public required decimal CoverageScore { get; init; }
    
    /// <summary>
    /// Percentage of extractions with valid evidence quotes (0-100).
    /// </summary>
    public required decimal GroundednessScore { get; init; }
}

public record AnalysisMetadata
{
    public required int TotalInputTokens { get; init; }
    public required int TotalOutputTokens { get; init; }
    public required int TotalLatencyMs { get; init; }
    public required string Model { get; init; }
    public required bool JdParseSuccess { get; init; }
    public required bool GapParseSuccess { get; init; }
    public required bool JdRepairAttempted { get; init; }
    public required bool GapRepairAttempted { get; init; }
    public required string? JdRawResponse { get; init; }
    public required string? GapRawResponse { get; init; }
}

public class AnalysisService : IAnalysisService
{
    private readonly ILlmClient _llmClient;
    private readonly ILogger<AnalysisService> _logger;

    public AnalysisService(ILlmClient llmClient, ILogger<AnalysisService> logger)
    {
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<AnalysisPipelineResult> AnalyzeAsync(
        string jobDescription,
        string resumeText,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var totalInputTokens = 0;
        var totalOutputTokens = 0;
        var model = string.Empty;
        
        // Step 1: Extract JD structure
        _logger.LogInformation("Starting JD extraction");
        var jdResult = await _llmClient.CompleteStructuredAsync<JdExtraction>(
            JdExtractionPrompt.SystemPrompt,
            JdExtractionPrompt.UserPrompt(jobDescription),
            cancellationToken);
        
        totalInputTokens += jdResult.Usage.InputTokens;
        totalOutputTokens += jdResult.Usage.OutputTokens;
        model = jdResult.Model;
        
        if (!jdResult.ParseSuccess)
        {
            _logger.LogWarning("JD extraction parse failed");
        }
        
        // Step 2: Gap analysis
        _logger.LogInformation("Starting gap analysis");
        var jdJson = JsonSerializer.Serialize(jdResult.Value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        
        var gapResult = await _llmClient.CompleteStructuredAsync<GapAnalysis>(
            GapAnalysisPrompt.SystemPrompt,
            GapAnalysisPrompt.UserPrompt(jdJson, resumeText),
            cancellationToken);
        
        totalInputTokens += gapResult.Usage.InputTokens;
        totalOutputTokens += gapResult.Usage.OutputTokens;
        
        if (!gapResult.ParseSuccess)
        {
            _logger.LogWarning("Gap analysis parse failed");
        }
        
        sw.Stop();
        
        // Step 3: Calculate deterministic scores
        var scores = CalculateScores(jdResult.Value, gapResult.Value);
        
        _logger.LogInformation(
            "Analysis complete. Coverage: {Coverage}%, Groundedness: {Groundedness}%, Latency: {Latency}ms",
            scores.CoverageScore, scores.GroundednessScore, sw.ElapsedMilliseconds);
        
        return new AnalysisPipelineResult
        {
            JdExtraction = jdResult.Value,
            GapAnalysis = gapResult.Value,
            Scores = scores,
            Metadata = new AnalysisMetadata
            {
                TotalInputTokens = totalInputTokens,
                TotalOutputTokens = totalOutputTokens,
                TotalLatencyMs = (int)sw.ElapsedMilliseconds,
                Model = model,
                JdParseSuccess = jdResult.ParseSuccess,
                GapParseSuccess = gapResult.ParseSuccess,
                JdRepairAttempted = jdResult.RepairAttempted,
                GapRepairAttempted = gapResult.RepairAttempted,
                JdRawResponse = jdResult.RawResponse,
                GapRawResponse = gapResult.RawResponse
            }
        };
    }
    
    /// <summary>
    /// Calculate deterministic scores. No LLM self-grading.
    /// </summary>
    private static AnalysisScores CalculateScores(JdExtraction jd, GapAnalysis gap)
    {
        // Coverage Score = (matched required / total required) * 100
        var totalRequired = jd.RequiredSkills.Count;
        var matchedRequired = gap.Matches.Count(m => m.IsRequired);
        var coverageScore = totalRequired > 0 
            ? Math.Round((decimal)matchedRequired / totalRequired * 100, 2) 
            : 100m;
        
        // Groundedness Score = % of skills with valid evidence
        var totalSkills = jd.RequiredSkills.Count + jd.PreferredSkills.Count;
        var skillsWithEvidence = jd.RequiredSkills.Count(s => !string.IsNullOrWhiteSpace(s.EvidenceQuote))
            + jd.PreferredSkills.Count(s => !string.IsNullOrWhiteSpace(s.EvidenceQuote));
        var matchesWithEvidence = gap.Matches.Count(m => !string.IsNullOrWhiteSpace(m.ResumeEvidence));
        
        var groundednessScore = totalSkills > 0
            ? Math.Round((decimal)(skillsWithEvidence + matchesWithEvidence) / (totalSkills + gap.Matches.Count) * 100, 2)
            : 100m;
        
        return new AnalysisScores
        {
            CoverageScore = coverageScore,
            GroundednessScore = groundednessScore
        };
    }
}
