# CLAUDE.md

## Working Style: Efficient Token-Saving Build Mode

Use this repository in a cost-efficient, deterministic-first way.

1. Default to deterministic processing before AI.
- Prefer scripts, rules, regex, and local matching for extraction/comparison tasks.
- Use AI only for ambiguous edge cases or evidence validation.

2. Keep context small.
- Read only the files needed for the current task.
- Use `rg`/targeted reads instead of loading large files end-to-end.
- Avoid repeating large summaries in chat.

3. Use low-cost execution patterns.
- Delegate discovery and narrow code search to subagents when available.
- Parallelize independent reads/checks.
- Minimize multi-pass prompt loops.

4. Reduce repeated AI calls in product code.
- Reuse cached outputs when input hashes match.
- Avoid sending full raw documents if preprocessed snippets are enough.

5. Prefer scriptable pipelines over model calls.
- Build preprocessors, fixture runners, and quality checks as local scripts.
- Keep evaluation mostly deterministic; reserve model eval for spot checks.

6. Keep outputs concise and implementation-first.
- Make the change, verify quickly, report only key results and next action.

7. Preserve this style in future work.
- Any new feature should include a note on how it controls token usage.
