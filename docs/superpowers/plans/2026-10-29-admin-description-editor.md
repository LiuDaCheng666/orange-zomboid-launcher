# Admin Description Editor Implementation Plan

**Goal:** Let administrators format server introductions visually and publish HTML that the launcher displays through the shared formatter.

**Architecture:** Extend the existing server form. `descriptionHtml` is optional (32768 characters); `extendedDescription` remains the 8000-character plain-text fallback. Advanced source retains the exact original string, including whitespace and unsupported markup. Only display is sanitized. An actual visual edit or visual paste saves the safe editor DOM. Opening or switching views never normalizes stored source. Copy the launcher's self-contained display formatter exactly and verify both against the same fixtures. No external editor dependency or new management page.

**Approved scope:** admin.html, admin.js, admin.css, ui-tests.cjs. Native/server persistence and CSP are owned by the native agent. No online credentials, production service or user server record is accessed. No commit or publication.

- [x] Add a failing real HTTP/DOM test for editor controls, legacy no-op behavior and rich save/reload/publish.
- [x] Add visual paragraph/heading/font size, emphasis, text/background color, bordered callout, list/alignment/link and table controls. Advanced HTML source has an immediate sanitized preview. Keep legacy text intact until rich content changes.
- [x] Mirror `cleanServerDescriptionHtml` and the agreed tag/style/URL whitelist; intercept HTML paste/drop and sanitize source preview. Preserve advanced source on save. Generate bounded plain fallback only following rich edits.
- [x] Run shared format fixtures, actual local service persistence/reload/static publication, hostile paste/source tests, toolbar interactions and responsive screenshots. Validate 32768-character Chinese source through HTTP; reject an oversized draft without writing, retain it and retry after correction.
- [x] Inspect screenshots and hand the admin UI to the launcher UI agent for independent review. Separately review the launcher's carousel and polling changes, which are not owned by this implementation.

**Evidence:** `artifacts/recommendations-browser/` contains actual headless Edge screenshots of the local HTTP admin, including the light/dark visual editor, 390px layout and the real published feed rendered by the production launcher DOM. `node deploy/recommendations/ui-tests.cjs` also exercises the existing login, draft/publish/export-failure retry, order, unpublish, delete and logout flow. The private store and credentials exist only in a temporary directory, removed by the runner. Outbound requests are blocked. These checks do not exercise a remote deployment, actual game or a WebView native bridge.
