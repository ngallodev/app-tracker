import { FormEvent, useEffect, useMemo, useState } from 'react';

type Job = {
  id: string;
  title: string;
  company: string;
  descriptionText?: string | null;
  sourceUrl?: string | null;
  createdAt: string;
  updatedAt: string;
};

type Resume = {
  id: string;
  name: string;
  contentPreview?: string | null;
  createdAt: string;
  updatedAt: string;
};

type Analysis = {
  analysisId: string;
  jobId: string;
  resumeId: string;
  status: string;
  gapAnalysisMode: string;
  usedGapLlmFallback: boolean;
  coverageScore: number;
  groundednessScore: number;
  requiredSkillsJson?: string | null;
  missingRequiredJson?: string | null;
  missingPreferredJson?: string | null;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  provider: string;
  executionMode: string;
  createdAt: string;
};

type EvalRunSummary = {
  id: string;
  mode: string;
  fixtureCount: number;
  passedCount: number;
  failedCount: number;
  schemaPassRate: number;
  groundednessRate: number;
  coverageStabilityDiff: number;
  avgLatencyMs: number;
  avgCostPerRunUsd: number;
  createdAt: string;
};

type EvalRunResponse = EvalRunSummary & {
  fixtureDirectory: string;
  results: Array<{
    name: string;
    passed: boolean;
    detail: string;
    latencyMs: number;
    groundednessRate: number;
    coverageStabilityDiff: number;
  }>;
};

type SkillRow = {
  skillName: string;
  evidenceQuote?: string;
  jdEvidence?: string;
  resumeEvidence?: string;
  isRequired?: boolean;
};

type ApiErrorPayload = {
  detail?: string;
  error?: string;
  title?: string;
};

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    headers: { 'Content-Type': 'application/json', ...(init?.headers ?? {}) },
    ...init
  });

  if (!res.ok) {
    let message = `Request failed: ${res.status}`;
    try {
      const body = (await res.json()) as ApiErrorPayload;
      message = body.detail || body.error || body.title || message;
    } catch {
      try {
        const text = await res.text();
        if (text) message = text;
      } catch {
        // ignore parse fallback failure
      }
    }
    throw new Error(message);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return (await res.json()) as T;
}

function parseSkillRows(jsonText?: string | null): SkillRow[] {
  if (!jsonText) return [];
  try {
    const raw = JSON.parse(jsonText);
    if (!Array.isArray(raw)) return [];
    return raw.map((item) => ({
      skillName: item.skill_name ?? item.SkillName ?? 'Unknown',
      evidenceQuote: item.evidence_quote ?? item.EvidenceQuote,
      jdEvidence: item.jd_evidence ?? item.JdEvidence,
      resumeEvidence: item.resume_evidence ?? item.ResumeEvidence,
      isRequired: item.is_required ?? item.IsRequired
    }));
  } catch {
    return [];
  }
}

function scoreTone(value: number): string {
  if (value >= 80) return '#0f766e';
  if (value >= 60) return '#b45309';
  return '#b91c1c';
}

