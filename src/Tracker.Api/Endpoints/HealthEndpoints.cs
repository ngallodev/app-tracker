using Microsoft.EntityFrameworkCore;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () => Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("HealthCheck");

        app.MapGet("/healthz/ready", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var dependencies = new Dictionary<string, object?>();
            var overallStatus = "healthy";
            var statusCode = StatusCodes.Status200OK;

            try
            {
                var databaseConnected = await db.Database.CanConnectAsync(ct);
                if (!databaseConnected)
                {
                    overallStatus = "degraded";
                    statusCode = StatusCodes.Status503ServiceUnavailable;
                }

                dependencies["database"] = new
                {
                    status = databaseConnected ? "healthy" : "degraded"
                };
            }
            catch (Exception ex)
            {
                overallStatus = "degraded";
                statusCode = StatusCodes.Status503ServiceUnavailable;
                dependencies["database"] = new
                {
                    status = "degraded",
                    reason = ex.GetType().Name
                };
            }

            return Results.Json(new
            {
                status = overallStatus,
                timestamp = DateTimeOffset.UtcNow,
                dependencies
            }, statusCode: statusCode);
        })
        .WithName("ReadinessCheck");

        return app;
    }
}
