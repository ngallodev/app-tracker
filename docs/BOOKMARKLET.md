# Bookmarklet: JD Capture Preprocessor

This bookmarklet captures job data from a job posting page and copies a cleaned JSON payload to your clipboard.

It is deterministic and local: no AI call, no server roundtrip.

## 1. Create the bookmark
- Open `docs/bookmarklet_jd_capture.js`.
- Collapse it to one line (for example: `tr -d '\n' < docs/bookmarklet_jd_capture.js`).
- Copy the `javascript:(...)` string.
- Create a new browser bookmark and paste it as the URL.

## 2. Use it on a job page
- Open a job posting in your browser.
- Click the bookmark.
- It will extract:
  - `title`
  - `company`
  - `descriptionText` (cleaned text)
  - `sourceUrl`
- JSON is copied to clipboard (or shown in a prompt fallback).

## 3. Send to API
Use the copied JSON with:

```bash
curl -X POST http://localhost:5278/api/jobs/ \
  -H "Content-Type: application/json" \
  -d '<PASTE_JSON_HERE>'
```

## Why this helps token cost
- Removes page chrome/noise before analysis.
- Reduces prompt size and repeated cleanup effort.
- Improves cache hit stability by normalizing source content earlier.

---

*Document created by: opencode (kimi-k2.5-free)*
