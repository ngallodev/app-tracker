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

    private static ResumeDto ToDto(Resume resume, bool fullContent = false) => new(
        resume.Id,
        resume.Name,
        fullContent ? resume.Content : GetContentPreview(resume.Content),
        resume.DesiredSalaryMin,
        resume.DesiredSalaryMax,
        resume.SalaryCurrency,
        resume.IsTestData,
        resume.CreatedAt,
        resume.UpdatedAt
    );
    
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
                .Select(r => ToDto(r))
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
            
            return Results.Ok(ToDto(resume, fullContent: true));
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
                DesiredSalaryMin = request.DesiredSalaryMin,
                DesiredSalaryMax = request.DesiredSalaryMax,
                SalaryCurrency = request.SalaryCurrency,
                IsTestData = request.IsTestData,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            
            db.Resumes.Add(resume);
            await db.SaveChangesAsync(ct);
            
            return Results.Created($"/api/resumes/{resume.Id}", ToDto(resume));
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
            if (request.DesiredSalaryMin.HasValue)
                resume.DesiredSalaryMin = request.DesiredSalaryMin.Value;
            if (request.DesiredSalaryMax.HasValue)
                resume.DesiredSalaryMax = request.DesiredSalaryMax.Value;
            if (request.SalaryCurrency is not null)
                resume.SalaryCurrency = request.SalaryCurrency;
            if (request.IsTestData.HasValue)
                resume.IsTestData = request.IsTestData.Value;
            
            resume.UpdatedAt = DateTimeOffset.UtcNow;
            
            await db.SaveChangesAsync(ct);
            
            return Results.Ok(ToDto(resume));
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
