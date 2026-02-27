import { FormEvent, useEffect, useMemo, useState } from "react";
import { api } from "../lib/api";
import type { AnalysisResult, Job, Resume } from "../types";

function prettyJson(raw: string | null): string {
  if (!raw) {
    return "-";
  }

  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
}

export function AnalysisPage() {
  const [jobs, setJobs] = useState<Job[]>([]);
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [analyses, setAnalyses] = useState<AnalysisResult[]>([]);
  const [selectedJobId, setSelectedJobId] = useState("");
  const [selectedResumeId, setSelectedResumeId] = useState("");
  const [running, setRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    setError(null);
    try {
      const [jobList, resumeList, analysisList] = await Promise.all([
        api.listJobs(),
        api.listResumes(),
        api.listAnalyses()
      ]);
      setJobs(jobList);
      setResumes(resumeList);
      setAnalyses(analysisList);
      if (!selectedJobId && jobList.length > 0) {
        setSelectedJobId(jobList[0].id);
      }
      if (!selectedResumeId && resumeList.length > 0) {
        setSelectedResumeId(resumeList[0].id);
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load analysis data");
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const latest = useMemo(() => analyses[0] ?? null, [analyses]);

  const onSubmit = async (event: FormEvent) => {
    event.preventDefault();
    if (!selectedJobId || !selectedResumeId) {
      setError("Select both a job and a resume");
      return;
    }

    setRunning(true);
    setError(null);
    try {
      const result = await api.createAnalysis({ jobId: selectedJobId, resumeId: selectedResumeId });
      setAnalyses((prev) => [result, ...prev.filter((item) => item.analysisId !== result.analysisId)]);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to run analysis");
    } finally {
      setRunning(false);
    }
  };

  return (
    <section className="page">
      <h2>Analysis</h2>
      <form className="panel" onSubmit={onSubmit}>
        <label>
          Job
          <select value={selectedJobId} onChange={(e) => setSelectedJobId(e.target.value)}>
            <option value="">Select a job</option>
            {jobs.map((job) => (
              <option key={job.id} value={job.id}>
                {job.title} - {job.company}
              </option>
            ))}
          </select>
        </label>
        <label>
          Resume
          <select value={selectedResumeId} onChange={(e) => setSelectedResumeId(e.target.value)}>
            <option value="">Select a resume</option>
            {resumes.map((resume) => (
              <option key={resume.id} value={resume.id}>
                {resume.name}
              </option>
            ))}
          </select>
        </label>
        <button type="submit" disabled={running}>
          {running ? "Running..." : "Run Analysis"}
        </button>
        <button type="button" onClick={() => void load()} disabled={running}>
          Refresh Data
        </button>
      </form>

      {error ? <p className="error">{error}</p> : null}

      <div className="panel">
        <h3>Latest Result</h3>
        {!latest ? <p>No analysis results yet.</p> : null}
        {latest ? (
          <div className="result-grid">
            <p>
              <strong>Status:</strong> {latest.status}
            </p>
            <p>
              <strong>Gap Analysis Mode:</strong> {latest.gapAnalysisMode}
            </p>
            <p>
              <strong>Used Gap LLM Fallback:</strong> {String(latest.usedGapLlmFallback)}
            </p>
            <p>
              <strong>Coverage:</strong> {latest.coverageScore}
            </p>
            <p>
              <strong>Groundedness:</strong> {latest.groundednessScore}
            </p>
            <p>
              <strong>Tokens:</strong> {latest.inputTokens} in / {latest.outputTokens} out
            </p>
            <p>
              <strong>Latency:</strong> {latest.latencyMs} ms
            </p>
            <pre>{prettyJson(latest.requiredSkillsJson)}</pre>
            <pre>{prettyJson(latest.missingRequiredJson)}</pre>
            <pre>{prettyJson(latest.missingPreferredJson)}</pre>
          </div>
        ) : null}
      </div>
    </section>
  );
}
