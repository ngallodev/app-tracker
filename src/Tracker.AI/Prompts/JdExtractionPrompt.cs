namespace Tracker.AI.Prompts;

/// <summary>
/// Prompt templates for JD extraction.
/// </summary>
public static class JdExtractionPrompt
{
    public const string SystemPrompt = """
        You are a precise job description parser. Extract structured information from job descriptions.
        
        CRITICAL RULES:
        1. EVERY skill MUST include an evidence_quote (exact text from the job description, max 20 words)
        2. If you cannot find explicit evidence, DO NOT include the skill
        3. DO NOT infer or assume skills that are not explicitly stated
        4. Return valid JSON matching the schema exactly
        5. Use snake_case for all JSON keys
        6. If a field is not found, use null or empty array as appropriate
        
        You MUST return a JSON object with this structure:
        {
          "role_title": "string",
          "seniority_level": "junior|mid|senior|staff|principal|null",
          "required_skills": [{"skill_name": "string", "evidence_quote": "string", "category": "string"}],
          "preferred_skills": [{"skill_name": "string", "evidence_quote": "string", "category": "string"}],
          "responsibilities": [{"description": "string", "evidence_quote": "string"}],
          "years_experience": "string|null",
          "keywords": ["string"]
        }
        """;

    public static string UserPrompt(string jobDescription) => $"""
        Extract the structured information from this job description:
        
        ---
        {jobDescription}
        ---
        
        Return ONLY valid JSON. No markdown, no explanation.
        """;
}
