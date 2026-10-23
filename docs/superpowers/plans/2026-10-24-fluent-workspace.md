# OZL Fluent Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development task-by-task, with a fresh implementer and a read-only spec/quality review per deliverable. All UI testing is headless.

**Goal:** Deliver a visibly redesigned Fluent-style OZL launcher and a restrained matching website refinement without changing any native functionality.

**Architecture:** Keep the WebView2 document/native bridge; reorganize its semantic navigation and existing home/workspace DOM. Consolidate superseded shell CSS rather than accumulating another contradictory theme. Independently refine the existing static website only after the new launcher screenshots are reviewed.

**Tech Stack:** Existing HTML/CSS/JS, Playwright fixture, Node tests; no new framework or runtime assets.

## Global Constraints

- Canonical checkout E:/Projects/OrangeLauncher; branch redesign/fluent-workspace; no extra worktree. User approved C texture with A management/B cover structure and waived repeated confirmation.
- External panel means OZL website orangezl.com; never change the 8790 server panel.
- No C# runtime/native contracts, real game/Steam/player/server/config mutations, process editing locks, installer/product builds, remote CI, release, deploy, update-feed or security-setting changes. VERSION stays 1.2.14; public stable13 unchanged.
- Preserve all11 routes, actual buttons/commands/payloads, default core3/optional expansions, drafts/focus/scroll and error/late-response behavior, both themes, OZL branding and original author/scene imagery. No new AI art, copied third-party UI code or CDN/runtime dependencies.
- Source edits use apply_patch; no unrelated cleanup. Keep tests/artifacts synthetic and headless. Use existing fixture/Playwright imports; never fall through a native test entry to UI.
- Six primary navigation controls remain accessible and use data-page; existing header-menu/header-tools, secondary-navigation, game-state, notice/task/progress/page and dialog IDs remain available.
- No filler/marketing/security disclaimers or fake progress. Semantic statuses retain text plus color. Do not hide necessary warnings/actions to meet geometry targets.
- Animate opacity/transform only with finite ownership and reduced-motion/hidden cancellation; no control waits, no animation restarts on same-route refresh, no unlimited effects/listeners.
- Browser baseline41 and existing motion6 remain green; update an old test only for its demonstrably obsolete layout assumption, preserving its functional assertions.
- Website existing static source only, no hosting migration; downloads/release.json/source/QQ/registration unchanged, home desktop<=2700px, no historical downloads. Export10 fresh screenshots after launcher review.

## Task 1: Fluent application shell and cover-first launch workspace

**Files:** Modify Orange3DTool/Web/index.html, app.css, app.js; create fluent-ui-tests.cjs and fluent-capture.cjs. Modify full-ui-tests.cjs only to include the new test module or adapt an obsolete structural assumption while retaining functional checks. No website asset exports in this task.

**Interfaces:** Consume existing refresh-fixture.cjs setup(browser,options), route(page,name), routes, chromium; existing render/enhance/arrangeHome/actualCover/launcherTools and native message events. Keep existing data attributes and orange.state/request contract. New capture utility defaults to artifacts/fluent-ui and exports website images ONLY with explicit --export-website. Test module exports {tests} and can run standalone like product-refresh-ui-tests.cjs.

- [x] Inspect current DOM, CSS blocks and 1440/768 baseline images. Read around arrangeHome and render lifecycle before moving existing nodes. Name the structural changes in report.
- [x] Add real DOM regression before production edits: primary nav does not overlap main and keeps all6controls inbounds; homepage cover spans most of content and launch controls sit below it; focused dirty editor remains intact on status refresh; resize and keyboard access retain settings/help/tools. Observe expected RED against old top-nav/side-by-side layout, not a missing helper exception.

Example geometry assertion after setup at1440x900 (existing selectors, no production test hooks):
```js
const boxes=await page.evaluate(()=>{const box=s=>document.querySelector(s).getBoundingClientRect().toJSON();return {nav:box('nav'),main:box('main'),cover:box('[data-home-cover]'),launch:box('.play-launch')};});
assert.ok(boxes.nav.right<=boxes.main.left+1,'navigation must not overlap the work area');
assert.ok(boxes.cover.width>=boxes.main.width*.9,'artwork occupies the main work area');
assert.ok(boxes.launch.top>=boxes.cover.bottom-1,'launch strip remains below artwork');
```

- [x] Move six main navigation buttons into a persistent left sidebar, add original simple SVGs with aria-hidden and accessible labels; preserve header utilities and actual status/choose-game entry. Move secondary navigation into main. Use208–224px desktop sidebar,64–76px compact rail below900; do not introduce fake titlebar/system buttons.
- [x] Rebuild arrangeHome/home presentation into full-width cover followed by launch strip, with configuration/target/button and expandable management/details. Preserve selectors/state, core next-step hints, target pickers, Java entry, recent targets, real config summary and original carousel lifecycle. Keep large original artwork, no crop distortion or duplicated controls.
- [x] Replace superseded top-navigation/left-launch-column CSS clusters with an organized Fluent shell section and coherent theme tokens. Keep unrelated feature rules until Task2; avoid piling another full set of overrides. Limit static blur to bounded chrome/floating surfaces with solid fallback, visible borders and contrast; no new media assets.
- [x] Add capture utility using existing fixture, all11routes/two themes/1440,1280,768x900 plus contact sheets and selected short-window states. Import original ZombieBuddy art through OZL_CAPTURE_BUDDY_ICON. Default must not write website/assets; --export-website writes only existing10names from1440 captures.
- [x] Run focused new tests while iterating; run npm test and motion-quality-ui-tests.cjs once after finishing. Inspect both themes home/components/mods at full resolution and compact rail/short-window utility menus. Self-review and commit scoped files. Full report task-1-report.md with actual RED/GREEN, screenshots, files/commits and concerns.

