using System.Text.Json;
using Tracker.AI;

namespace Tracker.Api.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(ex, "Unhandled exception occurred after response started");
            context.Abort();
            return;
        }

        var correlationId = context.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;

        var (status, title, detail) = MapException(ex);

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            [CorrelationIdMiddleware.CorrelationIdItemKey] = correlationId
        }))
        {
            if (status >= 500)
            {
                _logger.LogError(ex, "Unhandled server exception {ExceptionType}", ex.GetType().Name);
            }
            else
            {
                _logger.LogWarning(ex, "Handled exception {ExceptionType}", ex.GetType().Name);
            }
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = "https://www.rfc-editor.org/rfc/rfc7807",
            title,
            status,
            detail,
            instance = context.Request.Path.Value,
            correlationId,
            traceId = context.TraceIdentifier,
            stackTrace = _environment.IsDevelopment() ? ex.StackTrace : null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private (int status, string title, string detail) MapException(Exception ex)
    {
        return ex switch
        {
            OperationCanceledException oce when !IsRequestAborted(oce) =>
                (StatusCodes.Status504GatewayTimeout, "Request Timeout", "The request timed out while waiting for an upstream dependency."),
            TimeoutException =>
                (StatusCodes.Status504GatewayTimeout, "Request Timeout", "The request timed out while waiting for an upstream dependency."),
            LlmException llm when llm.StatusCode is >= 400 and <= 599 =>
                (llm.StatusCode.Value, "LLM Request Failed", llm.Message),
            LlmException llm =>
                (StatusCodes.Status502BadGateway, "LLM Request Failed", llm.Message),
            JsonException =>
                (StatusCodes.Status400BadRequest, "Invalid JSON", "Request body must contain valid JSON."),
            _ =>
                (StatusCodes.Status500InternalServerError,
                    "Internal Server Error",
                    _environment.IsDevelopment() ? ex.Message : "An unexpected server error occurred.")
        };
    }

    private static bool IsRequestAborted(OperationCanceledException ex) => ex.CancellationToken.IsCancellationRequested;
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionMiddleware>();
    }
}
