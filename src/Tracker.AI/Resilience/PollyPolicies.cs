using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Tracker.AI.Resilience;

public static class PollyPolicies
{
    public static IAsyncPolicy CreateLlmResiliencePolicy(
        ILogger logger,
        TimeSpan? timeout = null,
        int retryCount = 2,
        int circuitBreakThreshold = 5)
    {
        var timeoutPolicy = Policy.TimeoutAsync(timeout ?? TimeSpan.FromSeconds(30), TimeoutStrategy.Optimistic);

        var retryPolicy = Policy
            .Handle<Exception>(IsTransient)
            .WaitAndRetryAsync(
                retryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(50, 250)),
                (exception, delay, attempt, _) =>
                {
                    logger.LogWarning(
                        exception,
                        "LLM transient failure. Retry {Attempt}/{MaxAttempts} in {DelayMs}ms",
                        attempt,
                        retryCount,
                        delay.TotalMilliseconds);
                });

        var circuitBreakerPolicy = Policy
            .Handle<Exception>(IsTransient)
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: circuitBreakThreshold,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    logger.LogError(
                        exception,
                        "LLM circuit opened for {Duration}s",
                        duration.TotalSeconds);
                },
                onReset: () => logger.LogInformation("LLM circuit reset"),
                onHalfOpen: () => logger.LogInformation("LLM circuit half-open; testing next call"));

        return Policy.WrapAsync(circuitBreakerPolicy, retryPolicy, timeoutPolicy);
    }

    public static bool IsResilienceSignal(Exception exception)
    {
        return exception is TimeoutRejectedException
            || exception is BrokenCircuitException
            || exception is BrokenCircuitException<Exception>
            || IsTransient(exception);
    }

    private static bool IsTransient(Exception ex)
    {
        if (ex is TimeoutRejectedException)
        {
            return true;
        }

        if (ex is HttpRequestException)
        {
            return true;
        }

        if (ex is TaskCanceledException or TimeoutException)
        {
            return true;
        }

        var statusCode = TryReadStatusCode(ex);
        if (statusCode is 408 or 429)
        {
            return true;
        }

        if (statusCode is >= 500 and <= 599)
        {
            return true;
        }

        var message = ex.Message;
        return message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryReadStatusCode(Exception ex)
    {
        var type = ex.GetType();
        var property = type.GetProperty("StatusCode") ?? type.GetProperty("Status");
        if (property is null)
        {
            return null;
        }

        var value = property.GetValue(ex);
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            int code => code,
            _ when int.TryParse(value.ToString(), out var parsed) => parsed,
            _ => null
        };
    }
}
