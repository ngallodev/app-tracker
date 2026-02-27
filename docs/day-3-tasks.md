# AI Job Application Tracker — Day 3 Tasks (MVP Ship)

**Goal:** Usable end-to-end system with frontend. Users can paste a JD, upload resume text, and see analysis results in < 30 seconds.

---

## 1. Overview

### Day 3 Objectives
- Complete backend reliability for analysis endpoint
- Ship React frontend with core pages
- Implement eval harness with static fixtures
- Achieve end-to-end demo flow

### Dependencies on Day 2 (Complete)
- ✅ `ILlmClient` interface with structured output support
- ✅ `OpenAiClient` implementation with retry logic
- ✅ `JdExtraction` and `GapAnalysis` models with evidence quotes
- ✅ `AnalysisService` orchestrating the pipeline
- ✅ Deterministic scoring (coverage + groundedness)
- ✅ POST /api/analyses endpoint working
- ✅ `FakeLlmClient` for development without API key

### Files Already Created (Day 2)
```
src/Tracker.AI/
├── ILlmClient.cs
├── OpenAiClient.cs
├── Models/
│   ├── JdExtraction.cs
│   └── GapAnalysis.cs
├── Prompts/
│   ├── JdExtractionPrompt.cs
│   └── GapAnalysisPrompt.cs
└── Services/
    └── AnalysisService.cs
src/Tracker.Api/Endpoints/
└── AnalysesEndpoints.cs
```

---

## 2. Task Breakdown Table

| Task ID | Description | Complexity | Est. Effort (tokens) | Dependencies | Files to Create/Modify | Parallelization |
|---------|-------------|------------|---------------------|--------------|------------------------|-----------------|
| 3.1 | Analysis Endpoint Enhancement | Medium | ~8K | Day 2 | `AnalysesEndpoints.cs`, `AnalysisService.cs` | Lane A |
| 3.2 | React App Scaffold | Easy | ~6K | None | `web/` (new directory) | Lane B |
| 3.3 | Jobs List Page | Easy | ~5K | 3.2 | `web/src/pages/JobsPage.tsx` | Lane B |
| 3.4 | Resumes List Page | Easy | ~4K | 3.2 | `web/src/pages/ResumesPage.tsx` | Lane B |
| 3.5 | Analysis Page | Medium | ~8K | 3.1, 3.2, 3.3, 3.4 | `web/src/pages/AnalysisPage.tsx` | Lane C (after A+B) |
| 3.6 | Eval Harness | Medium | ~6K | 3.1 | `src/Tracker.Eval/`, `EvalEndpoints.cs` | Lane A |

---

## 3. Detailed Task Specifications

---

### 3.1: Analysis Endpoint Enhancement

**Goal:** Add caching, error handling, and status polling support to the analysis endpoint.

#### Requirements

1. **Hash-Based Caching**
   - Before calling LLM, check if `job.DescriptionHash + resume.ContentHash` pair exists in analyses table
   - If cached result exists with status `Completed`, return cached result immediately
   - Log cache hit/miss for observability

2. **Status Polling Support**
   - Analysis runs synchronously for MVP, but endpoint should support async pattern
   - Return `202 Accepted` with `Location` header for long-running analysis
   - Add `GET /api/analyses/{id}/status` endpoint for polling

3. **Error Handling**
   - Catch `LlmException` specifically
   - Store failure reason in analysis record
   - Return structured error response with `ProblemDetails`

4. **Input Validation**
   - Max JD length: 10,000 characters
   - Max resume length: 20,000 characters
   - Return 400 Bad Request with clear error message

#### Files to Modify

```
src/Tracker.Api/Endpoints/AnalysesEndpoints.cs
  - Add cache lookup before analysis
  - Add GET /api/analyses/{id}/status endpoint
  - Add input validation
  - Return 202 Accepted with Location header

src/Tracker.AI/Services/AnalysisService.cs
  - Add ICacheService dependency (or use in-memory cache)
  - Add cancellation token propagation

src/Tracker.Domain/Entities/Analysis.cs
  - Add ErrorMessage property (optional)
```

#### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/analyses | Create new analysis (returns 201 or 202) |
| GET | /api/analyses/{id} | Get full analysis result |
| GET | /api/analyses/{id}/status | Get status only (for polling) |
| GET | /api/analyses | List all analyses |

#### Implementation Notes

```csharp
// Cache check logic
var existingAnalysis = await db.Analyses
    .Include(a => a.Result)
    .FirstOrDefaultAsync(a => 
        a.Job.DescriptionHash == job.DescriptionHash && 
        a.Resume.ContentHash == resume.ContentHash && 
        a.Status == AnalysisStatus.Completed, ct);

if (existingAnalysis != null)
{
    logger.LogInformation("Cache hit for analysis");
    return Results.Ok(MapToDto(existingAnalysis));
}
```

---

### 3.2: React App Scaffold

**Goal:** Set up React frontend with Vite, TanStack Query, and routing.

#### Tech Stack
- Vite 5.x with React 18
- TanStack Query v5 for data fetching
- React Router v6 for navigation
- Tailwind CSS for styling
- Axios for API calls

#### Files to Create

```
web/
├── package.json
├── vite.config.ts
├── tsconfig.json
├── tsconfig.node.json
├── tailwind.config.js
├── postcss.config.js
├── index.html
├── .env.development
├── .env.production
└── src/
    ├── main.tsx
    ├── App.tsx
    ├── vite-env.d.ts
    ├── index.css
    ├── api/
    │   ├── client.ts          # Axios instance with base URL
    │   └── types.ts           # TypeScript interfaces for DTOs
    ├── hooks/
    │   ├── useJobs.ts         # TanStack Query hooks
    │   ├── useResumes.ts
    │   └── useAnalyses.ts
    ├── components/
    │   ├── Layout.tsx         # App shell with nav
    │   ├── LoadingSpinner.tsx
    │   ├── ErrorBoundary.tsx
    │   └── ScoreBadge.tsx     # Coverage/Groundedness display
    └── pages/
        ├── JobsPage.tsx
        ├── ResumesPage.tsx
        ├── AnalysisPage.tsx
        └── NewAnalysisPage.tsx
```

#### package.json Dependencies

```json
{
  "dependencies": {
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "react-router-dom": "^6.22.0",
    "@tanstack/react-query": "^5.24.0",
    "axios": "^1.6.0",
    "clsx": "^2.1.0"
  },
  "devDependencies": {
    "@types/react": "^18.2.0",
    "@types/react-dom": "^18.2.0",
    "@vitejs/plugin-react": "^4.2.0",
    "autoprefixer": "^10.4.0",
    "postcss": "^8.4.0",
    "tailwindcss": "^3.4.0",
    "typescript": "^5.3.0",
    "vite": "^5.1.0"
  }
}
```

#### API Client Setup

```typescript
// src/api/client.ts
import axios from 'axios';

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5000',
  headers: { 'Content-Type': 'application/json' },
});

export default api;
```

#### Environment Variables

```bash
# .env.development
VITE_API_URL=http://localhost:5000

# .env.production
VITE_API_URL=/api
```

---

### 3.3: Jobs List Page

**Goal:** CRUD interface for job postings.

#### Features
- List all jobs with title, company, created date
- Create new job (paste JD text)
- Edit job title/company/description
- Delete job with confirmation
- Copy job description to clipboard

#### Files to Create

```
web/src/pages/JobsPage.tsx
web/src/components/JobForm.tsx
web/src/components/JobCard.tsx
```

#### Component Structure

```tsx
// JobsPage.tsx
export function JobsPage() {
  const { data: jobs, isLoading, error } = useJobs();
  const [showForm, setShowForm] = useState(false);
  const [editingJob, setEditingJob] = useState<JobDto | null>(null);
  
  // Render job list, form modal, empty state
}
```

#### API Integration

