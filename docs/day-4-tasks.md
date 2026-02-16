# AI Job Application Tracker — Day 4 Tasks (Reliability + Observability)

**Goal:** Production-grade resilience and monitoring.

---

## 1. Overview

### Day 4 Objectives
- Implement Polly policies for LLM API resilience (retry, circuit breaker, timeout)
- Add correlation ID middleware for request tracing
- Configure structured logging with Serilog
- Implement global error handling with ProblemDetails (RFC7807)
- Security hardening (input validation, rate limiting)
- Enhance health checks for production readiness

### Dependencies on Day 3 (Complete)
- ✅ Analysis endpoint working with caching
- ✅ React frontend with CRUD pages
- ✅ Eval harness with fixtures
- ✅ End-to-end flow < 30 seconds
- ✅ Analysis page displays all metrics

### Files Already Created (Day 3)
```
src/Tracker.Api/
├── Program.cs
├── Endpoints/
│   ├── JobsEndpoints.cs
│   ├── ResumesEndpoints.cs
│   └── AnalysesEndpoints.cs
src/Tracker.AI/
├── OpenAiClient.cs (basic retry in place)
├── ILlmClient.cs
├── Services/
│   └── AnalysisService.cs
web/ (React frontend)
```

---

## 2. Task Breakdown Table

| Task ID | Description | Complexity | Est. Effort (tokens) | Dependencies | Files to Create/Modify | Parallelization |
|---------|-------------|------------|---------------------|--------------|------------------------|-----------------|
| 4.1 | Polly Policies | Medium | ~8K | Day 3 | `OpenAiClient.cs`, `Program.cs`, `PollyPolicies.cs` | Lane A |
| 4.2 | Correlation ID Middleware | Easy | ~4K | None | `CorrelationIdMiddleware.cs`, `Program.cs` | Lane B |
| 4.3 | Structured Logging (Serilog) | Easy | ~5K | None | `Program.cs`, `appsettings.json` | Lane B |
| 4.4 | Global Error Handling | Medium | ~6K | 4.3 | `ExceptionMiddleware.cs`, `ProblemDetailsExtensions.cs` | Lane C |
| 4.5 | Security Hardening | Medium | ~7K | None | `InputValidationMiddleware.cs`, `RateLimitingExtensions.cs` | Lane A |
| 4.6 | Health Check Enhancement | Easy | ~4K | 4.4 | `HealthEndpoints.cs`, `Program.cs` | Lane C |

**Total Estimated Effort:** ~34K tokens

---

## 3. Detailed Task Specifications

---

### 4.1: Polly Policies (Retry, Circuit Breaker, Timeout)

**Goal:** Wrap all LLM API calls with production-grade resilience policies.

#### Requirements

1. **Retry Policy**
   - 2 retry attempts (3 total calls max)
   - Exponential backoff: 1s, 3s
   - Only retry on transient errors (5xx, rate limits, timeouts)
   - Log each retry attempt with reason

2. **Circuit Breaker Policy**
   - Open after 5 consecutive failures within 60-second window
   - Stay open for 30 seconds (cooldown period)
   - Half-open state allows 1 test request
   - Log state transitions (open/half-open/closed)

3. **Timeout Policy**
   - 30-second timeout per request
   - 60-second timeout for full pipeline (extraction + gap analysis)
   - Return timeout exception with clear message

4. **Policy Coordination**
   - Wrap: Timeout → Retry → Circuit Breaker
   - Use `Policy.WrapAsync()` for composition
   - Shared circuit breaker across all LLM calls

#### Files to Create/Modify

```
src/Tracker.AI/
├── PollyPolicies.cs (NEW)
├── OpenAiClient.cs (MODIFY - inject policies)
└── ILlmClient.cs (no changes needed)

src/Tracker.Api/
└── Program.cs (MODIFY - register Polly services)
```

#### Policy Configuration

