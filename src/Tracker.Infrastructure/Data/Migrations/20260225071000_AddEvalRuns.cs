using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvalRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "eval_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    FixtureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PassedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaPassRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    GroundednessRate = table.Column<decimal>(type: "TEXT", precision: 5, scale: 2, nullable: false),
                    CoverageStabilityDiff = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false),
                    AvgLatencyMs = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: false),
                    AvgCostPerRunUsd = table.Column<decimal>(type: "TEXT", precision: 10, scale: 4, nullable: false),
                    ResultsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eval_runs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eval_runs_CreatedAt",
                table: "eval_runs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "eval_runs");
        }
    }
}
