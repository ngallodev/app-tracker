javascript:(() => {
  const textOf = (selectors) => {
    for (const selector of selectors) {
      const el = document.querySelector(selector);
      if (el && el.textContent) {
        const text = el.textContent.trim();
        if (text) return text;
      }
    }
    return "";
  };

  const normalize = (text) =>
    text
      .replace(/\u00a0/g, " ")
      .replace(/\s+/g, " ")
      .trim();

  const pickDescription = () => {
    const candidates = [
      "[data-testid*='job-description']",
      "[class*='job-description']",
      "[class*='description']",
      "[id*='job-description']",
      "main article",
      "main",
    ];

    let best = null;
    let bestLength = 0;
    for (const selector of candidates) {
      for (const el of document.querySelectorAll(selector)) {
        const clone = el.cloneNode(true);
        clone.querySelectorAll("script,style,noscript,button,svg,nav,footer").forEach((n) => n.remove());
        const text = normalize(clone.textContent || "");
        if (text.length > bestLength) {
          bestLength = text.length;
          best = text;
        }
      }
    }
    return best || "";
  };

  const title = normalize(
    textOf([
      "h1",
      "[data-testid*='job-title']",
      "[class*='job-title']",
      "[class*='topcard__title']",
      "meta[property='og:title']",
    ])
  );

  const company = normalize(
    textOf([
      "[data-testid*='company']",
      "[class*='company']",
      "[class*='topcard__org-name-link']",
      "[class*='topcard__flavor']",
    ])
  );

  const descriptionText = pickDescription();
  const sourceUrl = location.href;

  const payload = {
    title: title || "Unknown Title",
    company: company || "Unknown Company",
    descriptionText,
    sourceUrl,
  };

  const message = `Captured job payload:
- title: ${payload.title}
- company: ${payload.company}
- description chars: ${payload.descriptionText.length}

JSON copied to clipboard.`;

  const json = JSON.stringify(payload, null, 2);
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard
      .writeText(json)
      .then(() => alert(message))
      .catch(() => prompt("Copy this JSON to POST /api/jobs", json));
  } else {
    prompt("Copy this JSON to POST /api/jobs", json);
  }
})();