```csharp
// src/Tracker.AI/PollyPolicies.cs
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Tracker.AI;

public static class PollyPolicies
{
    public static AsyncRetryPolicy GetRetryPolicy(ILogger logger)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .OrResult<HttpResponseMessage>(r => 
                (int)r.StatusCode >= 500 || r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, timeSpan, retryCount, context) =>
                {
                    logger.LogWarning(
                        "LLM API retry {RetryCount} after {Delay}s. Reason: {Reason}",
                        retryCount,
                        timeSpan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString());
                });
    }

    public static AsyncCircuitBreakerPolicy GetCircuitBreakerPolicy(ILogger logger)
    {
        return Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .Or<Exception>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    logger.LogError(
                        "Circuit breaker OPENED for {Duration}s due to: {Exception}",
                        duration.TotalSeconds,
                        exception.Message);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker CLOSED - requests flowing normally");
                },
                onHalfOpen: () =>
                {
                    logger.LogWarning("Circuit breaker HALF-OPEN - testing with next request");
                });
    }

    public static AsyncTimeoutPolicy GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync(TimeSpan.FromSeconds(30));
    }

    public static AsyncPolicyWrap GetCombinedPolicy(ILogger logger)
    {
        return Policy.WrapAsync(
            GetTimeoutPolicy(),
            GetRetryPolicy(logger),
            GetCircuitBreakerPolicy(logger));
    }
}
```

#### OpenAiClient Modification

```csharp
// Modified OpenAiClient.cs constructor
public class OpenAiClient : ILlmClient
{
    private readonly OpenAIClient _client;
    private readonly string _chatModel;
    private readonly string _embeddingModel;
    private readonly ILogger<OpenAiClient> _logger;
    private readonly IAsyncPolicy _resiliencePolicy;

    public OpenAiClient(
        OpenAIClient client,
        ILogger<OpenAiClient> logger,
        IAsyncPolicy resiliencePolicy,
        string chatModel = "gpt-4o-mini",
        string embeddingModel = "text-embedding-3-small")
    {
        _client = client;
        _logger = logger;
        _resiliencePolicy = resiliencePolicy;
        _chatModel = chatModel;
        _embeddingModel = embeddingModel;
    }

    public async Task<LlmResult<T>> CompleteStructuredAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) where T : class
    {
        var sw = Stopwatch.StartNew();
        
        // Execute within resilience policy
        var result = await _resiliencePolicy.ExecuteAsync(async () =>
        {
            // ... existing implementation
        });
        
        // ... rest of implementation
    }
}
```

#### Program.cs Registration

```csharp
// In Program.cs
var loggerFactory = builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
var pollyLogger = loggerFactory.CreateLogger("Polly");

var retryPolicy = PollyPolicies.GetRetryPolicy(pollyLogger);
var circuitBreakerPolicy = PollyPolicies.GetCircuitBreakerPolicy(pollyLogger);
var timeoutPolicy = PollyPolicies.GetTimeoutPolicy();

var combinedPolicy = Policy.WrapAsync(timeoutPolicy, retryPolicy, circuitBreakerPolicy);

builder.Services.AddSingleton<IAsyncPolicy>(combinedPolicy);

// Update OpenAiClient registration
builder.Services.AddSingleton<ILlmClient>(sp => 
    new OpenAiClient(
        openAIClient,
        sp.GetRequiredService<ILogger<OpenAiClient>>(),
        sp.GetRequiredService<IAsyncPolicy>(),
        builder.Configuration["OpenAI:ChatModel"] ?? "gpt-4o-mini",
        builder.Configuration["OpenAI:EmbeddingModel"] ?? "text-embedding-3-small"));
```

#### Acceptance Criteria
- [ ] Retry policy attempts 2 retries with exponential backoff
- [ ] Circuit breaker opens after 5 failures, stays open for 30s
- [ ] Timeout policy cancels requests after 30s
- [ ] All policy events logged with correlation ID

---

### 4.2: Correlation ID Middleware

**Goal:** Unique request identifier for distributed tracing.

#### Requirements

1. **Header Handling**
   - Check for `X-Correlation-ID` header on incoming requests
   - Generate new GUID if not present
   - Add to response headers

2. **Context Propagation**
   - Store in `HttpContext.Items` for access in handlers
   - Set in `LogContext` for Serilog enrichment
   - Available in all log entries for the request

3. **Downstream Propagation**
   - Include in outgoing HTTP client headers (if any)
   - Pass to LLM client for logging

#### Files to Create/Modify

```
src/Tracker.Api/Middleware/
├── CorrelationIdMiddleware.cs (NEW)
└── CorrelationIdExtensions.cs (NEW)

src/Tracker.Api/Program.cs (MODIFY - register middleware)
```

#### Implementation

