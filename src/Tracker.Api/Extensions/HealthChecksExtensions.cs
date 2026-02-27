using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tracker.Api.Health;

namespace Tracker.Api.Extensions;

public static class HealthChecksExtensions
{
    public static IServiceCollection AddTrackerHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready", "deps"])
            .AddCheck<CliProviderHealthCheck>("providers", tags: ["deps"]);

        return services;
    }

    public static IEndpointRouteBuilder MapTrackerHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Ok(new
        {
            status = "Healthy",
            timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("LivenessHealthCheck");

        app.MapGet("/healthz/ready", async (HealthCheckService healthCheckService, CancellationToken ct) =>
        {
            var report = await healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("ready"),
                ct);

            return ToResponse(report, allowDegraded: false);
        })
        .WithName("ReadinessHealthCheck");

        app.MapGet("/healthz/deps", async (HealthCheckService healthCheckService, CancellationToken ct) =>
        {
            var report = await healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains("deps"),
                ct);

            return ToResponse(report, allowDegraded: true);
        })
        .WithName("DependenciesHealthCheck");

        return app;
    }

    private static IResult ToResponse(HealthReport report, bool allowDegraded)
    {
        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => StatusCodes.Status200OK,
            HealthStatus.Degraded when allowDegraded => StatusCodes.Status200OK,
            HealthStatus.Degraded => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status503ServiceUnavailable
        };

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                kv => kv.Key,
                kv => new
                {
                    status = kv.Value.Status.ToString(),
                    description = kv.Value.Description,
                    durationMs = kv.Value.Duration.TotalMilliseconds,
                    exception = kv.Value.Exception?.Message,
                    data = kv.Value.Data
                })
        };

        return Results.Json(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web), "application/json", statusCode);
    }
}
