using Microsoft.EntityFrameworkCore;
using Tracker.Api.Services;
using Tracker.Domain.DTOs;
using Tracker.Domain.DTOs.Requests;
using Tracker.Domain.Entities;
using Tracker.Infrastructure;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.Endpoints;

public static class JobsEndpoints
{
    private static JobDto ToDto(Job job) => new(
        job.Id,
        job.Title,
        job.Company,
        job.DescriptionText,
        job.SourceUrl,
        job.WorkType,
        job.EmploymentType,
        job.SalaryMin,
        job.SalaryMax,
        job.SalaryCurrency,
        job.RecruiterName,
        job.RecruiterEmail,
        job.RecruiterPhone,
        job.RecruiterLinkedIn,
        job.CompanyCareersUrl,
        job.IsTestData,
        job.CreatedAt,
        job.UpdatedAt
    );

    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/jobs");

        group.MapGet("/", async (TrackerDbContext db, CancellationToken ct) =>
        {
            var jobs = await db.Jobs.AsNoTracking().ToListAsync(ct);
            return Results.Ok(jobs.OrderByDescending(j => j.CreatedAt).Select(ToDto).ToList());
        })
        .WithName("GetAllJobs");

        group.MapGet("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([id], ct);
            return job is null ? Results.NotFound() : Results.Ok(ToDto(job));
        })
        .WithName("GetJobById");

        group.MapPost("/extract-from-url", async (ImportJobFromUrlRequest request, JobIngestionService ingest, CancellationToken ct) =>
        {
            var imported = await ingest.TryFetchFromUrlAsync(request.SourceUrl, ct);
            if (imported is null || string.IsNullOrWhiteSpace(imported.DescriptionText))
            {
                return Results.Problem(
                    detail: "Could not fetch a parseable job description from the URL.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "URL Import Failed");
            }

            var derived = ingest.ExtractMetadata(imported.DescriptionText, request.SourceUrl);
            return Results.Ok(new
            {
                title = imported.Title,
                company = imported.Company,
                descriptionText = imported.DescriptionText,
                sourceUrl = request.SourceUrl,
                workType = derived.WorkType,
                employmentType = derived.EmploymentType,
                salaryMin = derived.SalaryMin,
                salaryMax = derived.SalaryMax,
                salaryCurrency = derived.SalaryCurrency,
                recruiterEmail = derived.RecruiterEmail,
                recruiterPhone = derived.RecruiterPhone,
                recruiterLinkedIn = derived.RecruiterLinkedIn,
                companyCareersUrl = derived.CompanyCareersUrl
            });
        })
        .WithName("ExtractJobFromUrl");

        group.MapPost("/", async (CreateJobRequest request, TrackerDbContext db, JobIngestionService ingest, CancellationToken ct) =>
        {
            var title = request.Title?.Trim();
            var company = request.Company?.Trim();
            var descriptionText = request.DescriptionText;

            if (!string.IsNullOrWhiteSpace(request.SourceUrl) &&
                (string.IsNullOrWhiteSpace(descriptionText) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(company)))
            {
                var imported = await ingest.TryFetchFromUrlAsync(request.SourceUrl, ct);
                if (imported is not null)
                {
                    title ??= imported.Title;
                    company ??= imported.Company;
                    descriptionText ??= imported.DescriptionText;
                }
            }

            title = string.IsNullOrWhiteSpace(title) ? "Imported Job" : title;
            company = string.IsNullOrWhiteSpace(company) ? "Unknown Company" : company;

            var derived = string.IsNullOrWhiteSpace(descriptionText)
                ? null
                : ingest.ExtractMetadata(descriptionText, request.SourceUrl);

            var job = new Job
            {
                Id = Guid.NewGuid(),
                Title = title,
                Company = company,
                DescriptionText = descriptionText,
                DescriptionHash = descriptionText is not null ? HashUtility.ComputeNormalizedHash(descriptionText) : null,
                SourceUrl = request.SourceUrl,
                WorkType = request.WorkType ?? derived?.WorkType,
                EmploymentType = request.EmploymentType ?? derived?.EmploymentType,
                SalaryMin = request.SalaryMin ?? derived?.SalaryMin,
                SalaryMax = request.SalaryMax ?? derived?.SalaryMax,
                SalaryCurrency = request.SalaryCurrency ?? derived?.SalaryCurrency,
                RecruiterName = request.RecruiterName,
                RecruiterEmail = request.RecruiterEmail ?? derived?.RecruiterEmail,
                RecruiterPhone = request.RecruiterPhone ?? derived?.RecruiterPhone,
                RecruiterLinkedIn = request.RecruiterLinkedIn ?? derived?.RecruiterLinkedIn,
                CompanyCareersUrl = request.CompanyCareersUrl ?? derived?.CompanyCareersUrl,
                IsTestData = request.IsTestData,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            db.Jobs.Add(job);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/jobs/{job.Id}", ToDto(job));
        })
        .WithName("CreateJob");

        group.MapPut("/{id:guid}", async (Guid id, UpdateJobRequest request, TrackerDbContext db, JobIngestionService ingest, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([id], ct);
            if (job is null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                job.Title = request.Title;
            }

            if (!string.IsNullOrWhiteSpace(request.Company))
            {
                job.Company = request.Company;
            }

            if (request.DescriptionText is not null)
            {
                job.DescriptionText = request.DescriptionText;
                job.DescriptionHash = HashUtility.ComputeNormalizedHash(request.DescriptionText);
                var derived = ingest.ExtractMetadata(request.DescriptionText, request.SourceUrl ?? job.SourceUrl);
                job.WorkType = request.WorkType ?? derived.WorkType;
                job.EmploymentType = request.EmploymentType ?? derived.EmploymentType;
                job.SalaryMin = request.SalaryMin ?? derived.SalaryMin;
                job.SalaryMax = request.SalaryMax ?? derived.SalaryMax;
                job.SalaryCurrency = request.SalaryCurrency ?? derived.SalaryCurrency;
                job.RecruiterEmail = request.RecruiterEmail ?? derived.RecruiterEmail;
                job.RecruiterPhone = request.RecruiterPhone ?? derived.RecruiterPhone;
                job.RecruiterLinkedIn = request.RecruiterLinkedIn ?? derived.RecruiterLinkedIn;
                job.CompanyCareersUrl = request.CompanyCareersUrl ?? derived.CompanyCareersUrl;
            }

            if (request.SourceUrl is not null)
            {
                job.SourceUrl = request.SourceUrl;
            }

            if (request.WorkType is not null)
            {
                job.WorkType = request.WorkType;
            }

            if (request.EmploymentType is not null)
            {
                job.EmploymentType = request.EmploymentType;
            }

            if (request.SalaryMin.HasValue)
            {
                job.SalaryMin = request.SalaryMin.Value;
            }

            if (request.SalaryMax.HasValue)
            {
                job.SalaryMax = request.SalaryMax.Value;
            }

            if (request.SalaryCurrency is not null)
            {
                job.SalaryCurrency = request.SalaryCurrency;
            }

            if (request.RecruiterName is not null)
            {
                job.RecruiterName = request.RecruiterName;
            }

            if (request.RecruiterEmail is not null)
            {
                job.RecruiterEmail = request.RecruiterEmail;
            }

            if (request.RecruiterPhone is not null)
            {
                job.RecruiterPhone = request.RecruiterPhone;
            }

            if (request.RecruiterLinkedIn is not null)
            {
                job.RecruiterLinkedIn = request.RecruiterLinkedIn;
            }

            if (request.CompanyCareersUrl is not null)
            {
                job.CompanyCareersUrl = request.CompanyCareersUrl;
            }

            if (request.IsTestData.HasValue)
            {
                job.IsTestData = request.IsTestData.Value;
            }

            job.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(job));
        })
        .WithName("UpdateJob");

        group.MapDelete("/{id:guid}", async (Guid id, TrackerDbContext db, CancellationToken ct) =>
        {
            var job = await db.Jobs.FindAsync([id], ct);
            if (job is null)
            {
                return Results.NotFound();
            }

            db.Jobs.Remove(job);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithName("DeleteJob");

        return app;
    }
}
