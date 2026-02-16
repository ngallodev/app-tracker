using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Company = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DescriptionText = table.Column<string>(type: "TEXT", nullable: true),
                    DescriptionHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "resumes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResumeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PromptVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_analyses_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_analyses_resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_results",
                columns: table => new
                {
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RequiredSkillsJson = table.Column<string>(type: "TEXT", nullable: true),
                    MissingRequiredJson = table.Column<string>(type: "TEXT", nullable: true),
                    MissingPreferredJson = table.Column<string>(type: "TEXT", nullable: true),
                    CoverageScore = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    GroundednessScore = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_results", x => x.AnalysisId);
                    table.ForeignKey(
                        name: "FK_analysis_results_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    AnalysisId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RawResponse = table.Column<string>(type: "TEXT", nullable: true),
                    ParseSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    RepairAttempted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_llm_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_llm_logs_analyses_AnalysisId",
                        column: x => x.AnalysisId,
                        principalTable: "analyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_JobId_ResumeId_CreatedAt",
                table: "analyses",
                columns: new[] { "JobId", "ResumeId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_analyses_ResumeId",
                table: "analyses",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_CreatedAt",
                table: "jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_DescriptionHash",
                table: "jobs",
                column: "DescriptionHash");

            migrationBuilder.CreateIndex(
                name: "IX_llm_logs_AnalysisId",
                table: "llm_logs",
                column: "AnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_resumes_ContentHash",
                table: "resumes",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_resumes_CreatedAt",
                table: "resumes",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_results");

            migrationBuilder.DropTable(
                name: "llm_logs");

            migrationBuilder.DropTable(
                name: "analyses");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "resumes");
        }
    }
}
