using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tracker.Api.Middleware;

public class InputValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputValidationMiddleware> _logger;
    private const int MaxRequestBodySize = 2 * 1024 * 1024;
    private const int MaxJobDescriptionLength = 10000;
    private const int MaxResumeContentLength = 20000;
    private static readonly Regex ScriptTagRegex = new(@"<(script|iframe|object|embed|link|style)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EventHandlerRegex = new(@"\bon\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public InputValidationMiddleware(RequestDelegate next, ILogger<InputValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > MaxRequestBodySize)
        {
            await WriteProblemDetails(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "Payload Too Large",
                "Request body exceeds maximum allowed size of 2MB.");
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api/jobs") &&
            (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)))
        {
            if (!await ValidateJobRequest(context))
            {
                return;
            }
        }

        if (context.Request.Path.StartsWithSegments("/api/resumes") &&
            (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)))
        {
            if (!await ValidateResumeRequest(context))
            {
                return;
            }
        }

        await _next(context);
    }

    private async Task<bool> ValidateJobRequest(HttpContext context)
    {
        try
        {
            var root = await ReadJsonRootAsync(context);
            if (root.ValueKind != JsonValueKind.Object)
            {
                await WriteProblemDetails(context, 400, "Invalid JSON", "Request body must be a JSON object.");
                return false;
            }

            var isPost = HttpMethods.IsPost(context.Request.Method);
            var isExtractFromUrl = context.Request.Path.Value?.EndsWith("/extract-from-url", StringComparison.OrdinalIgnoreCase) == true;
            if (isExtractFromUrl)
            {
                if (!HasNonEmptyStringProperty(root, "sourceUrl"))
                {
                    await WriteProblemDetails(context, 400, "Validation Failed", "Field 'sourceUrl' is required.");
                    return false;
                }
            }
            else
            {
                var hasSourceUrl = HasNonEmptyStringProperty(root, "sourceUrl");

                if (isPost && !hasSourceUrl && !HasNonEmptyStringProperty(root, "title"))
                {
                    await WriteProblemDetails(context, 400, "Validation Failed", "Field 'title' is required when 'sourceUrl' is not supplied.");
                    return false;
                }

                if (isPost && !hasSourceUrl && !HasNonEmptyStringProperty(root, "company"))
                {
                    await WriteProblemDetails(context, 400, "Validation Failed", "Field 'company' is required when 'sourceUrl' is not supplied.");
                    return false;
                }
            }

            if (TryGetStringProperty(root, "descriptionText", out var descriptionText) && descriptionText is not null)
            {
                if (descriptionText.Length > MaxJobDescriptionLength)
                {
                    await WriteProblemDetails(
                        context,
                        400,
                        "Validation Failed",
                        $"Job description exceeds maximum length of {MaxJobDescriptionLength} characters.");
                    return false;
                }

                if (ContainsInjectionAttempt(descriptionText))
                {
                    _logger.LogWarning("Potential injection attempt detected in job description");
                    await WriteProblemDetails(context, 400, "Validation Failed", "Request contains potentially unsafe content.");
                    return false;
                }
            }
            else if (root.TryGetProperty("descriptionText", out var descProp) && descProp.ValueKind != JsonValueKind.Null)
            {
                await WriteProblemDetails(context, 400, "Validation Failed", "Field 'descriptionText' must be a string when provided.");
                return false;
            }
        }
        catch (JsonException)
        {
            await WriteProblemDetails(context, 400, "Invalid JSON", "Request body must be valid JSON.");
            return false;
        }

        return true;
    }

    private async Task<bool> ValidateResumeRequest(HttpContext context)
    {
        try
        {
            var root = await ReadJsonRootAsync(context);
            if (root.ValueKind != JsonValueKind.Object)
            {
                await WriteProblemDetails(context, 400, "Invalid JSON", "Request body must be a JSON object.");
                return false;
            }

            var isPost = HttpMethods.IsPost(context.Request.Method);
            if (isPost && !HasNonEmptyStringProperty(root, "name"))
            {
                await WriteProblemDetails(context, 400, "Validation Failed", "Field 'name' is required.");
                return false;
            }

            if (TryGetStringProperty(root, "content", out var content) && content is not null)
            {
                if (content.Length > MaxResumeContentLength)
                {
                    await WriteProblemDetails(
                        context,
                        400,
                        "Validation Failed",
                        $"Resume content exceeds maximum length of {MaxResumeContentLength} characters.");
                    return false;
                }

                if (ContainsInjectionAttempt(content))
                {
                    _logger.LogWarning("Potential injection attempt detected in resume content");
                    await WriteProblemDetails(context, 400, "Validation Failed", "Request contains potentially unsafe content.");
                    return false;
                }
            }
            else if (isPost)
            {
                await WriteProblemDetails(context, 400, "Validation Failed", "Field 'content' is required.");
                return false;
            }
            else if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind != JsonValueKind.Null)
            {
                await WriteProblemDetails(context, 400, "Validation Failed", "Field 'content' must be a string when provided.");
                return false;
            }
        }
        catch (JsonException)
        {
            await WriteProblemDetails(context, 400, "Invalid JSON", "Request body must be valid JSON.");
            return false;
        }

        return true;
    }

    private static bool ContainsInjectionAttempt(string text)
    {
        if (ScriptTagRegex.IsMatch(text))
        {
            return true;
        }

        if (EventHandlerRegex.IsMatch(text))
        {
            return true;
        }

        return false;
    }

    private static async Task<JsonElement> ReadJsonRootAsync(HttpContext context)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new JsonException("Empty request body.");
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static bool HasNonEmptyStringProperty(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static bool TryGetStringProperty(JsonElement root, string propertyName, out string? value)
    {
        value = null;

        if (!root.TryGetProperty(propertyName, out var prop))
        {
            return false;
        }

        if (prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = prop.GetString();
        return true;
    }

    private static async Task WriteProblemDetails(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title,
            status,
            detail
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}

public static class InputValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseInputValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<InputValidationMiddleware>();
    }
}
