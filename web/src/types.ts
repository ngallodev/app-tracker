export type Job = {
  id: string;
  title: string;
  company: string;
  descriptionText: string | null;
  sourceUrl: string | null;
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

export type Resume = {
  id: string;
  name: string;
  contentPreview: string | null;
  desiredSalaryMin?: number | null;
  desiredSalaryMax?: number | null;
  salaryCurrency?: string | null;
  isTestData?: boolean;
  createdAt: string;
  updatedAt: string;
};

export type AnalysisResult = {
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
  salaryAlignmentScore?: number;
  salaryAlignmentNote?: string | null;
  requiredSkillsJson: string | null;
  missingRequiredJson: string | null;
  missingPreferredJson: string | null;
  inputTokens: number;
  outputTokens: number;
  latencyMs: number;
  provider?: string;
  executionMode?: string;
  isTestData?: boolean;
  createdAt: string;
};

export type AnalysisStatus = {
  id: string;
  status: string;
  createdAt: string;
  errorMessage?: string;
};

export type ProviderAvailability = {
  name: string;
  available: boolean;
  message: string;
};

export type AnalysisProvidersResponse = {
  defaultProvider: string;
  providers: ProviderAvailability[];
};