| Action | TanStack Query Hook | HTTP Method |
|--------|---------------------|-------------|
| List jobs | `useJobs()` | GET /api/jobs |
| Create job | `useCreateJob()` | POST /api/jobs |
| Update job | `useUpdateJob()` | PUT /api/jobs/{id} |
| Delete job | `useDeleteJob()` | DELETE /api/jobs/{id} |

#### UI Mockup

```
┌─────────────────────────────────────────────────────┐
│  Jobs                                    [+ New Job] │
├─────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────┐│
│  │ Senior AI Engineer - Acme Corp                  ││
│  │ Created: Feb 16, 2026                           ││
│  │ [Edit] [Delete] [Analyze]                       ││
│  └─────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────┐│
│  │ ML Platform Engineer - TechStartup              ││
│  │ Created: Feb 15, 2026                           ││
│  │ [Edit] [Delete] [Analyze]                       ││
│  └─────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────┘
```

---

### 3.4: Resumes List Page

**Goal:** CRUD interface for resume versions.

#### Features
- List all resumes with name, created date
- Create new resume (paste text)
- Edit resume name/content
- Delete resume with confirmation
- Copy resume content to clipboard

#### Files to Create

```
web/src/pages/ResumesPage.tsx
web/src/components/ResumeForm.tsx
web/src/components/ResumeCard.tsx
```

#### Component Structure

```tsx
// ResumesPage.tsx
export function ResumesPage() {
  const { data: resumes, isLoading } = useResumes();
  const [showForm, setShowForm] = useState(false);
  
  // Render resume list, form modal, empty state
}
```

#### API Integration

| Action | TanStack Query Hook | HTTP Method |
|--------|---------------------|-------------|
| List resumes | `useResumes()` | GET /api/resumes |
| Create resume | `useCreateResume()` | POST /api/resumes |
| Update resume | `useUpdateResume()` | PUT /api/resumes/{id} |
| Delete resume | `useDeleteResume()` | DELETE /api/resumes/{id} |

#### UI Mockup

```
┌─────────────────────────────────────────────────────┐
│  Resumes                               [+ New Resume]│
├─────────────────────────────────────────────────────┤
│  ┌─────────────────────────────────────────────────┐│
│  │ Resume v3 (AI Focused)                          ││
│  │ Created: Feb 16, 2026 • 2,450 words             ││
│  │ [Edit] [Copy] [Delete]                          ││
│  └─────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────┐│
│  │ Resume v2 (General)                             ││
│  │ Created: Feb 10, 2026 • 1,800 words             ││
│  │ [Edit] [Copy] [Delete]                          ││
│  └─────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────┘
```

---

### 3.5: Analysis Page (Centerpiece Demo)

**Goal:** Display complete analysis results with all metrics. This is the interview demo centerpiece.

#### Features
- Select job + resume to analyze
- Trigger analysis (or view cached)
- Display coverage score with visual indicator
- Display groundedness score with visual indicator
- Show required skills with evidence quotes
- Show missing required skills (highlighted in red)
- Show missing preferred skills (highlighted in yellow)
- Show token usage and latency
- Show parse success indicators

#### Files to Create

```
web/src/pages/AnalysisPage.tsx
web/src/pages/NewAnalysisPage.tsx
web/src/components/ScoreGauge.tsx
web/src/components/SkillList.tsx
web/src/components/MissingSkillsPanel.tsx
web/src/components/AnalysisMetrics.tsx
```

#### Component Structure

```tsx
// NewAnalysisPage.tsx - Job/Resume selection
export function NewAnalysisPage() {
  const { data: jobs } = useJobs();
  const { data: resumes } = useResumes();
  const [selectedJobId, setSelectedJobId] = useState<string>();
  const [selectedResumeId, setSelectedResumeId] = useState<string>();
  const createAnalysis = useCreateAnalysis();
  
  // Render selection dropdowns and "Analyze" button
}

// AnalysisPage.tsx - Results display
export function AnalysisPage() {
  const { id } = useParams();
  const { data: analysis, isLoading } = useAnalysis(id!);
  
  // Render scores, skills, metrics
}
```

#### Score Visualization

