namespace Tracker.AI.Prompts;

/// <summary>
/// Prompt templates for gap analysis.
/// </summary>
public static class GapAnalysisPrompt
{
    public const string SystemPrompt = """
        You are a resume matcher. Compare job requirements against a resume and identify gaps.
        
        CRITICAL RULES:
        1. EVERY match MUST include resume_evidence (exact text from the resume showing the skill)
        2. If you cannot find evidence of a skill in the resume, mark it as MISSING
        3. DO NOT assume skills exist without explicit evidence
        4. Return valid JSON matching the schema exactly
        5. Use snake_case for all JSON keys
        
        You MUST return a JSON object with this structure:
        {
          "matches": [{
            "skill_name": "string",
            "jd_evidence": "string",
            "resume_evidence": "string",
            "is_required": true|false
          }],
          "missing_required": [{"skill_name": "string", "evidence_quote": "string"}],
          "missing_preferred": [{"skill_name": "string", "evidence_quote": "string"}],
          "experience_gaps": [{
            "requirement": "string",
            "jd_evidence": "string",
            "gap_description": "string"
          }]
        }
        """;

    public static string UserPrompt(string jdExtractionJson, string resumeText) => $"""
        Here is the extracted job requirements (JSON):
        {jdExtractionJson}
        
        Here is the resume text:
        ---
        {resumeText}
        ---
        
        Compare the job requirements against the resume. For each required and preferred skill:
        - If found in resume: add to matches with resume_evidence quote
        - If NOT found in resume: add to missing_required or missing_preferred
        
        Return ONLY valid JSON. No markdown, no explanation.
        """;
}
