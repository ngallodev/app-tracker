using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Tracker.AI.Cli;
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
        string? providerOverride = null,
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
    public required string Provider { get; init; }
    public required string ExecutionMode { get; init; }
    public required int JdInputTokens { get; init; }
    public required int JdOutputTokens { get; init; }
    public required int JdLatencyMs { get; init; }
    public required int GapInputTokens { get; init; }
    public required int GapOutputTokens { get; init; }
    public required int GapLatencyMs { get; init; }
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
    public required string GapAnalysisMode { get; init; }
    public required bool UsedGapLlmFallback { get; init; }
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
        string? providerOverride = null,
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
            providerOverride,
            cancellationToken);
        
        totalInputTokens += jdResult.Usage.InputTokens;
        totalOutputTokens += jdResult.Usage.OutputTokens;
        model = jdResult.Model;
        
        if (!jdResult.ParseSuccess)
        {
            _logger.LogWarning("JD extraction parse failed");
        }
        
        // Step 2: Deterministic gap analysis to reduce token cost and latency.
        _logger.LogInformation("Starting deterministic gap analysis");
        var deterministicSw = Stopwatch.StartNew();
        var deterministicGap = DeterministicGapMatcher.Build(jdResult.Value, resumeText);
        deterministicSw.Stop();
        var deterministicElapsedMs = (int)deterministicSw.ElapsedMilliseconds;
        var requiredMatches = deterministicGap.Matches.Count(m => m.IsRequired);
        var totalRequired = jdResult.Value.RequiredSkills.Count;
        var missingRequiredNames = deterministicGap.MissingRequired.Select(skill => skill.SkillName).ToArray();
        var missingRequiredText = missingRequiredNames.Length > 0
            ? string.Join(", ", missingRequiredNames)
            : "<none>";
        _logger.LogInformation(
            "Deterministic matcher results: {MatchCount} matches ({RequiredMatches}/{RequiredTotal} required) in {ElapsedMs}ms; missing required qualities: {MissingRequired}",
            deterministicGap.Matches.Count,
            requiredMatches,
            totalRequired,
            deterministicElapsedMs,
            missingRequiredText);
        var gapResult = new LlmResult<GapAnalysis>
        {
            Value = deterministicGap,
            Usage = new LlmUsage { InputTokens = 0, OutputTokens = 0 },
            Provider = "deterministic",
            Model = "deterministic-skill-matcher",
            LatencyMs = (int)deterministicSw.ElapsedMilliseconds,
            ParseSuccess = true,
            RepairAttempted = false,
            RawResponse = JsonSerializer.Serialize(deterministicGap)
        };
        var usedGapLlmFallback = false;
        var gapAnalysisMode = "deterministic";

        if (DeterministicGapMatcher.ShouldFallbackToLlm(jdResult.Value, deterministicGap))
        {
            _logger.LogInformation("Deterministic matcher confidence low. Running LLM fallback for gap analysis.");
            var jdJson = JsonSerializer.Serialize(jdResult.Value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            gapResult = await _llmClient.CompleteStructuredAsync<GapAnalysis>(
                GapAnalysisPrompt.SystemPrompt,
                GapAnalysisPrompt.UserPrompt(jdJson, resumeText),
                providerOverride,
                cancellationToken);

            totalInputTokens += gapResult.Usage.InputTokens;
            totalOutputTokens += gapResult.Usage.OutputTokens;
            usedGapLlmFallback = true;
            gapAnalysisMode = "llm_fallback";
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
                Provider = jdResult.Provider,
                ExecutionMode = ResolveExecutionMode(jdResult.Provider),
                JdInputTokens = jdResult.Usage.InputTokens,
                JdOutputTokens = jdResult.Usage.OutputTokens,
                JdLatencyMs = jdResult.LatencyMs,
                GapInputTokens = gapResult.Usage.InputTokens,
                GapOutputTokens = gapResult.Usage.OutputTokens,
                GapLatencyMs = gapResult.LatencyMs,
                TotalInputTokens = totalInputTokens,
                TotalOutputTokens = totalOutputTokens,
                TotalLatencyMs = (int)sw.ElapsedMilliseconds,
                Model = model,
                JdParseSuccess = jdResult.ParseSuccess,
                GapParseSuccess = gapResult.ParseSuccess,
                JdRepairAttempted = jdResult.RepairAttempted,
                GapRepairAttempted = gapResult.RepairAttempted,
                JdRawResponse = jdResult.RawResponse,
                GapRawResponse = gapResult.RawResponse,
                GapAnalysisMode = gapAnalysisMode,
                UsedGapLlmFallback = usedGapLlmFallback
            }
        };
    }

    private static string ResolveExecutionMode(string provider)
        => string.Equals(provider, LlmProviderCatalog.Lmstudio, StringComparison.OrdinalIgnoreCase)
            ? "openai_compatible"
            : "cli_headless";
    
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
