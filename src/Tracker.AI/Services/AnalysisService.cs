using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly IOptionsMonitor<LlmCliOptions> _options;
    private const int LmStudioChunkTargetChars = 2200;

    public AnalysisService(
        ILlmClient llmClient,
        ILogger<AnalysisService> logger,
        IOptionsMonitor<LlmCliOptions> options)
    {
        _llmClient = llmClient;
        _logger = logger;
        _options = options;
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
        var resolvedProvider = LlmProviderCatalog.NormalizeOrThrow(providerOverride ?? _options.CurrentValue.DefaultProvider);
        
        // Step 1: Extract JD structure
        _logger.LogInformation("Starting JD extraction");
        var jdResult = await ExtractJdAsync(jobDescription, providerOverride, resolvedProvider, cancellationToken);
        
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

    private async Task<LlmResult<JdExtraction>> ExtractJdAsync(
        string jobDescription,
        string? providerOverride,
        string resolvedProvider,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(resolvedProvider, LlmProviderCatalog.Lmstudio, StringComparison.OrdinalIgnoreCase) ||
            jobDescription.Length <= LmStudioChunkTargetChars)
        {
            return await _llmClient.CompleteStructuredAsync<JdExtraction>(
                JdExtractionPrompt.SystemPrompt,
                JdExtractionPrompt.UserPrompt(jobDescription),
                providerOverride,
                cancellationToken);
        }

        var chunks = ChunkByLength(jobDescription, LmStudioChunkTargetChars);
        _logger.LogInformation("LM Studio chunking enabled for JD extraction. ChunkCount={ChunkCount}", chunks.Count);
        var partials = new List<JdExtraction>(chunks.Count);
        var totalIn = 0;
        var totalOut = 0;
        var totalLatency = 0;
        var anyRepair = false;
        var allParseSuccess = true;
        var rawResponses = new List<string>();
        var model = string.Empty;

        foreach (var chunk in chunks)
        {
            var result = await _llmClient.CompleteStructuredAsync<JdExtraction>(
                JdExtractionPrompt.SystemPrompt,
                JdExtractionPrompt.UserPrompt(chunk),
                providerOverride,
                cancellationToken);
            partials.Add(result.Value);
            totalIn += result.Usage.InputTokens;
            totalOut += result.Usage.OutputTokens;
            totalLatency += result.LatencyMs;
            anyRepair |= result.RepairAttempted;
            allParseSuccess &= result.ParseSuccess;
            model = result.Model;
            if (!string.IsNullOrWhiteSpace(result.RawResponse))
            {
                rawResponses.Add(result.RawResponse);
            }
        }

        var merged = MergeExtractions(partials);
        return new LlmResult<JdExtraction>
        {
            Value = merged,
            Usage = new LlmUsage { InputTokens = totalIn, OutputTokens = totalOut },
            Provider = resolvedProvider,
            Model = model,
            LatencyMs = totalLatency,
            ParseSuccess = allParseSuccess,
            RepairAttempted = anyRepair,
            RawResponse = string.Join("\n\n---chunk---\n\n", rawResponses)
        };
    }

    private static List<string> ChunkByLength(string input, int maxChars)
    {
        var chunks = new List<string>();
        var start = 0;
        while (start < input.Length)
        {
            var remaining = input.Length - start;
            if (remaining <= maxChars)
            {
                chunks.Add(input[start..].Trim());
                break;
            }

            var candidateEnd = start + maxChars;
            var breakIndex = input.LastIndexOf('\n', candidateEnd, maxChars);
            if (breakIndex <= start)
            {
                breakIndex = input.LastIndexOf(' ', candidateEnd, maxChars);
            }

            if (breakIndex <= start)
            {
                breakIndex = candidateEnd;
            }

            chunks.Add(input[start..breakIndex].Trim());
            start = breakIndex;
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private static JdExtraction MergeExtractions(List<JdExtraction> parts)
    {
        var first = parts.FirstOrDefault() ?? new JdExtraction
        {
            RoleTitle = "Unknown Role",
            SeniorityLevel = null,
            YearsExperience = null
        };

        var requiredByName = new Dictionary<string, SkillWithEvidence>(StringComparer.OrdinalIgnoreCase);
        var preferredByName = new Dictionary<string, SkillWithEvidence>(StringComparer.OrdinalIgnoreCase);
        var responsibilities = new List<ResponsibilityWithEvidence>();
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            foreach (var skill in part.RequiredSkills)
            {
                if (!requiredByName.ContainsKey(skill.SkillName))
                {
                    requiredByName[skill.SkillName] = skill;
                }
            }

            foreach (var skill in part.PreferredSkills)
            {
                if (!preferredByName.ContainsKey(skill.SkillName) &&
                    !requiredByName.ContainsKey(skill.SkillName))
                {
                    preferredByName[skill.SkillName] = skill;
                }
            }

            foreach (var responsibility in part.Responsibilities)
            {
                if (!responsibilities.Any(r => r.Description.Equals(responsibility.Description, StringComparison.OrdinalIgnoreCase)))
                {
                    responsibilities.Add(responsibility);
                }
            }

            foreach (var keyword in part.Keywords)
            {
                keywords.Add(keyword);
            }
        }

        return new JdExtraction
        {
            RoleTitle = first.RoleTitle,
            SeniorityLevel = parts.Select(p => p.SeniorityLevel).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            RequiredSkills = requiredByName.Values.ToList(),
            PreferredSkills = preferredByName.Values.ToList(),
            Responsibilities = responsibilities,
            YearsExperience = parts.Select(p => p.YearsExperience).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)),
            Keywords = keywords.ToList()
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
