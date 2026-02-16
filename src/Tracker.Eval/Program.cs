using System.Text.Json;
using Tracker.AI.Models;
using Tracker.AI.Services;

namespace Tracker.Eval;

public static class Program
{
    public static int Main(string[] args)
    {
        var fixtureDirectory = args.Length > 0
            ? args[0]
            : Path.Combine(AppContext.BaseDirectory, "Fixtures");

        if (!Directory.Exists(fixtureDirectory))
        {
            Console.Error.WriteLine($"Fixture directory not found: {fixtureDirectory}");
            return 2;
        }

        var fixtureFiles = Directory.GetFiles(fixtureDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fixtureFiles.Count == 0)
        {
            Console.Error.WriteLine($"No fixture files found in {fixtureDirectory}");
            return 2;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var results = new List<EvalResult>();

        foreach (var file in fixtureFiles)
        {
            var fixture = JsonSerializer.Deserialize<DeterministicEvalFixture>(
                File.ReadAllText(file),
                options);

            if (fixture is null)
            {
                results.Add(new EvalResult(Path.GetFileName(file), false, "Could not parse fixture JSON."));
                continue;
            }

            var extraction = ToJdExtraction(fixture);
            var gap = DeterministicGapMatcher.Build(extraction, fixture.ResumeText);
            var pass = EvaluateExpectations(fixture, gap, out var detail);

            results.Add(new EvalResult(fixture.Name, pass, detail));
        }

        var passed = results.Count(r => r.Passed);
        var failed = results.Count - passed;

        Console.WriteLine("Deterministic Eval Results");
        Console.WriteLine($"Fixtures: {results.Count}, Passed: {passed}, Failed: {failed}");
        Console.WriteLine();

        foreach (var result in results)
        {
            var status = result.Passed ? "PASS" : "FAIL";
            Console.WriteLine($"[{status}] {result.Name} - {result.Detail}");
        }

        return failed == 0 ? 0 : 1;
    }

    private static JdExtraction ToJdExtraction(DeterministicEvalFixture fixture)
    {
        return new JdExtraction
        {
            RoleTitle = fixture.RoleTitle,
            SeniorityLevel = fixture.SeniorityLevel,
            RequiredSkills = fixture.RequiredSkills
                .Select(ToSkillWithEvidence)
                .ToList(),
            PreferredSkills = fixture.PreferredSkills
                .Select(ToSkillWithEvidence)
                .ToList(),
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
        out string detail)
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

        var failedChecks = checks
            .Where(c => !c.passed)
            .Select(c => c.name)
            .ToList();

        detail =
            $"requiredMatched={requiredMatched}, missingRequired={missingRequired}, preferredMatched={preferredMatched}";

        if (failedChecks.Count > 0)
        {
            detail = $"{detail}, failed={string.Join(",", failedChecks)}";
            return false;
        }

        return true;
    }

    private sealed record EvalResult(string Name, bool Passed, string Detail);
}

public sealed record DeterministicEvalFixture
{
    public required string Name { get; init; }
    public required string RoleTitle { get; init; }
    public string? SeniorityLevel { get; init; }
    public required List<FixtureSkill> RequiredSkills { get; init; }
    public required List<FixtureSkill> PreferredSkills { get; init; }
    public required string ResumeText { get; init; }
    public required FixtureExpectations Expectations { get; init; }
}

public sealed record FixtureSkill
{
    public required string SkillName { get; init; }
    public required string EvidenceQuote { get; init; }
    public string? Category { get; init; }
}

public sealed record FixtureExpectations
{
    public required int MinRequiredMatches { get; init; }
    public required int MaxMissingRequired { get; init; }
    public required int MinPreferredMatches { get; init; }
}