```csharp
// src/Tracker.Api/Middleware/CorrelationIdMiddleware.cs
using Serilog.Context;

namespace Tracker.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        
        context.Items[CorrelationIdHeader] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogDebug("Processing request {Method} {Path}", 
                context.Request.Method, context.Request.Path);
            
            await _next(context);
        }
    }

    private static string GetCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var id) 
            && !string.IsNullOrEmpty(id))
        {
            return id.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}

// src/Tracker.Api/Middleware/CorrelationIdExtensions.cs
namespace Tracker.Api.Middleware;

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items["X-Correlation-ID"]?.ToString();
    }
}
```

#### Registration in Program.cs

```csharp
// Add early in the pipeline
app.UseCorrelationId();
```

#### Acceptance Criteria
- [ ] Every request has a correlation ID (from header or generated)
- [ ] Correlation ID appears in response headers
- [ ] Correlation ID present in all structured logs
- [ ] Works with Serilog LogContext enrichment

---

### 4.3: Structured Logging (Serilog)

**Goal:** Production-ready structured logging with multiple sinks and enrichment.

#### Requirements

1. **Sinks Configuration**
   - Console sink (development, colored output)
   - File sink (production, rolling files)
   - Optional: Seq/DataDog for production observability

2. **Enrichment**
   - Correlation ID
   - Environment name
   - Application version
   - Request ID
   - User agent (sanitized)

3. **Output Template**
   - JSON format for production
   - Human-readable for development
   - Include source context

4. **Log Levels**
   - Information for API endpoints
   - Debug for LLM interactions
   - Warning for retries, circuit breaker events
   - Error for exceptions

#### Files to Create/Modify

```
src/Tracker.Api/
├── Program.cs (MODIFY - Serilog bootstrap)
├── appsettings.json (MODIFY - Serilog config)
└── appsettings.Development.json (MODIFY - dev overrides)
```

#### Implementation

```csharp
// src/Tracker.Api/Program.cs (top)
using Serilog;
using Serilog.Events;

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "JobTracker")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
```

```json
// src/Tracker.Api/appsettings.json
{
  "Serilog": {
    "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.EntityFrameworkCore": "Warning",
        "Tracker.AI": "Debug",
        "Polly": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/tracker-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": [ "FromLogContext", "WithMachineName", "WithThreadId" ]
  }
}
```

```json
// src/Tracker.Api/appsettings.Development.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {SourceContext}: {Message:lj}{NewLine}{Exception}"
        }
      }
    ]
  }
}
```

#### LLM Logging Enhancement

```csharp
// Enhanced LLM logging in OpenAiClient.cs
using (LogContext.PushProperty("LlmModel", _chatModel))
using (LogContext.PushProperty("LlmCallId", Guid.NewGuid()))
{
    _logger.LogDebug(
        "LLM request: {InputTokens} input tokens, prompt length {PromptLength}",
        estimatedInputTokens,
        userPrompt.Length);

    // ... call LLM ...

    _logger.LogInformation(
        "LLM response: {OutputTokens} tokens, {LatencyMs}ms, parse success: {ParseSuccess}",
        result.Usage.OutputTokens,
        result.LatencyMs,
        result.ParseSuccess);
}
```

#### Acceptance Criteria
- [ ] Serilog configured as primary logger
- [ ] Console output colored in development
- [ ] Log files rolling daily, retained 7 days
- [ ] Correlation ID in every log entry
- [ ] LLM calls logged with token counts and latency

---

### 4.4: Global Error Handling (ProblemDetails RFC7807)

**Goal:** Consistent, machine-readable error responses across all endpoints.

#### Requirements

1. **ProblemDetails Format (RFC7807)**
   ```json
   {
     "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
     "title": "One or more validation errors occurred.",
     "status": 400,
     "detail": "The JobId field is required.",
     "instance": "/api/analyses",
     "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
     "correlationId": "a1b2c3d4e5f6"
   }
   ```

2. **Exception Mapping**
   | Exception Type | Status Code | Title |
   |---------------|-------------|-------|
   | ValidationException | 400 | Validation Failed |
   | NotFoundException | 404 | Resource Not Found |
   | LlmException | 502 | LLM Service Error |
   | CircuitBrokenException | 503 | Service Unavailable |
   | TimeoutException | 504 | Request Timeout |
   | Exception | 500 | Internal Server Error |

3. **Sensitive Data Handling**
   - Never expose stack traces in production
   - Redact API keys, connection strings
   - Sanitize error messages

