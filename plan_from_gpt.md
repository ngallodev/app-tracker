Here is the complete rewritten plan as a clean, ready-to-commit Markdown document.

You can save this as:

AI-Job-Tracker-Execution-Plan.md

# AI-Augmented Job Application Tracker
## 5–7 Day Execution Plan (Production-Focused, Cost-Optimized)

Author: Nathan Gallo  
Target Roles: AI Engineer, Applied AI, Senior Full-Stack (AI Differentiated)  
Primary Goal: Ship a functional, production-minded MVP in 5–7 days that is both usable for real job applications and impressive to hiring managers.

---

# 🎯 Product Definition

A job application tracker with AI-powered job description analysis.

Core Capabilities:

- Track jobs and resumes
- Extract structured requirements from job descriptions
- Compare JD vs resume
- Identify missing required and preferred skills
- Compute deterministic coverage score
- Compute groundedness score
- Log model usage, latency, schema compliance
- Provide evaluation dashboard

Explicitly removed:
- No auto-editing or resume rewriting
- No background job queue
- No heavy vector DB
- No agent orchestration

This is a disciplined AI system, not a ChatGPT wrapper.

---

# 🏗 Architecture Overview

## Stack

- .NET 9 Minimal API
- React (Vite)
- SQLite
- OpenAI API
- Polly (retry + circuit breaker)

## Core AI Engineering Patterns (Portfolio Signal)

1. Structured Outputs + JSON Schema Validation + Repair Loop
2. Grounded Extraction (evidence quotes required)
3. Eval Harness + Observability
4. Cost Controls (hash caching + token logging)
5. Deterministic scoring instead of LLM self-grading

---

# 📅 Execution Plan

---

# DAY 1 — Foundation & Production Skeleton

Goal: A production-quality backend skeleton that compiles, runs, persists to SQLite, and has observability scaffolding.

No AI provider calls yet.

## Solution Structure



src/
Tracker.Api
Tracker.Domain
Tracker.Infrastructure
Tracker.AI
Tracker.Eval
Web


### Interfaces

