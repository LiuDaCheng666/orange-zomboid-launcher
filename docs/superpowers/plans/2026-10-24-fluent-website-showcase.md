# OZL Fluent Website Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development task-by-task, with read-only task and final review. The user waived repeated design/execution confirmations; execute headlessly.

**Goal:** Actually deliver the selected C-style website, a readable large app preview and visible finite motion rather than another spacing-only pass.

**Architecture:** Recompose the existing static three-page site. Keep current download validation and stable preview controls; refine the presentation/motion lifecycle without adding a framework.

**Tech Stack:** HTML/CSS/JS, existing Playwright/Node tests and screenshots, no new dependency.

## Global Constraints

- Canonical E:/Projects/OrangeLauncher, current branch redesign/fluent-website-showcase, no worktree. Source baseline acab135. User already selected C texture and background execution without repeated confirmations.
- Website only: no native/UI/game/Steam/server/config writes, no executable build/run/restore/repackage, release/deploy/CI/feed/security changes. VERSION stays 1.2.14; release.json unchanged.
- Preserve all source/QQ/registration/download destinations, five visible feature summaries, keyboard navigation, two preview themes, native-resolution enlarge/close/focus, no-JS content and reduced-motion/hidden lifecycle.
- Reuse existing icon and reviewed ten screenshots. No AI art, third-party code/assets, CDN or new dependency. No invented statistics, filler or defensive product copy.
- Use apply_patch; no unrelated cleanup. Tests/recording headless only; local preview http://127.0.0.1:3837 is already running and serves this website directory.

## Task 1: Recompose the Fluent product website with readable previews and finite motion

**Files:** Modify website/index.html, site.css, site.js; help.html/changes.html only for shared semantic alignment if necessary. Modify tests/website.test.cjs; create tests/website-showcase-capture.cjs for reproducible screenshots/8–12sec recording. Do not change release.json, ten launcher PNGs, dependencies or launcher code.

**Interfaces:** Existing .site-header, [role=tab], data-feature-image/title/description/points, theme select, data-enlarge/close and download metadata validation. Existing tests start a loopback server. New capture script accepts OZL_SITE_URL (defaults http://127.0.0.1:3837), outputs artifacts/website-showcase, uses existing playwright-runtime.cjs, no publication.

- [ ] Add actual-DOM regression before production edits: preview width at least1050px at1440 and1280, original8:5 aspect and no crop/stretch; viewport intersection at least360px on1440x900; five summaries visible and usable. Capture genuineRED against840px oldlayout, not a missing-selector exception.

```js
const r=await page.locator('[data-feature-image]').boundingBox();
assert.ok(r.width>=1050,'software preview must remain readable');
assert.ok(Math.abs(r.width/r.height-1.6)<.015,'show the complete screenshot proportionally');
```

- [ ] Recompose actual DOM: compact centered title/description/download controls, fullwidth large app showcase, five horizontally arranged feature choices with visible descriptions (wrap on mobile), no redundant small landscape hero/photo. Use a1240px maximum content area with at least1050px preview on1280. Shared cool light neutral Fluent palette, orangebrand, fine borders, bounded glass navigation/toolbars and coherent help/changes layouts. Consolidate CSS rather than overlay another fulltheme. Keep readable sizes and44px controls. Desktop home<=2700px.
- [ ] Add visible finite motion: first entrance450–650ms,16–24px translation and optional0.985→1scale with <=120msstagger; tab content300–420ms,16–24pxtranslation/opacity. Animate opacity/transform only. Keep selection/click immediate; cancel superseded effects/reduce/hidden/close/pagehide. Do not autoplay feature choices or add loops. Retain source/hashes/downloadvalidation unchanged.
- [ ] Add focused actual animation-state tests (keyframe distance/duration and intermediate changing pose), stale picture load/race/failed load handling if added, and lifecycle checks. Existing 180–460ms selected-panel duration range remains valid; do not weaken other tests to get green. New screenshot width criterion deliberately replaces old cramped presentation, not capability checks.
- [ ] Create capture script: all3pages at1440/768/390 plus1280home, selected10previewtheme/stateimages, and8–12second headless recording showingload, featureselection, theme and enlarge/close; report video path. Capture real nativebrowserUI rendering, not synthetic progress. Close owned headless browsers; do notstop preexistingpreviewserver.
- [ ] Run focused/new then full website tests, inspect fullresolution desktop/mobile and video/contactframes, recorddimensions/limits/logs, self-review and commit scoped source/test files. Fullreport task-1-report.md in this plan's ignored scratch; never force-add scratch. Return only status/commits/testsummary/concerns.

## Task 2: Main verification, independent review and source handoff

**Files:** docs/quality/2026-10-24-website-showcase.md and plan results; reviewed fixes only in production.

- [ ] Fresh website+license suite,52 existing launcher UI checks if website changes somehow touch launcher files (otherwise no need), exactpreviewasset hashes unchanged, no horizontaloverflow and image/motionstates at four widths. Inspect actualrecording (or timed rendered frames if playbacktool unavailable), not only assertion counts.
- [ ] Independent taskreview and wholebranchreview, scoped fixloop for concrete defects. Separate security audit observations from verified Defender cause; do not claim executableproblemfixed by website edits.
- [ ] Update local preview, show actual changed page/capture; merge/push source per existing authorization onlyaftergreen. No installation/deploy/updatefeed changes. Note live site vs local source clearly.

## Results

Task1 complete 8ee1d6b+0a4766e+a9c74ea; task review and final scoped re-review approved. Main final fresh website+license28total/27pass/0fail/1deploymentSKIP, exit0. See docs/quality/2026-10-24-website-showcase.md for actual geometry, recorded motion and limits. Local preview remains3837; live deployment not part of this change.
