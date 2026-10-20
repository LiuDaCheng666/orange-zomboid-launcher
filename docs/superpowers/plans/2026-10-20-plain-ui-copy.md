# OZL direct interface copy implementation plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Work in the existing checkout on fix/plain-ui-copy, as requested by the user.

**Goal:** Remove repetitive defensive text across OZL while preserving accurate state and all existing functionality.

**Architecture:** Edit display string literals in existing presentation/controller/help files. No output sanitizer, schema changes or functional refactoring. Keep installed/configured/currently-loaded branches distinct.

**Tech Stack:** C# .NET Framework 4.8, WebView2, JavaScript, headless Edge/Playwright, PowerShell release tooling.

## Global constraints

- Version 1.2.13; existing update trust root and 18-file ownership list unchanged.
- No foreground game tests, game/server writes or new operation restrictions.
- Actual errors, permission information, destructive confirmations and license terms remain available.

### Task 1: State and page regression coverage

- [x] Extend StatusTests to require configured-but-not-loaded to say 下次启动启用, loading-confirmed to say 已加载, missing loader to remain an error.
- [x] Add Web/plain-copy-ui-tests.cjs using the existing experience fixture and real DOM: cover values remain, no disclaimer footer; selected profile says 待应用; connection request does not claim entry; permission/error/delete dialogs remain actionable.
- [x] Run the tests against 1.2.12 and record expected failures before edits.

### Task 2: Copy audit and edits

- [x] Audit Web/app.js and C# presentation/controller/help files for 不代表、不等于、待验证、不保证、需实测 and contextual equivalents.
- [x] Rewrite each normal-state explanation in place; remove the homepage footer. Keep exact requirements and actual error details, using concise cause/action text.
- [x] Update native fallback help to its actual checkbox/buttons/tabs; keep modern help aligned with the component-card/updater UI; retain copyright chapter.
- [x] Run new tests, existing UI suites and the complete native self-test. Adjust old text assertions without removing their behavioral coverage.

### Task 3: Review and release

- [x] Inspect dark/light homepage and component/help screenshots headlessly, check diff for display-only changes, request read-only review.
- [x] Update VERSION and CHANGELOG, build signed release with Build-CoreRelease.ps1, validate manifest, ZIP extraction and packed installer.
- [x] User explicitly authorized full publication without Microsoft review. Build installer in Windows CI from the frozen tag and signed core; publish all 22 assets, compare public hashes, and test the previous launcher default update channel through Ready.
- [x] Record verified results and pending publication accurately in docs/releases/1.2.13-validation.md.
