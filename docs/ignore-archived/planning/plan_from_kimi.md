AppTrack AI: Execution Plan
AI-Augmented Job Application Tracker
Target: MVP in 5-7 days | .NET 9 Minimal API + React + SQLite + OpenAI
Portfolio Goal: Demonstrate AI engineering patterns (RAG, prompt chaining, evals, function calling)
Architecture Overview
plain
Copy
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   React SPA     │────▶│  .NET 9 Minimal  │────▶│   SQLite        │
│   (Vite)        │◀────│      API         │◀────│  (EF Core)      │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌──────────────────┐
                        │   OpenAI API     │
                        │  (GPT-4o, Emb)   │
                        └──────────────────┘
Key Patterns:
Prompt Chaining: JD → Extraction → Gap Analysis → Suggestions
RAG: Vector search of past applications for similar role matching
Function Calling: Structured resume edit generation
Evaluation: Automated ATS scoring with metrics tracking
Technology Stack
Table
Copy
Layer	Technology	.NET 9 Features Used
Backend	.NET 9 Minimal API	IAsyncEnumerable streaming, TimeProvider, KeyedServices
Database	SQLite + EF Core	Migrations, JSON columns for flexible analysis storage
AI	OpenAI API (GPT-4o, text-embedding-3-small)	Function calling, structured outputs
Frontend	React 18 + Vite + TanStack Query	Polling, optimistic updates
Testing	xUnit + NSubstitute + TestContainers	WebApplicationFactory, HttpClient mocking
DevEx	Docker + Docker Compose	Single-command local setup
Phase 1: Foundation (Day 1) — Sequential, Blocking
Goal: Working CRUD API with database persistence
Table
Copy
Task	Complexity	Claude Code Model	OpenAI Codex Model	Dependencies	Deliverable
1.1 Project Scaffold	Low	—	codex-mini-latest	None	.sln, src/ folders, Docker Compose
1.2 Domain Models	Low	claude-sonnet-4-20250514	—	None	Entities with relationships
1.3 EF Core Setup	Low	—	codex-mini-latest	1.2	Migrations, AppDbContext
1.4 Minimal API CRUD	Low-Med	—	codex-latest	1.2, 1.3	Controllers/endpoints for Jobs, Resumes
1.5 React Scaffold	Low	—	codex-mini-latest	None	Vite + React + TanStack Query setup
1.6 Auth Stub	Low	claude-haiku-3-20250514	—	1.2	Single user row, JWT middleware scaffold
1.1 Detailed Spec (Codex)
plain
Copy
Create .NET 9 Minimal API solution structure:
- src/
  - AppTrackAI.Api/ (Minimal API endpoints)
  - AppTrackAI.Core/ (Domain models, interfaces)
  - AppTrackAI.Infrastructure/ (EF Core, LLM clients)
- tests/
  - AppTrackAI.Tests/
- docker-compose.yml (SQLite volume mount)
- README.md with setup instructions

Use:
- EF Core 9 SQLite provider
- Scalar for API documentation
- Carter or native Minimal API groupings
1.2 Domain Models (Claude)
csharp
Copy
// Core entities with relationships
public class JobApplication {
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Company { get; set; }
    public string JobDescriptionText { get; set; }
    public string SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public ApplicationStage Stage { get; set; }

    // Navigation
    public ResumeVersion SubmittedResume { get; set; }
    public AnalysisResult Analysis { get; set; }
    public List<ContactNote> Notes { get; set; }
}

public class ResumeVersion {
    public Guid Id { get; set; }
    public string Name { get; set; } // "Backend-Focused", "Full-Stack"
    public string RawText { get; set; }
    public ResumeStructure Structured { get; set; } // JSON column
    public DateTime CreatedAt { get; set; }
    public bool IsBaseVersion { get; set; }
    public Guid? ParentVersionId { get; set; }
}

public class AnalysisResult {
    public Guid Id { get; set; }
    public double SemanticSimilarityScore { get; set; } // 0-1
    public double AtsKeywordScore { get; set; } // 0-100
    public List<string> MissingKeywords { get; set; }
    public List<string> SuggestedEdits { get; set; }
    public JDExtraction ExtractedRequirements { get; set; } // JSON
    public DateTime AnalyzedAt { get; set; }
}

