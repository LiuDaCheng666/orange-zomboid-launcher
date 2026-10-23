# OZL UI Motion and Quality Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development task-by-task; each implementation receives a focused requirements brief, behavioral tests and a read-only task review. All visual preview stays headless/background.

**Goal:** Improve the existing OZL launcher and website's visual hierarchy and smooth interactions while preserving every feature and verifying the full isolated functional matrix.

**Architecture:** Keep the existing native boundary and static WebView2 UI. Refine tokens and established selectors, and use a bounded presentation-only animation lifecycle around actual route changes and dialogs. The website remains the existing static Aliyun-hosted source with no new hosting or runtime dependencies.

**Tech Stack:** Existing C#/.NET Framework tests, plain HTML/CSS/JS, Node built-in tests and Playwright, Python deployment/Lua tests.

## Global Constraints

- Work only in E:/Projects/OrangeLauncher on polish/ui-motion-quality. No additional worktree.
- No release, installer build, remote CI, website deployment, update-feed change, security exclusion or quarantine restore. VERSION stays 1.2.14 and public stable stays 1.2.13.
- No real game or foreground windows; no real player/server changes, Steam subscriptions, workshop deletion or user file cleanup. Destructive functional tests use isolated fixtures only.
- Keep OZL branding, original covers and author images; no generated/replacement art, PCL code/assets or new runtime dependencies.
- Preserve all 11 routes, existing native request/payload contracts, dirty-draft/error behavior and no process-state editing locks.
- Keep both themes, state labels/colors and the existing compact cards/list+detail layout. No filler, claims of game effects, public demo/security disclaimers or fake progress.
- Animate opacity/transform with bounded lifecycles. Polling must not replay route entrances or steal focus. Rapid navigation must not accumulate active animations. Reduced motion must suppress non-essential animations; hidden tabs must not run new decorative loops.
- Website uses current launcher screenshots from Task 1; no stale screenshot substitution, historical download expansion, paid service, analytics or hosting migration. Desktop homepage height <=2700px.
- Use apply_patch for source edits. Never commit unrelated changes or test artifacts. Main agent owns aggregate records and final integration.

## Task 1: Launcher visual hierarchy and smooth presentation lifecycle

**Files:** Modify Orange3DTool/Web/app.css and Orange3DTool/Web/app.js; add Orange3DTool/Web/motion-quality-ui-tests.cjs; extend refresh-fixture.cjs only for explicit test inputs if necessary. Existing full-ui-tests.cjs consumers must continue to run unchanged. A dedicated capture utility may write ignored screenshots, never production data.

**Consumes:** Existing state.route, render(), enhanceShell(), sceneMotion media query, native events and the actual refresh-fixture setup/route helpers. Read their actual implementation before choosing hooks.

**Produces:** Polished existing UI and testable user-visible animation behavior; no new native methods or data schema. Make helper functions local unless they belong to the existing exported orange interface.

- [ ] Inspect fresh headless baseline screenshots for home/components/mods/saves/servers/settings/memory/maintenance/help in both themes. Name specific spacing, hierarchy or animation problems before changing them.
- [ ] Add actual DOM tests for (a) route entrance not restarting on same-route refresh; (b) rapid route switches canceling old finite presentation animations and final route interactive; (c) focused dirty inputs retaining value/focus during non-route updates; (d) reduced-motion having no active decorative entrance/dialog animations; (e) semantic statuses, readable button targets and no horizontal overflow at 768×900 and 1100×650. Assert outcomes/active animations, not source-text matches or a specific private helper name. Run the focused test and record the expected missing-behavior failure.
- [ ] Refine coherent typography/surfaces/spacing across every route and both themes. Preserve large homepage image, compact cards and original cover proportions. Treat overlay/dialog styling consistently; do not hide functionality to improve screenshots.
- [ ] Implement a single bounded presentation animation lifecycle at actual navigation. Example contract, not required public API: cancel previous entrance before animating the new page, skip animation for same-route renders or reduce media preference; guard stale finish callbacks against newer route ownership. Do not defer or disable working controls for animation.
- [ ] If improving cover or dialog transitions, verify pause, hidden-tab and close/disconnect behavior; do not add constant pointer/scroll listeners, new animated gradients or unrestricted view-transition snapshots of the entire app.
- [ ] Run the focused tests, npm test and existing product-refresh visual capture. Inspect contact sheets and individual full-resolution views; correct clipping/overlap/contrast. Keep final screenshot export for Task 2 until after task review.
- [ ] Commit only launcher/test changes and write the full task report including RED/GREEN commands/results, modified files, screenshots inspected and remaining observations.