## Task 2: Dense Fluent management pages and unified dialogs

**Files:** Modify app.js and app.css; extend fluent-ui-tests.cjs and fluent-capture.cjs only for needed fixtures/captures. Test fixture additions belong in refresh-fixture.cjs or test-only helpers, never production methods. Do not export website images yet.

**Interfaces:** Consume reviewed Task1 shell and unchanged route/data-action/data-* handlers. Existing components(), mods(), catalog, serverPage(), rich saves/settings/memory/maintenance/help remain functionally equivalent. Use existing status objects; do not invent update availability or game-loading results.

- [x] Capture baseline after Task1 for all10 non-home routes; inspect actual relevant markup/state. Add behavioral/geometry tests first for dense mod rows, bounded core cards, original-image fitting, short viewport dialogs and existing selected actions dispatching unchanged payloads. Before implementing, run to genuine RED for the missing workspace behavior.

Example visible-row measurement against the normal eight-row synthetic catalog at1280x900 (not a source grep):
```js
const count=await page.locator('.catalog-row').evaluateAll(rows=>rows.filter(e=>{const r=e.getBoundingClientRect();return r.top>=0&&r.bottom<=innerHeight;}).length);
assert.ok(count>=6,'catalog must expose useful list density without scrolling');
```

- [x] Reorganize component card anatomy around72–88px contained author cover, title, version, semantic state and aligned action. Keep3independent core cards and expandable extras, source/version details and warnings. At1440x900 normal collapsed core cards should be<=250px high; narrow layouts wrap rather than shrinking text below12px.
- [x] Compress mod profile entry/toolbars; give list and detail clear hierarchy. At1280x900 show>=6complete normal catalog rows, with readable>=12px metadata and>=32px operation targets. Do not remove profile management, ordering, search/filter, selection, source evidence, dependency warnings or editor actions.
- [x] Refine saves/servers horizontal cards using bounded proportional thumbnails (no long empty image banners), readable metrics/actions, consistent focus/selection. Memory becomes allocation editor plus compact system metrics; settings/maintenance/help become organized sections, not oversized empty panels. Preserve all controls and their native payloads; no new edit locks.
- [x] Unify tasks/authorization/confirmation/server/profile/editor dialogs with legible surface, close/return and status. Existing close/pending-write semantics must survive; optional finite dialog motion must cancel on close/hidden/reduce and never gate interaction. Keep important path/hash/confirmation data available.
- [x] Run new tests, npm test and motion6. Generate current all-route captures with no website export; inspect contact sheets and full-resolution route controls, dialogs and density. Commit scoped changes; report task-2-report.md with RED/GREEN/results and visual proof.

## Task 3: Small website refinement and refreshed launcher previews

**Files:** website/index.html, help.html, changes.html, site.css, site.js only if behavior needs change; tests/website.test.cjs; existing10 website/assets/launcher-{home,components,mods,servers,maintenance}-{dark,light}.png. Never website/release.json.

**Interfaces:** Consume reviewed Task2 UI, fluent-capture.cjs --export-website and existing static site download/tab/theme/dialog lifecycle. User explicitly confirmed website, not server admin panel. Preserve all links, five visible summaries, three pages, no-JS and reduced-motion behavior.

- [x] Inspect existing three pages at1440/768/390. Refine only first-screen proportions, restrained depth/nav/preview framing and spacing to accompany Fluent app; no wholesale content rewrite, added sections or decorative filler.
- [x] Any new interaction must first have a failing actual-DOM test. If only presentation changes, extend responsive geometry/usable controls assertions before CSS edits; keep all existing website behavior tests.
- [x] Export10 current screenshots after Task2 review; verify exact capture hashes and inspect images. Use original icon path from Task1. Keep warm-white/orange visual identity and desktop height<=2700px; no asset/source/registration/contact/download changes.
- [x] Run node --test tests/website.test.cjs; live-only cache test may skip when no deployment. Capture3pages at1440/768/390 and inspect them. No publication, sync to server or binary build.
- [x] Commit only scoped source/test/10PNG files; report task-3-report.md with tests, measurements, captures/limits.

## Task 4: Final regression, comparison and source integration

**Files:** Add docs/quality/2026-10-24-fluent-workspace.md, mark plan results. No production edits except reviewed fixes for concrete new regression.

- [x] Run integrated npm test, motion6, standalone Fluent tests if not included in npm, website and license guide tests; retain raw logs under artifacts/fluent-* and actual skips.
- [x] Headless all11routes/two themes/1440,1280,768x900; check733x507,500x760,1100x650 controls and overlay states. Compare before/after home/components/mods to ensure structural rather than token-only change; report exact responsive limits rather than universal performance.
- [x] Record no native/runtime/binary/realgame/Steam/server mutation and no site deployment; existing earlier native matrix is historical not freshly rerun. Document which code was actually exercised.
- [ ] Obtain whole-branch read-only review; address concrete findings through one scoped fix wave and re-review; merge and sync source per existing authorization only after green. Keep original active game/installed app and public release untouched.