public class ContactNote {
    public Guid Id { get; set; }
    public string ContactName { get; set; }
    public string Note { get; set; }
    public DateTime Date { get; set; }
    public ContactType Type { get; set; } // Email, Phone, Interview
}
1.6 Auth Stub (Claude)
csharp
Copy
// Middleware that always sets UserId to static GUID
// JWT validation disabled in dev, scaffolded for production
// Single "Users" table with one seeded row
Day 1 Success Criteria:
[ ] POST /api/jobs creates job
[ ] POST /api/resumes creates resume
[ ] GET endpoints return data
[ ] React app displays list of jobs
[ ] Database persists across restarts
Phase 2: AI Core (Day 2) — Parallelizable
Goal: LLM integration with resilience, semantic scoring working
Table
Copy
Task	Complexity	Claude Code Model	OpenAI Codex Model	Dependencies	Deliverable
2.1 LLM Client Abstraction	Med-High	claude-sonnet-4-20250514	—	None	Resilient, observable LLM client
2.2 Prompt Chain: JD Extraction	Med	—	codex-latest	2.1	Structured JD analysis
2.3 Resume Parsing (Text)	Low	—	codex-mini-latest	None	Text → Structured sections
2.4 Semantic Scoring	Med	claude-opus-4-20250514	—	2.1, 2.2, 2.3	Embedding similarity
2.5 Caching Layer	Low	—	codex-mini-latest	2.1	In-memory embedding cache
2.1 LLM Client Spec (Claude - Critical)
csharp
Copy
public interface ILLMClient {
    // Streaming for UX, structured output for reliability
    IAsyncEnumerable<string> StreamCompletionAsync(PromptChain chain);
    Task<T> GetStructuredOutputAsync<T>(PromptChain chain, JsonSchema schema);
    Task<float[]> GetEmbeddingAsync(string text);

    // Observability
    event EventHandler<LLMRequestLog> OnRequestLogged;
}

// Resilience patterns
- Polly retry (3x exponential backoff)
- Circuit breaker (5 failures in 60s = 30s cooldown)
- Token counting (tiktoken) for budget guardrails
- Request/response logging to SQLite (for evals)
Key Decisions:
Use IAsyncEnumerable for streaming suggestions to frontend
TimeProvider abstraction for testable retry timing
KeyedServices for multiple provider support (OpenAI, Anthropic stubs)
2.2 Prompt Chain: JD Extraction (Codex)
plain
Copy
Chain Step 1: Raw JD → Structured Extraction

Prompt:
"Extract key information from this job description. 
Return JSON with: role_title, seniority_level, required_skills[], 
preferred_skills[], responsibilities[], company_industry, culture_keywords[]

Job Description: {jd_text}"

Output Schema (strict JSON mode):
{
  "role_title": "string",
  "seniority_level": "enum[junior, mid, senior, staff, principal]",
  "required_skills": ["string"],
  "preferred_skills": ["string"],
  "responsibilities": ["string"],
  "company_industry": "string",
  "culture_keywords": ["string"]
}
2.4 Semantic Scoring Spec (Claude - Opus)
csharp
Copy
// Embedding comparison with caching
public class SemanticScorer {
    public async Task<double> CalculateSimilarityAsync(string jdText, string resumeText) {
        // Check cache first (JD hash → embedding)
        var jdEmbedding = await _cache.GetOrCreateAsync(
            Hash(jdText), 
            () => _llm.GetEmbeddingAsync(jdText)
        );

        var resumeEmbedding = await _llm.GetEmbeddingAsync(resumeText);

        // Cosine similarity
        return CosineSimilarity(jdEmbedding, resumeEmbedding);
    }
}

// Cache implementation: IMemoryCache with 1hr sliding expiration
// Cost savings: ~60% on repeated JD analysis
Day 2 Success Criteria:
[ ] Paste JD → structured extraction in < 3s
[ ] Upload resume text → similarity score 0-1
[ ] Retry logic tested (simulate OpenAI failure)
[ ] Token usage logged per request
Phase 3: Intelligence Layer (Day 3) — Parallel with Dependencies
Goal: Full optimization loop with suggestions and versioning
Table
Copy
Task	Complexity	Claude Code Model	OpenAI Codex Model	Dependencies	Deliverable
3.1 Gap Analysis Engine	Med-High	claude-opus-4-20250514	—	2.2, 2.3	Missing keywords identification
3.2 ATS Scoring Algorithm	Med	—	codex-latest	3.1	Automated 0-100 score
3.3 Suggestion Engine	High	—	codex-latest	3.1, 3.2	Specific edit recommendations
3.4 Resume Versioning	Med	—	codex-mini-latest	3.3	Clone + apply edits
3.5 Background Job Queue	Med	claude-sonnet-4-20250514	—	2.1	Async analysis processing
3.6 Timeline/Notes	Low-Med	—	codex-mini-latest	1.2	Stage tracking, contact logging
3.1 Gap Analysis Spec (Claude - Opus)
plain
Copy
Prompt Chain Step 2: Compare Extraction vs Resume

