using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Dictionary<string, string[]> SkillSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["javascript"] = ["js", "ecmascript", "node.js", "nodejs"],
        ["typescript"] = ["ts"],
        ["c#"] = ["csharp", "dotnet", ".net"],
        ["kubernetes"] = ["k8s"],
        ["postgresql"] = ["postgres", "psql"],
        ["machine learning"] = ["ml"],
        ["artificial intelligence"] = ["ai"],
        ["amazon web services"] = ["aws"],
        ["google cloud platform"] = ["gcp"],
        ["microsoft azure"] = ["azure"]
    };

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
        
        // Step 2: Deterministic gap analysis to reduce token cost and latency.
        _logger.LogInformation("Starting deterministic gap analysis");
        var deterministicSw = Stopwatch.StartNew();
        var deterministicGap = BuildDeterministicGapAnalysis(jdResult.Value, resumeText);
        deterministicSw.Stop();
        var gapResult = new LlmResult<GapAnalysis>
        {
            Value = deterministicGap,
            Usage = new LlmUsage { InputTokens = 0, OutputTokens = 0 },
            Model = "deterministic-skill-matcher",
            LatencyMs = (int)deterministicSw.ElapsedMilliseconds,
            ParseSuccess = true,
            RepairAttempted = false,
            RawResponse = "deterministic_local_matcher"
        };
        
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

    private static GapAnalysis BuildDeterministicGapAnalysis(JdExtraction jdExtraction, string resumeText)
    {
        var normalizedResume = NormalizeText(resumeText);
        var matches = new List<SkillMatch>();
        var missingRequired = new List<SkillWithEvidence>();
        var missingPreferred = new List<SkillWithEvidence>();

        ProcessSkills(
            jdExtraction.RequiredSkills,
            isRequired: true,
            normalizedResume,
            resumeText,
            matches,
            missingRequired);

        ProcessSkills(
            jdExtraction.PreferredSkills,
            isRequired: false,
            normalizedResume,
            resumeText,
            matches,
            missingPreferred);

        return new GapAnalysis
        {
            Matches = matches,
            MissingRequired = missingRequired,
            MissingPreferred = missingPreferred,
            ExperienceGaps = []
        };
    }

    private static void ProcessSkills(
        IEnumerable<SkillWithEvidence> skills,
        bool isRequired,
        string normalizedResume,
        string rawResume,
        List<SkillMatch> matches,
        List<SkillWithEvidence> missingSkills)
    {
        foreach (var skill in skills)
        {
            var searchTerms = GetSearchTerms(skill.SkillName);
            var matched = searchTerms.FirstOrDefault(term => ContainsSkill(normalizedResume, term));

            if (matched is not null)
            {
                matches.Add(new SkillMatch
                {
                    SkillName = skill.SkillName,
                    JdEvidence = skill.EvidenceQuote,
                    ResumeEvidence = ExtractResumeEvidence(rawResume, matched),
                    IsRequired = isRequired
                });
                continue;
            }

            missingSkills.Add(new SkillWithEvidence
            {
                SkillName = skill.SkillName,
                EvidenceQuote = skill.EvidenceQuote,
                Category = skill.Category
            });
        }
    }

    private static List<string> GetSearchTerms(string skillName)
    {
        var canonical = NormalizeSkillToken(skillName);
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            skillName,
            canonical
        };

        if (SkillSynonyms.TryGetValue(canonical, out var synonyms))
        {
            foreach (var synonym in synonyms)
            {
                terms.Add(synonym);
            }
        }

        return terms
            .Select(NormalizeSkillToken)
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .ToList();
    }

    private static bool ContainsSkill(string normalizedResume, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return false;
        }

        // Multi-token phrases are matched directly; single tokens use word boundaries.
        if (searchTerm.Contains(' '))
        {
            return normalizedResume.Contains(searchTerm, StringComparison.Ordinal);
        }

        return Regex.IsMatch(normalizedResume, $@"\b{Regex.Escape(searchTerm)}\b");
    }

    private static string ExtractResumeEvidence(string resumeText, string matchedTerm)
    {
        var index = resumeText.IndexOf(matchedTerm, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return matchedTerm;
        }

        var start = Math.Max(0, index - 40);
        var length = Math.Min(resumeText.Length - start, Math.Max(matchedTerm.Length + 80, 120));
        var snippet = resumeText.Substring(start, length)
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return snippet.Length > 200 ? snippet[..200] : snippet;
    }

    private static string NormalizeText(string text)
    {
        var normalized = text.ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9+#.\- ]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private static string NormalizeSkillToken(string skill)
    {
        return NormalizeText(skill);
    }
}