4. **Logging Integration**
   - Log all exceptions with correlation ID
   - Include stack trace in logs (not in response)
   - Different log levels by status code

#### Files to Create/Modify

```
src/Tracker.Api/Middleware/
├── ExceptionMiddleware.cs (NEW)
└── ExceptionMiddlewareExtensions.cs (NEW)

src/Tracker.Domain/Exceptions/
├── ValidationException.cs (NEW)
├── NotFoundException.cs (NEW)
└── LlmException.cs (EXISTS - update)

src/Tracker.Api/ProblemDetails/
└── ProblemDetailsOptions.cs (NEW)
```

#### Implementation

```csharp
// src/Tracker.Api/Middleware/ExceptionMiddleware.cs
using System.Net;
using System.Text.Json;
using Serilog.Context;

namespace Tracker.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        var (statusCode, title, detail) = exception switch
        {
            ValidationException vex => ((int)HttpStatusCode.BadRequest, "Validation Failed", vex.Message),
            NotFoundException nex => ((int)HttpStatusCode.NotFound, "Resource Not Found", nex.Message),
            LlmException lex => ((int)HttpStatusCode.BadGateway, "LLM Service Error", 
                _env.IsDevelopment() ? lex.Message : "An error occurred while processing your request."),
            BrokenCircuitException => ((int)HttpStatusCode.ServiceUnavailable, "Service Unavailable",
                "The service is temporarily unavailable. Please try again later."),
            TimeoutException => ((int)HttpStatusCode.GatewayTimeout, "Request Timeout",
                "The request timed out. Please try again."),
            TaskCanceledException => ((int)HttpStatusCode.GatewayTimeout, "Request Timeout",
                "The request was cancelled."),
            _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error",
                _env.IsDevelopment() ? exception.Message : "An unexpected error occurred.")
        };

        LogException(exception, statusCode, correlationId);

        var problem = new ProblemDetails
        {
            Type = GetProblemType(statusCode),
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["correlationId"] = correlationId;

        if (_env.IsDevelopment())
        {
            problem.Extensions["stackTrace"] = exception.StackTrace;
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _env.IsDevelopment()
        }));
    }

    private void LogException(Exception exception, int statusCode, string correlationId)
    {
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            if (statusCode >= 500)
            {
                _logger.LogError(exception, "Server error: {ExceptionType}", exception.GetType().Name);
            }
            else if (statusCode >= 400)
            {
                _logger.LogWarning("Client error: {ExceptionType} - {Message}", 
                    exception.GetType().Name, exception.Message);
            }
        }
    }

    private static string GetProblemType(int statusCode) => statusCode switch
    {
        400 => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
        404 => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
        502 => "https://tools.ietf.org/html/rfc7231#section-6.6.3",
        503 => "https://tools.ietf.org/html/rfc7231#section-6.6.4",
        504 => "https://tools.ietf.org/html/rfc7231#section-6.6.5",
        _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
    };
}

// Microsoft.AspNetCore.Mvc.ProblemDetails wrapper if not available
public class ProblemDetails
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Status { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Instance { get; set; } = string.Empty;
    public Dictionary<string, object?> Extensions { get; } = new();
}
```

```csharp
// src/Tracker.Domain/Exceptions/NotFoundException.cs
namespace Tracker.Domain.Exceptions;

public class NotFoundException : Exception
{
    public string ResourceType { get; }
    public object ResourceId { get; }

    public NotFoundException(string resourceType, object resourceId)
        : base($"{resourceType} with ID '{resourceId}' was not found.")
    {
        ResourceType = resourceType;
        ResourceId = resourceId;
    }
}

// src/Tracker.Domain/Exceptions/ValidationException.cs
namespace Tracker.Domain.Exceptions;

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(string property, string message)
        : base($"Validation failed: {property} - {message}")
    {
        Errors = new Dictionary<string, string[]>
        {
            [property] = new[] { message }
        };
    }

    public ValidationException(Dictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
```

#### Registration in Program.cs

```csharp
// Add early in pipeline, after CorrelationId
app.UseMiddleware<ExceptionMiddleware>();
```

#### Acceptance Criteria
- [ ] All exceptions return RFC7807 ProblemDetails
- [ ] Status codes mapped correctly
- [ ] Stack traces hidden in production
- [ ] Correlation ID in every error response
- [ ] Exceptions logged with appropriate level

