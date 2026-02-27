# AI-Augmented Job Application Tracker — Day 1 Plan (Parallelized)

Goal for Day 1: A production-quality skeleton that compiles, runs locally, persists to SQLite, exposes CRUD endpoints, and has real observability scaffolding (correlation IDs, structured logs, error handling). **No AI calls required today** beyond interface contracts.

Deliverable by end of Day 1:
- .NET 9 Minimal API service running with SQLite (EF Core migrations applied)
- Core tables created: `jobs`, `resumes`, `analyses`, `analysis_results`, `llm_logs`
- CRUD endpoints for jobs & resumes (tested)
- Structured logging with per-request correlation ID + basic metrics
- Clean architecture boundaries (Domain / Infrastructure / API / AI)
- Dockerfile + local `docker compose` (optional but recommended)

---

## 0) Parallelization Map (Run these lanes simultaneously)

### Lane A — Backend Skeleton + API
Owner: Codex (gpt-4.1)  
Output: solution structure, endpoints, error handling

### Lane B — Database + EF Core + Migrations
Owner: Codex (gpt-4.1)  
Output: entity models, DbContext, migrations, SQLite config

### Lane C — Observability + Reliability Plumbing
Owner: Claude Code (Claude 3.5 Sonnet)  
Output: logging strategy, correlation ID middleware, Polly policies scaffolding, token/cost logging model

### Lane D — Frontend Scaffold (Optional on Day 1)
Owner: Codex (gpt-4.1)  
Output: Vite React shell, API client, basic pages stub (Jobs/Resumes)

### Lane E — “AI Layer Contract” (Interfaces + DTOs only)
Owner: Claude Code (Claude 3.5 Sonnet)  
Output: `ILlmClient`, `IEmbeddingClient`, prompt/schema version fields, validation primitives

---

## 1) Repo + Solution Setup (Lane A)

### 1.1 Create solution structure
**Tasks**
- Create .NET 9 solution with projects:
  - `Tracker.Api` (Minimal API)
  - `Tracker.Domain`
  - `Tracker.Infrastructure`
  - `Tracker.AI` (interfaces + future OpenAI client)
  - `Tracker.Eval` (empty for now)
- Add references:
  - Api -> Domain, Infrastructure, AI
  - Infrastructure -> Domain
  - AI -> Domain

**Acceptance**
- `dotnet build` succeeds
- project references are clean and circular-free

**Codex model**: gpt-4.1  
**Claude model**: Claude 3.5 Sonnet (review architecture)

---

## 2) Domain Model + DTOs (Lane A + B)

### 2.1 Define core entities in `Tracker.Domain`
**Entities**
- `Job`
  - `Id` (Guid)
  - `Title`, `Company` (string)
  - `DescriptionText` (string)
  - `DescriptionHash` (string)
  - `CreatedAt`, `UpdatedAt` (DateTimeOffset)
- `Resume`
  - `Id`
  - `Name`
  - `Content`
  - `ContentHash`
  - `CreatedAt`, `UpdatedAt`
- `Analysis`
  - `Id`
  - `JobId`, `ResumeId`
  - `Status` (enum: `Success`, `Failed`, `Running`)
  - `Model`, `PromptVersion`, `SchemaVersion` (string)
  - `InputTokens`, `OutputTokens` (int)
  - `LatencyMs` (int)
  - `CreatedAt`
- `AnalysisResult`
  - `AnalysisId`
  - `RequiredSkillsJson` (string)
  - `MissingRequiredJson` (string)
  - `MissingPreferredJson` (string)
  - `CoverageScore` (decimal)
  - `GroundednessScore` (decimal)
- `LlmLog`
  - `Id`
  - `AnalysisId`
  - `StepName`
  - `RawResponse`
  - `ParseSuccess` (bool)
  - `RepairAttempted` (bool)
  - `CreatedAt`

**Acceptance**
- All entities compile
- Enums + basic validations exist (e.g., non-null strings)

**Codex**: gpt-4.1  
**Claude**: Claude 3.5 Sonnet (domain cleanliness)

### 2.2 API DTOs & request models
**Tasks**
- Create request DTOs:
  - `CreateJobRequest`, `UpdateJobRequest`
  - `CreateResumeRequest`, `UpdateResumeRequest`
- Create response DTOs:
  - `JobDto`, `ResumeDto` (include timestamps)
- Add mapping helpers (manual or Mapster; manual is fine for Day 1)

**Acceptance**
- Endpoints return DTOs not EF entities

**Codex**: gpt-4.1

---

## 3) SQLite + EF Core Setup (Lane B)

### 3.1 EF Core + SQLite packages
**Tasks**
- Add:
  - `Microsoft.EntityFrameworkCore`
  - `Microsoft.EntityFrameworkCore.Sqlite`
  - `Microsoft.EntityFrameworkCore.Design`
- Create `TrackerDbContext` in `Tracker.Infrastructure`

**Acceptance**
- `dotnet ef migrations add InitialCreate` works
- `dotnet ef database update` creates SQLite file successfully

**Codex**: gpt-4.1

### 3.2 DbContext configuration
**Tasks**
- Configure table names explicitly (`jobs`, `resumes`, etc.)
- Configure indexes:
  - `jobs.description_hash`
  - `resumes.content_hash`
  - `analyses(job_id, resume_id, created_at)`
- Configure constraints:
  - Required fields
  - Max lengths where sane
- Use `DateTimeOffset` consistently

**Acceptance**
- Generated migration includes indexes + constraints

**Codex**: gpt-4.1  
**Claude**: Claude 3.5 Sonnet (review for future-proofing)

---

## 4) Minimal API Endpoints (Lane A)

### 4.1 Health + Version endpoints
**Tasks**
- `/healthz` returns 200
- `/version` returns commit hash/build version (env var or assembly version)

**Acceptance**
- curl endpoints work

**Codex**: gpt-4.1

### 4.2 Jobs CRUD
**Endpoints**
- `GET /api/jobs`
- `GET /api/jobs/{id}`
- `POST /api/jobs`
- `PUT /api/jobs/{id}`
- `DELETE /api/jobs/{id}`

**Logic**
- Compute `DescriptionHash` on create/update (SHA-256 of normalized text)
- `UpdatedAt` maintained

**Acceptance**
- Create -> read -> update -> list -> delete works end-to-end
- Hash changes on update

**Codex**: gpt-4.1

### 4.3 Resumes CRUD
Same pattern as Jobs.
- Compute `ContentHash` via SHA-256 on create/update

**Codex**: gpt-4.1

---

## 5) Observability + Error Handling (Lane C)

### 5.1 Correlation ID Middleware
**Tasks**
- Accept incoming `X-Correlation-Id` or generate new GUID
- Attach to:
  - response header
  - `ILogger` scope
  - any structured log fields

**Acceptance**
- Logs contain correlation ID for each request
- Response includes `X-Correlation-Id`

**Claude**: Claude 3.5 Sonnet

### 5.2 Structured logging setup
**Tasks**
- Use built-in logging