```
Coverage Score: 73%
┌────────────────────────────────────────────────────┐
│████████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░│
└────────────────────────────────────────────────────┘

Groundedness Score: 85%
┌────────────────────────────────────────────────────┐
│████████████████████████████████░░░░░░░░░░░░░░░░░░░│
└────────────────────────────────────────────────────┘
```

#### Skills Display

```
✓ REQUIRED SKILLS (5 matched, 2 missing)
┌────────────────────────────────────────────────────┐
│ ✓ Python                          "5+ years Python"│
│ ✓ Machine Learning                "ML model deploy"│
│ ✓ SQL                             "PostgreSQL exp" │
│ ✗ TensorFlow                      NOT FOUND        │
│ ✗ Kubernetes                      NOT FOUND        │
└────────────────────────────────────────────────────┘

⚡ PREFERRED SKILLS (3 matched, 1 missing)
┌────────────────────────────────────────────────────┐
│ ✓ AWS                              "cloud infra"   │
│ ✓ Docker                           "containerize"  │
│ ✗ Rust                             NOT FOUND        │
└────────────────────────────────────────────────────┘
```

#### Metrics Panel

```
┌─────────────────────────────────────────────────────┐
│ METRICS                                             │
├─────────────────────────────────────────────────────┤
│ Model: gpt-4o-mini                                  │
│ Input Tokens: 4,892                                 │
│ Output Tokens: 1,245                                │
│ Total Cost: ~$0.04                                  │
│ Latency: 3.2s                                       │
│ Parse Success: ✓ JD ✓ Gap                          │
│ Cached: No                                          │
└─────────────────────────────────────────────────────┘
```

#### API Integration

| Action | TanStack Query Hook | HTTP Method |
|--------|---------------------|-------------|
| Get analysis | `useAnalysis(id)` | GET /api/analyses/{id} |
| Create analysis | `useCreateAnalysis()` | POST /api/analyses |
| Check status | `useAnalysisStatus(id)` | GET /api/analyses/{id}/status |

---

### 3.6: Eval Harness

**Goal:** Basic evaluation framework with static fixtures to measure AI performance.

#### Requirements

1. **Static Fixtures**
   - 10 JD fixtures stored in `src/Tracker.Eval/Fixtures/`
   - Each fixture: `title`, `company`, `description_text`
   - Include variety: engineering, ML, data science, full-stack

2. **Eval Runner**
   - Run all fixtures against a sample resume
   - Collect metrics: schema pass rate, groundedness, latency, cost

3. **Metrics Tracked**
   - Schema pass rate: % of analyses with parse success
   - Groundedness rate: average groundedness score
   - Coverage stability: run twice, compare coverage diff
   - Avg latency
   - Avg cost per run (estimated from tokens)

4. **Storage**
   - Persist eval runs in database
   - Add `eval_runs` table (optional) or reuse analyses with flag

#### Files to Create

```
src/Tracker.Eval/
├── Tracker.Eval.csproj
├── EvalRunner.cs
├── Models/
│   └── EvalResult.cs
└── Fixtures/
    ├── jd_senior_ai_engineer.json
    ├── jd_ml_platform.json
    ├── jd_data_scientist.json
    ├── jd_fullstack_react.json
    ├── jd_backend_dotnet.json
    ├── jd_devops_engineer.json
    ├── jd_ml_researcher.json
    ├── jd_staff_engineer.json
    ├── jd_tech_lead.json
    └── jd_junior_dev.json

src/Tracker.Api/Endpoints/
└── EvalEndpoints.cs
```

#### API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | /api/eval/run | Run eval on all fixtures |
| GET | /api/eval/fixtures | List available fixtures |
| GET | /api/eval/results | Get latest eval results |

#### EvalRunner Interface

```csharp
public interface IEvalRunner
{
    Task<EvalSummary> RunAsync(Guid resumeId, CancellationToken ct = default);
}

public record EvalSummary
{
    public required int TotalRuns { get; init; }
    public required int SuccessfulRuns { get; init; }
    public required double SchemaPassRate { get; init; }
    public required double AvgGroundedness { get; init; }
    public required double AvgCoverage { get; init; }
    public required double AvgLatencyMs { get; init; }
    public required double AvgCostUsd { get; init; }
    public required List<EvalRunDetail> Details { get; init; }
}
```

