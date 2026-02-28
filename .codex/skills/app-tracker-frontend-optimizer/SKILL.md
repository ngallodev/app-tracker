---
name: app-tracker-frontend-optimizer
description: Frontend execution checklist for high-signal, low-token UX updates in the app-tracker web UI.
---

## Use When
- Implementing or reviewing frontend UI/UX in `web/src/`.
- Adding forms, status feedback, and data-dense dashboards.

## Workflow
1. Keep component state deterministic and colocated with API calls.
2. Prefer visible status/error surfaces over hidden console-only failures.
3. Use compact explanatory copy for model metrics (`coverage`, `groundedness`, etc.).
4. Ensure mobile-safe responsive grids and clear section headings.
5. Add direct import actions for user artifacts (`.txt`, `.md`, drag/drop) when manual paste is repetitive.
6. Keep visual style cohesive with panel primitives, badges, and concise metric cards.

## Output Bar
- User can complete core flow without guessing what fields mean.
- Failures surface actionable message text.
- New controls do not require nested navigation to discover.

Maintainer: codex gpt-5