---

### 4.5: Security Hardening (Input Validation, Rate Limiting)

**Goal:** Protect the API from abuse and ensure input integrity.

#### Requirements

1. **Input Validation**
   - JD text: max 15,000 characters
   - Resume text: max 25,000 characters
   - Title/company: max 200 characters each
   - No HTML/script tags (strip or reject)
   - Validate GUIDs in route parameters

2. **Rate Limiting**
   - Per-IP: 100 requests/minute (general)
   - Per-IP: 10 analysis requests/minute (expensive endpoint)
   - Return 429 Too Many Requests with Retry-After header
   - Use sliding window algorithm

3. **Security Headers**
   - X-Content-Type-Options: nosniff
   - X-Frame-Options: DENY
   - Content-Security-Policy: default-src 'self'
   - Remove X-Powered-By and Server headers

4. **Prompt Injection Protection**
   - Redact email addresses and phone numbers before logging
   - Never allow user input in system prompts
   - Sanitize user content in error messages

#### Files to Create/Modify

```
src/Tracker.Api/Middleware/
├── InputValidationMiddleware.cs (NEW)
├── RateLimitingMiddleware.cs (NEW)
└── SecurityHeadersMiddleware.cs (NEW)

src/Tracker.Api/Validators/
├── JobValidator.cs (NEW)
├── ResumeValidator.cs (NEW)
└── AnalysisRequestValidator.cs (NEW)

src/Tracker.Api/Program.cs (MODIFY - register middleware)
```

#### Implementation

```csharp
// src/Tracker.Api/Middleware/RateLimitingMiddleware.cs
using System.Collections.Concurrent;

namespace Tracker.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, RateLimitEntry> _entries = new();
    
    private const int GeneralLimit = 100;
    private const int AnalysisLimit = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = GetClientIp(context);
        var path = context.Request.Path.Value ?? "";
        var (limit, key) = path.Contains("/analyses", StringComparison.OrdinalIgnoreCase)
            ? (AnalysisLimit, $"analysis:{ip}")
            : (GeneralLimit, $"general:{ip}");

        var entry = _entries.GetOrAdd(key, _ => new RateLimitEntry());
        
        lock (entry)
        {
            var now = DateTime.UtcNow;
            if (now - entry.WindowStart > Window)
            {
                entry.WindowStart = now;
                entry.RequestCount = 0;
            }

            entry.RequestCount++;
        }

        var remaining = limit - entry.RequestCount;
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, remaining).ToString();

        if (entry.RequestCount > limit)
        {
            var retryAfter = (int)(Window - (DateTime.UtcNow - entry.WindowStart)).TotalSeconds;
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            
            _logger.LogWarning("Rate limit exceeded for {Key}: {Count}/{Limit}", 
                key, entry.RequestCount, limit);
            
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                detail = $"Rate limit exceeded. Try again in {retryAfter} seconds.",
                instance = path
            });
            return;
        }

        await _next(context);
    }

    private static string GetClientIp(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').First().Trim();
        }
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private class RateLimitEntry
    {
        public DateTime WindowStart { get; set; } = DateTime.UtcNow;
        public int RequestCount { get; set; }
    }
}
```

```csharp
// src/Tracker.Api/Middleware/SecurityHeadersMiddleware.cs
namespace Tracker.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        
        // Remove identifying headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}
```

```csharp
// src/Tracker.Api/Validators/AnalysisRequestValidator.cs
using System.Text.RegularExpressions;
using FluentValidation;
using Tracker.Domain.DTOs.Requests;

namespace Tracker.Api.Validators;

public class CreateAnalysisRequestValidator : AbstractValidator<CreateAnalysisRequest>
{
    public CreateAnalysisRequestValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required")
            .Must(BeValidGuid).WithMessage("JobId must be a valid GUID");

        RuleFor(x => x.ResumeId)
            .NotEmpty().WithMessage("ResumeId is required")
            .Must(BeValidGuid).WithMessage("ResumeId must be a valid GUID");
    }

    private static bool BeValidGuid(string value)
    {
        return Guid.TryParse(value, out _);
    }
}

public class CreateJobRequestValidator : AbstractValidator<CreateJobRequest>
{
    private static readonly Regex HtmlTagRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public CreateJobRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters")
            .Must(NotContainHtml).WithMessage("Title must not contain HTML");

        RuleFor(x => x.Company)
            .NotEmpty().WithMessage("Company is required")
            .MaximumLength(200).WithMessage("Company must not exceed 200 characters")
            .Must(NotContainHtml).WithMessage("Company must not contain HTML");

        RuleFor(x => x.DescriptionText)
            .NotEmpty().WithMessage("Description is required")
            .MaximumLength(15000).WithMessage("Description must not exceed 15,000 characters");
    }

    private static bool NotContainHtml(string value)
    {
        return !HtmlTagRegex.IsMatch(value);
    }
}

public class CreateResumeRequestValidator : AbstractValidator<CreateResumeRequest>
{
    public CreateResumeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Content is required")
            .MaximumLength(25000).WithMessage("Content must not exceed 25,000 characters");
    }
}
```

