export type Job = {
  id: string;
  title: string;
  company: string;
  descriptionText: string | null;
  sourceUrl: string | null;
  createdAt: string;
  updatedAt: string;
};

export type Resume = {
  id: string;
  name: string;
  contentPreview: string | null;
  createdAt: string;
  updatedAt: string;
};

export type AnalysisResult = {
  analysisId: string;
  jobId: string;
  resumeId: string;
  status: string;
  gapAnalysisMode: string;
  usedGapLlmFallback: boolean;
  coverageScore: number;
  groundednessScore: number;
  requiredSkillsJson: string | null;
  missingRequiredJson: string | null;
  missingPreferredJson: string | null;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  createdAt: string;
};

export type AnalysisStatus = {
  id: string;
  status: string;
  createdAt: string;
  errorMessage?: string;
};