Input: JDExtraction (from 2.2) + ResumeStructure
Output: GapAnalysis {
  missing_required_skills: [],
  missing_preferred_skills: [],
  experience_gaps: [], // "JD wants 5 years K8s, resume has 2"
  keyword_frequency_map: {}, // For ATS score
  culture_fit_signals: []
}

Logic:
- Fuzzy matching for skills ("K8s" ≈ "Kubernetes")
- Experience years extraction with regex fallback
- Weighted scoring: required (3x), preferred (1x), culture (0.5x)
3.2 ATS Scoring Algorithm (Codex)
csharp
Copy
public class AtsScorer {
    public AtsScore Calculate(JobDescription jd, Resume resume) {
        var keywordScore = CalculateKeywordMatch(jd, resume); // 40% weight
        var semanticScore = _semanticScorer.Score(jd, resume); // 35% weight
        var formatScore = CheckFormatting(resume.RawText); // 15% weight (no tables, standard fonts)
        var lengthScore = CalculateOptimalLength(resume); // 10% weight

        return new AtsScore {
            Total = (keywordScore * 0.4) + (semanticScore * 0.35) + (formatScore * 0.15) + (lengthScore * 0.10),
            Breakdown = new { keywordScore, semanticScore, formatScore, lengthScore },
            Improvements = GenerateImprovements(keywordScore, semanticScore)
        };
    }
}

// Format checks: No tables, standard fonts (Arial/Calibri/Times), 1-2 pages, no graphics
3.3 Suggestion Engine Spec (Codex - Critical)
plain
Copy
Prompt Chain Step 3: Generate Specific Edits

Use OpenAI Function Calling:

functions: [{
  name: "suggest_resume_edits",
  parameters: {
    type: "object",
    properties: {
      suggestions: {
        type: "array",
        items: {
          type: "object",
          properties: {
            section: "enum[summary, experience, skills, projects]",
            original_text: "string",
            suggested_text: "string",
            reasoning: "string",
            impact: "enum[high, medium, low]"
          }
        }
      }
    }
  }
}]

Prompt:
"Given this gap analysis and resume, suggest 3-5 specific edits.
Each suggestion must include the exact original text to replace.
Prioritize high-impact changes that address missing keywords.

Gap Analysis: {gap_analysis}
Resume: {resume_text}"
3.5 Background Job Queue Spec (Claude)
csharp
Copy
// For long-running analysis (10-30s)
public class AnalysisJobQueue : BackgroundService {
    private readonly Channel<AnalysisJob> _queue;

    protected override async Task ExecuteAsync(CancellationToken ct) {
        await foreach (var job in _queue.Reader.ReadAllAsync(ct)) {
            var result = await RunPromptChainAsync(job);
            await _hubContext.Clients.User(job.UserId)
                .SendAsync("AnalysisComplete", result);
        }
    }
}

