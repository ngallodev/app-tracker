import { FormEvent, useEffect, useMemo, useState } from 'react';

type Job = {
  id: string;
  title: string;
  company: string;
  descriptionText?: string | null;
  sourceUrl?: string | null;
  workType?: string | null;
  employmentType?: string | null;
  salaryMin?: number | null;
  salaryMax?: number | null;
  salaryCurrency?: string | null;
  recruiterName?: string | null;
  recruiterEmail?: string | null;
  recruiterPhone?: string | null;
  recruiterLinkedIn?: string | null;
  companyCareersUrl?: string | null;
  isTestData?: boolean;
  createdAt: string;
  updatedAt: string;
};

type Resume = {
  id: string;
  name: string;
  contentPreview?: string | null;
  desiredSalaryMin?: number | null;
  desiredSalaryMax?: number | null;
  salaryCurrency?: string | null;
  isTestData?: boolean;
  createdAt: string;
  updatedAt: string;
};

type Analysis = {
  analysisId: string;
  jobId: string;
  resumeId: string;
  status: string;
  errorMessage?: string | null;
  errorCategory?: string | null;
  gapAnalysisMode: string;
  usedGapLlmFallback: boolean;
  coverageScore: number;
  groundednessScore: number;
  salaryAlignmentScore: number;
  salaryAlignmentNote?: string | null;
  requiredSkillsJson?: string | null;
  missingRequiredJson?: string | null;
  missingPreferredJson?: string | null;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  provider: string;
  executionMode: string;
  isTestData?: boolean;
  createdAt: string;
};

type ProviderAvailability = {
  name: string;
  available: boolean;
  message: string;
};

type AnalysisProvidersResponse = {
  defaultProvider: string;
  providers: ProviderAvailability[];
};

type JobApplicationEvent = {
  id: string;
  jobApplicationId: string;
  eventType: string;
  eventAt: string;
  notes?: string | null;
  channel?: string | null;
  positiveOutcome: boolean;
  createdAt: string;
};

type JobApplication = {
  id: string;
  jobId: string;
  resumeId: string;
  jobTitle: string;
  company: string;
  resumeName: string;
  status: string;
  appliedAt: string;
  closedAt?: string | null;
  applicationUrl?: string | null;
  notes?: string | null;
  isTestData: boolean;
  createdAt: string;
  updatedAt: string;
  events: JobApplicationEvent[];
};

type SkillRow = {
  skillName: string;
  evidenceQuote?: string;
  jdEvidence?: string;
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
      const text = await res.text();
      if (text) message = text;
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
      jdEvidence: item.jd_evidence ?? item.JdEvidence
    }));
  } catch {
    return [];
  }
}

