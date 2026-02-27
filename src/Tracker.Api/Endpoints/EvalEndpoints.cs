using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tracker.AI.Models;
using Tracker.AI.Services;
using Tracker.Domain.Entities;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class EvalEndpoints
{
    public static IEndpointRouteBuilder MapEvalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/eval");

        group.MapGet("/runs", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var runs = await db.EvalRuns
                .AsNoTracking()
                .ToListAsync(ct);

            // SQLite provider cannot translate DateTimeOffset ORDER BY.
            runs = runs
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToList();

            return Results.Ok(runs.Select(x => new
            {
                x.Id,
                x.Mode,
                x.FixtureCount,
                x.PassedCount,
                x.FailedCount,
                x.SchemaPassRate,
                x.GroundednessRate,
                x.CoverageStabilityDiff,
                x.AvgLatencyMs,
                x.AvgCostPerRunUsd,
                x.CreatedAt
            }));
        })
        .WithName("GetEvalRuns");

        group.MapPost("/run", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var fixtureDirectory = ResolveFixtureDirectory();
            if (fixtureDirectory is null)
            {
                return Results.Problem(
                    detail: "Could not locate deterministic eval fixtures.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    title: "Eval Fixture Error");
            }

            var fixtureFiles = Directory.GetFiles(fixtureDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (fixtureFiles.Count == 0)
            {
                return Results.Problem(
                    detail: $"No fixture files found in {fixtureDirectory}",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Eval Fixture Error");
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var results = new List<EvalFixtureResult>();
            var latencies = new List<long>();
            var groundednessRates = new List<decimal>();
            var stabilityDiffs = new List<decimal>();
            var schemaParsedCount = 0;

            foreach (var file in fixtureFiles)
            {
                ct.ThrowIfCancellationRequested();
                var fixtureJson = await File.ReadAllTextAsync(file, ct);
                var fixture = JsonSerializer.Deserialize<DeterministicEvalFixture>(fixtureJson, options);
                if (fixture is null)
                {
                    results.Add(new EvalFixtureResult(Path.GetFileName(file), false, "Could not parse fixture JSON.", 0, 0m, 0m));
                    continue;
                }

                schemaParsedCount++;
                var extraction = ToJdExtraction(fixture);

                var sw1 = Stopwatch.StartNew();
                var gap1 = DeterministicGapMatcher.Build(extraction, fixture.ResumeText);
                sw1.Stop();

                var sw2 = Stopwatch.StartNew();
                var gap2 = DeterministicGapMatcher.Build(extraction, fixture.ResumeText);
                sw2.Stop();

                latencies.Add(sw1.ElapsedMilliseconds);
                latencies.Add(sw2.ElapsedMilliseconds);

                var pass = EvaluateExpectations(fixture, gap1, out var detail, out var coverageScore);
                var groundednessRate = CalculateGroundednessRate(extraction, gap1);
                var coverageScore2 = CalculateCoverageScore(extraction, gap2);
                var stabilityDiff = Math.Abs(coverageScore - coverageScore2);

                groundednessRates.Add(groundednessRate);
                stabilityDiffs.Add(stabilityDiff);

                results.Add(new EvalFixtureResult(
                    fixture.Name,
                    pass,
                    detail,
                    sw1.ElapsedMilliseconds,
                    groundednessRate,
                    stabilityDiff));
            }

            var passed = results.Count(x => x.Passed);
            var failed = results.Count - passed;
            var fixtureCount = results.Count;

            var summary = new EvalRun
            {
                Id = Guid.NewGuid(),
                Mode = "deterministic",
                FixtureCount = fixtureCount,
                PassedCount = passed,
                FailedCount = failed,
                SchemaPassRate = fixtureCount == 0 ? 0m : Math.Round((decimal)schemaParsedCount / fixtureCount * 100m, 2),
                GroundednessRate = groundednessRates.Count == 0 ? 0m : Math.Round(groundednessRates.Average(), 2),
                CoverageStabilityDiff = stabilityDiffs.Count == 0 ? 0m : Math.Round(stabilityDiffs.Average(), 4),
                AvgLatencyMs = latencies.Count == 0 ? 0m : Math.Round((decimal)latencies.Average(), 2),
                AvgCostPerRunUsd = 0m,
                ResultsJson = JsonSerializer.Serialize(results),
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.EvalRuns.Add(summary);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                summary.Id,
                summary.Mode,
                fixtureDirectory,
                summary.FixtureCount,
                summary.PassedCount,
                summary.FailedCount,
                summary.SchemaPassRate,
                summary.GroundednessRate,
                summary.CoverageStabilityDiff,
                summary.AvgLatencyMs,
                summary.AvgCostPerRunUsd,
                summary.CreatedAt,
                results
            });
        })
        .WithName("RunDeterministicEval");

        return app;
    }

    private static string? ResolveFixtureDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Tracker.Eval", "Fixtures"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Tracker.Eval", "Fixtures"))
        };

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static JdExtraction ToJdExtraction(DeterministicEvalFixture fixture)
    {
        return new JdExtraction
        {
            RoleTitle = fixture.RoleTitle,
            SeniorityLevel = fixture.SeniorityLevel,
            RequiredSkills = fixture.RequiredSkills.Select(ToSkillWithEvidence).ToList(),
            PreferredSkills = fixture.PreferredSkills.Select(ToSkillWithEvidence).ToList(),
            Responsibilities = [],
            YearsExperience = null,
            Keywords = []
        };
    }

    private static SkillWithEvidence ToSkillWithEvidence(FixtureSkill skill)
    {
        return new SkillWithEvidence
        {
            SkillName = skill.SkillName,
            EvidenceQuote = skill.EvidenceQuote,
            Category = skill.Category
        };
    }

    private static bool EvaluateExpectations(
        DeterministicEvalFixture fixture,
        GapAnalysis gap,
        out string detail,
        out decimal coverageScore)
    {
        var requiredMatched = gap.Matches.Count(m => m.IsRequired);
        var missingRequired = gap.MissingRequired.Count;
        var preferredMatched = gap.Matches.Count(m => !m.IsRequired);

        var checks = new List<(bool passed, string name)>
        {
            (requiredMatched >= fixture.Expectations.MinRequiredMatches, "min_required_matches"),
            (missingRequired <= fixture.Expectations.MaxMissingRequired, "max_missing_required"),
            (preferredMatched >= fixture.Expectations.MinPreferredMatches, "min_preferred_matches")
        };

        var failedChecks = checks.Where(c => !c.passed).Select(c => c.name).ToList();
        coverageScore = CalculateCoverageScore(ToJdExtraction(fixture), gap);

        detail = $"requiredMatched={requiredMatched}, missingRequired={missingRequired}, preferredMatched={preferredMatched}, coverage={coverageScore}";
        if (failedChecks.Count > 0)
        {
            detail = $"{detail}, failed={string.Join(",", failedChecks)}";
            return false;
        }

        return true;
    }

    private static decimal CalculateCoverageScore(JdExtraction jd, GapAnalysis gap)
    {
        var totalRequired = jd.RequiredSkills.Count;
        if (totalRequired == 0)
        {
            return 100m;
        }

        var matchedRequired = gap.Matches.Count(m => m.IsRequired);
        return Math.Round((decimal)matchedRequired / totalRequired * 100m, 2);
    }

    private static decimal CalculateGroundednessRate(JdExtraction jd, GapAnalysis gap)
    {
        var totalSkills = jd.RequiredSkills.Count + jd.PreferredSkills.Count;
        if (totalSkills == 0 && gap.Matches.Count == 0)
        {
            return 100m;
        }

        var skillsWithEvidence = jd.RequiredSkills.Count(s => !string.IsNullOrWhiteSpace(s.EvidenceQuote))
            + jd.PreferredSkills.Count(s => !string.IsNullOrWhiteSpace(s.EvidenceQuote));
        var matchesWithEvidence = gap.Matches.Count(m => !string.IsNullOrWhiteSpace(m.ResumeEvidence));
        var denominator = totalSkills + gap.Matches.Count;
        if (denominator == 0)
        {
            return 100m;
        }

        return Math.Round((decimal)(skillsWithEvidence + matchesWithEvidence) / denominator * 100m, 2);
    }

    private sealed record EvalFixtureResult(
        string Name,
        bool Passed,
        string Detail,
        long LatencyMs,
        decimal GroundednessRate,
        decimal CoverageStabilityDiff);

    private sealed record DeterministicEvalFixture
    {
        public required string Name { get; init; }
        public required string RoleTitle { get; init; }
        public string? SeniorityLevel { get; init; }
        public required List<FixtureSkill> RequiredSkills { get; init; }
        public required List<FixtureSkill> PreferredSkills { get; init; }
        public required string ResumeText { get; init; }
        public required FixtureExpectations Expectations { get; init; }
    }

    private sealed record FixtureSkill
    {
        public required string SkillName { get; init; }
        public required string EvidenceQuote { get; init; }
        public string? Category { get; init; }
    }

    private sealed record FixtureExpectations
    {
        public required int MinRequiredMatches { get; init; }
        public required int MaxMissingRequired { get; init; }
        public required int MinPreferredMatches { get; init; }
    }
}