## Task 2: Website three-page visual refinement using fresh launcher screenshots

**Files:** Modify website/index.html, website/help.html, website/changes.html, website/site.css, website/site.js, tests/website.test.cjs; regenerate only website/assets/launcher-{home,components,mods,servers,maintenance}-{dark,light}.png after Task 1. Do not change website/release.json or immutable download metadata.

**Consumes:** Reviewed Task 1 UI and existing product-refresh-capture.cjs exports; existing download metadata and five-feature keyboard/tab/dialog behavior. Website remains local-only in this round.

**Produces:** Coherent warm-white/orange compact public website and current screenshots, with simple smooth navigation/section and overlay transitions that retain keyboard support, no-script and reduced-motion behavior.

- [ ] Inspect local three-page screenshots at desktop/mobile. Keep actual functional explanations and existing real download, source, registration and QQ destinations; remove no feature merely for aesthetic simplicity.
- [ ] Add a behavioral regression covering any new navigation/dialog/motion lifecycle behavior before implementing it. Examples: closing an image overlay during transition leaves focus restored and no backdrop; rapid tab selection preserves final selected content; reducing motion while an effect is active stops it without hiding content. Use real DOM, not a mock animation test.
- [ ] Refine type scale, whitespace, section transitions, visual depth, cards, navigation and responsive help/history content. Preserve homepage desktop <=2700px and no historical download links. Keep modest motion, no autoplay video or heavy framework.
- [ ] Export all ten screenshots from reviewed current launcher UI, inspect each at full resolution and reference only those correct local asset names. No new AI imagery. Preserve original artwork.
- [ ] Run node --test tests/website.test.cjs (local live-cache case may legitimately skip), new focused checks and no-JS/reduced-motion/keyboard cases. Check 1440/768/390 widths on all three pages, no overflow or page errors. Record headless visual evidence in ignored artifacts.
- [ ] Commit scoped website changes and report behavior, RED/GREEN, screenshots, tests and exact skipped live-only checks. Do not deploy or label 1.2.14 as released.

## Task 3: Aggregate functional matrix, final visual QA and review

**Files:** Add docs/quality/2026-10-23-ui-motion-regression.md; mark this plan's verified results. Modify production only for a reproduced task-related regression with its own failing test and scoped review.

**Consumes:** Native baseline audit, Task 1/2 reports, existing full native/self-test, separate core-service/transaction tests, Lua bridge tests and deployment tool tests.

- [ ] Run new native test entry only after scanning the exact test executable; never use quarantined production EXEs. Run --self-test with isolated test data, core update service/transaction suites, Lua tests and Python deployment suites; preserve exit status, raw logs and actual assertion counts. Inspect test entry behavior before running it.
- [ ] Run full Web UI, new motion tests, website and license-guide tests once on the integrated branch; do not claim skipped live/Linux checks passed.
- [ ] Run headless capture across both themes/all 11 routes at 1440/1280/768×900, plus 1100×650 where controls must fit. Inspect contact sheets and representative full-resolution controls, mod lists, authorization, tasks and server dialogs. Capture website all three pages at 1440/768/390 and final screen previews.
- [ ] Record each feature group's fixture result and any real-world tests not performed (actual Steam downloads, live game/server entry, actual quarantined installer). Report performance measurements as observed values, not universal guarantees. Include source revision and no-release/no-deploy status.
- [ ] Obtain one whole-branch read-only review, address concrete blockers with focused tests, then run covering checks. Merge and push verified source per existing user authorization without changing public releases or live site.
