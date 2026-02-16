# Memory: Cost Efficiency Playbook

## Principle
- Treat AI as escalation, not the default path.
- Do deterministic processing first, then call LLM only on unresolved/ambiguous items.

## Deterministic-first pipeline (MVP-safe)
1. Preprocess JD/resume text (normalize whitespace, remove boilerplate, segment bullets/sentences).
2. Extract candidate skills with regex + curated dictionary.
3. Canonicalize skill names with local synonym map (`k8s -> kubernetes`, `js -> javascript`).
4. Perform exact/fuzzy string matching in code for coverage scoring.
5. Call AI only for:
   - Ambiguous matches
   - Evidence quote verification
   - Optional explanatory text for UI

## Browser bookmarklet (high leverage)
- Capture visible JD title/company/body from job board DOM.
- Strip nav/footer/recommended-job noise before sending text to app.
- Package a compact JSON payload for `/api/jobs`.
- Optional: pre-highlight likely required/preferred bullets in-page for user confirmation.

## About fetching synonyms from Google in React
- Not recommended for this MVP:
  - Google Programmable Search API is search-result oriented, not a lexical synonym service.
  - Exposing keys in frontend is risky; server proxy adds cost/latency/quotas.
- Better approach:
  - Local synonym dictionary JSON (hand-curated, domain-specific).
  - Optional server-side enrichment source with caching if needed later.

## 1-week practical split
- Day A: implement deterministic parser + synonym map + hash cache lookup.
- Day B: add bookmarklet + job import endpoint integration.
- Day C: wire AI fallback path only for unresolved skills.
- Day D: build lightweight eval script against fixtures (mostly no AI calls).
- Day E: UI polish + deployment path.