#### Fixture Example

```json
{
  "id": "jd_001",
  "title": "Senior AI Engineer",
  "company": "Acme AI Labs",
  "description_text": "We're looking for a Senior AI Engineer to join our team... 5+ years Python, ML frameworks, SQL required. TensorFlow, Kubernetes preferred..."
}
```

---

## 4. Success Criteria

### Day 3 Checklist

- [ ] **End-to-end flow < 30 seconds**
  - User can paste JD → create resume text → run analysis → see results
  
- [ ] **Cached analysis < 5 seconds**
  - Re-running same JD/resume pair returns cached result instantly

- [ ] **Analysis page displays all metrics**
  - Coverage score with visual indicator
  - Groundedness score with visual indicator
  - Required skills with evidence quotes
  - Missing required (highlighted)
  - Missing preferred (highlighted)
  - Token usage
  - Latency
  - Parse success flags

- [ ] **Jobs CRUD works in UI**
  - Create, read, update, delete jobs
  - Paste JD text into textarea

- [ ] **Resumes CRUD works in UI**
  - Create, read, update, delete resumes
  - Paste resume text into textarea

- [ ] **Eval harness runs against fixtures**
  - POST /api/eval/run executes
  - Returns metrics summary
  - At least 8/10 fixtures succeed

- [ ] **Frontend builds and runs**
  - `npm run dev` starts dev server
  - `npm run build` produces production build
  - No console errors

---

## 5. Parallel Execution Plan

### Lane A: Backend (Tasks 3.1, 3.6)

**Can run in parallel with Lane B**

```
Task 3.1 (Analysis Enhancement) ──┐
                                   ├──▶ Integration Testing
Task 3.6 (Eval Harness) ──────────┘
```

**Owner:** Backend-focused agent
**Output:** Enhanced API endpoints, eval runner

### Lane B: Frontend Scaffold (Tasks 3.2, 3.3, 3.4)

**Can run in parallel with Lane A**

```
Task 3.2 (React Scaffold) ──▶ Task 3.3 (Jobs Page)
                           ──▶ Task 3.4 (Resumes Page)
```

**Owner:** Frontend-focused agent
**Output:** Working React app with basic pages

### Lane C: Integration (Task 3.5)

**Depends on Lane A + Lane B complete**

```
Lane A (Backend) ──┐
                   ├──▶ Task 3.5 (Analysis Page)
Lane B (Frontend) ─┘
```

**Owner:** Full-stack agent
**Output:** Complete end-to-end demo

### Execution Timeline

```
Hour 1-2:   Lane A (3.1) + Lane B (3.2) in parallel
Hour 3-4:   Lane A (3.6) + Lane B (3.3, 3.4) in parallel  
Hour 5-6:   Lane C (3.5) - Integration
Hour 7:     Testing + Bug fixes
Hour 8:     Documentation + Demo prep
```

### Dependency Graph

```
Day 2 Complete
      │
      ├──────────────────────┬─────────────────────┐
      ▼                      ▼                     │
   Task 3.1               Task 3.2                │
      │                      │                     │
      ▼                      ▼                     │
   Task 3.6              Task 3.3                 │
      │                      │                     │
      │                      ▼                     │
      │                   Task 3.4                 │
      │                      │                     │
      ├──────────────────────┴─────────────────────┘
      │
      ▼
   Task 3.5 (Analysis Page)
      │
      ▼
   Success Criteria Check
```

---

## Appendix: Type Definitions

### TypeScript DTOs (frontend)

