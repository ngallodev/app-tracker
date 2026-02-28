using System.Data;
using System.ClientModel;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using Serilog;
using Tracker.Api.Endpoints;
using Tracker.Api.Extensions;
using Tracker.Api.Middleware;
using Tracker.AI;
using Tracker.AI.Cli;
using Tracker.AI.Cli.Providers;
using Tracker.AI.Services;
using Tracker.Infrastructure.Data;
using Tracker.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Tracker.Api")
        .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
});

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddAnalysisRateLimiting();
builder.Services.AddTrackerHealthChecks();
builder.Services.Configure<LlmCliOptions>(builder.Configuration.GetSection("Llm"));
builder.Services.Configure<LmStudioOptions>(builder.Configuration.GetSection("Llm:LmStudio"));

// Configure SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=tracker.db";

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddSingleton<IHeadlessCliExecutor, HeadlessCliExecutor>();
builder.Services.AddSingleton<ICliProviderAdapter, ClaudeCliProviderAdapter>();
builder.Services.AddSingleton<ICliProviderAdapter, CodexCliProviderAdapter>();
builder.Services.AddSingleton<ICliProviderAdapter, GeminiCliProviderAdapter>();
builder.Services.AddSingleton<ICliProviderAdapter, QwenCliProviderAdapter>();
builder.Services.AddSingleton<ICliProviderAdapter, KilocodeCliProviderAdapter>();
builder.Services.AddSingleton<ICliProviderAdapter, OpencodeCliProviderAdapter>();
builder.Services.AddSingleton<CliLlmClientRouter>();
builder.Services.AddSingleton<OpenAiClient>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<IConfiguration>().GetSection("Llm:LmStudio").Get<LmStudioOptions>() ?? new LmStudioOptions();
    var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
    var policyLogger = loggerFactory.CreateLogger("LmStudioPolicy");
    var startupLogger = loggerFactory.CreateLogger("LmStudioStartup");
    var openAiClientLogger = serviceProvider.GetRequiredService<ILogger<OpenAiClient>>();
    var policy = PollyPolicies.CreateLlmCallPolicy(policyLogger);
    var apiKey = ResolveLmStudioApiKey(environment.ContentRootPath, options, startupLogger);
    var baseUrl = EnsureVersionedOpenAiBaseUrl(options.BaseUrl);
    var openAiClient = new OpenAIClient(
        new ApiKeyCredential(apiKey),
        new OpenAIClientOptions { Endpoint = new Uri(baseUrl) });

    return new OpenAiClient(
        openAiClient,
        openAiClientLogger,
        policy,
        providerName: LlmProviderCatalog.Lmstudio,
        chatModel: options.ChatModel,
        embeddingModel: options.EmbeddingModel,
        maxInputTokens: options.MaxInputTokens);
});
builder.Services.AddSingleton<ILlmClient, HybridLlmClientRouter>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddSingleton<JobIngestionService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseGlobalExceptionHandling();
app.UseCorrelationId();
app.UseSecurityHeaders();
app.UseInputValidation();
app.UseRateLimiter();
app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    if (context.Response.HasStarted ||
        !string.IsNullOrWhiteSpace(context.Response.ContentType) ||
        context.Response.ContentLength.GetValueOrDefault() > 0)
    {
        return;
    }

    var (title, detail, type) = context.Response.StatusCode switch
    {
        StatusCodes.Status400BadRequest => ("Bad Request", "The request could not be understood or was invalid.", "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1"),
        StatusCodes.Status404NotFound => ("Not Found", "The requested resource was not found.", "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5"),
        StatusCodes.Status429TooManyRequests => ("Too Many Requests", "Rate limit exceeded. Please try again later.", "https://www.rfc-editor.org/rfc/rfc6585#section-4"),
        StatusCodes.Status500InternalServerError => ("Internal Server Error", "An unexpected server error occurred.", "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1"),
        _ => (string.Empty, string.Empty, string.Empty)
    };

    if (string.IsNullOrWhiteSpace(title))
    {
        return;
    }

    await context.WriteProblemDetailsAsync(context.Response.StatusCode, title, detail, type);
});
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapTrackerHealthEndpoints();

// Version endpoint
app.MapGet("/version", () => Results.Ok(new
{
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
    environment = app.Environment.EnvironmentName,
    llmMode = "hybrid_cli_openai_compatible",
    defaultProvider = builder.Configuration["Llm:DefaultProvider"] ?? LlmProviderCatalog.Claude
}))
.WithName("GetVersion");

