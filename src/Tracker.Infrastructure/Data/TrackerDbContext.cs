using Microsoft.EntityFrameworkCore;
using Tracker.Domain.Entities;

namespace Tracker.Infrastructure.Data;

public class TrackerDbContext : DbContext
{
    public TrackerDbContext(DbContextOptions<TrackerDbContext> options) : base(options) { }
    
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Resume> Resumes => Set<Resume>();
    public DbSet<Analysis> Analyses => Set<Analysis>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<LlmLog> LlmLogs => Set<LlmLog>();
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<AnalysisRequestMetric> AnalysisRequestMetrics => Set<AnalysisRequestMetric>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Jobs table
        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Company).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DescriptionText);
            entity.Property(e => e.DescriptionHash).HasMaxLength(64);
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasIndex(e => e.DescriptionHash);
            entity.HasIndex(e => e.CreatedAt);
        });
        
        // Resumes table
        modelBuilder.Entity<Resume>(entity =>
        {
            entity.ToTable("resumes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ContentHash).HasMaxLength(64);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.UpdatedAt).IsRequired();
            
            entity.HasIndex(e => e.ContentHash);
            entity.HasIndex(e => e.CreatedAt);
        });
        
        // Analyses table
        modelBuilder.Entity<Analysis>(entity =>
        {
            entity.ToTable("analyses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.JobId).IsRequired();
            entity.Property(e => e.ResumeId).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(50);
            entity.Property(e => e.PromptVersion).HasMaxLength(20);
            entity.Property(e => e.SchemaVersion).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Job)
                .WithMany(j => j.Analyses)
                .HasForeignKey(e => e.JobId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Resume)
                .WithMany(r => r.Analyses)
                .HasForeignKey(e => e.ResumeId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => new { e.JobId, e.ResumeId, e.CreatedAt });
        });
        
        // AnalysisResults table
        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.ToTable("analysis_results");
            entity.HasKey(e => e.AnalysisId);
            entity.Property(e => e.CoverageScore).HasPrecision(5, 2);
            entity.Property(e => e.GroundednessScore).HasPrecision(5, 2);
            
            entity.HasOne(e => e.Analysis)
                .WithOne(a => a.Result)
                .HasForeignKey<AnalysisResult>(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // LlmLogs table
        modelBuilder.Entity<LlmLog>(entity =>
        {
            entity.ToTable("llm_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.StepName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ParseSuccess).IsRequired();
            entity.Property(e => e.RepairAttempted).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            
            entity.HasOne(e => e.Analysis)
                .WithMany(a => a.Logs)
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.AnalysisId);
        });

        modelBuilder.Entity<EvalRun>(entity =>
        {
            entity.ToTable("eval_runs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.Mode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SchemaPassRate).HasPrecision(5, 2);
            entity.Property(e => e.GroundednessRate).HasPrecision(5, 2);
            entity.Property(e => e.CoverageStabilityDiff).HasPrecision(8, 4);
            entity.Property(e => e.AvgLatencyMs).HasPrecision(10, 2);
            entity.Property(e => e.AvgCostPerRunUsd).HasPrecision(10, 4);
            entity.Property(e => e.ResultsJson).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<AnalysisRequestMetric>(entity =>
        {
            entity.ToTable("analysis_request_metrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.JobId).IsRequired();
            entity.Property(e => e.ResumeId).IsRequired();
            entity.Property(e => e.JobHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ResumeHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.CacheHit).IsRequired();
            entity.Property(e => e.RequestMode).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Outcome).IsRequired().HasMaxLength(16);
            entity.Property(e => e.UsedGapLlmFallback).IsRequired();
            entity.Property(e => e.InputTokens).IsRequired();
            entity.Property(e => e.OutputTokens).IsRequired();
            entity.Property(e => e.LatencyMs).IsRequired();
            entity.Property(e => e.Provider).HasMaxLength(64);
            entity.Property(e => e.ErrorCategory).HasMaxLength(32);
            entity.Property(e => e.CreatedAt).IsRequired();

            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.JobHash);
            entity.HasIndex(e => e.RequestMode);
            entity.HasIndex(e => e.CacheHit);
        });
    }
}
