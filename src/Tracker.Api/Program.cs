using System.Data;
using Microsoft.EntityFrameworkCore;
using OpenAI;
using Tracker.Api.Endpoints;
using Tracker.Api.Extensions;
using Tracker.Api.Middleware;
using Tracker.AI;
using Tracker.AI.Services;
using Tracker.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();
builder.Services.AddAnalysisRateLimiting();

// Configure SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=tracker.db";

builder.Services.AddDbContext<TrackerDbContext>(options =>
    options.UseSqlite(connectionString));

// Configure OpenAI
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (!string.IsNullOrEmpty(openAiApiKey))
{
    var openAIClient = new OpenAIClient(openAiApiKey);
    builder.Services.AddSingleton<OpenAIClient>(openAIClient);
    builder.Services.AddSingleton<ILlmClient>(sp => 
        new OpenAiClient(
            openAIClient,
            sp.GetRequiredService<ILogger<OpenAiClient>>(),
            builder.Configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini",
            builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small"));
    builder.Services.AddScoped<IAnalysisService, AnalysisService>();
}
else
{
    // In development without API key, use a placeholder
    builder.Services.AddSingleton<ILlmClient>(sp => 
        new FakeLlmClient(sp.GetRequiredService<ILogger<FakeLlmClient>>()));
    builder.Services.AddScoped<IAnalysisService, AnalysisService>();
}

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseInputValidation();
app.UseRateLimiter();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health endpoint
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
    .WithName("HealthCheck");

// Version endpoint
app.MapGet("/version", () => Results.Ok(new { 
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
    environment = app.Environment.EnvironmentName,
    openAiConfigured = !string.IsNullOrEmpty(openAiApiKey)
}))
.WithName("GetVersion");

// Job endpoints
app.MapJobEndpoints();

// Resume endpoints
app.MapResumeEndpoints();

// Analysis endpoints
app.MapAnalysisEndpoints();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");
    MigrationBootstrapper.ApplyMigrationsWithLegacySqliteSupport(db, logger);
}

app.Run();

file static class MigrationBootstrapper
{
    private static readonly string[] LegacyCoreTables = ["jobs", "resumes", "analyses", "analysis_results", "llm_logs"];
    private const string HistoryTable = "__EFMigrationsHistory";
    private const string InitialCreateMigrationSuffix = "_InitialCreate";
    private const string AddErrorMessageMigrationSuffix = "_AddAnalysisErrorMessage";

    public static void ApplyMigrationsWithLegacySqliteSupport(TrackerDbContext db, ILogger logger)
    {
        if (db.Database.IsSqlite())
        {
            BackfillMigrationHistoryForLegacySqlite(db, logger);
        }

        db.Database.Migrate();
    }

    private static void BackfillMigrationHistoryForLegacySqlite(TrackerDbContext db, ILogger logger)
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

// Fake LLM client for development without API key
file record FakeLlmClient(ILogger<FakeLlmClient> Logger) : ILlmClient
{
    public Task<LlmResult<T>> CompleteStructuredAsync<T>(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) where T : class
    {
        Logger.LogWarning("Using FakeLlmClient - Set OPENAI_API_KEY environment variable for real LLM calls");
        throw new LlmException("OpenAI API key not configured. Set OPENAI_API_KEY environment variable.");
    }

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("Using FakeLlmClient - Set OPENAI_API_KEY environment variable for real LLM calls");
        throw new LlmException("OpenAI API key not configured. Set OPENAI_API_KEY environment variable.");
    }

    public int CountTokens(string text) => text.Length / 4;
}