function App() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [analyses, setAnalyses] = useState<Analysis[]>([]);
  const [evalRuns, setEvalRuns] = useState<EvalRunSummary[]>([]);
  const [latestEvalResult, setLatestEvalResult] = useState<EvalRunResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const [jobForm, setJobForm] = useState({
    title: '',
    company: '',
    descriptionText: '',
    sourceUrl: ''
  });
  const [resumeForm, setResumeForm] = useState({
    name: '',
    content: ''
  });

  const [selectedJobId, setSelectedJobId] = useState<string>('');
  const [selectedResumeId, setSelectedResumeId] = useState<string>('');
  const [selectedAnalysisId, setSelectedAnalysisId] = useState<string>('');
  const [analysisProvider, setAnalysisProvider] = useState<string>('');

  const selectedAnalysis = useMemo(
    () => analyses.find((a) => a.analysisId === selectedAnalysisId) ?? null,
    [analyses, selectedAnalysisId]
  );
  const requiredSkills = parseSkillRows(selectedAnalysis?.requiredSkillsJson);
  const missingRequired = parseSkillRows(selectedAnalysis?.missingRequiredJson);
  const missingPreferred = parseSkillRows(selectedAnalysis?.missingPreferredJson);

  useEffect(() => {
    void refreshAll();
  }, []);

  async function refreshAll() {
    try {
      setLoading(true);
      setError(null);
      const [jobsData, resumesData, analysesData] = await Promise.all([
        api<Job[]>('/api/jobs'),
        api<Resume[]>('/api/resumes'),
        api<Analysis[]>('/api/analyses')
      ]);
      setJobs(jobsData);
      setResumes(resumesData);
      setAnalyses(analysesData);
      setSelectedJobId((current) => current || jobsData[0]?.id || '');
      setSelectedResumeId((current) => current || resumesData[0]?.id || '');
      setSelectedAnalysisId((current) => current || analysesData[0]?.analysisId || '');
      void refreshEvalRuns();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }

  async function refreshEvalRuns() {
    try {
      const runs = await api<EvalRunSummary[]>('/eval/runs');
      setEvalRuns(runs);
    } catch {
      // Keep main UI usable even if eval endpoints are unavailable.
    }
  }

  async function handleRunDeterministicEval() {
    try {
      setBusy('eval');
      setError(null);
      const result = await api<EvalRunResponse>('/eval/run', { method: 'POST' });
      setLatestEvalResult(result);
      await refreshEvalRuns();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Eval run failed');
    } finally {
      setBusy(null);
    }
  }

  async function handleCreateJob(e: FormEvent) {
    e.preventDefault();
    if (!jobForm.title.trim() || !jobForm.company.trim()) return;
    try {
      setBusy('job');
      setError(null);
      const created = await api<Job>('/api/jobs', {
        method: 'POST',
        body: JSON.stringify({
          title: jobForm.title.trim(),
          company: jobForm.company.trim(),
          descriptionText: jobForm.descriptionText.trim() || null,
          sourceUrl: jobForm.sourceUrl.trim() || null
        })
      });
      setJobs((prev) => [created, ...prev]);
      setSelectedJobId(created.id);
      setJobForm({ title: '', company: '', descriptionText: '', sourceUrl: '' });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create job');
    } finally {
      setBusy(null);
    }
  }

  async function handleCreateResume(e: FormEvent) {
    e.preventDefault();
    if (!resumeForm.name.trim() || !resumeForm.content.trim()) return;
    try {
      setBusy('resume');
      setError(null);
      const created = await api<Resume>('/api/resumes', {
        method: 'POST',
        body: JSON.stringify({
          name: resumeForm.name.trim(),
          content: resumeForm.content.trim()
        })
      });
      setResumes((prev) => [created, ...prev]);
      setSelectedResumeId(created.id);
      setResumeForm({ name: '', content: '' });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create resume');
    } finally {
      setBusy(null);
    }
  }

  async function handleDeleteJob(id: string) {
    if (!window.confirm('Delete this job?')) return;
    try {
      setBusy(`delete-job-${id}`);
      setError(null);
      await api<void>(`/api/jobs/${id}`, { method: 'DELETE' });
      setJobs((prev) => prev.filter((j) => j.id !== id));
      setAnalyses((prev) => prev.filter((a) => a.jobId !== id));
      if (selectedJobId === id) setSelectedJobId('');
      if (selectedAnalysis && selectedAnalysis.jobId === id) setSelectedAnalysisId('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete job');
    } finally {
      setBusy(null);
    }
  }

  async function handleDeleteResume(id: string) {
    if (!window.confirm('Delete this resume?')) return;
    try {
      setBusy(`delete-resume-${id}`);
      setError(null);
      await api<void>(`/api/resumes/${id}`, { method: 'DELETE' });
      setResumes((prev) => prev.filter((r) => r.id !== id));
      setAnalyses((prev) => prev.filter((a) => a.resumeId !== id));
      if (selectedResumeId === id) setSelectedResumeId('');
      if (selectedAnalysis && selectedAnalysis.resumeId === id) setSelectedAnalysisId('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete resume');
    } finally {
      setBusy(null);
    }
  }

  async function handleRunAnalysis(e: FormEvent) {
    e.preventDefault();
    if (!selectedJobId || !selectedResumeId) {
      setError('Select both a job and a resume before running analysis.');
      return;
    }

    try {
      setBusy('analysis');
      setError(null);
      const created = await api<Analysis>('/api/analyses', {
        method: 'POST',
        body: JSON.stringify({
          jobId: selectedJobId,
          resumeId: selectedResumeId,
          provider: analysisProvider.trim() || undefined
        })
      });
      setAnalyses((prev) => {
        const withoutSame = prev.filter((a) => a.analysisId !== created.analysisId);
        return [created, ...withoutSame];
      });
      setSelectedAnalysisId(created.analysisId);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Analysis failed');
    } finally {
      setBusy(null);
    }
  }

  if (loading) {
    return <div style={{ padding: 24, fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace' }}>Loading tracker...</div>;
  }

  return (
    <div style={{ background: 'linear-gradient(180deg, #f4f1ea 0%, #fdfcf8 60%, #ffffff 100%)', minHeight: '100vh', color: '#1f2937' }}>
      <div style={{ maxWidth: 1320, margin: '0 auto', padding: 20 }}>
        <header style={{ padding: 16, border: '1px solid #d6d3d1', borderRadius: 16, background: 'rgba(255,255,255,0.85)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap', alignItems: 'center' }}>
            <div>
              <h1 style={{ margin: 0, fontSize: 28, letterSpacing: '-0.03em' }}>AI Job Application Tracker</h1>
              <p style={{ margin: '8px 0 0', color: '#57534e' }}>
                CRUD + analysis workflow on the current API (jobs, resumes, analyses, metrics).
              </p>
            </div>
            <button onClick={() => void refreshAll()} disabled={busy !== null} style={buttonStyle('#111827')}>
              Refresh
            </button>
          </div>
          {error && (
            <div style={{ marginTop: 12, padding: 10, borderRadius: 10, background: '#fee2e2', border: '1px solid #fecaca', color: '#991b1b' }}>
              {error}
            </div>
          )}
        </header>

        <section style={{ marginTop: 16, display: 'grid', gap: 16, gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))' }}>
          <StatCard label="Jobs" value={jobs.length} />
          <StatCard label="Resumes" value={resumes.length} />
          <StatCard label="Analyses" value={analyses.length} />
          <StatCard label="Latest Mode" value={analyses[0]?.gapAnalysisMode ?? 'n/a'} />
        </section>

        <section style={{ marginTop: 16, display: 'grid', gap: 16, gridTemplateColumns: 'minmax(320px, 420px) 1fr' }}>
          <Panel title="Deterministic Eval">
            <div style={{ display: 'grid', gap: 10 }}>
              <button onClick={() => void handleRunDeterministicEval()} disabled={busy !== null} style={buttonStyle('#4c1d95')}>
                {busy === 'eval' ? 'Running eval...' : 'Run /eval/run'}
              </button>
              <button onClick={() => void refreshEvalRuns()} disabled={busy !== null} style={buttonStyle('#374151')}>
                Refresh Eval History
              </button>
              {latestEvalResult && (
                <div style={{ border: '1px solid #e7e5e4', borderRadius: 10, padding: 10, background: '#fff' }}>
                  <div style={{ fontWeight: 700, marginBottom: 6 }}>Latest Eval Summary</div>
                  <div style={{ fontSize: 12, color: '#57534e' }}>{latestEvalResult.fixtureDirectory}</div>
                  <div style={{ marginTop: 8, display: 'grid', gap: 6 }}>
                    <div>Fixtures: {latestEvalResult.fixtureCount}</div>
                    <div>Passed/Failed: {latestEvalResult.passedCount}/{latestEvalResult.failedCount}</div>
                    <div>Schema Pass: {latestEvalResult.schemaPassRate}%</div>
                    <div>Groundedness: {latestEvalResult.groundednessRate}%</div>
                    <div>Stability Diff: {latestEvalResult.coverageStabilityDiff}</div>
                    <div>Avg Latency: {latestEvalResult.avgLatencyMs} ms</div>
                  </div>
                </div>
              )}
            </div>
          </Panel>

          <Panel title={`Eval History (${evalRuns.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 240, overflow: 'auto' }}>
              {evalRuns.map((run) => (
                <div key={run.id} style={{ border: '1px solid #e7e5e4', borderRadius: 10, padding: 10, background: '#fff' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                    <strong style={{ fontSize: 13 }}>{new Date(run.createdAt).toLocaleString()}</strong>
                    <Badge text={run.mode} tone="#334155" />
                  </div>
                  <div style={{ marginTop: 6, display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 6, fontSize: 12 }}>
                    <span>Fixtures: {run.fixtureCount}</span>
                    <span>Pass: {run.passedCount}</span>
                    <span>Fail: {run.failedCount}</span>
                    <span>Schema: {run.schemaPassRate}%</span>
                    <span>Grounded: {run.groundednessRate}%</span>
                    <span>Latency: {run.avgLatencyMs} ms</span>
                  </div>
                </div>
              ))}
              {evalRuns.length === 0 && <Muted> No eval runs recorded yet. </Muted>}
            </div>
          </Panel>
        </section>

        <section style={{ marginTop: 16, display: 'grid', gap: 16, gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))' }}>
          <Panel title="Create Job">
            <form onSubmit={handleCreateJob} style={{ display: 'grid', gap: 8 }}>
              <input placeholder="Title" value={jobForm.title} onChange={(e) => setJobForm({ ...jobForm, title: e.target.value })} style={inputStyle} />
              <input placeholder="Company" value={jobForm.company} onChange={(e) => setJobForm({ ...jobForm, company: e.target.value })} style={inputStyle} />
              <input placeholder="Source URL (optional)" value={jobForm.sourceUrl} onChange={(e) => setJobForm({ ...jobForm, sourceUrl: e.target.value })} style={inputStyle} />
              <textarea placeholder="Job description text" value={jobForm.descriptionText} onChange={(e) => setJobForm({ ...jobForm, descriptionText: e.target.value })} rows={6} style={inputStyle} />
              <button type="submit" disabled={busy !== null} style={buttonStyle('#0f766e')}>
                {busy === 'job' ? 'Creating...' : 'Create Job'}
              </button>
            </form>
          </Panel>

          <Panel title="Create Resume">
            <form onSubmit={handleCreateResume} style={{ display: 'grid', gap: 8 }}>
              <input placeholder="Resume name" value={resumeForm.name} onChange={(e) => setResumeForm({ ...resumeForm, name: e.target.value })} style={inputStyle} />
              <textarea placeholder="Resume content" value={resumeForm.content} onChange={(e) => setResumeForm({ ...resumeForm, content: e.target.value })} rows={9} style={inputStyle} />
              <button type="submit" disabled={busy !== null} style={buttonStyle('#1d4ed8')}>
                {busy === 'resume' ? 'Creating...' : 'Create Resume'}
              </button>
            </form>
          </Panel>
        </section>

        <section style={{ marginTop: 16, display: 'grid', gap: 16, gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))' }}>
          <Panel title={`Jobs (${jobs.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 360, overflow: 'auto' }}>
              {jobs.map((job) => (
                <ItemCard
                  key={job.id}
                  selected={selectedJobId === job.id}
                  onSelect={() => setSelectedJobId(job.id)}
                  onDelete={() => void handleDeleteJob(job.id)}
                  deleteBusy={busy === `delete-job-${job.id}`}
                  title={`${job.title} @ ${job.company}`}
                  subtitle={job.descriptionText?.slice(0, 110) || 'No JD text yet'}
                />
              ))}
              {jobs.length === 0 && <Muted> No jobs yet. </Muted>}
            </div>
          </Panel>

          <Panel title={`Resumes (${resumes.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 360, overflow: 'auto' }}>
              {resumes.map((resume) => (
                <ItemCard
                  key={resume.id}
                  selected={selectedResumeId === resume.id}
                  onSelect={() => setSelectedResumeId(resume.id)}
                  onDelete={() => void handleDeleteResume(resume.id)}
                  deleteBusy={busy === `delete-resume-${resume.id}`}
                  title={resume.name}
                  subtitle={resume.contentPreview || 'No content'}
                />
              ))}
              {resumes.length === 0 && <Muted> No resumes yet. </Muted>}
            </div>
          </Panel>
        </section>

        <section style={{ marginTop: 16, display: 'grid', gap: 16, gridTemplateColumns: '1fr' }}>
          <Panel title="Run Analysis">
            <form onSubmit={handleRunAnalysis} style={{ display: 'grid', gap: 10 }}>
              <div style={{ display: 'grid', gap: 10, gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))' }}>
                <label style={labelStyle}>
                  <span>Job</span>
                  <select value={selectedJobId} onChange={(e) => setSelectedJobId(e.target.value)} style={inputStyle}>
                    <option value="">Select job</option>
                    {jobs.map((job) => (
                      <option key={job.id} value={job.id}>
                        {job.title} @ {job.company}
                      </option>
                    ))}
                  </select>
                </label>
                <label style={labelStyle}>
                  <span>Resume</span>
                  <select value={selectedResumeId} onChange={(e) => setSelectedResumeId(e.target.value)} style={inputStyle}>
                    <option value="">Select resume</option>
                    {resumes.map((resume) => (
                      <option key={resume.id} value={resume.id}>
                        {resume.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label style={labelStyle}>
                  <span>Provider (optional)</span>
                  <input placeholder="claude / codex / gemini..." value={analysisProvider} onChange={(e) => setAnalysisProvider(e.target.value)} style={inputStyle} />
                </label>
              </div>
              <button type="submit" disabled={busy !== null || jobs.length === 0 || resumes.length === 0} style={buttonStyle('#7c2d12')}>
                {busy === 'analysis' ? 'Running analysis...' : 'Run Analysis'}
              </button>
            </form>
          </Panel>
        </section>

        <section style={{ marginTop: 16, display: 'grid', gap: 16, gridTemplateColumns: 'minmax(300px, 420px) 1fr' }}>
          <Panel title={`Analyses (${analyses.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 520, overflow: 'auto' }}>
              {analyses.map((analysis) => (
                <button
                  key={analysis.analysisId}
                  onClick={() => setSelectedAnalysisId(analysis.analysisId)}
                  style={{
                    textAlign: 'left',
                    padding: 10,
                    borderRadius: 10,
                    border: selectedAnalysisId === analysis.analysisId ? '2px solid #0f766e' : '1px solid #d6d3d1',
                    background: '#fff'
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                    <strong style={{ fontSize: 13 }}>{analysis.status}</strong>
                    <span style={{ fontSize: 12, color: '#57534e' }}>{new Date(analysis.createdAt).toLocaleString()}</span>
                  </div>
                  <div style={{ marginTop: 6, display: 'flex', gap: 8, flexWrap: 'wrap', fontSize: 12 }}>
                    <Badge text={`coverage ${analysis.coverageScore}%`} tone={scoreTone(analysis.coverageScore)} />
                    <Badge text={`grounded ${analysis.groundednessScore}%`} tone={scoreTone(analysis.groundednessScore)} />
                    <Badge text={analysis.gapAnalysisMode} tone="#334155" />
                  </div>
                </button>
              ))}
              {analyses.length === 0 && <Muted> No analyses yet. Run one above. </Muted>}
            </div>
          </Panel>

          <Panel title="Analysis Detail">
            {!selectedAnalysis && <Muted> Select an analysis to inspect scores and extracted skills. </Muted>}
            {selectedAnalysis && (
              <div style={{ display: 'grid', gap: 16 }}>
                <div style={{ display: 'grid', gap: 10, gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))' }}>
                  <Metric label="Status" value={selectedAnalysis.status} />
                  <Metric label="Provider" value={selectedAnalysis.provider} />
                  <Metric label="Mode" value={selectedAnalysis.gapAnalysisMode} />
                  <Metric label="Coverage" value={`${selectedAnalysis.coverageScore}%`} tone={scoreTone(selectedAnalysis.coverageScore)} />
                  <Metric label="Groundedness" value={`${selectedAnalysis.groundednessScore}%`} tone={scoreTone(selectedAnalysis.groundednessScore)} />
                  <Metric label="Latency" value={`${selectedAnalysis.latencyMs} ms`} />
                  <Metric label="Input Tokens" value={String(selectedAnalysis.inputTokens)} />
                  <Metric label="Output Tokens" value={String(selectedAnalysis.outputTokens)} />
                </div>

                <div style={{ display: 'grid', gap: 16, gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))' }}>
                  <SkillList
                    title={`Required Skills (${requiredSkills.length})`}
                    rows={requiredSkills}
                    renderExtra={(row) => row.evidenceQuote || row.jdEvidence || ''}
                  />
                  <SkillList
                    title={`Missing Required (${missingRequired.length})`}
                    rows={missingRequired}
                    renderExtra={(row) => row.evidenceQuote || row.jdEvidence || ''}
                  />
                  <SkillList
                    title={`Missing Preferred (${missingPreferred.length})`}
                    rows={missingPreferred}
                    renderExtra={(row) => row.evidenceQuote || row.jdEvidence || ''}
                  />
                </div>
              </div>
            )}
          </Panel>
        </section>
      </div>
    </div>
  );
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section style={{ border: '1px solid #d6d3d1', borderRadius: 16, background: 'rgba(255,255,255,0.9)', padding: 14 }}>
      <h2 style={{ margin: '0 0 10px', fontSize: 18 }}>{title}</h2>
      {children}
    </section>
  );
}

function StatCard({ label, value }: { label: string; value: number | string }) {
  return (
    <div style={{ border: '1px solid #d6d3d1', borderRadius: 14, background: '#fff', padding: 12 }}>
      <div style={{ fontSize: 12, color: '#57534e', textTransform: 'uppercase', letterSpacing: '0.08em' }}>{label}</div>
      <div style={{ marginTop: 4, fontSize: 24, fontWeight: 700 }}>{value}</div>
    </div>
  );
}

function ItemCard({
  selected,
  onSelect,
  onDelete,
  deleteBusy,
  title,
  subtitle
}: {
  selected: boolean;
  onSelect: () => void;
  onDelete: () => void;
  deleteBusy: boolean;
  title: string;
  subtitle: string;
}) {
  return (
    <div style={{ border: selected ? '2px solid #0f766e' : '1px solid #d6d3d1', borderRadius: 10, background: '#fff', padding: 10 }}>
      <button onClick={onSelect} style={{ ...buttonReset, width: '100%', textAlign: 'left' }}>
        <div style={{ fontWeight: 600 }}>{title}</div>
        <div style={{ marginTop: 4, fontSize: 12, color: '#57534e' }}>{subtitle}</div>
      </button>
      <div style={{ marginTop: 8 }}>
        <button onClick={onDelete} disabled={deleteBusy} style={buttonStyle('#991b1b')}>
          {deleteBusy ? 'Deleting...' : 'Delete'}
        </button>
      </div>
    </div>
  );
}

function SkillList({
  title,
  rows,
  renderExtra
}: {
  title: string;
  rows: SkillRow[];
  renderExtra: (row: SkillRow) => string;
}) {
  return (
    <div style={{ border: '1px solid #e7e5e4', borderRadius: 12, padding: 10, background: '#fff' }}>
      <h3 style={{ margin: '0 0 8px', fontSize: 14 }}>{title}</h3>
      <div style={{ display: 'grid', gap: 8, maxHeight: 280, overflow: 'auto' }}>
        {rows.map((row, index) => (
          <div key={`${row.skillName}-${index}`} style={{ borderTop: index === 0 ? 'none' : '1px solid #f1f5f9', paddingTop: index === 0 ? 0 : 8 }}>
            <div style={{ fontWeight: 600, fontSize: 13 }}>{row.skillName}</div>
            {renderExtra(row) && <div style={{ marginTop: 3, fontSize: 12, color: '#57534e' }}>{renderExtra(row)}</div>}
          </div>
        ))}
        {rows.length === 0 && <Muted> None </Muted>}
      </div>
    </div>
  );
}

function Metric({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div style={{ border: '1px solid #e7e5e4', borderRadius: 10, padding: 10, background: '#fff' }}>
      <div style={{ fontSize: 11, color: '#57534e', textTransform: 'uppercase', letterSpacing: '0.08em' }}>{label}</div>
      <div style={{ marginTop: 5, fontWeight: 700, color: tone || '#111827' }}>{value}</div>
    </div>
  );
}

function Badge({ text, tone }: { text: string; tone: string }) {
  return (
    <span style={{ borderRadius: 999, background: `${tone}15`, color: tone, border: `1px solid ${tone}30`, padding: '2px 8px' }}>
      {text}
    </span>
  );
}

function Muted({ children }: { children: React.ReactNode }) {
  return <div style={{ color: '#57534e', fontSize: 13 }}>{children}</div>;
}

const buttonReset: React.CSSProperties = {
  border: 'none',
  background: 'transparent',
  padding: 0,
  cursor: 'pointer'
};

const inputStyle: React.CSSProperties = {
  width: '100%',
  border: '1px solid #d6d3d1',
  borderRadius: 10,
  padding: '9px 10px',
  font: 'inherit',
  background: '#fff'
};

const labelStyle: React.CSSProperties = {
  display: 'grid',
  gap: 6,
  fontSize: 13,
  color: '#44403c'
};

function buttonStyle(background: string): React.CSSProperties {
  return {
    border: 'none',
    borderRadius: 10,
    padding: '9px 12px',
    fontWeight: 600,
    color: '#fff',
    background,
    cursor: 'pointer'
  };
}

export default App;
