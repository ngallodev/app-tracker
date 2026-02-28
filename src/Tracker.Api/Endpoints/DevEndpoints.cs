using Microsoft.EntityFrameworkCore;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class DevEndpoints
{
    public static IEndpointRouteBuilder MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dev");

        group.MapDelete("/test-data", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var analyses = await db.Analyses.Where(a => a.IsTestData).ToListAsync(ct);
            var applications = await db.JobApplications.Where(a => a.IsTestData).ToListAsync(ct);
            var jobs = await db.Jobs.Where(j => j.IsTestData).ToListAsync(ct);
            var resumes = await db.Resumes.Where(r => r.IsTestData).ToListAsync(ct);

            db.Analyses.RemoveRange(analyses);
            db.JobApplications.RemoveRange(applications);
            db.Jobs.RemoveRange(jobs);
            db.Resumes.RemoveRange(resumes);

            var changes = await db.SaveChangesAsync(ct);
            return Results.Ok(new
            {
                deletedAnalyses = analyses.Count,
                deletedApplications = applications.Count,
                deletedJobs = jobs.Count,
                deletedResumes = resumes.Count,
                dbChanges = changes
            });
        })
        .WithName("DeleteTestData");

        return app;
    }
}
