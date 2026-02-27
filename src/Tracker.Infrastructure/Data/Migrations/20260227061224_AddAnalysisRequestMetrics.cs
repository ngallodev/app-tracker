using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalysisRequestMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_request_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResumeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ResumeHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CacheHit = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequestMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    UsedGapLlmFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    InputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputTokens = table.Column<int>(type: "INTEGER", nullable: false),
                    LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ErrorCategory = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_request_metrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_analysis_request_metrics_CacheHit",
                table: "analysis_request_metrics",
                column: "CacheHit");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_request_metrics_CreatedAt",
                table: "analysis_request_metrics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_request_metrics_JobHash",
                table: "analysis_request_metrics",
                column: "JobHash");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_request_metrics_RequestMode",
                table: "analysis_request_metrics",
                column: "RequestMode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_request_metrics");
        }
    }
}