```csharp
public interface ILlmClient
{
    Task<LlmResult<T>> CompleteStructuredAsync<T>(...);
}

public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text);
}


Use .NET 9 KeyedServices to allow:

OpenAI implementation

FakeLLM for tests

Future provider swap

Database Schema
jobs

id

title

company

description_text

description_hash

created_at

updated_at

resumes

id

name

content

content_hash

created_at

updated_at

analyses

id

job_id

resume_id

status

model

prompt_version

schema_version

input_tokens

output_tokens

latency_ms

created_at

analysis_results

analysis_id

required_skills_json

missing_required_json

missing_preferred_json

coverage_score

groundedness_score

llm_logs

id

analysis_id

step_name

raw_response

parse_success

repair_attempted

created_at

Day 1 Technical Requirements

EF Core + SQLite configured

Migrations created and applied

CRUD endpoints for jobs

CRUD endpoints for resumes

Correlation ID middleware

Structured request logging

Global exception handler (ProblemDetails)

Polly policies registered (not used yet)

Basic integration tests

Hash utilities (SHA-256)

Dockerfile (optional but recommended)

Observability (Day 1)

Per request:

Correlation ID

Path

Status

Latency

No PII logged by default.

Day 1 Acceptance Criteria

dotnet build succeeds

EF migration applies cleanly

Jobs + Resumes CRUD work end-to-end

Correlation ID present in logs and response header

Global error handler returns RFC7807 payload

Integration tests pass

DAY 2 — AI Core (Grounded + Deterministic)

Goal: Implement structured JD extraction + gap analysis with schema validation and repair loop.

Step 1: JD Extraction

Model: gpt-4o-mini

Strict JSON schema requiring:

role_title

required_skills (with evidence quotes)

preferred_skills (with evidence quotes)

responsibilities (with evidence quotes)

years_experience

keywords

Rules:

Every skill must include a <=20 word evidence quote

Null allowed if not explicitly stated

No inference allowed

Step 2: Gap Analysis

Input:

Extracted JD JSON

Resume text

Output:

matches (with resume evidence quotes)

missing_required

missing_preferred

Rule:
No resume quote = must mark missing.

Schema Validation + Repair Loop

Flow:

Call LLM

Attempt JSON parse + schema validation

If invalid:

Run repair prompt once

If still invalid:

Mark analysis failed

Store:

parse_success

repair_attempted

Deterministic Scoring

Coverage Score:

(required_matched / total_required) * 100


Groundedness Score:

% required skills with JD evidence

% matches with resume evidence

No LLM-based scoring allowed.

Cost Optimization

Before calling LLM:

Hash JD text

Hash resume text

If identical pair already analyzed → return cached result

Model usage:

Extraction + Gap: gpt-4o-mini

Embeddings (future use): text-embedding-3-small

Estimated cost:
~$0.04–$0.05 per analysis
~$15/month at 10/day

DAY 3 — MVP Ship

Goal: Usable end-to-end system.

Frontend Pages

Jobs list

Resumes list

Run analysis button

Analysis detail page

Analysis page shows:

Required skills

Missing required

Missing preferred

Coverage score

Groundedness score

Token usage

Latency

Parse success flag

This page is the interview demo centerpiece.

Eval Harness

Create /eval/run

Use 10 static JD fixtures.

Metrics:

Schema pass rate

Groundedness rate

Coverage stability (run twice diff)

Avg latency

Avg cost per run

Persist results in DB.

DAY 4–5 — Reliability + Interview Polish
Resilience

Polly retry (2 attempts)

Exponential backoff

Circuit breaker

Timeout

Optional model fallback

Security

Treat JD text as untrusted input

Never allow user text to influence tool selection

Redact email/phone before logging prompts

Deployment

Simplest approach:

Single Docker container

SQLite persistent volume

Fly.io or Railway

Rate limit per IP

Limit concurrency

💰 Cost Estimate

Per analysis:

~5k input tokens

~1.3k output tokens

Using 4o-mini pricing:

≈ $0.04–$0.05 per run

10/day:

≈ $0.50/day
≈ $15/month

With caching:

Likely under $10/month.

🎤 Interview Positioning Strategy

Emphasize:

70% Senior Engineering Judgment

Eval harness

Observability

Deterministic scoring

Schema validation + repair

Cost control

Retry + circuit breaker

30% Modern AI

Structured outputs

Grounded extraction

Embeddings

Model routing

Injection awareness

Avoid over-indexing on agents.

🔥 Top 5 Interview Questions + 30-Second Answers
1. How do you prevent hallucinations?

"I require evidence quotes for every extracted requirement and resume match. If the model cannot provide evidence, it must mark the skill missing. All outputs are validated against a strict JSON schema, with a repair loop for malformed responses."

2. How do you evaluate model performance?

"I maintain a golden dataset and measure schema pass rate, groundedness, latency, and coverage stability across runs. This allows detection of regressions when prompts or models change."

3. How do you control cost?

"I hash JD and resume pairs and cache results. I use 4o-mini for extraction, log token usage per step, and enforce a daily token budget."

4. What happens when the API fails?

"I use Polly retry with exponential backoff and a circuit breaker. Partial runs are persisted so users can retry without losing context."

5. How would you scale this?

"I would introduce multi-tenancy, move to Postgres and a proper vector store, add background jobs, encrypted PII storage, and expand the evaluation pipeline. The architecture isolates the LLM layer to enable this."

Final Goal

Ship something that demonstrates:

Discipline

Reliability

Evaluation rigor

Cost awareness

Structured AI integration

Not a wrapper.
Not a demo.
A real system.


---

If you'd like next, I can generate:

- A production-grade README
- A one-page “Architecture Overview” diagram description
- Or a hiring-manager-focused portfolio writeup that frames your gap year strategically.