// Frontend polls GET /api/analysis/{id}/status every 2s
// Or use Server-Sent Events if time permits (optional enhancement)
Day 3 Success Criteria:
[ ] Submit JD + Resume → get ATS score (0-100)
[ ] See specific "Change X to Y" suggestions
[ ] Apply suggestions → new resume version saved
[ ] Job application created with linked resume version
[ ] Stage tracking: Applied → Phone Screen → etc.
Phase 4: AI Engineering Showcase (Day 4-5) — Parallel
Goal: RAG, evaluation framework, advanced patterns
Table
Copy
Task	Complexity	Claude Code Model	OpenAI Codex Model	Dependencies	Deliverable
4.1 RAG Pipeline	High	claude-opus-4-20250514	—	2.4, 3.1	Similar past applications retrieval
4.2 Evaluation Framework	High	claude-opus-4-20250514	—	3.2, 3.3	Prompt performance metrics
4.3 Function Calling Migration	Med	—	codex-latest	3.3	Structured outputs via tools
4.4 JD Ingestion (Paste+URL)	Med	—	codex-latest	1.1	URL metadata extraction
4.5 Export/Backup	Low	—	codex-mini-latest	1.2	JSON/CSV export
4.1 RAG Pipeline Spec (Claude - Opus - Showcase Feature)
csharp
Copy
// When viewing new JD, find similar past applications
public class SimilarApplicationRAG {
    public async Task<List<SimilarCase>> FindSimilarAsync(string jdText) {
        // 1. Embed the new JD
        var embedding = await _llm.GetEmbeddingAsync(jdText);

        // 2. Vector search in SQLite (using sqlite-vec extension or in-memory)
        var similar = await _db.JobApplications
            .Select(j => new {
                Job = j,
                Similarity = CosineSimilarity(embedding, j.Analysis.JdEmbedding)
            })
            .Where(x => x.Similarity > 0.75)
            .OrderByDescending(x => x.Similarity)
            .Take(3)
            .ToListAsync();

        // 3. Return with outcomes (did you get interview?)
        return similar.Select(s => new SimilarCase {
            Role = s.Job.Title,
            Company = s.Job.Company,
            Similarity = s.Similarity,
            Outcome = s.Job.Stage, // If >= Phone Screen, considered success
            ResumeVersionUsed = s.Job.SubmittedResume.Name,
            KeyDifferences = AnalyzeDifferences(jdText, s.Job.JobDescriptionText)
        }).ToList();
    }
}

// UI shows: "You applied to 3 similar roles. 2 got interviews. Here's what worked..."
4.2 Evaluation Framework Spec (Claude - Opus - Critical Differentiator)
csharp
Copy
// Track which AI features actually help
public class EvaluationService {
    // Automated metrics
    public async Task RecordSuggestionOutcome(Guid suggestionId, bool wasApplied) {
        await _db.SuggestionMetrics.AddAsync(new {
            SuggestionId = suggestionId,
            Applied = wasApplied,
            Timestamp = DateTime.UtcNow
        });
    }

    // Correlation analysis (run nightly or on-demand)
    public EvaluationReport GenerateReport() {
        return new EvaluationReport {
            // Did applications with high ATS scores get more interviews?
            AtsScoreCorrelation = CalculateCorrelation(
                x: applications.Select(a => a.Analysis.AtsKeywordScore),
                y: applications.Select(a => GotInterview(a) ? 1 : 0)
            ),

            // Which suggestion types are most applied?
            SuggestionTypeEffectiveness = _db.SuggestionMetrics
                .GroupBy(m => m.SuggestionType)
                .Select(g => new {
                    Type = g.Key,
                    ApplyRate = g.Count(m => m.Applied) / (double)g.Count()
                }),

            // Prompt A/B testing (if we test different prompts)
            PromptVersionComparison = ComparePromptVersions()
        };
    }
}

// UI: "AI suggestions applied: 67% | Interview rate with high ATS scores: 3x higher"
4.3 Function Calling Migration (Codex)
csharp
Copy
// Migrate 3.3 to native OpenAI function calling
var tools = new List<Tool> {
    new FunctionTool {
        Name = "suggest_edits",
        Parameters = JsonSchema.FromType<SuggestionResult>()
    }
};

var response = await _openai.Chat.CreateChatCompletionAsync(new {
    Model = "gpt-4o",
    Messages = messages,
    Tools = tools,
    ToolChoice = "auto"
});

// Parse function call arguments into strongly-typed object
var suggestions = JsonSerializer.Deserialize<SuggestionResult>(
    response.Choices[0].Message.ToolCalls[0].Function.Arguments
);
Day 4-5 Success Criteria:
[ ] Viewing JD shows "Similar applications you submitted" with outcomes
[ ] Dashboard shows "AI effectiveness metrics" (suggestion apply rate, score correlation)
[ ] Export all data to JSON
[ ] URL paste auto-fetches job title/company from meta tags
Phase 5: Deployment & Portfolio (Day 6-7)
Table
Copy
Task	Complexity	Claude Code Model	OpenAI Codex Model	Deliverable
5.1 Docker + Fly.io Deploy	Low	—	codex-mini-latest	Live URL
5.2 README + Architecture Docs	Low	claude-sonnet-4-20250514	—	GitHub-ready documentation
5.3 Demo Video/GIF	Low	You	—	60-second walkthrough
5.4 Blog Post Outline	Low	claude-sonnet-4-20250514	—	"Building an AI-Native App in .NET 9"
5.2 README Structure (Claude)
Markdown
Fullscreen 
Download 
Fit
Code
Preview
Architecture Diagram
1. Prompt Chaining: JD → Extraction → Gap Analysis → Suggestions
2. RAG: Vector search of application history for context
3. Function Calling: Structured resume edit generation
4. Evaluation Framework: Correlation analysis of AI suggestions vs outcomes
5. Resilience: Circuit breaker, retry, caching
AI Engineering Patterns Demonstrated
.NET 9 (IAsyncEnumerable, TimeProvider, KeyedServices)
OpenAI API (GPT-4o, Embeddings)
React + TanStack Query
SQLite + EF Core
Tech Stack
docker-compose up
# or
dotnet run --project src/AppTrackAI.Api
Local Setup
AppTrack AI
Key Features
[Screenshot: ATS Score]
[Screenshot: Suggestion Diff]
[Screenshot: Similar Applications RAG]
plain
Copy

