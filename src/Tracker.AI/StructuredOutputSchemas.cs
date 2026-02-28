namespace Tracker.AI;

public sealed record StructuredOutputSchema(string Name, string Description, string SchemaJson);

public static class StructuredOutputSchemas
{
    public static StructuredOutputSchema? GetForType<T>() where T : class
    {
        var typeName = typeof(T).Name;
        return typeName switch
        {
            "JdExtraction" => new StructuredOutputSchema(
                "jd_extraction",
                "Structured extraction from a job description.",
                """
                {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "role_title": { "type": "string" },
                    "seniority_level": { "type": ["string", "null"] },
                    "required_skills": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "skill_name": { "type": "string" },
                          "evidence_quote": { "type": "string" },
                          "category": { "type": ["string", "null"] }
                        },
                        "required": ["skill_name", "evidence_quote", "category"]
                      }
                    },
                    "preferred_skills": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "skill_name": { "type": "string" },
                          "evidence_quote": { "type": "string" },
                          "category": { "type": ["string", "null"] }
                        },
                        "required": ["skill_name", "evidence_quote", "category"]
                      }
                    },
                    "responsibilities": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "description": { "type": "string" },
                          "evidence_quote": { "type": "string" }
                        },
                        "required": ["description", "evidence_quote"]
                      }
                    },
                    "years_experience": { "type": ["string", "null"] },
                    "keywords": {
                      "type": "array",
                      "items": { "type": "string" }
                    }
                  },
                  "required": [
                    "role_title",
                    "seniority_level",
                    "required_skills",
                    "preferred_skills",
                    "responsibilities",
                    "years_experience",
                    "keywords"
                  ]
                }
                """),
            "GapAnalysis" => new StructuredOutputSchema(
                "gap_analysis",
                "Gap analysis between job requirements and a resume.",
                """
                {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "matches": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "skill_name": { "type": "string" },
                          "jd_evidence": { "type": "string" },
                          "resume_evidence": { "type": "string" },
                          "is_required": { "type": "boolean" }
                        },
                        "required": ["skill_name", "jd_evidence", "resume_evidence", "is_required"]
                      }
                    },
                    "missing_required": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "skill_name": { "type": "string" },
                          "evidence_quote": { "type": "string" },
                          "category": { "type": ["string", "null"] }
                        },
                        "required": ["skill_name", "evidence_quote", "category"]
                      }
                    },
                    "missing_preferred": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "skill_name": { "type": "string" },
                          "evidence_quote": { "type": "string" },
                          "category": { "type": ["string", "null"] }
                        },
                        "required": ["skill_name", "evidence_quote", "category"]
                      }
                    },
                    "experience_gaps": {
                      "type": "array",
                      "items": {
                        "type": "object",
                        "additionalProperties": false,
                        "properties": {
                          "requirement": { "type": "string" },
                          "jd_evidence": { "type": "string" },
                          "gap_description": { "type": "string" }
                        },
                        "required": ["requirement", "jd_evidence", "gap_description"]
                      }
                    }
                  },
                  "required": [
                    "matches",
                    "missing_required",
                    "missing_preferred",
                    "experience_gaps"
                  ]
                }
                """),
            _ => null
        };
    }
}
