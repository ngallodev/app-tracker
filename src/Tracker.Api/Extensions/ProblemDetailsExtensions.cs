using System.Text.Json;

namespace Tracker.Api.Extensions;

public static class ProblemDetailsExtensions
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    public static Task WriteProblemDetailsAsync(
        this HttpContext context,
        int status,
        string title,
        string detail,
        string? type = null,
        IDictionary<string, object?>? extensions = null,
        CancellationToken cancellationToken = default)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var payload = new Dictionary<string, object?>
        {
            ["type"] = type ?? ResolveType(status),
            ["title"] = title,
            ["status"] = status,
            ["detail"] = detail,
            ["instance"] = context.Request.Path.Value,
            ["correlationId"] = context.GetCorrelationId()
        };

        if (extensions is not null)
        {
            foreach (var item in extensions)
            {
                payload[item.Key] = item.Value;
            }
        }

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload), cancellationToken);
    }

    public static string GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue("CorrelationId", out var value) &&
            value is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var headerValue))
        {
            var fromHeader = headerValue.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(fromHeader))
            {
                return fromHeader;
            }
        }

        return context.TraceIdentifier;
    }

    private static string ResolveType(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
            StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
            StatusCodes.Status429TooManyRequests => "https://www.rfc-editor.org/rfc/rfc6585#section-4",
            StatusCodes.Status500InternalServerError => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
            StatusCodes.Status503ServiceUnavailable => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.4",
            _ => "about:blank"
        };
}
