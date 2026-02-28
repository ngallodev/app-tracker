using System.Net;
using System.Text.RegularExpressions;

namespace Tracker.Api.Services;

public sealed class JobIngestionService
{
    private static readonly Regex ScriptStyleRegex = new(
        "<(script|style)[^>]*>.*?</\\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(
        "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}",
        RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(
        "(\\+?\\d[\\d\\s().-]{7,}\\d)",
        RegexOptions.Compiled);
    private static readonly Regex LinkedInRegex = new(
        "https?://(?:www\\.)?linkedin\\.com/[A-Za-z0-9\\-_/%.?=&]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(
        "https?://[^\\s\"'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SalaryRegex = new(
        "(\\$|USD\\s*)?(\\d{2,3}(?:,\\d{3})+|\\d+)(?:\\s*[kK])?(?:\\s*(?:-|to)\\s*(\\$|USD\\s*)?(\\d{2,3}(?:,\\d{3})+|\\d+)(?:\\s*[kK])?)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<ImportedJobContent?> TryFetchFromUrlAsync(string sourceUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            var html = await client.GetStringAsync(uri, ct);
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            var title = ExtractTitle(html);
            var text = ExtractText(html);
            var companyFromHost = uri.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
            return new ImportedJobContent(
                string.IsNullOrWhiteSpace(title) ? "Imported Job" : title,
                companyFromHost,
                text);
        }
        catch
        {
            return null;
        }
    }

    public JobDerivedMetadata ExtractMetadata(string description, string? sourceUrl)
    {
        var normalized = Normalize(description);
        var workType = DetectWorkType(normalized);
        var employmentType = DetectEmploymentType(normalized);
        var salary = DetectSalary(normalized);
        var recruiterEmail = EmailRegex.Match(normalized).Success ? EmailRegex.Match(normalized).Value : null;
        var recruiterPhone = PhoneRegex.Match(normalized).Success ? PhoneRegex.Match(normalized).Value.Trim() : null;
        var recruiterLinkedIn = LinkedInRegex.Match(normalized).Success ? LinkedInRegex.Match(normalized).Value : null;
        var companyCareersUrl = ResolveCareersUrl(sourceUrl, normalized);
        return new JobDerivedMetadata(
            workType,
            employmentType,
            salary.Min,
            salary.Max,
            salary.Currency,
            recruiterEmail,
            recruiterPhone,
            recruiterLinkedIn,
            companyCareersUrl);
    }

    private static string ExtractTitle(string html)
    {
        var match = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return string.Empty;
        }

        return Normalize(WebUtility.HtmlDecode(match.Groups[1].Value));
    }

    private static string ExtractText(string html)
    {
        var noScripts = ScriptStyleRegex.Replace(html, " ");
        var withoutTags = TagRegex.Replace(noScripts, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Normalize(decoded);
    }

    private static string Normalize(string text) => WhitespaceRegex.Replace(text, " ").Trim();

    private static string DetectWorkType(string text)
    {
        if (text.Contains("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            return "hybrid";
        }

        if (text.Contains("remote", StringComparison.OrdinalIgnoreCase))
        {
            return "remote";
        }

        if (text.Contains("on-site", StringComparison.OrdinalIgnoreCase) || text.Contains("onsite", StringComparison.OrdinalIgnoreCase))
        {
            return "on-site";
        }

        return "unknown";
    }

    private static string DetectEmploymentType(string text)
    {
        if (text.Contains("contract", StringComparison.OrdinalIgnoreCase))
        {
            return "contract";
        }

        if (text.Contains("temporary", StringComparison.OrdinalIgnoreCase) || text.Contains("temp ", StringComparison.OrdinalIgnoreCase))
        {
            return "temporary";
        }

        if (text.Contains("part-time", StringComparison.OrdinalIgnoreCase))
        {
            return "part-time";
        }

        if (text.Contains("intern", StringComparison.OrdinalIgnoreCase))
        {
            return "internship";
        }

        if (text.Contains("full-time", StringComparison.OrdinalIgnoreCase))
        {
            return "full-time";
        }

        return "unknown";
    }

    private static (decimal? Min, decimal? Max, string? Currency) DetectSalary(string text)
    {
        var match = SalaryRegex.Match(text);
        if (!match.Success)
        {
            return (null, null, null);
        }

        var minRaw = ParseSalary(match.Groups[2].Value, match.Value.Contains('k', StringComparison.OrdinalIgnoreCase));
        var hasMax = !string.IsNullOrWhiteSpace(match.Groups[4].Value);
        var maxRaw = hasMax
            ? ParseSalary(match.Groups[4].Value, match.Value[(match.Groups[4].Index - match.Index)..].Contains('k', StringComparison.OrdinalIgnoreCase))
            : minRaw;
        if (minRaw is null)
        {
            return (null, null, null);
        }

        return (minRaw, maxRaw, "USD");
    }

    private static decimal? ParseSalary(string raw, bool hasK)
    {
        var digits = raw.Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        if (!decimal.TryParse(digits, out var value))
        {
            return null;
        }

        if (hasK && value < 1000m)
        {
            value *= 1000m;
        }

        return value;
    }

    private static string? ResolveCareersUrl(string? sourceUrl, string description)
    {
        foreach (Match match in UrlRegex.Matches(description))
        {
            var url = match.Value;
            if (url.Contains("career", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("jobs", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("apply", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }

        return sourceUrl;
    }
}

public record ImportedJobContent(string Title, string Company, string DescriptionText);

public record JobDerivedMetadata(
    string WorkType,
    string EmploymentType,
    decimal? SalaryMin,
    decimal? SalaryMax,
    string? SalaryCurrency,
    string? RecruiterEmail,
    string? RecruiterPhone,
    string? RecruiterLinkedIn,
    string? CompanyCareersUrl
);