#### Program.cs Registration

```csharp
// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Rate limiting middleware
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
```

#### Acceptance Criteria
- [ ] Input validation on all endpoints
- [ ] Rate limiting returns 429 with Retry-After
- [ ] Security headers present on all responses
- [ ] HTML tags rejected in input
- [ ] GUIDs validated before database query

---

### 4.6: Health Check Enhancement

**Goal:** Production-ready health endpoints for orchestration and monitoring.

#### Requirements

1. **Health Endpoints**
   - `/healthz` - Liveness probe (always returns 200 if process is alive)
   - `/healthz/ready` - Readiness probe (checks DB, external services)
   - `/healthz/startup` - Startup probe (for slow-starting containers)

2. **Health Checks**
   - SQLite database connectivity
   - OpenAI API connectivity (optional, can make unhealthy)
   - Disk space for logs

3. **Response Format**
   ```json
   {
     "status": "Healthy",
     "checks": [
       { "name": "database", "status": "Healthy", "duration": "5ms" },
       { "name": "openai-api", "status": "Healthy", "duration": "152ms" }
     ],
     "duration": "158ms"
   }
   ```

4. **Integration**
   - Fly.io health checks via `/healthz`
   - Kubernetes probes for readiness/liveness

#### Files to Create/Modify

```
src/Tracker.Api/Endpoints/
└── HealthEndpoints.cs (MODIFY or NEW)

src/Tracker.Api/HealthChecks/
├── DatabaseHealthCheck.cs (NEW)
└── OpenAiHealthCheck.cs (NEW)

src/Tracker.Api/Program.cs (MODIFY - register health checks)
```

#### Implementation

```csharp
// src/Tracker.Api/HealthChecks/DatabaseHealthCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tracker.Infrastructure.Data;

namespace Tracker.Api.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly TrackerDbContext _dbContext;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(TrackerDbContext dbContext, ILogger<DatabaseHealthCheck> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            sw.Stop();

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy(
                    "Cannot connect to database",
                    data: new Dictionary<string, object>
                    {
                        ["duration_ms"] = sw.ElapsedMilliseconds
                    });
            }

            return HealthCheckResult.Healthy(
                "Database connection successful",
                data: new Dictionary<string, object>
                {
                    ["duration_ms"] = sw.ElapsedMilliseconds
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
}
```

```csharp
// src/Tracker.Api/HealthChecks/OpenAiHealthCheck.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenAI;

namespace Tracker.Api.HealthChecks;

public class OpenAiHealthCheck : IHealthCheck
{
    private readonly OpenAIClient? _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAiHealthCheck> _logger;

    public OpenAiHealthCheck(
        OpenAIClient? client,
        IConfiguration configuration,
        ILogger<OpenAiHealthCheck> logger)
    {
        _client = client;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_client == null)
        {
            return HealthCheckResult.Degraded("OpenAI client not configured");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            // Simple models list call as health check
            var models = await _client.GetModelsClient().GetModelsAsync(cancellationToken);
            sw.Stop();

            return HealthCheckResult.Healthy(
                "OpenAI API accessible",
                data: new Dictionary<string, object>
                {
                    ["duration_ms"] = sw.ElapsedMilliseconds
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenAI health check failed");
            sw.Stop();
            
            // Degraded instead of unhealthy - app can still serve cached results
            return HealthCheckResult.Degraded(
                "OpenAI API unavailable",
                ex,
                data: new Dictionary<string, object>
                {
                    ["duration_ms"] = sw.ElapsedMilliseconds
                });
        }
    }
}
```

