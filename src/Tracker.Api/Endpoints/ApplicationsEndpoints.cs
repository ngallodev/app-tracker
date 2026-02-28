using Microsoft.EntityFrameworkCore;
using Tracker.Domain.DTOs;
using Tracker.Domain.DTOs.Requests;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class ApplicationsEndpoints
{
    private static JobApplicationEventDto ToEventDto(JobApplicationEvent evt) => new(
        evt.Id,
        evt.JobApplicationId,
        evt.EventType.ToString(),
        evt.EventAt,
        evt.Notes,
        evt.Channel,
        evt.PositiveOutcome,
        evt.CreatedAt
    );

    private static JobApplicationDto ToDto(JobApplication app) => new(
        app.Id,
        app.JobId,
        app.ResumeId,
        app.Job.Title,
        app.Job.Company,
        app.Resume.Name,
        app.Status.ToString(),
        app.AppliedAt,
        app.ClosedAt,
        app.ApplicationUrl,
        app.Notes,
        app.IsTestData,
        app.CreatedAt,
        app.UpdatedAt,
        app.Events.OrderByDescending(e => e.EventAt).Select(ToEventDto).ToList()
    );

    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications");

        group.MapGet("/", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var rows = await db.JobApplications
                .AsNoTracking()
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Include(a => a.Events)
                .ToListAsync(ct);
            return Results.Ok(rows.OrderByDescending(a => a.CreatedAt).Select(ToDto).ToList());
        })
        .WithName("GetApplications");

        group.MapGet("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var row = await db.JobApplications
                .AsNoTracking()
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Include(a => a.Events)
                .FirstOrDefaultAsync(a => a.Id == id, ct);
            return row is null ? Results.NotFound() : Results.Ok(ToDto(row));
        })
        .WithName("GetApplicationById");

        group.MapPost("/", async (CreateJobApplicationRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([request.JobId], ct);
            if (job is null)
            {
                return Results.Problem(detail: "Job not found", statusCode: 400, title: "Invalid application");
            }

            var resume = await db.Resumes.FindAsync([request.ResumeId], ct);
            if (resume is null)
            {
                return Results.Problem(detail: "Resume not found", statusCode: 400, title: "Invalid application");
            }

            var now = DateTimeOffset.UtcNow;
            var appRow = new JobApplication
            {
                Id = Guid.NewGuid(),
                JobId = request.JobId,
                ResumeId = request.ResumeId,
                Status = JobApplicationStatus.Applied,
                AppliedAt = request.AppliedAt ?? now,
                ApplicationUrl = request.ApplicationUrl,
                Notes = request.Notes,
                IsTestData = request.IsTestData || job.IsTestData || resume.IsTestData,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.JobApplications.Add(appRow);
            db.JobApplicationEvents.Add(new JobApplicationEvent
            {
                Id = Guid.NewGuid(),
                JobApplicationId = appRow.Id,
                EventType = JobApplicationEventType.StatusChange,
                EventAt = appRow.AppliedAt,
                Notes = "Application created and marked as applied.",
                PositiveOutcome = true,
                CreatedAt = now
            });
            await db.SaveChangesAsync(ct);

            var created = await db.JobApplications
                .AsNoTracking()
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Include(a => a.Events)
                .FirstAsync(a => a.Id == appRow.Id, ct);
            return Results.Created($"/api/applications/{appRow.Id}", ToDto(created));
        })
        .WithName("CreateApplication");

        group.MapPut("/{id:guid}", async (Guid id, UpdateJobApplicationRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var appRow = await db.JobApplications.FindAsync([id], ct);
            if (appRow is null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                if (!Enum.TryParse<JobApplicationStatus>(request.Status, true, out var parsedStatus))
                {
                    return Results.Problem(detail: "Invalid application status.", statusCode: 400, title: "Invalid application");
                }

                appRow.Status = parsedStatus;
                if (parsedStatus is JobApplicationStatus.Rejected or JobApplicationStatus.Closed)
                {
                    appRow.ClosedAt = request.ClosedAt ?? DateTimeOffset.UtcNow;
                }

                db.JobApplicationEvents.Add(new JobApplicationEvent
                {
                    Id = Guid.NewGuid(),
                    JobApplicationId = appRow.Id,
                    EventType = JobApplicationEventType.StatusChange,
                    EventAt = DateTimeOffset.UtcNow,
                    Notes = $"Status changed to {parsedStatus}.",
                    PositiveOutcome = parsedStatus is JobApplicationStatus.Interviewing or JobApplicationStatus.Offer,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            if (request.ClosedAt.HasValue)
            {
                appRow.ClosedAt = request.ClosedAt.Value;
            }

            if (request.ApplicationUrl is not null)
            {
                appRow.ApplicationUrl = request.ApplicationUrl;
            }

            if (request.Notes is not null)
            {
                appRow.Notes = request.Notes;
            }

            appRow.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            var updated = await db.JobApplications
                .AsNoTracking()
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Include(a => a.Events)
                .FirstAsync(a => a.Id == id, ct);
            return Results.Ok(ToDto(updated));
        })
        .WithName("UpdateApplication");

        group.MapPost("/{id:guid}/events", async (Guid id, AddJobApplicationEventRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var appRow = await db.JobApplications.FindAsync([id], ct);
            if (appRow is null)
            {
                return Results.NotFound();
            }

            if (!Enum.TryParse<JobApplicationEventType>(request.EventType, true, out var eventType))
            {
                return Results.Problem(detail: "Invalid event type.", statusCode: 400, title: "Invalid application event");
            }

            var evt = new JobApplicationEvent
            {
                Id = Guid.NewGuid(),
                JobApplicationId = id,
                EventType = eventType,
                EventAt = request.EventAt ?? DateTimeOffset.UtcNow,
                Notes = request.Notes,
                Channel = request.Channel,
                PositiveOutcome = request.PositiveOutcome,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.JobApplicationEvents.Add(evt);
            appRow.UpdatedAt = DateTimeOffset.UtcNow;

            if (eventType == JobApplicationEventType.Rejection)
            {
                appRow.Status = JobApplicationStatus.Rejected;
                appRow.ClosedAt = DateTimeOffset.UtcNow;
            }
            else if (eventType == JobApplicationEventType.Offer)
            {
                appRow.Status = JobApplicationStatus.Offer;
            }
            else if (eventType == JobApplicationEventType.Interview)
            {
                appRow.Status = JobApplicationStatus.Interviewing;
            }

            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/applications/{id}/events/{evt.Id}", ToEventDto(evt));
        })
        .WithName("AddApplicationEvent");

        return app;
    }
}
