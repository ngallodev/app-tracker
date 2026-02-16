using Microsoft.EntityFrameworkCore;
using Tracker.Domain.DTOs;
using Tracker.Domain.DTOs.Requests;
using Tracker.Domain.Entities;
using Tracker.Infrastructure;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class ResumesEndpoints
{
    private static string? GetContentPreview(string content) =>
        content.Length > 200 ? content.Substring(0, 200) + "..." : content;
    
    public static IEndpointRouteBuilder MapResumeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/resumes");
        
        // GET /api/resumes
        group.MapGet("/", async (TrackerDbContext db, CancellationToken ct) =>
        {
            // SQLite doesn't support DateTimeOffset in ORDER BY, so we materialize first
            var resumes = await db.Resumes
                .AsNoTracking()
                .ToListAsync(ct);
            
            var result = resumes
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ResumeDto(
                    r.Id,
                    r.Name,
                    GetContentPreview(r.Content),
                    r.CreatedAt,
                    r.UpdatedAt
                ))
                .ToList();
            return Results.Ok(result);
        })
        .WithName("GetAllResumes");
        
        // GET /api/resumes/{id}
        group.MapGet("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var resume = await db.Resumes.FindAsync([id], ct);
            if (resume is null)
                return Results.NotFound();
            
            return Results.Ok(new ResumeDto(
                resume.Id,
                resume.Name,
                resume.Content, // Return full content for individual fetch
                resume.CreatedAt,
                resume.UpdatedAt
            ));
        })
        .WithName("GetResumeById");
        
        // POST /api/resumes
        group.MapPost("/", async (CreateResumeRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var resume = new Resume
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Content = request.Content,
                ContentHash = HashUtility.ComputeNormalizedHash(request.Content),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            
            db.Resumes.Add(resume);
            await db.SaveChangesAsync(ct);
            
            return Results.Created($"/api/resumes/{resume.Id}", new ResumeDto(
                resume.Id,
                resume.Name,
                GetContentPreview(resume.Content),
                resume.CreatedAt,
                resume.UpdatedAt
            ));
        })
        .WithName("CreateResume");
        
        // PUT /api/resumes/{id}
        group.MapPut("/{id:guid}", async (Guid id, UpdateResumeRequest request, TrackerDbContext db, CancellationToken ct) =>
        {
            var resume = await db.Resumes.FindAsync([id], ct);
            if (resume is null)
                return Results.NotFound();
            
            if (request.Name is not null)
                resume.Name = request.Name;
            if (request.Content is not null)
            {
                resume.Content = request.Content;
                resume.ContentHash = HashUtility.ComputeNormalizedHash(request.Content);
            }
            
            resume.UpdatedAt = DateTimeOffset.UtcNow;
            
            await db.SaveChangesAsync(ct);
            
            return Results.Ok(new ResumeDto(
                resume.Id,
                resume.Name,
                GetContentPreview(resume.Content),
                resume.CreatedAt,
                resume.UpdatedAt
            ));
        })
        .WithName("UpdateResume");
        
        // DELETE /api/resumes/{id}
        group.MapDelete("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var resume = await db.Resumes.FindAsync([id], ct);
            if (resume is null)
                return Results.NotFound();
            
            db.Resumes.Remove(resume);
            await db.SaveChangesAsync(ct);
            
            return Results.NoContent();
        })
        .WithName("DeleteResume");
        
        return app;
    }
}
