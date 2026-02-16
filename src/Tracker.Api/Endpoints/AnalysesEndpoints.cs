using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tracker.AI;
using Tracker.AI.Services;
using Tracker.Domain.DTOs;
using Tracker.Domain.DTOs.Requests;
using Tracker.Domain.Entities;
using Tracker.Domain.Enums;
using Tracker.Infrastructure;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class AnalysesEndpoints
{
    private static AnalysisResultDto ToDto(Analysis analysis)
    {
        return new AnalysisResultDto(
            analysis.Id,
            analysis.JobId,
            analysis.ResumeId,
            analysis.Status.ToString(),
            analysis.Result?.CoverageScore ?? 0,
            analysis.Result?.GroundednessScore ?? 0,
            analysis.Result?.RequiredSkillsJson,
            analysis.Result?.MissingRequiredJson,
            analysis.Result?.MissingPreferredJson,
            analysis.InputTokens,
            analysis.OutputTokens,
            analysis.LatencyMs,
            analysis.CreatedAt
        );
    }

    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analyses");
        
        // GET /api/analyses
        group.MapGet("/", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var analyses = await db.Analyses
                .AsNoTracking()
                .Include(a => a.Result)
                .ToListAsync(ct);
            
            var result = analyses
                .OrderByDescending(a => a.CreatedAt)
                .Select(ToDto)
                .ToList();
            return Results.Ok(result);
        })
        .WithName("GetAllAnalyses");
        
        // GET /api/analyses/{id}
        group.MapGet("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var analysis = await db.Analyses
                .AsNoTracking()
                .Include(a => a.Result)
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .FirstOrDefaultAsync(a => a.Id == id, ct);
            
            if (analysis is null)
                return Results.NotFound();
            
            // Parse JSON for detailed view
            List<SkillMatchDto>? matches = null;
            List<SkillDto>? missingRequired = null;
            List<SkillDto>? missingPreferred = null;
            
            if (analysis.Result is not null)
            {
                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    // Would need actual GapAnalysis model to parse properly
                    // For now, return raw JSON strings
                }
                catch { /* ignore parse errors */ }
            }
            
            return Results.Ok(ToDto(analysis));
        })
        .WithName("GetAnalysisById");
        
        // POST /api/analyses
        group.MapPost("/", async (
            CreateAnalysisRequest request,
            TrackerDbContext db,
            IAnalysisService analysisService,
            CancellationToken ct) =>
        {
            // Verify job and resume exist
            var job = await db.Jobs.FindAsync([request.JobId], ct);
            if (job is null)
                return Results.BadRequest(new { error = "Job not found" });
            
            var resume = await db.Resumes.FindAsync([request.ResumeId], ct);
            if (resume is null)
                return Results.BadRequest(new { error = "Resume not found" });
            
            if (string.IsNullOrWhiteSpace(job.DescriptionText))
                return Results.BadRequest(new { error = "Job has no description" });
            
            if (string.IsNullOrWhiteSpace(resume.Content))
                return Results.BadRequest(new { error = "Resume has no content" });

            // Ensure normalized hashes are present for cache lookups.
            var jobHash = string.IsNullOrWhiteSpace(job.DescriptionHash)
                ? HashUtility.ComputeNormalizedHash(job.DescriptionText)
                : job.DescriptionHash;
            var resumeHash = string.IsNullOrWhiteSpace(resume.ContentHash)
                ? HashUtility.ComputeNormalizedHash(resume.Content)
                : resume.ContentHash;

            if (job.DescriptionHash != jobHash)
            {
                job.DescriptionHash = jobHash;
            }

            if (resume.ContentHash != resumeHash)
            {
                resume.ContentHash = resumeHash;
            }

            if (db.ChangeTracker.HasChanges())
            {
                await db.SaveChangesAsync(ct);
            }

            // Hash-pair cache: reuse latest completed analysis for identical JD/resume content.
            var cachedAnalysis = await db.Analyses
                .AsNoTracking()
                .Include(a => a.Result)
                .Include(a => a.Job)
                .Include(a => a.Resume)
                .Where(a =>
                    a.Status == AnalysisStatus.Completed &&
                    a.Result != null &&
                    a.Job.DescriptionHash == jobHash &&
                    a.Resume.ContentHash == resumeHash)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (cachedAnalysis is not null)
            {
                return Results.Ok(ToDto(cachedAnalysis));
            }
            
            // Create analysis record
            var analysis = new Analysis
            {
                Id = Guid.NewGuid(),
                JobId = request.JobId,
                ResumeId = request.ResumeId,
                Status = AnalysisStatus.Running,
                CreatedAt = DateTimeOffset.UtcNow
            };
            
            db.Analyses.Add(analysis);
            await db.SaveChangesAsync(ct);
            
            try
            {
                // Run analysis
                var result = await analysisService.AnalyzeAsync(
                    job.DescriptionText,
                    resume.Content,
                    ct);
                
                // Update analysis record
                analysis.Status = AnalysisStatus.Completed;
                analysis.Model = result.Metadata.Model;
                analysis.InputTokens = result.Metadata.TotalInputTokens;
                analysis.OutputTokens = result.Metadata.TotalOutputTokens;
                analysis.LatencyMs = result.Metadata.TotalLatencyMs;
                
                // Store result
                var analysisResult = new AnalysisResult
                {
                    AnalysisId = analysis.Id,
                    CoverageScore = result.Scores.CoverageScore,
                    GroundednessScore = result.Scores.GroundednessScore,
                    RequiredSkillsJson = JsonSerializer.Serialize(result.JdExtraction.RequiredSkills),
                    MissingRequiredJson = JsonSerializer.Serialize(result.GapAnalysis.MissingRequired),
                    MissingPreferredJson = JsonSerializer.Serialize(result.GapAnalysis.MissingPreferred)
                };
                analysis.Result = analysisResult;
                
                db.AnalysisResults.Add(analysisResult);
                
                // Log each LLM step for parse/repair observability.
                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = "jd_extraction",
                    RawResponse = result.Metadata.JdRawResponse,
                    ParseSuccess = result.Metadata.JdParseSuccess,
                    RepairAttempted = result.Metadata.JdRepairAttempted,
                    CreatedAt = DateTimeOffset.UtcNow
                });

                db.LlmLogs.Add(new LlmLog
                {
                    Id = Guid.NewGuid(),
                    AnalysisId = analysis.Id,
                    StepName = "gap_analysis",
                    RawResponse = result.Metadata.GapRawResponse,
                    ParseSuccess = result.Metadata.GapParseSuccess,
                    RepairAttempted = result.Metadata.GapRepairAttempted,
                    CreatedAt = DateTimeOffset.UtcNow
                });
                
                await db.SaveChangesAsync(ct);
                
                return Results.Created($"/api/analyses/{analysis.Id}", ToDto(analysis));
            }
            catch (Exception ex)
            {
                analysis.Status = AnalysisStatus.Failed;
                await db.SaveChangesAsync(ct);
                return Results.Problem(detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("CreateAnalysis");
        
        return app;
    }
}
