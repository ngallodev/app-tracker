using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Tracker.AI;

/// <summary>
/// Shared Polly resilience policies for LLM operations.
/// </summary>
public static class PollyPolicies
{
    private const int RetryCount = 3;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);
    private const int ExceptionsAllowedBeforeBreaking = 5;
    private static readonly TimeSpan CircuitBreakDuration = TimeSpan.FromSeconds(45);

    public static IAsyncPolicy CreateLlmCallPolicy(ILogger logger)
    {
        var timeoutPolicy = Policy
            .TimeoutAsync(
                RequestTimeout,
                TimeoutStrategy.Optimistic,
                onTimeoutAsync: (context, timeout, _, exception) =>
                {
                    logger.LogWarning(
                        exception,
                        "LLM policy timeout {Event} {Operation} {TimeoutMs}",
                        "llm_timeout",
                        GetOperation(context),
                        (int)timeout.TotalMilliseconds);

                    return Task.CompletedTask;
                });

        var retryPolicy = Policy
            .Handle<Exception>(IsTransientFailure)
            .WaitAndRetryAsync(
                RetryCount,
                attempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)),
                (exception, delay, attempt, context) =>
                {
                    logger.LogWarning(
                        exception,
                        "LLM policy retry {Event} {Operation} {Attempt} {DelayMs} {ExceptionType}",
                        "llm_retry",
                        GetOperation(context),
                        attempt,
                        (int)delay.TotalMilliseconds,
                        exception.GetType().Name);
                });

        var circuitBreakerPolicy = Policy
            .Handle<Exception>(IsTransientFailure)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: ExceptionsAllowedBeforeBreaking,
                durationOfBreak: CircuitBreakDuration,
                onBreak: (exception, breakDelay, context) =>
                {
                    logger.LogError(
                        exception,
                        "LLM circuit state {Event} {Operation} {BreakMs} {ExceptionType}",
                        "llm_circuit_open",
                        GetOperation(context),
                        (int)breakDelay.TotalMilliseconds,
                        exception.GetType().Name);
                },
                onReset: context =>
                {
                    logger.LogInformation(
                        "LLM circuit state {Event} {Operation}",
                        "llm_circuit_closed",
                        GetOperation(context));
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation(
                        "LLM circuit state {Event}",
                        "llm_circuit_half_open");
                });

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
    }

    private static string GetOperation(Context context)
    {
        if (context.TryGetValue("operation", out var operation) && operation is string operationName)
        {
            return operationName;
        }

        return "unknown";
    }

    private static bool IsTransientFailure(Exception exception)
    {
        if (exception is LlmException)
        {
            return false;
        }

        if (exception is OperationCanceledException)
        {
            return false;
        }

        if (exception is TimeoutRejectedException or BrokenCircuitException)
        {
            return true;
        }

        if (exception is HttpRequestException or TimeoutException)
        {
            return true;
        }

        if (TryGetStatusCode(exception, out var statusCode))
        {
            return statusCode == 408 || statusCode == 429 || statusCode >= 500;
        }

        var typeName = exception.GetType().Name;
        return typeName.Contains("RateLimit", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("ServerError", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("Transient", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetStatusCode(Exception exception, out int statusCode)
    {
        statusCode = 0;

        var statusCodeProperty = exception.GetType().GetProperty("Status")
                                 ?? exception.GetType().GetProperty("StatusCode");
        if (statusCodeProperty?.GetValue(exception) is null)
        {
            return false;
        }

        var value = statusCodeProperty.GetValue(exception);
        switch (value)
        {
            case int intCode:
                statusCode = intCode;
                return true;
            case long longCode when longCode <= int.MaxValue:
                statusCode = (int)longCode;
                return true;
            default:
                return int.TryParse(value?.ToString(), out statusCode);
        }
    }
}
