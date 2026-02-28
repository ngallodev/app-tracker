using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tracker.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobImportAndApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DesiredSalaryMax",
                table: "resumes",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DesiredSalaryMin",
                table: "resumes",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestData",
                table: "resumes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SalaryCurrency",
                table: "resumes",
                type: "TEXT",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyCareersUrl",
                table: "jobs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmploymentType",
                table: "jobs",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestData",
                table: "jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterEmail",
                table: "jobs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterLinkedIn",
                table: "jobs",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterName",
                table: "jobs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterPhone",
                table: "jobs",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryCurrency",
                table: "jobs",
                type: "TEXT",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMax",
                table: "jobs",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryMin",
                table: "jobs",
                type: "TEXT",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkType",
                table: "jobs",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalaryAlignmentNote",
                table: "analysis_results",
                type: "TEXT",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SalaryAlignmentScore",
                table: "analysis_results",
                type: "TEXT",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ErrorCategory",
                table: "analyses",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestData",
                table: "analyses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "job_applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ResumeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AppliedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ApplicationUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsTestData = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_applications_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_applications_resumes_ResumeId",
                        column: x => x.ResumeId,
                        principalTable: "resumes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_application_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false, defaultValueSql: "newid()"),
                    JobApplicationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventType = table.Column<int>(type: "INTEGER", nullable: false),
                    EventAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Channel = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PositiveOutcome = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_job_application_events_job_applications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "job_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resumes_IsTestData",
                table: "resumes",
                column: "IsTestData");

            migrationBuilder.CreateIndex(
                name: "IX_jobs_IsTestData",
                table: "jobs",
                column: "IsTestData");

            migrationBuilder.CreateIndex(
                name: "IX_analyses_IsTestData",
                table: "analyses",
                column: "IsTestData");

            migrationBuilder.CreateIndex(
                name: "IX_job_application_events_EventAt",
                table: "job_application_events",
                column: "EventAt");

            migrationBuilder.CreateIndex(
                name: "IX_job_application_events_JobApplicationId",
                table: "job_application_events",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_CreatedAt",
                table: "job_applications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_IsTestData",
                table: "job_applications",
                column: "IsTestData");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_JobId",
                table: "job_applications",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_ResumeId",
                table: "job_applications",
                column: "ResumeId");

            migrationBuilder.CreateIndex(
                name: "IX_job_applications_Status",
                table: "job_applications",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_application_events");

            migrationBuilder.DropTable(
                name: "job_applications");

            migrationBuilder.DropIndex(
                name: "IX_resumes_IsTestData",
                table: "resumes");

            migrationBuilder.DropIndex(
                name: "IX_jobs_IsTestData",
                table: "jobs");

            migrationBuilder.DropIndex(
                name: "IX_analyses_IsTestData",
                table: "analyses");

            migrationBuilder.DropColumn(
                name: "DesiredSalaryMax",
                table: "resumes");

            migrationBuilder.DropColumn(
                name: "DesiredSalaryMin",
                table: "resumes");

            migrationBuilder.DropColumn(
                name: "IsTestData",
                table: "resumes");

            migrationBuilder.DropColumn(
                name: "SalaryCurrency",
                table: "resumes");

            migrationBuilder.DropColumn(
                name: "CompanyCareersUrl",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "IsTestData",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "RecruiterEmail",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "RecruiterLinkedIn",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "RecruiterName",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "RecruiterPhone",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryCurrency",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryMax",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryMin",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "WorkType",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "SalaryAlignmentNote",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "SalaryAlignmentScore",
                table: "analysis_results");

            migrationBuilder.DropColumn(
                name: "ErrorCategory",
                table: "analyses");

            migrationBuilder.DropColumn(
                name: "IsTestData",
                table: "analyses");
        }
    }
}
