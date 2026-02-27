import type { AnalysisResult, AnalysisStatus, Job, Resume } from "../types";

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {})
    },
    ...init
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed: ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export const api = {
  listJobs: () => request<Job[]>("/api/jobs/"),
  createJob: (payload: {
    title: string;
    company: string;
    descriptionText?: string;
    sourceUrl?: string;
  }) =>
    request<Job>("/api/jobs/", {
      method: "POST",
      body: JSON.stringify(payload)
    }),

  listResumes: () => request<Resume[]>("/api/resumes/"),
  createResume: (payload: { name: string; content: string }) =>
    request<Resume>("/api/resumes/", {
      method: "POST",
      body: JSON.stringify(payload)
    }),

  listAnalyses: () => request<AnalysisResult[]>("/api/analyses/"),
  createAnalysis: (payload: { jobId: string; resumeId: string }) =>
    request<AnalysisResult>("/api/analyses/", {
      method: "POST",
      body: JSON.stringify(payload)
    }),
  getAnalysisStatus: (id: string) => request<AnalysisStatus>(`/api/analyses/${id}/status`)
};
