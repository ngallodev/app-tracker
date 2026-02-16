using System.Text.RegularExpressions;
using Tracker.AI.Models;

namespace Tracker.AI.Services;

public static class DeterministicGapMatcher
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

    public static GapAnalysis Build(JdExtraction jdExtraction, string resumeText)
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

    public static bool ShouldFallbackToLlm(JdExtraction jd, GapAnalysis deterministicGap)
    {
        var totalRequired = jd.RequiredSkills.Count;
        if (totalRequired == 0)
        {
            return false;
        }

        var matchedRequired = deterministicGap.Matches.Count(m => m.IsRequired);
        if (matchedRequired == 0 && totalRequired >= 2)
        {
            return true;
        }

        var coverageRatio = (decimal)matchedRequired / totalRequired;
        return totalRequired >= 5 && coverageRatio < 0.25m;
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
