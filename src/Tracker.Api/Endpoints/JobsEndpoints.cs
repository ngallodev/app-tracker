using Microsoft.EntityFrameworkCore;
using Tracker.Domain.DTOs;
using Tracker.Domain.DTOs.Requests;
using Tracker.Domain.Entities;
using Tracker.Infrastructure;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs");
        
        // GET /api/jobs
        group.MapGet("/", async (TrackerDbContext db, CancellationToken ct) =>
        {
            // SQLite doesn't support DateTimeOffset in ORDER BY, so we materialize first
            var jobs = await db.Jobs
                .AsNoTracking()
                .ToListAsync(ct);
            
            var result = jobs
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new JobDto(
                    j.Id,
                    j.Title,
                    j.Company,
                    j.DescriptionText,
                    j.SourceUrl,
                    j.CreatedAt,
                    j.UpdatedAt
                ))
                .ToList();
            return Results.Ok(result);
        })
        .WithName("GetAllJobs");
        
        // GET /api/jobs/{id}
        group.MapGet("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([id], ct);
            if (job is null)
                return Results.NotFound();
            
            return Results.Ok(new JobDto(
                job.Id,
                job.Title,
                job.Company,
                job.DescriptionText,
                job.SourceUrl,
                job.CreatedAt,
                job.UpdatedAt
            ));
        })
        .WithName("GetJobById");
        
        // POST /api/jobs
        group.MapPost("/", async (CreateJobRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = new Job
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Company = request.Company,
                DescriptionText = request.DescriptionText,
                DescriptionHash = request.DescriptionText is not null 
                    ? HashUtility.ComputeNormalizedHash(request.DescriptionText) 
                    : null,
                SourceUrl = request.SourceUrl,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            
            db.Jobs.Add(job);
            await db.SaveChangesAsync(ct);
            
            return Results.Created($"/api/jobs/{job.Id}", new JobDto(
                job.Id,
                job.Title,
                job.Company,
                job.DescriptionText,
                job.SourceUrl,
                job.CreatedAt,
                job.UpdatedAt
            ));
        })
        .WithName("CreateJob");
        
        // PUT /api/jobs/{id}
        group.MapPut("/{id:guid}", async (Guid id, UpdateJobRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([id], ct);
            if (job is null)
                return Results.NotFound();
            
            if (request.Title is not null)
                job.Title = request.Title;
            if (request.Company is not null)
                job.Company = request.Company;
            if (request.DescriptionText is not null)
            {
                job.DescriptionText = request.DescriptionText;
                job.DescriptionHash = HashUtility.ComputeNormalizedHash(request.DescriptionText);
            }
            if (request.SourceUrl is not null)
                job.SourceUrl = request.SourceUrl;
            
            job.UpdatedAt = DateTimeOffset.UtcNow;
            
            await db.SaveChangesAsync(ct);
            
            return Results.Ok(new JobDto(
                job.Id,
                job.Title,
                job.Company,
                job.DescriptionText,
                job.SourceUrl,
                job.CreatedAt,
                job.UpdatedAt
            ));
        })
        .WithName("UpdateJob");
        
        // DELETE /api/jobs/{id}
        group.MapDelete("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([id], ct);
            if (job is null)
                return Results.NotFound();
            
            db.Jobs.Remove(job);
            await db.SaveChangesAsync(ct);
            
            return Results.NoContent();
        })
        .WithName("DeleteJob");
        
        return app;
    }
}
