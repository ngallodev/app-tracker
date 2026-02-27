using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace Tracker.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "CorrelationId";

    private static readonly Regex ValidCorrelationId = new("^[a-zA-Z0-9-_.]{8,128}$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty(CorrelationIdItemKey, correlationId))
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            [CorrelationIdItemKey] = correlationId
        }))
        {
            await _next(context);
        }
    }

    private string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incoming) && ValidCorrelationId.IsMatch(incoming))
        {
            return incoming;
        }

        if (!string.IsNullOrWhiteSpace(incoming))
        {
            _logger.LogWarning("Rejected invalid correlation ID from header {HeaderName}", CorrelationIdHeader);
        }

        if (!string.IsNullOrWhiteSpace(Activity.Current?.TraceId.ToString()))
        {
            return Activity.Current!.TraceId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    public static string? GetCorrelationId(this HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var value) && value is string correlationId)
        {
            return correlationId;
        }

        var headerValue = context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeader].FirstOrDefault();
        return string.IsNullOrWhiteSpace(headerValue) ? null : headerValue;
    }
}