```typescript
// src/api/types.ts

export interface JobDto {
  id: string;
  title: string;
  company: string;
  descriptionText: string;
  descriptionHash: string;
  createdAt: string;
  updatedAt: string;
}

export interface ResumeDto {
  id: string;
  name: string;
  content: string;
  contentHash: string;
  createdAt: string;
  updatedAt: string;
}

export interface AnalysisResultDto {
  id: string;
  jobId: string;
  resumeId: string;
  status: 'Running' | 'Completed' | 'Failed';
  coverageScore: number;
  groundednessScore: number;
  requiredSkillsJson: string;
  missingRequiredJson: string;
  missingPreferredJson: string;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  createdAt: string;
}

export interface CreateAnalysisRequest {
  jobId: string;
  resumeId: string;
}

export interface SkillMatch {
  skillName: string;
  isRequired: boolean;
  jdEvidence: string;
  resumeEvidence: string;
}

export interface Skill {
  name: string;
  evidenceQuote: string;
}
```

---

*End of Day 3 Task Breakdown*

---

## Day 3 Optimization Addendum (Uncompleted Scope Only)

This addendum replaces the remaining Day 3 execution order with a lower-cost, multi-agent-friendly workflow.

### Remaining outcomes (only)
- Ship a minimal `web/` frontend for Jobs, Resumes, and Analysis.
- Expose analysis mode metadata in API (`deterministic` vs `llm_fallback`).
- Run deterministic fixture evals with near-zero AI usage.

### Agent + model assignment matrix

| Lane | Scope | Agent Type | Model Tier Suggestion | Skills |
|------|-------|------------|------------------------|--------|
| A | Backend API completion (`/api/analyses` metadata, DTO alignment) | `worker` | Medium | None required |
| B | Deterministic eval harness (`Tracker.Eval`, fixtures, runner script) | `worker` | Small/Medium | None required |
| C | Frontend scaffold + pages (`web/`) | `worker` | Medium | None required |
| D | Rapid codebase discovery, file targeting, dependency checks | `explorer` | Small | `exec-statusline-json` when collecting exec telemetry |
| E | Integration QA, checklist pass/fail verification | `explorer` + `worker` | Small for checks, Medium for fixes | `exec-statusline-json` optional |

### Optimized sequence
1. Lane D: verify existing backend contracts and produce exact DTO/API map.
2. Lane A: finish backend metadata and endpoint behavior first (blocks UI/API alignment risk).
3. Lane B in parallel: complete deterministic eval runner and 3-5 fixtures.
4. Lane C: build frontend against finalized DTOs.
5. Lane E: run regression checks and acceptance checklist.

### Detailed task packs for delegation

#### Pack A1: Analysis API metadata completion
- Files: `src/Tracker.Domain/DTOs/AnalysisResultDto.cs`, `src/Tracker.Api/Endpoints/AnalysesEndpoints.cs`, `src/Tracker.AI/Services/AnalysisService.cs`
- Deliverables:
  - Response includes `gapAnalysisMode` and `usedGapLlmFallback`.
  - Cached and newly created analyses return the same metadata contract.
  - LLM logs preserve step-level mode evidence.
- Acceptance:
  - New analyses with deterministic path show `gapAnalysisMode=deterministic`.
  - Low-confidence path shows `gapAnalysisMode=llm_fallback`.

#### Pack B1: Deterministic eval runner
- Files: `src/Tracker.Eval/Program.cs`, `src/Tracker.Eval/Fixtures/*.json`, `scripts/run_deterministic_eval.sh`
- Deliverables:
  - Fixture-driven pass/fail output with non-zero exit on failure.
  - No API key required.
- Acceptance:
  - Runner executes all fixtures.
  - Summary includes fixture count, pass count, fail count.

#### Pack C1: MVP frontend only (no extras)
- Files: `web/src/pages/{Jobs,Resumes,Analysis}Page.tsx`, `web/src/lib/api.ts`
- Deliverables:
  - Jobs + resumes CRUD list views.
  - Analysis trigger and result view including mode metadata.
- Acceptance:
  - End-to-end path works from job + resume creation to analysis display.

### Token and cost controls for Day 3
- Use deterministic eval as default QA signal.
- Reserve model calls for:
  - JD extraction
  - deterministic low-confidence fallback only
- Keep frontend prompts/data requests to required fields only.
