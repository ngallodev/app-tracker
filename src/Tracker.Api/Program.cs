using Microsoft.EntityFrameworkCore;
using OpenAI;
using Tracker.Api.Endpoints;
using Tracker.AI;
using Tracker.AI.Services;
using Tracker.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddOpenApi();

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

// Auto-apply migrations in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TrackerDbContext>();
    db.Database.EnsureCreated();
}

app.Run();

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