// Job endpoints
app.MapJobEndpoints();

// Resume endpoints
app.MapResumeEndpoints();

// Analysis endpoints
app.MapAnalysisEndpoints();

// Eval endpoints
app.MapEvalEndpoints();

// Application tracking endpoints
app.MapApplicationEndpoints();

// Development helper endpoints
app.MapDevEndpoints();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    MigrationBootstrapper.ApplyMigrationsWithLegacySqliteSupport(db, logger);
}

app.Run();

static string EnsureVersionedOpenAiBaseUrl(string? baseUrl)
{
    var trimmed = string.IsNullOrWhiteSpace(baseUrl)
        ? "http://127.0.0.1:1234/v1"
        : baseUrl.Trim();
    return trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
        ? trimmed
        : $"{trimmed.TrimEnd('/')}/v1";
}

static string ResolveLmStudioApiKey(
    string contentRootPath,
    LmStudioOptions options,
    Microsoft.Extensions.Logging.ILogger logger)
{
    if (!string.IsNullOrWhiteSpace(options.ApiKeyFile))
    {
        var configuredPath = options.ApiKeyFile.Trim();
        var resolvedPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(contentRootPath, configuredPath));

        if (File.Exists(resolvedPath))
        {
            var fromFile = File.ReadAllText(resolvedPath).Trim();
            if (!string.IsNullOrWhiteSpace(fromFile))
            {
                return fromFile;
            }

            logger.LogWarning("Configured LM Studio api key file was empty: {Path}", resolvedPath);
        }
        else
        {
            logger.LogWarning("Configured LM Studio api key file was not found: {Path}", resolvedPath);
        }
    }

    return string.IsNullOrWhiteSpace(options.ApiKey) ? "lm-studio" : options.ApiKey;
}