function App() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [analyses, setAnalyses] = useState<Analysis[]>([]);
  const [providers, setProviders] = useState<ProviderAvailability[]>([]);
  const [providerDefault, setProviderDefault] = useState('');
  const [applications, setApplications] = useState<JobApplication[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  const [jobForm, setJobForm] = useState({
    sourceUrl: '',
    title: '',
    company: '',
    descriptionText: '',
    recruiterEmail: '',
    recruiterPhone: '',
    recruiterLinkedIn: '',
    companyCareersUrl: '',
    isTestData: false
  });

  const [resumeForm, setResumeForm] = useState({
    name: '',
    content: '',
    desiredSalaryMin: '',
    desiredSalaryMax: '',
    salaryCurrency: 'USD',
    isTestData: false
  });

  const [selectedJobId, setSelectedJobId] = useState('');
  const [selectedResumeId, setSelectedResumeId] = useState('');
  const [selectedAnalysisId, setSelectedAnalysisId] = useState('');
  const [analysisProvider, setAnalysisProvider] = useState('');
  const [analysisIsTestData, setAnalysisIsTestData] = useState(false);

  const [appForm, setAppForm] = useState({
    jobId: '',
    resumeId: '',
    applicationUrl: '',
    notes: '',
    isTestData: false
  });
  const [selectedApplicationId, setSelectedApplicationId] = useState('');
  const [eventForm, setEventForm] = useState({
    eventType: 'Note',
    channel: '',
    notes: '',
    positiveOutcome: false
  });

  const selectedAnalysis = useMemo(
    () => analyses.find((a) => a.analysisId === selectedAnalysisId) ?? analyses[0] ?? null,
    [analyses, selectedAnalysisId]
  );

  const selectedApplication = useMemo(
    () => applications.find((a) => a.id === selectedApplicationId) ?? applications[0] ?? null,
    [applications, selectedApplicationId]
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
      const [jobsData, resumesData, analysesData, providersData, appsData] = await Promise.all([
        api<Job[]>('/api/jobs'),
        api<Resume[]>('/api/resumes'),
        api<Analysis[]>('/api/analyses'),
        api<AnalysisProvidersResponse>('/api/analyses/providers'),
        api<JobApplication[]>('/api/applications')
      ]);
      setJobs(jobsData);
      setResumes(resumesData);
      setAnalyses(analysesData);
      setProviders(providersData.providers);
      setProviderDefault(providersData.defaultProvider);
      setApplications(appsData);
      setSelectedJobId((curr) => curr || jobsData[0]?.id || '');
      setSelectedResumeId((curr) => curr || resumesData[0]?.id || '');
      setSelectedAnalysisId((curr) => curr || analysesData[0]?.analysisId || '');
      setAnalysisProvider((curr) => curr || providersData.defaultProvider || '');
      setAppForm((curr) => ({
        ...curr,
        jobId: curr.jobId || jobsData[0]?.id || '',
        resumeId: curr.resumeId || resumesData[0]?.id || ''
      }));
      setSelectedApplicationId((curr) => curr || appsData[0]?.id || '');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unknown error');
    } finally {
      setLoading(false);
    }
  }

  async function handleImportFromUrl() {
    if (!jobForm.sourceUrl.trim()) return;
    try {
      setBusy('import-job-url');
      setError(null);
      const imported = await api<{
        title: string;
        company: string;
        descriptionText: string;
        sourceUrl: string;
        recruiterEmail?: string;
        recruiterPhone?: string;
        recruiterLinkedIn?: string;
        companyCareersUrl?: string;
      }>('/api/jobs/extract-from-url', {
        method: 'POST',
        body: JSON.stringify({ sourceUrl: jobForm.sourceUrl.trim() })
      });
      setJobForm((prev) => ({
        ...prev,
        title: imported.title,
        company: imported.company,
        descriptionText: imported.descriptionText,
        recruiterEmail: imported.recruiterEmail || prev.recruiterEmail,
        recruiterPhone: imported.recruiterPhone || prev.recruiterPhone,
        recruiterLinkedIn: imported.recruiterLinkedIn || prev.recruiterLinkedIn,
        companyCareersUrl: imported.companyCareersUrl || prev.companyCareersUrl
      }));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to import job from URL');
    } finally {
      setBusy(null);
    }
  }

  async function handleCreateJob(e: FormEvent) {
    e.preventDefault();
    try {
      setBusy('create-job');
      setError(null);
      const created = await api<Job>('/api/jobs', {
        method: 'POST',
        body: JSON.stringify({
          sourceUrl: jobForm.sourceUrl.trim() || undefined,
          title: jobForm.title.trim() || undefined,
          company: jobForm.company.trim() || undefined,
          descriptionText: jobForm.descriptionText.trim() || undefined,
          recruiterEmail: jobForm.recruiterEmail.trim() || undefined,
          recruiterPhone: jobForm.recruiterPhone.trim() || undefined,
          recruiterLinkedIn: jobForm.recruiterLinkedIn.trim() || undefined,
          companyCareersUrl: jobForm.companyCareersUrl.trim() || undefined,
          isTestData: jobForm.isTestData
        })
      });
      setJobs((prev) => [created, ...prev]);
      setSelectedJobId(created.id);
      setAppForm((prev) => ({ ...prev, jobId: created.id }));
      setJobForm({
        sourceUrl: '',
        title: '',
        company: '',
        descriptionText: '',
        recruiterEmail: '',
        recruiterPhone: '',
        recruiterLinkedIn: '',
        companyCareersUrl: '',
        isTestData: false
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create job');
    } finally {
      setBusy(null);
    }
  }

  async function handleCreateResume(e: FormEvent) {
    e.preventDefault();
    if (!resumeForm.name.trim() || !resumeForm.content.trim()) {
      setError('Resume name and content are required.');
      return;
    }

    try {
      setBusy('create-resume');
      setError(null);
      const created = await api<Resume>('/api/resumes', {
        method: 'POST',
        body: JSON.stringify({
          name: resumeForm.name.trim(),
          content: resumeForm.content,
          desiredSalaryMin: parseNumber(resumeForm.desiredSalaryMin),
          desiredSalaryMax: parseNumber(resumeForm.desiredSalaryMax),
          salaryCurrency: resumeForm.salaryCurrency.trim() || undefined,
          isTestData: resumeForm.isTestData
        })
      });
      setResumes((prev) => [created, ...prev]);
      setSelectedResumeId(created.id);
      setAppForm((prev) => ({ ...prev, resumeId: created.id }));
      setResumeForm({
        name: '',
        content: '',
        desiredSalaryMin: '',
        desiredSalaryMax: '',
        salaryCurrency: 'USD',
        isTestData: false
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create resume');
    } finally {
      setBusy(null);
    }
  }

  async function handleRunAnalysis(e: FormEvent) {
    e.preventDefault();
    if (!selectedJobId || !selectedResumeId) {
      setError('Select a job and resume first.');
      return;
    }

    try {
      setBusy('run-analysis');
      setError(null);
      const result = await api<Analysis>('/api/analyses', {
        method: 'POST',
        body: JSON.stringify({
          jobId: selectedJobId,
          resumeId: selectedResumeId,
          provider: analysisProvider,
          isTestData: analysisIsTestData
        })
      });
      setAnalyses((prev) => [result, ...prev.filter((a) => a.analysisId !== result.analysisId)]);
      setSelectedAnalysisId(result.analysisId);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Analysis failed');
    } finally {
      setBusy(null);
    }
  }

  async function handleCreateApplication(e: FormEvent) {
    e.preventDefault();
    if (!appForm.jobId || !appForm.resumeId) {
      setError('Select job and resume for application.');
      return;
    }

    try {
      setBusy('create-app');
      setError(null);
      const created = await api<JobApplication>('/api/applications', {
        method: 'POST',
        body: JSON.stringify({
          jobId: appForm.jobId,
          resumeId: appForm.resumeId,
          applicationUrl: appForm.applicationUrl || undefined,
          notes: appForm.notes || undefined,
          isTestData: appForm.isTestData
        })
      });
      setApplications((prev) => [created, ...prev]);
      setSelectedApplicationId(created.id);
      setAppForm((prev) => ({ ...prev, applicationUrl: '', notes: '', isTestData: false }));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create application');
    } finally {
      setBusy(null);
    }
  }

  async function handleUpdateApplicationStatus(status: string) {
    if (!selectedApplication) return;
    try {
      setBusy('update-app-status');
      setError(null);
      const updated = await api<JobApplication>(`/api/applications/${selectedApplication.id}`, {
        method: 'PUT',
        body: JSON.stringify({ status })
      });
      setApplications((prev) => prev.map((a) => (a.id === updated.id ? updated : a)));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update application');
    } finally {
      setBusy(null);
    }
  }

  async function handleAddApplicationEvent(e: FormEvent) {
    e.preventDefault();
    if (!selectedApplication) return;
    try {
      setBusy('add-app-event');
      setError(null);
      await api<JobApplicationEvent>(`/api/applications/${selectedApplication.id}/events`, {
        method: 'POST',
        body: JSON.stringify(eventForm)
      });
      const refreshed = await api<JobApplication>(`/api/applications/${selectedApplication.id}`);
      setApplications((prev) => prev.map((a) => (a.id === refreshed.id ? refreshed : a)));
      setEventForm({ eventType: 'Note', channel: '', notes: '', positiveOutcome: false });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add event');
    } finally {
      setBusy(null);
    }
  }

  async function handleClearTestData() {
    if (!window.confirm('Delete all test-generated jobs, resumes, analyses, and applications?')) return;
    try {
      setBusy('clear-test-data');
      setError(null);
      await api<{ dbChanges: number }>('/api/dev/test-data', { method: 'DELETE' });
      await refreshAll();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to clear test data');
    } finally {
      setBusy(null);
    }
  }

  async function readTextFile(file: File, onLoaded: (content: string) => void, onName?: (name: string) => void) {
    if (!file) return;
    const text = await file.text();
    onLoaded(text);
    if (onName) {
      const baseName = file.name.replace(/\.[^.]+$/, '');
      onName(baseName);
    }
  }

  if (loading) {
    return <div style={{ padding: 24, fontFamily: '"Space Grotesk", "Segoe UI", sans-serif' }}>Loading app tracker...</div>;
  }

  const availableProviders = providers.filter((p) => p.available);

  return (
    <div style={{ minHeight: '100vh', background: 'radial-gradient(circle at 20% 0%, #fff7ed 0%, #f8fafc 50%, #eef2ff 100%)', color: '#0f172a', fontFamily: '"Space Grotesk", "Segoe UI", sans-serif' }}>
      <div style={{ maxWidth: 1400, margin: '0 auto', padding: 20, display: 'grid', gap: 14 }}>
        <header style={panelStyle}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 10, flexWrap: 'wrap' }}>
            <div>
              <h1 style={{ margin: 0, fontSize: 30 }}>Application Tracker</h1>
              <p style={{ margin: '6px 0 0', color: '#475569' }}>
                URL + bookmarklet ingestion, provider-aware analysis, salary alignment, and full application lifecycle tracking.
              </p>
            </div>
            <div style={{ display: 'flex', gap: 8 }}>
              <button onClick={() => void refreshAll()} disabled={busy !== null} style={buttonStyle('#0f172a')}>Refresh</button>
              <button onClick={() => void handleClearTestData()} disabled={busy !== null} style={buttonStyle('#991b1b')}>Clear Test Data</button>
            </div>
          </div>
          {error && <div style={{ marginTop: 10, border: '1px solid #fecaca', background: '#fee2e2', color: '#991b1b', borderRadius: 10, padding: 10 }}>{error}</div>}
        </header>

        <section style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))' }}>
          <Stat label="Jobs" value={jobs.length} />
          <Stat label="Resumes" value={resumes.length} />
          <Stat label="Analyses" value={analyses.length} />
          <Stat label="Applications" value={applications.length} />
          <Stat label="Default Provider" value={providerDefault || 'n/a'} />
        </section>

        <section style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))' }}>
          <Panel title="Create Job">
            <form onSubmit={handleCreateJob} style={{ display: 'grid', gap: 8 }}>
              <label style={labelStyle}>
                <span>Source URL (bookmarklet/job post URL)</span>
                <input style={inputStyle} value={jobForm.sourceUrl} onChange={(e) => setJobForm({ ...jobForm, sourceUrl: e.target.value })} placeholder="https://..." />
              </label>
              <button type="button" onClick={() => void handleImportFromUrl()} disabled={busy !== null || !jobForm.sourceUrl.trim()} style={buttonStyle('#7c2d12')}>
                {busy === 'import-job-url' ? 'Importing...' : 'Fetch Job Details From URL'}
              </button>
              <label style={labelStyle}>
                <span>Upload JD `.txt`/`.md`</span>
                <input
                  type="file"
                  accept=".txt,.md,text/plain,text/markdown"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (!file) return;
                    void readTextFile(file, (content) => setJobForm((prev) => ({ ...prev, descriptionText: content })), (name) => {
                      if (!jobForm.title) {
                        setJobForm((prev) => ({ ...prev, title: name }));
                      }
                    });
                  }}
                />
              </label>
              <DropZone
                label="Or drag and drop a JD file here"
                onFile={(file) => void readTextFile(file, (content) => setJobForm((prev) => ({ ...prev, descriptionText: content })))}
              />
              <input style={inputStyle} value={jobForm.title} onChange={(e) => setJobForm({ ...jobForm, title: e.target.value })} placeholder="Title (optional when URL set)" />
              <input style={inputStyle} value={jobForm.company} onChange={(e) => setJobForm({ ...jobForm, company: e.target.value })} placeholder="Company (optional when URL set)" />
              <textarea style={inputStyle} rows={6} value={jobForm.descriptionText} onChange={(e) => setJobForm({ ...jobForm, descriptionText: e.target.value })} placeholder="Job description text" />
              <div style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))' }}>
                <input style={inputStyle} value={jobForm.recruiterEmail} onChange={(e) => setJobForm({ ...jobForm, recruiterEmail: e.target.value })} placeholder="Recruiter email" />
                <input style={inputStyle} value={jobForm.recruiterPhone} onChange={(e) => setJobForm({ ...jobForm, recruiterPhone: e.target.value })} placeholder="Recruiter phone" />
                <input style={inputStyle} value={jobForm.recruiterLinkedIn} onChange={(e) => setJobForm({ ...jobForm, recruiterLinkedIn: e.target.value })} placeholder="Recruiter LinkedIn URL" />
                <input style={inputStyle} value={jobForm.companyCareersUrl} onChange={(e) => setJobForm({ ...jobForm, companyCareersUrl: e.target.value })} placeholder="Company careers URL" />
              </div>
              <label style={{ ...labelStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
                <input type="checkbox" checked={jobForm.isTestData} onChange={(e) => setJobForm({ ...jobForm, isTestData: e.target.checked })} />
                Mark as test-generated
              </label>
              <button type="submit" disabled={busy !== null} style={buttonStyle('#0f766e')}>
                {busy === 'create-job' ? 'Creating...' : 'Create Job'}
              </button>
            </form>
          </Panel>

          <Panel title="Create Resume">
            <form onSubmit={handleCreateResume} style={{ display: 'grid', gap: 8 }}>
              <input style={inputStyle} value={resumeForm.name} onChange={(e) => setResumeForm({ ...resumeForm, name: e.target.value })} placeholder="Resume name" required />
              <label style={labelStyle}>
                <span>Upload resume `.txt`/`.md`</span>
                <input
                  type="file"
                  accept=".txt,.md,text/plain,text/markdown"
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    if (!file) return;
                    void readTextFile(file, (content) => setResumeForm((prev) => ({ ...prev, content })), (name) => {
                      if (!resumeForm.name) {
                        setResumeForm((prev) => ({ ...prev, name }));
                      }
                    });
                  }}
                />
              </label>
              <DropZone
                label="Or drag and drop a resume file here"
                onFile={(file) => void readTextFile(file, (content) => setResumeForm((prev) => ({ ...prev, content })))}
              />
              <textarea style={inputStyle} rows={8} value={resumeForm.content} onChange={(e) => setResumeForm({ ...resumeForm, content: e.target.value })} placeholder="Resume content" required />
              <div style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))' }}>
                <input style={inputStyle} value={resumeForm.desiredSalaryMin} onChange={(e) => setResumeForm({ ...resumeForm, desiredSalaryMin: e.target.value })} placeholder="Desired salary min" />
                <input style={inputStyle} value={resumeForm.desiredSalaryMax} onChange={(e) => setResumeForm({ ...resumeForm, desiredSalaryMax: e.target.value })} placeholder="Desired salary max" />
                <input style={inputStyle} value={resumeForm.salaryCurrency} onChange={(e) => setResumeForm({ ...resumeForm, salaryCurrency: e.target.value })} placeholder="Currency" />
              </div>
              <label style={{ ...labelStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
                <input type="checkbox" checked={resumeForm.isTestData} onChange={(e) => setResumeForm({ ...resumeForm, isTestData: e.target.checked })} />
                Mark as test-generated
              </label>
              <button type="submit" disabled={busy !== null} style={buttonStyle('#1d4ed8')}>
                {busy === 'create-resume' ? 'Creating...' : 'Create Resume'}
              </button>
            </form>
          </Panel>
        </section>

        <section style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))' }}>
          <Panel title="Run Analysis">
            <form onSubmit={handleRunAnalysis} style={{ display: 'grid', gap: 10 }}>
              <select value={selectedJobId} onChange={(e) => setSelectedJobId(e.target.value)} style={inputStyle}>
                <option value="">Select job</option>
                {jobs.map((job) => (
                  <option key={job.id} value={job.id}>{job.title} @ {job.company}</option>
                ))}
              </select>
              <select value={selectedResumeId} onChange={(e) => setSelectedResumeId(e.target.value)} style={inputStyle}>
                <option value="">Select resume</option>
                {resumes.map((resume) => (
                  <option key={resume.id} value={resume.id}>{resume.name}</option>
                ))}
              </select>
              <label style={labelStyle}>
                <span>Provider</span>
                <select value={analysisProvider} onChange={(e) => setAnalysisProvider(e.target.value)} style={inputStyle}>
                  {availableProviders.map((p) => (
                    <option key={p.name} value={p.name}>{p.name}</option>
                  ))}
                </select>
                <small style={{ color: '#64748b' }}>Default active provider: <strong>{providerDefault}</strong></small>
              </label>
              <label style={{ ...labelStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
                <input type="checkbox" checked={analysisIsTestData} onChange={(e) => setAnalysisIsTestData(e.target.checked)} />
                Mark analysis as test-generated
              </label>
              <button type="submit" disabled={busy !== null || !selectedJobId || !selectedResumeId} style={buttonStyle('#7c2d12')}>
                {busy === 'run-analysis' ? 'Running...' : 'Run Analysis'}
              </button>
            </form>
            <div style={{ marginTop: 10, fontSize: 12, color: '#475569' }}>
              <div><strong>Coverage %</strong>: matched required skills / total required skills.</div>
              <div><strong>Groundedness %</strong>: how many extracted skills and matches include direct evidence quotes.</div>
              <div><strong>LM Studio reliability mode</strong>: long JDs are chunked and merged to fit local context windows.</div>
            </div>
          </Panel>

          <Panel title="Provider Health">
            <div style={{ display: 'grid', gap: 8 }}>
              {providers.map((provider) => (
                <div key={provider.name} style={{ border: '1px solid #e2e8f0', borderRadius: 10, padding: 8, background: '#fff' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                    <strong>{provider.name}</strong>
                    <span style={{ color: provider.available ? '#0f766e' : '#b91c1c' }}>{provider.available ? 'available' : 'unavailable'}</span>
                  </div>
                  <div style={{ marginTop: 4, fontSize: 12, color: '#475569' }}>{provider.message}</div>
                </div>
              ))}
            </div>
          </Panel>
        </section>

        <section style={{ display: 'grid', gap: 12, gridTemplateColumns: 'minmax(320px, 430px) 1fr' }}>
          <Panel title={`Analyses (${analyses.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 500, overflow: 'auto' }}>
              {analyses.map((analysis) => (
                <button
                  key={analysis.analysisId}
                  onClick={() => setSelectedAnalysisId(analysis.analysisId)}
                  style={{
                    textAlign: 'left',
                    border: selectedAnalysis?.analysisId === analysis.analysisId ? '2px solid #0f766e' : '1px solid #d6d3d1',
                    borderRadius: 10,
                    background: '#fff',
                    padding: 10
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
                    <strong>{analysis.status}</strong>
                    <span style={{ fontSize: 12, color: '#64748b' }}>{new Date(analysis.createdAt).toLocaleString()}</span>
                  </div>
                  <div style={{ marginTop: 6, display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                    <Badge text={`${analysis.provider}`} tone="#334155" />
                    <Badge text={`coverage ${analysis.coverageScore}%`} tone="#0f766e" />
                    <Badge text={`grounded ${analysis.groundednessScore}%`} tone="#0369a1" />
                    <Badge text={`salary ${analysis.salaryAlignmentScore}%`} tone="#7c3aed" />
                  </div>
                </button>
              ))}
              {analyses.length === 0 && <Muted>No analyses yet.</Muted>}
            </div>
          </Panel>

          <Panel title="Analysis Detail">
            {!selectedAnalysis && <Muted>Select an analysis.</Muted>}
            {selectedAnalysis && (
              <div style={{ display: 'grid', gap: 12 }}>
                <div style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))' }}>
                  <Metric label="Status" value={selectedAnalysis.status} />
                  <Metric label="Provider" value={selectedAnalysis.provider} />
                  <Metric label="Mode" value={selectedAnalysis.gapAnalysisMode} />
                  <Metric label="Coverage" value={`${selectedAnalysis.coverageScore}%`} />
                  <Metric label="Groundedness" value={`${selectedAnalysis.groundednessScore}%`} />
                  <Metric label="Salary Match" value={`${selectedAnalysis.salaryAlignmentScore}%`} />
                  <Metric label="Latency" value={`${selectedAnalysis.latencyMs} ms`} />
                </div>
                {selectedAnalysis.errorMessage && (
                  <div style={{ border: '1px solid #fecaca', background: '#fff1f2', color: '#9f1239', borderRadius: 10, padding: 10 }}>
                    <strong>Analysis Failure Detail</strong>
                    <div>Category: {selectedAnalysis.errorCategory || 'unknown'}</div>
                    <div>{selectedAnalysis.errorMessage}</div>
                  </div>
                )}
                {selectedAnalysis.salaryAlignmentNote && (
                  <div style={{ border: '1px solid #ddd6fe', background: '#f5f3ff', color: '#5b21b6', borderRadius: 10, padding: 10 }}>
                    <strong>Salary Alignment Note</strong>
                    <div>{selectedAnalysis.salaryAlignmentNote}</div>
                  </div>
                )}
                <div style={{ display: 'grid', gap: 10, gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))' }}>
                  <SkillList title={`Required Skills (${requiredSkills.length})`} rows={requiredSkills} />
                  <SkillList title={`Missing Required (${missingRequired.length})`} rows={missingRequired} />
                  <SkillList title={`Missing Preferred (${missingPreferred.length})`} rows={missingPreferred} />
                </div>
              </div>
            )}
          </Panel>
        </section>

        <section style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))' }}>
          <Panel title="Application Tracking">
            <form onSubmit={handleCreateApplication} style={{ display: 'grid', gap: 8 }}>
              <select value={appForm.jobId} onChange={(e) => setAppForm({ ...appForm, jobId: e.target.value })} style={inputStyle}>
                <option value="">Select job to mark as applied</option>
                {jobs.map((job) => (
                  <option key={job.id} value={job.id}>{job.title} @ {job.company}</option>
                ))}
              </select>
              <select value={appForm.resumeId} onChange={(e) => setAppForm({ ...appForm, resumeId: e.target.value })} style={inputStyle}>
                <option value="">Select resume used for application</option>
                {resumes.map((resume) => (
                  <option key={resume.id} value={resume.id}>{resume.name}</option>
                ))}
              </select>
              <input style={inputStyle} value={appForm.applicationUrl} onChange={(e) => setAppForm({ ...appForm, applicationUrl: e.target.value })} placeholder="Application URL" />
              <textarea style={inputStyle} rows={3} value={appForm.notes} onChange={(e) => setAppForm({ ...appForm, notes: e.target.value })} placeholder="Application notes" />
              <label style={{ ...labelStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
                <input type="checkbox" checked={appForm.isTestData} onChange={(e) => setAppForm({ ...appForm, isTestData: e.target.checked })} />
                Mark application as test-generated
              </label>
              <button type="submit" disabled={busy !== null} style={buttonStyle('#0f766e')}>
                {busy === 'create-app' ? 'Saving...' : 'Mark Job As Applied'}
              </button>
            </form>

            <div style={{ marginTop: 12, display: 'grid', gap: 8, maxHeight: 220, overflow: 'auto' }}>
              {applications.map((appItem) => (
                <button
                  key={appItem.id}
                  onClick={() => setSelectedApplicationId(appItem.id)}
                  style={{
                    textAlign: 'left',
                    border: selectedApplication?.id === appItem.id ? '2px solid #2563eb' : '1px solid #d6d3d1',
                    borderRadius: 10,
                    background: '#fff',
                    padding: 8
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: 6 }}>
                    <strong>{appItem.jobTitle}</strong>
                    <Badge text={appItem.status} tone="#1e40af" />
                  </div>
                  <div style={{ fontSize: 12, color: '#64748b' }}>{appItem.company} | Resume: {appItem.resumeName}</div>
                </button>
              ))}
              {applications.length === 0 && <Muted>No applications tracked yet.</Muted>}
            </div>
          </Panel>

          <Panel title="Application Events & Status">
            {!selectedApplication && <Muted>Select an application.</Muted>}
            {selectedApplication && (
              <div style={{ display: 'grid', gap: 10 }}>
                <div style={{ display: 'grid', gap: 8, gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))' }}>
                  {['Applied', 'Interviewing', 'Offer', 'Rejected', 'Closed'].map((status) => (
                    <button
                      key={status}
                      onClick={() => void handleUpdateApplicationStatus(status)}
                      disabled={busy !== null}
                      style={buttonStyle(status === 'Rejected' || status === 'Closed' ? '#991b1b' : '#334155')}
                    >
                      {status}
                    </button>
                  ))}
                </div>
                <form onSubmit={handleAddApplicationEvent} style={{ display: 'grid', gap: 8 }}>
                  <select value={eventForm.eventType} onChange={(e) => setEventForm({ ...eventForm, eventType: e.target.value })} style={inputStyle}>
                    {['Note', 'Email', 'Call', 'Interview', 'Offer', 'Rejection', 'StatusChange'].map((type) => (
                      <option key={type} value={type}>{type}</option>
                    ))}
                  </select>
                  <input style={inputStyle} value={eventForm.channel} onChange={(e) => setEventForm({ ...eventForm, channel: e.target.value })} placeholder="Channel (email/phone/linkedin)" />
                  <textarea style={inputStyle} rows={3} value={eventForm.notes} onChange={(e) => setEventForm({ ...eventForm, notes: e.target.value })} placeholder="Event notes" />
                  <label style={{ ...labelStyle, display: 'flex', alignItems: 'center', gap: 8 }}>
                    <input type="checkbox" checked={eventForm.positiveOutcome} onChange={(e) => setEventForm({ ...eventForm, positiveOutcome: e.target.checked })} />
                    Positive event outcome
                  </label>
                  <button type="submit" disabled={busy !== null} style={buttonStyle('#1d4ed8')}>
                    {busy === 'add-app-event' ? 'Adding...' : 'Add Event'}
                  </button>
                </form>
                <div style={{ display: 'grid', gap: 8, maxHeight: 240, overflow: 'auto' }}>
                  {selectedApplication.events.map((evt) => (
                    <div key={evt.id} style={{ border: '1px solid #e2e8f0', borderRadius: 10, padding: 8, background: '#fff' }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 6 }}>
                        <strong>{evt.eventType}</strong>
                        <span style={{ fontSize: 12, color: '#64748b' }}>{new Date(evt.eventAt).toLocaleString()}</span>
                      </div>
                      <div style={{ marginTop: 4, fontSize: 12, color: '#475569' }}>{evt.notes || 'No notes'}</div>
                    </div>
                  ))}
                  {selectedApplication.events.length === 0 && <Muted>No events yet.</Muted>}
                </div>
              </div>
            )}
          </Panel>
        </section>

        <section style={{ display: 'grid', gap: 12, gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))' }}>
          <Panel title={`Jobs (${jobs.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 240, overflow: 'auto' }}>
              {jobs.map((job) => (
                <div key={job.id} style={{ border: '1px solid #e2e8f0', borderRadius: 10, padding: 8, background: '#fff' }}>
                  <strong>{job.title} @ {job.company}</strong>
                  <div style={{ marginTop: 4, fontSize: 12, color: '#334155' }}>
                    {job.workType || 'unknown'} | {job.employmentType || 'unknown'}
                  </div>
                  <div style={{ marginTop: 4, fontSize: 12, color: '#475569' }}>
                    Salary: {formatSalary(job.salaryMin, job.salaryMax, job.salaryCurrency)}
                  </div>
                  <div style={{ marginTop: 4, fontSize: 12, color: '#475569' }}>
                    Contact: {job.recruiterEmail || '-'} | {job.recruiterPhone || '-'}
                  </div>
                </div>
              ))}
            </div>
          </Panel>

          <Panel title={`Resumes (${resumes.length})`}>
            <div style={{ display: 'grid', gap: 8, maxHeight: 240, overflow: 'auto' }}>
              {resumes.map((resume) => (
                <div key={resume.id} style={{ border: '1px solid #e2e8f0', borderRadius: 10, padding: 8, background: '#fff' }}>
                  <strong>{resume.name}</strong>
                  <div style={{ marginTop: 4, fontSize: 12, color: '#475569' }}>{resume.contentPreview || 'No preview'}</div>
                  <div style={{ marginTop: 4, fontSize: 12, color: '#334155' }}>
                    Desired Salary: {formatSalary(resume.desiredSalaryMin, resume.desiredSalaryMax, resume.salaryCurrency)}
                  </div>
                </div>
              ))}
            </div>
          </Panel>
        </section>
      </div>
    </div>
  );
}

function parseNumber(value: string): number | undefined {
  const trimmed = value.trim();
  if (!trimmed) return undefined;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : undefined;
}

function formatSalary(min?: number | null, max?: number | null, currency?: string | null): string {
  if (min == null && max == null) return 'not specified';
  const curr = currency || 'USD';
  if (min != null && max != null) return `${curr} ${min} - ${max}`;
  return `${curr} ${min ?? max}`;
}

function Panel({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section style={panelStyle}>
      <h2 style={{ margin: '0 0 10px', fontSize: 18 }}>{title}</h2>
      {children}
    </section>
  );
}

function Stat({ label, value }: { label: string; value: number | string }) {
  return (
    <div style={{ ...panelStyle, padding: 10 }}>
      <div style={{ textTransform: 'uppercase', fontSize: 11, letterSpacing: '0.08em', color: '#64748b' }}>{label}</div>
      <div style={{ marginTop: 4, fontWeight: 700, fontSize: 24 }}>{value}</div>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ border: '1px solid #e2e8f0', background: '#fff', borderRadius: 10, padding: 8 }}>
      <div style={{ textTransform: 'uppercase', fontSize: 10, letterSpacing: '0.08em', color: '#64748b' }}>{label}</div>
      <div style={{ marginTop: 4, fontWeight: 700 }}>{value}</div>
    </div>
  );
}

function SkillList({ title, rows }: { title: string; rows: SkillRow[] }) {
  return (
    <div style={{ border: '1px solid #e2e8f0', borderRadius: 10, background: '#fff', padding: 10 }}>
      <h3 style={{ margin: '0 0 8px', fontSize: 14 }}>{title}</h3>
      <div style={{ display: 'grid', gap: 8, maxHeight: 250, overflow: 'auto' }}>
        {rows.map((row, idx) => (
          <div key={`${row.skillName}-${idx}`} style={{ borderTop: idx === 0 ? 'none' : '1px solid #f1f5f9', paddingTop: idx === 0 ? 0 : 8 }}>
            <div style={{ fontWeight: 600 }}>{row.skillName}</div>
            <div style={{ marginTop: 3, fontSize: 12, color: '#64748b' }}>{row.evidenceQuote || row.jdEvidence || ''}</div>
          </div>
        ))}
        {rows.length === 0 && <Muted>None</Muted>}
      </div>
    </div>
  );
}

function Badge({ text, tone }: { text: string; tone: string }) {
  return <span style={{ borderRadius: 999, background: `${tone}18`, border: `1px solid ${tone}44`, color: tone, padding: '2px 8px', fontSize: 12 }}>{text}</span>;
}

function DropZone({ label, onFile }: { label: string; onFile: (file: File) => void }) {
  const [active, setActive] = useState(false);
  return (
    <div
      onDragOver={(e) => {
        e.preventDefault();
        setActive(true);
      }}
      onDragLeave={() => setActive(false)}
      onDrop={(e) => {
        e.preventDefault();
        setActive(false);
        const file = e.dataTransfer.files?.[0];
        if (file) onFile(file);
      }}
      style={{
        border: active ? '2px solid #2563eb' : '1px dashed #94a3b8',
        borderRadius: 10,
        padding: 10,
        color: '#475569',
        background: active ? '#eff6ff' : '#f8fafc',
        fontSize: 13
      }}
    >
      {label}
    </div>
  );
}

function Muted({ children }: { children: React.ReactNode }) {
  return <div style={{ color: '#64748b', fontSize: 13 }}>{children}</div>;
}

const panelStyle: React.CSSProperties = {
  border: '1px solid #cbd5e1',
  borderRadius: 14,
  background: 'rgba(255,255,255,0.92)',
  padding: 12,
  boxShadow: '0 8px 20px rgba(15, 23, 42, 0.04)'
};

const inputStyle: React.CSSProperties = {
  width: '100%',
  border: '1px solid #cbd5e1',
  borderRadius: 10,
  padding: '9px 10px',
  font: 'inherit',
  background: '#fff'
};

const labelStyle: React.CSSProperties = {
  display: 'grid',
  gap: 6,
  fontSize: 13,
  color: '#334155'
};

function buttonStyle(background: string): React.CSSProperties {
  return {
    border: 'none',
    borderRadius: 10,
    padding: '8px 11px',
    fontWeight: 600,
    color: '#fff',
    background,
    cursor: 'pointer'
  };
}

export default App;