---

## Model Assignment Strategy

| Task Type | Claude Code | OpenAI Codex | Rationale |
|-----------|-------------|--------------|-----------|
| **Architecture & Design** | `claude-opus-4-20250514` | — | Complex reasoning, trade-off analysis |
| **Standard Implementation** | `claude-sonnet-4-20250514` | `codex-latest` | Balanced capability |
| **Simple/Scaffold** | `claude-haiku-3-20250514` | `codex-mini-latest` | Fast, cheap, good enough |
| **Fire-and-Forget Parallel** | — | `codex-mini-latest` | Cost-effective for bulk tasks |
| **AI/LLM Logic** | `claude-opus-4-20250514` | `codex-latest` | Both strong; Opus for novel patterns |

---

## Parallelization Strategy

### Can Run in Parallel
- Day 1: 1.1, 1.5 (scaffold API and React simultaneously)
- Day 2: 2.2, 2.3, 2.5 (JD extraction, resume parsing, caching)
- Day 3: 3.4, 3.6 (versioning and timeline, independent)
- Day 4-5: 4.3, 4.4, 4.5 (function calling, URL fetch, export)

### Must Be Sequential
- 1.2 → 1.3 → 1.4 (models → migrations → API)
- 2.1 → 2.2, 2.4 (LLM client must exist first)
- 3.1 → 3.2 → 3.3 (gap → score → suggestions)
- 2.4 → 4.1 (embeddings needed for RAG)

### Critical Path (Longest Chain)
1.2 → 1.3 → 1.4 → 2.1 → 2.2 → 3.1 → 3.2 → 3.3 → 3.4
**~2.5 days if sequential, ~1.5 days with parallelization**

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| OpenAI API downtime | Circuit breaker + "analyze later" queue |
| Slow analysis UX | Background jobs + polling + optimistic UI |
| API cost overruns | Embedding cache, token counting, rate limiting |
| Complex file parsing | Scope cut: text paste only, no PDF/DOCX |
| Auth complexity | Stub single-user, scaffold for future |

---

## Success Metrics (MVP)

- [ ] End-to-end flow < 30 seconds (paste JD → see suggestions)
- [ ] ATS score calculation < 5 seconds (cached)
- [ ] 100% test coverage on LLM client resilience
- [ ] Zero hardcoded secrets (User Secrets + Env vars)
- [ ] Deployed and accessible via URL

---

## Post-MVP Enhancements (Week 2+)

1. **Browser Extension**: One-click JD capture
2. **Multi-Provider**: Anthropic Claude, Azure OpenAI fallback
3. **Fine-Tuning**: Train model on your successful applications
4. **Interview Prep**: Generate questions based on JD gaps
5. **Salary Negotiation**: Market data integration

---

## Target Roles & Positioning

### Primary: AI-Native Startups
**Role**: AI Engineer / Applied AI / LLM Product Engineer  
**Pitch**: "I built a full AI pipeline with RAG, evals, and function calling in .NET 9"

### Secondary: Enterprise AI Teams
**Role**: Senior/Principal Engineer - Intelligent Applications  
**Pitch**: "I don't just ship AI features—I measure their effectiveness and build resilient systems"

### Tertiary: Senior Full-Stack
**Role**: Senior Software Engineer  
**Pitch**: "I integrated AI deeply into a domain-specific workflow, not just chatbot wrappers"

---

*Generated for sprint execution. Modify complexity estimates as you discover implementation details.*