file static class MigrationBootstrapper
{
    private static readonly string[] LegacyCoreTables = ["jobs", "resumes", "analyses", "analysis_results", "llm_logs"];
    private const string HistoryTable = "__EFMigrationsHistory";
    private const string InitialCreateMigrationSuffix = "_InitialCreate";
    private const string AddErrorMessageMigrationSuffix = "_AddAnalysisErrorMessage";

    public static void ApplyMigrationsWithLegacySqliteSupport(TrackerDbContext db, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (db.Database.IsSqlite())
        {
            BackfillMigrationHistoryForLegacySqlite(db, logger);
        }

        db.Database.Migrate();

        if (db.Database.IsSqlite())
        {
            EnsureSupplementalTables(db, logger);
        }
    }

    private static void BackfillMigrationHistoryForLegacySqlite(TrackerDbContext db, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (TableExists(db, HistoryTable))
        {
            return;
        }

        if (!HasCompleteLegacySchema(db))
        {
            return;
        }

        logger.LogWarning(
            "Detected legacy SQLite schema without migration history. Bootstrapping {HistoryTable} before migration.",
            HistoryTable);

        ExecuteNonQuery(
            db,
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """);

        var migrations = db.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            return;
        }

        var initialMigration = migrations.FirstOrDefault(m => m.EndsWith(InitialCreateMigrationSuffix, StringComparison.Ordinal));
        if (initialMigration is not null)
        {
            MarkMigrationAsApplied(db, initialMigration);
        }

        var analysesHasErrorMessageColumn = ColumnExists(db, "analyses", "ErrorMessage");
        if (analysesHasErrorMessageColumn)
        {
            var addErrorMessageMigration = migrations.FirstOrDefault(m => m.EndsWith(AddErrorMessageMigrationSuffix, StringComparison.Ordinal));
            if (addErrorMessageMigration is not null)
            {
                MarkMigrationAsApplied(db, addErrorMessageMigration);
            }
        }
    }

    private static void MarkMigrationAsApplied(TrackerDbContext db, string migrationId)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ($migrationId, $productVersion);
            """;
        AddParameter(command, "$migrationId", migrationId);
        AddParameter(command, "$productVersion", GetProductVersion());
        EnsureOpenAndExecuteNonQuery(command);
    }

    private static bool HasCompleteLegacySchema(TrackerDbContext db)
        => LegacyCoreTables.All(tableName => TableExists(db, tableName));

    private static bool TableExists(TrackerDbContext db, string tableName)
    {
        const string sql = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        var count = ExecuteScalarInt64(db, sql, ("$name", tableName));
        return count > 0;
    }

    private static bool ColumnExists(TrackerDbContext db, string tableName, string columnName)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        var openedHere = command.Connection?.State != ConnectionState.Open;
        if (openedHere)
        {
            command.Connection?.Open();
        }

        try
        {
            using var reader = command.ExecuteReader();
            var nameOrdinal = reader.GetOrdinal("name");
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(nameOrdinal), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (openedHere)
            {
                command.Connection?.Close();
            }
        }
    }

    private static long ExecuteScalarInt64(TrackerDbContext db, string sql, (string Name, string Value) parameter)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        AddParameter(command, parameter.Name, parameter.Value);
        return EnsureOpenAndExecuteScalarInt64(command);
    }

    private static void ExecuteNonQuery(TrackerDbContext db, string sql)
    {
        using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        EnsureOpenAndExecuteNonQuery(command);
    }

    private static void EnsureSupplementalTables(TrackerDbContext db, Microsoft.Extensions.Logging.ILogger logger)
    {
        EnsureLegacyColumns(db, logger);

        ExecuteNonQuery(
            db,
            """
            CREATE TABLE IF NOT EXISTS "eval_runs" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_eval_runs" PRIMARY KEY,
                "Mode" TEXT NOT NULL,
                "FixtureCount" INTEGER NOT NULL,
                "PassedCount" INTEGER NOT NULL,
                "FailedCount" INTEGER NOT NULL,
                "SchemaPassRate" TEXT NOT NULL,
                "GroundednessRate" TEXT NOT NULL,
                "CoverageStabilityDiff" TEXT NOT NULL,
                "AvgLatencyMs" TEXT NOT NULL,
                "AvgCostPerRunUsd" TEXT NOT NULL,
                "ResultsJson" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_eval_runs_CreatedAt" ON "eval_runs" ("CreatedAt");

            CREATE TABLE IF NOT EXISTS "analysis_request_metrics" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_analysis_request_metrics" PRIMARY KEY,
                "JobId" TEXT NOT NULL,
                "ResumeId" TEXT NOT NULL,
                "JobHash" TEXT NOT NULL,
                "ResumeHash" TEXT NOT NULL,
                "CacheHit" INTEGER NOT NULL,
                "RequestMode" TEXT NOT NULL,
                "Outcome" TEXT NOT NULL,
                "UsedGapLlmFallback" INTEGER NOT NULL,
                "InputTokens" INTEGER NOT NULL,
                "OutputTokens" INTEGER NOT NULL,
                "LatencyMs" INTEGER NOT NULL,
                "Provider" TEXT NULL,
                "ErrorCategory" TEXT NULL,
                "CreatedAt" TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_analysis_request_metrics_CreatedAt" ON "analysis_request_metrics" ("CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_analysis_request_metrics_JobHash" ON "analysis_request_metrics" ("JobHash");
            CREATE INDEX IF NOT EXISTS "IX_analysis_request_metrics_RequestMode" ON "analysis_request_metrics" ("RequestMode");
            CREATE INDEX IF NOT EXISTS "IX_analysis_request_metrics_CacheHit" ON "analysis_request_metrics" ("CacheHit");
            """);
    }

    private static void EnsureLegacyColumns(TrackerDbContext db, Microsoft.Extensions.Logging.ILogger logger)
    {
        if (!ColumnExists(db, "analyses", "ErrorMessage"))
        {
            logger.LogWarning(
                "Legacy SQLite schema missing analyses.ErrorMessage column. Applying compatibility column patch.");
            ExecuteNonQuery(db, "ALTER TABLE \"analyses\" ADD COLUMN \"ErrorMessage\" TEXT NULL;");
        }
    }

    private static long EnsureOpenAndExecuteScalarInt64(IDbCommand command)
    {
        var openedHere = command.Connection?.State != ConnectionState.Open;
        if (openedHere)
        {
            command.Connection?.Open();
        }

        try
        {
            return Convert.ToInt64(command.ExecuteScalar());
        }
        finally
        {
            if (openedHere)
            {
                command.Connection?.Close();
            }
        }
    }

    private static void EnsureOpenAndExecuteNonQuery(IDbCommand command)
    {
        var openedHere = command.Connection?.State != ConnectionState.Open;
        if (openedHere)
        {
            command.Connection?.Open();
        }

        try
        {
            command.ExecuteNonQuery();
        }
        finally
        {
            if (openedHere)
            {
                command.Connection?.Close();
            }
        }
    }

    private static void AddParameter(IDbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string GetProductVersion()
        => typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "unknown";
}
