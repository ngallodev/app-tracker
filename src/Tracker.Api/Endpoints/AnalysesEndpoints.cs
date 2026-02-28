using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Tracker.AI;
using Tracker.AI.Cli;
using Tracker.AI.Services;
using Tracker.Domain.DTOs;
using Tracker.Domain.DTOs.Requests;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class AnalysesEndpoints
{
    private const string DeterministicMode = "deterministic";
    private const string LlmFallbackMode = "llm_fallback";
    private const string CachedMode = "cached";
    private const string FailedMode = "failed";
    private const string CompletedOutcome = "completed";
    private const string FailedOutcome = "failed";

    private static AnalysisResultDto ToDto(Analysis analysis)
    {
        var (gapMode, usedLlmFallback) = ResolveGapAnalysisMode(analysis);

        return new AnalysisResultDto(
            analysis.Id,
            analysis.JobId,
            analysis.ResumeId,
            analysis.Status.ToString(),
            analysis.ErrorMessage,
            analysis.ErrorCategory,
            gapMode,
            usedLlmFallback,
            analysis.Result?.CoverageScore ?? 0,
            analysis.Result?.GroundednessScore ?? 0,
            analysis.Result?.SalaryAlignmentScore ?? 0,
            analysis.Result?.SalaryAlignmentNote,
            analysis.Result?.RequiredSkillsJson,
            analysis.Result?.MissingRequiredJson,
            analysis.Result?.MissingPreferredJson,
            analysis.InputTokens,
            analysis.OutputTokens,
            analysis.LatencyMs,
            TokensPerSecond(analysis.InputTokens, analysis.OutputTokens, analysis.LatencyMs),
            ResolveProvider(analysis),
            ResolveExecutionMode(analysis),
            analysis.IsTestData,
            analysis.CreatedAt
        );
    }

    private static AnalysisResultDto ToDto(
        Analysis analysis,
        string gapMode,
        bool usedLlmFallback,
        string provider,
        string executionMode)
    {
        return new AnalysisResultDto(
            analysis.Id,
            analysis.JobId,
            analysis.ResumeId,
            analysis.Status.ToString(),
            analysis.ErrorMessage,
            analysis.ErrorCategory,
            gapMode,
            usedLlmFallback,
            analysis.Result?.CoverageScore ?? 0,
            analysis.Result?.GroundednessScore ?? 0,
            analysis.Result?.SalaryAlignmentScore ?? 0,
            analysis.Result?.SalaryAlignmentNote,
            analysis.Result?.RequiredSkillsJson,
            analysis.Result?.MissingRequiredJson,
            analysis.Result?.MissingPreferredJson,
            analysis.InputTokens,
            analysis.OutputTokens,
            analysis.LatencyMs,
            TokensPerSecond(analysis.InputTokens, analysis.OutputTokens, analysis.LatencyMs),
            provider,
            executionMode,
            analysis.IsTestData,
            analysis.CreatedAt
        );
    }

    private static (decimal Score, string Note) ScoreSalaryAlignment(Job job, Resume resume)
    {
        if (job.SalaryMin is null && job.SalaryMax is null)
        {
            return (0m, "No salary range detected in job posting.");
        }

        if (resume.DesiredSalaryMin is null && resume.DesiredSalaryMax is null)
        {
            return (0m, "Resume has no salary preference set.");
        }

        var jobMin = job.SalaryMin ?? job.SalaryMax ?? 0m;
        var jobMax = job.SalaryMax ?? job.SalaryMin ?? jobMin;
        var desiredMin = resume.DesiredSalaryMin ?? resume.DesiredSalaryMax ?? 0m;
        var desiredMax = resume.DesiredSalaryMax ?? resume.DesiredSalaryMin ?? desiredMin;

        var overlaps = desiredMin <= jobMax && desiredMax >= jobMin;
        if (overlaps)
        {
            return (100m, "Salary ranges overlap.");
        }

        if (desiredMin > jobMax)
        {
            return (25m, "Desired salary appears above posted range.");
        }

        return (40m, "Desired salary appears below posted range.");
    }

    private static (string gapMode, bool usedLlmFallback) ResolveGapAnalysisMode(Analysis analysis)
    {
        var logs = analysis.Logs;
        if (logs is null || logs.Count == 0)
        {
            return ("unknown", false);
        }

        var usedFallback = logs.Any(l => l.StepName.StartsWith("gap_analysis_llm_fallback", StringComparison.Ordinal));
        if (usedFallback)
        {
            return (LlmFallbackMode, true);
        }

        var deterministic = logs.Any(l => l.StepName.StartsWith("gap_analysis_deterministic", StringComparison.Ordinal));
        return (deterministic ? DeterministicMode : "unknown", false);
    }

    private static string ResolveProvider(Analysis analysis)
    {
        if (!string.IsNullOrWhiteSpace(analysis.Model))
        {
            var slashIdx = analysis.Model.IndexOf('/');
            if (slashIdx > 0)
            {
                return analysis.Model[..slashIdx];
            }
        }

        var providerStep = analysis.Logs
            .Select(l => l.StepName)
            .FirstOrDefault(name => name.StartsWith("provider_", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(providerStep))
        {
            return providerStep["provider_".Length..];
        }

        return "unknown";
    }

    private static string ResolveExecutionMode(Analysis analysis)
    {
        var provider = ResolveProvider(analysis);
        if (provider == "unknown")
        {
            return "unknown";
        }

        return string.Equals(provider, LlmProviderCatalog.Lmstudio, StringComparison.OrdinalIgnoreCase)
            ? "openai_compatible"
            : "cli_headless";
    }

    private static AnalysisRequestMetric CreateRequestMetric(
        Guid jobId,
        Guid resumeId,
        string jobHash,
        string resumeHash,
        bool cacheHit,
        string requestMode,
        string outcome,
        bool usedGapLlmFallback,
        int inputTokens,
        int outputTokens,
        int latencyMs,
        string? provider,
        string? errorCategory = null)
    {
        return new AnalysisRequestMetric
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            ResumeId = resumeId,
            JobHash = jobHash,
            ResumeHash = resumeHash,
            CacheHit = cacheHit,
            RequestMode = requestMode,
            Outcome = outcome,
            UsedGapLlmFallback = usedGapLlmFallback,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            LatencyMs = latencyMs,
            Provider = provider,
            ErrorCategory = errorCategory,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static decimal Percent(int numerator, int denominator)
        => denominator == 0 ? 0m : Math.Round((decimal)numerator / denominator * 100m, 2);

    private static decimal AverageTokens(IReadOnlyCollection<AnalysisRequestMetric> metrics)
        => metrics.Count == 0
            ? 0m
            : Math.Round((decimal)metrics.Average(m => m.InputTokens + m.OutputTokens), 2);

    private static int P95LatencyMs(IReadOnlyCollection<AnalysisRequestMetric> metrics)
    {
        if (metrics.Count == 0)
        {
            return 0;
        }

        var ordered = metrics.Select(x => x.LatencyMs).OrderBy(x => x).ToList();
        var rank = (int)Math.Ceiling(ordered.Count * 0.95m);
        rank = Math.Clamp(rank, 1, ordered.Count);
        return ordered[rank - 1];
    }

    private static decimal AverageTokensPerSecond(IReadOnlyCollection<AnalysisRequestMetric> metrics)
    {
        if (metrics.Count == 0)
        {
            return 0m;
        }

        return Math.Round(metrics.Average(m => TokensPerSecond(m.InputTokens, m.OutputTokens, m.LatencyMs)), 2);
    }

    private static decimal TokensPerSecond(int inputTokens, int outputTokens, int latencyMs)
    {
        var totalTokens = Math.Max(0, inputTokens + outputTokens);
        if (latencyMs <= 0 || totalTokens == 0)
        {
            return 0m;
        }

        var seconds = latencyMs / 1000m;
        return seconds <= 0m ? 0m : Math.Round(totalTokens / seconds, 2);
    }

    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyses");

        // GET /api/analyses
        group.MapGet("/", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var analyses = await db.Analyses
                .AsNoTracking()
                .Include(a => a.Result)
                .Include(a => a.Logs)
                .ToListAsync(ct);

            var result = analyses
                .OrderByDescending(a => a.CreatedAt)
                .Select(ToDto)
                .ToList();
            return Results.Ok(result);
        })
        .WithName("GetAllAnalyses");

        group.MapGet("/providers", (
            IEnumerable<ICliProviderAdapter> adapters,
            IOptionsMonitor<LlmCliOptions> options) =>
        {
            var byProvider = adapters.ToDictionary(a => a.ProviderName, StringComparer.OrdinalIgnoreCase);
            var providers = LlmProviderCatalog.List()
                .OrderBy(x => x)
                .Select(name =>
                {
                    if (string.Equals(name, LlmProviderCatalog.Lmstudio, StringComparison.OrdinalIgnoreCase))
                    {
                        return new ProviderAvailabilityDto(
                            name,
                            true,
                            "OpenAI-compatible LM Studio endpoint");
                    }

                    if (!byProvider.TryGetValue(name, out var adapter))
                    {
                        return new ProviderAvailabilityDto(name, false, "Adapter not registered");
                    }

                    var availability = adapter.GetAvailability();
                    return new ProviderAvailabilityDto(name, availability.Available, availability.Message ?? string.Empty);
                })
                .ToList();

            var configuredDefault = options.CurrentValue.DefaultProvider;
            var preferredDefault = providers.FirstOrDefault(p =>
                p.Available && string.Equals(p.Name, configuredDefault, StringComparison.OrdinalIgnoreCase));
            var fallbackDefault = providers.FirstOrDefault(p => p.Available);
            var defaultProvider = preferredDefault?.Name ?? fallbackDefault?.Name ?? configuredDefault;

            return Results.Ok(new AnalysisProvidersDto(defaultProvider, providers));
        })
        .WithName("GetAnalysisProviders");

        // GET /api/analyses/metrics
        group.MapGet("/metrics", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var requestMetrics = await db.AnalysisRequestMetrics
                .AsNoTracking()
                .ToListAsync(ct);

            var completed = requestMetrics
                .Where(x => string.Equals(x.Outcome, CompletedOutcome, StringComparison.Ordinal))
                .ToList();
            var uncachedCompleted = completed.Where(x => !x.CacheHit).ToList();
            var cachedCompleted = completed.Where(x => x.CacheHit).ToList();
            var deterministicCompleted = uncachedCompleted
                .Where(x => string.Equals(x.RequestMode, DeterministicMode, StringComparison.Ordinal))
                .ToList();
            var fallbackCompleted = uncachedCompleted
                .Where(x => string.Equals(x.RequestMode, LlmFallbackMode, StringComparison.Ordinal))
                .ToList();

            var repeatedJdRequests = 0;
            var repeatedJdCacheHits = 0;
            foreach (var groupByJd in requestMetrics
                .Where(x => !string.IsNullOrWhiteSpace(x.JobHash))
                .GroupBy(x => x.JobHash))
            {
                var ordered = groupByJd.OrderBy(x => x.CreatedAt).ToList();
                if (ordered.Count < 2)
                {
                    continue;
                }

                var repeatedEntries = ordered.Skip(1).ToList();
                repeatedJdRequests += repeatedEntries.Count;
                repeatedJdCacheHits += repeatedEntries.Count(x => x.CacheHit);
            }

            var deterministicResolutionRate = Percent(deterministicCompleted.Count, uncachedCompleted.Count);
            var cacheHitRateOverall = Percent(cachedCompleted.Count, completed.Count);
            var cacheHitRateRepeatedJds = Percent(repeatedJdCacheHits, repeatedJdRequests);
            var avgTokensPerRequest = AverageTokens(completed);
            var fallbackAvgTokens = AverageTokens(fallbackCompleted);
            var deterministicAvgTokens = AverageTokens(deterministicCompleted);
            var averageTokensPerSecond = AverageTokensPerSecond(completed);
            var fallbackAverageTokensPerSecond = AverageTokensPerSecond(fallbackCompleted);
            var deterministicAverageTokensPerSecond = AverageTokensPerSecond(deterministicCompleted);

            var latestEvalRun = await db.EvalRuns
                .AsNoTracking()
                .ToListAsync(ct);
            var latestDeterministicEval = latestEvalRun
                .Where(x => string.Equals(x.Mode, "deterministic", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            var evalFixtureCount = latestDeterministicEval?.FixtureCount ?? 0;
            var evalPassRate = latestDeterministicEval is null || latestDeterministicEval.FixtureCount == 0
                ? 0m
                : Math.Round((decimal)latestDeterministicEval.PassedCount / latestDeterministicEval.FixtureCount * 100m, 2);

            return Results.Ok(new
            {
                totals = new
                {
                    totalRequests = requestMetrics.Count,
                    completedRequests = completed.Count,
                    uncachedCompletedRequests = uncachedCompleted.Count,
                    cachedCompletedRequests = cachedCompleted.Count,
                    fallbackCompletedRequests = fallbackCompleted.Count
                },
                metrics = new
                {
                    deterministicResolutionRatePct = deterministicResolutionRate,
                    averageTokensPerRequest = avgTokensPerRequest,
                    fallbackAverageTokensPerRequest = fallbackAvgTokens,
                    deterministicAverageTokensPerRequest = deterministicAvgTokens,
                    averageTokensPerSecond = averageTokensPerSecond,
                    fallbackAverageTokensPerSecond = fallbackAverageTokensPerSecond,
                    deterministicAverageTokensPerSecond = deterministicAverageTokensPerSecond,
                    cacheHitRateOverallPct = cacheHitRateOverall,
                    cacheHitRateOnRepeatedJdsPct = cacheHitRateRepeatedJds,
                    p95LatencyMs = new
                    {
                        coldDeterministic = P95LatencyMs(deterministicCompleted),
                        fallbackLlm = P95LatencyMs(fallbackCompleted),
                        cached = P95LatencyMs(cachedCompleted)
                    }
                },
                resumeBullets = new[]
                {
                    $"Deterministic-first matching resolved {deterministicResolutionRate}% of uncached completed requests without LLM fallback, reducing inference cost.",
                    $"Content-hash caching eliminated {cacheHitRateRepeatedJds}% of redundant requests on repeated job descriptions.",
                    $"Achieved {evalPassRate}% reproducibility across {evalFixtureCount} fixture-based eval cases (latest deterministic eval run).",
                    $"Average token usage per completed request: deterministic {deterministicAvgTokens} tokens vs fallback {fallbackAvgTokens} tokens.",
                    $"Average throughput: deterministic {deterministicAverageTokensPerSecond} tokens/sec vs fallback {fallbackAverageTokensPerSecond} tokens/sec."
                }
            });
        })
        .WithName("GetAnalysisMetrics");

        // GET /api/analyses/{id}
        group.MapGet("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var analysis = await db.Analyses
                .AsNoTracking()
                .Include(a => a.Result)
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Include(a => a.Logs)
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (analysis is null)
                return Results.NotFound();

            return Results.Ok(ToDto(analysis));
        })
        .WithName("GetAnalysisById");

        // GET /api/analyses/{id}/status - for polling
        group.MapGet("/{id:guid}/status", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var analysis = await db.Analyses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id, ct);

            if (analysis is null)
                return Results.NotFound();

            return Results.Ok(new
            {
                id = analysis.Id,
                status = analysis.Status.ToString(),
                createdAt = analysis.CreatedAt,
                errorMessage = analysis.ErrorMessage
            });
        })
        .WithName("GetAnalysisStatus");

        group.MapPost("/", [
            EnableRateLimiting("StrictAnalysisPolicy")
        ] async (
            CreateAnalysisRequest request,
            TrackerDbContext db,
            IAnalysisService analysisService,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("AnalysisRequests");
            var requestStopwatch = Stopwatch.StartNew();

            // Verify job and resume exist
            var job = await db.Jobs.FindAsync([request.JobId], ct);
            if (job is null)
                return Results.Problem(
                    detail: "Job not found",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");

            var resume = await db.Resumes.FindAsync([request.ResumeId], ct);
            if (resume is null)
                return Results.Problem(
                    detail: "Resume not found",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");

            if (string.IsNullOrWhiteSpace(job.DescriptionText))
                return Results.Problem(
                    detail: "Job has no description",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");

            if (string.IsNullOrWhiteSpace(resume.Content))
                return Results.Problem(
                    detail: "Resume has no content",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");

            if (!string.IsNullOrWhiteSpace(request.Provider) && !LlmProviderCatalog.IsSupported(request.Provider))
            {
                return Results.Problem(
                    detail: $"Invalid provider '{request.Provider}'. Allowed providers: {string.Join(", ", LlmProviderCatalog.List().OrderBy(x => x))}",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");
            }

            // Input validation: max lengths
            const int MaxJdLength = 10000;
            const int MaxResumeLength = 20000;

            if (job.DescriptionText.Length > MaxJdLength)
                return Results.Problem(
                    detail: $"Job description exceeds maximum length of {MaxJdLength} characters",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");

            if (resume.Content.Length > MaxResumeLength)
                return Results.Problem(
                    detail: $"Resume content exceeds maximum length of {MaxResumeLength} characters",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid Analysis Request");

            // Ensure normalized hashes are present for cache lookups.
            var jobHash = string.IsNullOrWhiteSpace(job.DescriptionHash)
                ? HashUtility.ComputeNormalizedHash(job.DescriptionText)
                : job.DescriptionHash;
            var resumeHash = string.IsNullOrWhiteSpace(resume.ContentHash)
                ? HashUtility.ComputeNormalizedHash(resume.Content)
                : resume.ContentHash;

            if (job.DescriptionHash != jobHash)
            {
                job.DescriptionHash = jobHash;
            }

            if (resume.ContentHash != resumeHash)
            {
                resume.ContentHash = resumeHash;
            }

            if (db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync(ct);
            }

            // Hash-pair cache: reuse latest completed analysis for identical JD/resume content.
            var cachedCandidates = await db.Analyses
                .AsNoTracking()
                .Include(a => a.Result)
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Include(a => a.Logs)
                .Where(a =>
                    a.Status == AnalysisStatus.Completed &&
                    a.Result != null &&
                    a.Job.DescriptionHash == jobHash &&
                    a.Resume.ContentHash == resumeHash)
                .ToListAsync(ct);

            // SQLite provider cannot translate DateTimeOffset ORDER BY; pick latest in-memory.
            var cachedAnalysis = cachedCandidates
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            if (cachedAnalysis is not null)
            {
                requestStopwatch.Stop();
                var (cachedGapMode, cachedUsedFallback) = ResolveGapAnalysisMode(cachedAnalysis);
                db.AnalysisRequestMetrics.Add(CreateRequestMetric(
                    request.JobId,
                    request.ResumeId,
                    jobHash,
                    resumeHash,
                    cacheHit: true,
                    requestMode: CachedMode,
                    outcome: CompletedOutcome,
                    usedGapLlmFallback: cachedUsedFallback,
                    inputTokens: 0,
                    outputTokens: 0,
                    latencyMs: (int)requestStopwatch.ElapsedMilliseconds,
                    provider: ResolveProvider(cachedAnalysis)));
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Analysis request served from cache. JobId={JobId}, ResumeId={ResumeId}, GapMode={GapMode}, LatencyMs={LatencyMs}",
                    request.JobId,
                    request.ResumeId,
                    cachedGapMode,
                    requestStopwatch.ElapsedMilliseconds);
                return Results.Ok(ToDto(cachedAnalysis));
            }

            // Create analysis record
            var analysis = new Analysis
            {
                Id = Guid.NewGuid(),
                JobId = request.JobId,
                ResumeId = request.ResumeId,
                Status = AnalysisStatus.Running,
                IsTestData = request.IsTestData ?? job.IsTestData || resume.IsTestData,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.Analyses.Add(analysis);
            await db.SaveChangesAsync(ct);

            try
            {
                // Run analysis
                var result = await analysisService.AnalyzeAsync(
                    job.DescriptionText,
                    resume.Content,
                    request.Provider,
                    ct);

                // Update analysis record
                analysis.Status = AnalysisStatus.Completed;
                analysis.Model = $"{result.Metadata.Provider}/{result.Metadata.Model}";
                analysis.InputTokens = result.Metadata.TotalInputTokens;
                analysis.OutputTokens = result.Metadata.TotalOutputTokens;
                analysis.LatencyMs = result.Metadata.TotalLatencyMs;
                analysis.ErrorMessage = null;
                analysis.ErrorCategory = null;
                var (salaryScore, salaryNote) = ScoreSalaryAlignment(job, resume);

                // Store result
                var analysisResult = new AnalysisResult
                {
                    AnalysisId = analysis.Id,
                    CoverageScore = result.Scores.CoverageScore,
                    GroundednessScore = result.Scores.GroundednessScore,
                    SalaryAlignmentScore = salaryScore,
                    SalaryAlignmentNote = salaryNote,
                    RequiredSkillsJson = JsonSerializer.Serialize(result.JdExtraction.RequiredSkills),
                    MissingRequiredJson = JsonSerializer.Serialize(result.GapAnalysis.MissingRequired),
                    MissingPreferredJson = JsonSerializer.Serialize(result.GapAnalysis.MissingPreferred)
                };
                analysis.Result = analysisResult;

                db.AnalysisResults.Add(analysisResult);

                // Log each LLM step for parse/repair observability.
                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = "jd_extraction",
                    RawResponse = result.Metadata.JdRawResponse,
                    ParseSuccess = result.Metadata.JdParseSuccess,
                    RepairAttempted = result.Metadata.JdRepairAttempted,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = "metrics_jd",
                    RawResponse = JsonSerializer.Serialize(new
                    {
                        inputTokens = result.Metadata.JdInputTokens,
                        outputTokens = result.Metadata.JdOutputTokens,
                        latencyMs = result.Metadata.JdLatencyMs,
                        tokensPerSecond = TokensPerSecond(result.Metadata.JdInputTokens, result.Metadata.JdOutputTokens, result.Metadata.JdLatencyMs)
                    }),
                    ParseSuccess = true,
                    RepairAttempted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = "metrics_gap",
                    RawResponse = JsonSerializer.Serialize(new
                    {
                        mode = result.Metadata.GapAnalysisMode,
                        inputTokens = result.Metadata.GapInputTokens,
                        outputTokens = result.Metadata.GapOutputTokens,
                        latencyMs = result.Metadata.GapLatencyMs,
                        tokensPerSecond = TokensPerSecond(result.Metadata.GapInputTokens, result.Metadata.GapOutputTokens, result.Metadata.GapLatencyMs)
                    }),
                    ParseSuccess = true,
                    RepairAttempted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = "metrics_overall",
                    RawResponse = JsonSerializer.Serialize(new
                    {
                        inputTokens = result.Metadata.TotalInputTokens,
                        outputTokens = result.Metadata.TotalOutputTokens,
                        latencyMs = result.Metadata.TotalLatencyMs,
                        tokensPerSecond = TokensPerSecond(result.Metadata.TotalInputTokens, result.Metadata.TotalOutputTokens, result.Metadata.TotalLatencyMs)
                    }),
                    ParseSuccess = true,
                    RepairAttempted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = result.Metadata.UsedGapLlmFallback
                        ? "gap_analysis_llm_fallback"
                        : "gap_analysis_deterministic",
                    RawResponse = result.Metadata.GapRawResponse,
                    ParseSuccess = result.Metadata.GapParseSuccess,
                    RepairAttempted = result.Metadata.GapRepairAttempted,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = $"provider_{result.Metadata.Provider}",
                    RawResponse = null,
                    ParseSuccess = true,
                    RepairAttempted = false,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                requestStopwatch.Stop();
                var requestMode = result.Metadata.UsedGapLlmFallback
                    ? LlmFallbackMode
                    : DeterministicMode;
                db.AnalysisRequestMetrics.Add(CreateRequestMetric(
                    request.JobId,
                    request.ResumeId,
                    jobHash,
                    resumeHash,
                    cacheHit: false,
                    requestMode: requestMode,
                    outcome: CompletedOutcome,
                    usedGapLlmFallback: result.Metadata.UsedGapLlmFallback,
                    inputTokens: result.Metadata.TotalInputTokens,
                    outputTokens: result.Metadata.TotalOutputTokens,
                    latencyMs: (int)requestStopwatch.ElapsedMilliseconds,
                    provider: result.Metadata.Provider));

                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Analysis request completed. JobId={JobId}, ResumeId={ResumeId}, RequestMode={RequestMode}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, LatencyMs={LatencyMs}",
                    request.JobId,
                    request.ResumeId,
                    requestMode,
                    result.Metadata.TotalInputTokens,
                    result.Metadata.TotalOutputTokens,
                    requestStopwatch.ElapsedMilliseconds);

                return Results.Created(
                    $"/api/analyses/{analysis.Id}",
                    ToDto(
                        analysis,
                        result.Metadata.GapAnalysisMode,
                        result.Metadata.UsedGapLlmFallback,
                        result.Metadata.Provider,
                        result.Metadata.ExecutionMode));
            }
            catch (LlmException llmEx)
            {
                requestStopwatch.Stop();
                analysis.Status = AnalysisStatus.Failed;
                analysis.ErrorMessage = llmEx.Message;
                analysis.ErrorCategory = llmEx.ErrorCode ?? "llm";
                db.AnalysisRequestMetrics.Add(CreateRequestMetric(
                    request.JobId,
                    request.ResumeId,
                    jobHash,
                    resumeHash,
                    cacheHit: false,
                    requestMode: FailedMode,
                    outcome: FailedOutcome,
                    usedGapLlmFallback: false,
                    inputTokens: analysis.InputTokens,
                    outputTokens: analysis.OutputTokens,
                    latencyMs: (int)requestStopwatch.ElapsedMilliseconds,
                    provider: request.Provider,
                    errorCategory: "llm"));
                await db.SaveChangesAsync(ct);
                logger.LogWarning(
                    "Analysis request failed with LLM error. JobId={JobId}, ResumeId={ResumeId}, Provider={Provider}, LatencyMs={LatencyMs}, StatusCode={StatusCode}",
                    request.JobId,
                    request.ResumeId,
                    request.Provider,
                    requestStopwatch.ElapsedMilliseconds,
                    llmEx.StatusCode);
                return Results.Problem(
                    detail: llmEx.Message,
                    statusCode: llmEx.StatusCode ?? 502,
                    title: "LLM Provider Error");
            }
            catch (Exception ex)
            {
                requestStopwatch.Stop();
                analysis.Status = AnalysisStatus.Failed;
                analysis.ErrorMessage = ex.Message;
                analysis.ErrorCategory = "unhandled";
                db.AnalysisRequestMetrics.Add(CreateRequestMetric(
                    request.JobId,
                    request.ResumeId,
                    jobHash,
                    resumeHash,
                    cacheHit: false,
                    requestMode: FailedMode,
                    outcome: FailedOutcome,
                    usedGapLlmFallback: false,
                    inputTokens: analysis.InputTokens,
                    outputTokens: analysis.OutputTokens,
                    latencyMs: (int)requestStopwatch.ElapsedMilliseconds,
                    provider: request.Provider,
                    errorCategory: "unhandled"));
                await db.SaveChangesAsync(ct);
                logger.LogError(
                    ex,
                    "Analysis request failed unexpectedly. JobId={JobId}, ResumeId={ResumeId}, Provider={Provider}, LatencyMs={LatencyMs}",
                    request.JobId,
                    request.ResumeId,
                    request.Provider,
                    requestStopwatch.ElapsedMilliseconds);
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: 500,
                    title: "Analysis Failed");
            }
        })
        .WithName("CreateAnalysis");

        return app;
    }
}