```csharp
// src/Tracker.Api/Endpoints/HealthEndpoints.cs
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tracker.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // Liveness probe - always healthy if process is alive
        app.MapGet("/healthz", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
            .WithName("HealthCheck")
            .WithDescription("Liveness probe")
            .ExcludeFromDescription();

        // Readiness probe - checks dependencies
        app.MapGet("/healthz/ready", async (HealthCheckService healthCheck) =>
            {
                var report = await healthCheck.CheckHealthAsync();
                var response = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        duration = e.Value.Duration.TotalMilliseconds + "ms",
                        description = e.Value.Description
                    }),
                    duration = report.TotalDuration.TotalMilliseconds + "ms"
                };

                return report.Status == HealthStatus.Healthy
                    ? Results.Ok(response)
                    : Results.Json(response, statusCode: 503);
            })
            .WithName("ReadinessCheck")
            .WithDescription("Readiness probe with dependency checks");

        // Startup probe
        app.MapGet("/healthz/startup", () => Results.Ok(new { status = "Started" }))
            .WithName("StartupCheck")
            .ExcludeFromDescription();

        return app;
    }
}
```

#### Program.cs Registration

```csharp
// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<OpenAiHealthCheck>("openai-api", tags: new[] { "ready" });

// Map health endpoints
app.MapHealthEndpoints();
```

#### Acceptance Criteria
- [ ] `/healthz` returns 200 when process is alive
- [ ] `/healthz/ready` checks database and OpenAI connectivity
- [ ] Health checks include duration in response
- [ ] OpenAI failure results in "Degraded" (not Unhealthy)

---

## 4. Code Snippets for Key Implementations

### Complete Middleware Pipeline (Program.cs)

```csharp
using Serilog;
using Tracker.Api.Middleware;
using Tracker.Api.Endpoints;

// Early Serilog setup
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// ... service registration ...

var app = builder.Build();

// Middleware pipeline (order matters!)
app.UseMiddleware<ExceptionMiddleware>();      // 1. Handle all exceptions
app.UseCorrelationId();                        // 2. Correlation ID for tracing
app.UseMiddleware<SecurityHeadersMiddleware>(); // 3. Security headers
app.UseMiddleware<RateLimitingMiddleware>();   // 4. Rate limiting

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Health endpoints (no auth required)
app.MapHealthEndpoints();

// API endpoints
app.MapJobEndpoints();
app.MapResumeEndpoints();
app.MapAnalysisEndpoints();

app.Run();
```

### LLM Call with Full Observability

```csharp
public async Task<LlmResult<T>> CompleteStructuredAsync<T>(
    string systemPrompt,
    string userPrompt,
    CancellationToken cancellationToken = default) where T : class
{
    var correlationId = _httpContextAccessor?.HttpContext?.GetCorrelationId() ?? "none";
    var callId = Guid.NewGuid().ToString("N")[..8];
    
    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty("LlmCallId", callId))
    using (LogContext.PushProperty("LlmModel", _chatModel))
    {
        var sw = Stopwatch.StartNew();
        
        _logger.LogDebug("LLM request starting: prompt length {Length}", userPrompt.Length);
        
        try
        {
            var result = await _resiliencePolicy.ExecuteAsync(async () =>
            {
                // ... LLM call implementation
            });
            
            _logger.LogInformation(
                "LLM request completed: {InputTokens}in/{OutputTokens}out, {LatencyMs}ms, parse={ParseSuccess}",
                result.Usage.InputTokens,
                result.Usage.OutputTokens,
                result.LatencyMs,
                result.ParseSuccess);
            
            return result;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError("Circuit breaker is open, rejecting LLM request");
            throw;
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning("LLM request timed out after {Timeout}s", _timeoutSeconds);
            throw;
        }
    }
}
```

---

## 5. Success Criteria Checkboxes

### Task 4.1: Polly Policies
- [ ] Retry policy configured with 2 attempts and exponential backoff
- [ ] Circuit breaker opens after 5 failures, stays open for 30s
- [ ] Timeout policy cancels requests after 30s
- [ ] All policy events logged with structured logging
- [ ] Policies composed correctly (Timeout → Retry → Circuit Breaker)

### Task 4.2: Correlation ID Middleware
- [ ] X-Correlation-ID header accepted from client
- [ ] New correlation ID generated if not provided
- [ ] Correlation ID present in all response headers
- [ ] Correlation ID enriched in Serilog logs
- [ ] Accessible via HttpContext extension method

### Task 4.3: Structured Logging
- [ ] Serilog configured as primary logger
- [ ] Console sink with colored output in development
- [ ] File sink with rolling daily files
- [ ] All logs include correlation ID
- [ ] LLM calls logged with token counts and latency

### Task 4.4: Global Error Handling
- [ ] All exceptions return RFC7807 ProblemDetails
- [ ] Exception types mapped to appropriate status codes
- [ ] Stack traces hidden in production
- [ ] Sensitive data redacted from error messages
- [ ] Exceptions logged with correlation ID

### Task 4.5: Security Hardening
- [ ] Input validation on all endpoints (FluentValidation)
- [ ] Rate limiting returns 429 with Retry-After header
- [ ] Security headers present on all responses
- [ ] HTML/script tags rejected in input
- [ ] GUIDs validated before database queries

### Task 4.6: Health Check Enhancement
- [ ] `/healthz` liveness probe returns 200
- [ ] `/healthz/ready` checks database and OpenAI
- [ ] Health check responses include duration
- [ ] OpenAI failure results in "Degraded" status
- [ ] Compatible with Fly.io/Kubernetes probes

### Overall Day 4 Success
- [ ] Circuit breaker prevents cascading failures
- [ ] All errors return structured ProblemDetails
- [ ] Every request has correlation ID in logs
- [ ] Rate limiting protects expensive endpoints
- [ ] Health checks suitable for orchestration
- [ ] Application handles OpenAI outages gracefully

---

## 6. Parallel Execution Plan

### Lane A: Backend Resilience (Tasks 4.1, 4.5)

**Can run in parallel with Lane B**

```
Task 4.1 (Polly Policies) ──────┐
                                  ├──▶ Integration Testing
Task 4.5 (Security Hardening) ───┘
```

**Output:** Resilient API with rate limiting

### Lane B: Observability (Tasks 4.2, 4.3)

**Can run in parallel with Lane A**

```
Task 4.2 (Correlation ID) ──────┐
                                  ├──▶ Unified Logging
Task 4.3 (Serilog) ──────────────┘
```

**Output:** Traced requests with structured logs

### Lane C: Error Handling + Health (Tasks 4.4, 4.6)

**Depends on Lane B for logging context**

```
Lane B (Observability) ──▶ Task 4.4 (Error Handling) ──▶ Task 4.6 (Health Checks)
```

**Output:** Production-ready error handling and health probes

### Execution Timeline

```
Hour 1-2:   Lane A (4.1) + Lane B (4.2, 4.3) in parallel
Hour 3-4:   Lane A (4.5) continues, Lane C (4.4) starts
Hour 5-6:   Lane C (4.6) completes
Hour 7:     Integration testing
Hour 8:     Documentation + verification
```

### Dependency Graph

```
Day 3 Complete
       │
       ├──────────────────────────┬─────────────────────┐
       ▼                          ▼                     │
    Task 4.1                   Task 4.2                │
    (Polly)                    (Correlation)           │
       │                          │                     │
       ▼                          ▼                     │
    Task 4.5                   Task 4.3                │
    (Security)                 (Serilog)               │
       │                          │                     │
       │                          ▼                     │
       │                       Task 4.4                 │
       │                       (Errors)                 │
       │                          │                     │
       │                          ▼                     │
       │                       Task 4.6                 │
       │                       (Health)                 │
       │                          │                     │
       ├──────────────────────────┴─────────────────────┘
       │
       ▼
    Success Criteria Check
```

---

## Appendix: Package Dependencies

```xml
<!-- Add to Tracker.Api.csproj -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.9.0" />
<PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks" Version="8.0.0" />

<!-- Add to Tracker.AI.csproj -->
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
<PackageReference Include="Polly" Version="8.3.0" />
```

---

## Appendix: Configuration Reference

```json
// appsettings.Production.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Tracker.AI": "Debug"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "/app/logs/tracker-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  },
  "RateLimiting": {
    "GeneralLimit": 100,
    "AnalysisLimit": 10,
    "WindowMinutes": 1
  },
  "Polly": {
    "RetryCount": 2,
    "CircuitBreakerThreshold": 5,
    "CircuitBreakerDurationSeconds": 30,
    "TimeoutSeconds": 30
  }
}
```

---

*End of Day 4 Task Breakdown*
