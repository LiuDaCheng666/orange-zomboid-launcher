# OZL C Glass Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver a visibly glass-layered, smoothly animated C-style launcher first, then refresh its public website from the approved real application screenshots.

**Architecture:** Keep the existing WebView2 UI and native protocol. Restrict application production edits to its existing HTML/CSS/JS resources and presentation helpers; keep all native behaviors intact. A second task consumes approved captures to update and deploy the existing static SSH-hosted website.

**Tech Stack:** Existing HTML/CSS, Web Animations API, existing Node/Playwright headless fixture, Python deployment helpers and Caddy. No new runtime dependencies.

## Global Constraints

- Work only in E:/Projects/OrangeLauncher on redesign/c-glass-motion; no additional worktree and no work on main during implementation.
- Preserve all native C# code, message names/payloads, 11 routes, two themes, data actions, VERSION 1.2.14, update/install/uninstall protocols and existing copy. No new operation restrictions.
- No product EXE execution/restoration/build/repack, no CI or binary/feed publication, no Defender setting changes; background headless UI only, never foreground/game/Steam/server tests.
- Preserve original home and author covers. No new AI imagery, CDNs, animation libraries, fonts or runtime resource files.
- Clear actual glass layers with readable text, visible finite interaction motion, reduced-motion/hidden/rapid-navigation cancellation, no infinite decor or pointer following.
- Keep six complete readable catalog rows at 1280×900 and usable controls/dialogs at 733×507 and 500×760; no overflow.
- Task 2 begins only after Task 1 spec, quality and controller visual acceptance. Export ten screenshots from accepted Task 1 source, not an old snapshot.
- Website keeps three pages, five concrete categories, QQ/source/registration and original download behavior; preserve live release.json, both signed latest metadata files and all 22 v1.2.13 immutable artifacts byte-for-byte. No DNS/certificate/security/paid-resource changes.
- User explicitly waived further design/execution confirmations. Follow this approved scope continuously; ask only for genuinely missing authority or irreconcilable requirements.

## Task 1: Glass material system and fluid application presentation

**Files:**
- Modify: Orange3DTool/Web/app.css, Orange3DTool/Web/app.js; Orange3DTool/Web/index.html only if decorative presentation hooks are needed.
- Create: Orange3DTool/Web/c-glass-ui-tests.cjs, Orange3DTool/Web/c-glass-capture.cjs.
- Existing tests: Orange3DTool/Web/full-ui-tests.cjs, motion-quality-ui-tests.cjs, fluent-ui-tests.cjs.
- Evidence: artifacts/c-glass-ui/; no writes to website/assets yet.

**Interfaces:**
- Consume existing refresh-fixture.cjs setup(browser, options), route(page, name), routes, chromium. These use actual application DOM and a synthetic native boundary, never real game state.
- Preserve existing render/navigation/animation lifecycle and native calls. New helpers can own presentation elements, cancellation and alignment only.
- Produce settled five-page/two-theme 1440×900 captures named {theme}-{home,components,mods,servers,maintenance}-1440.png plus screenshot matrix and a verified software-motion.webm; Task 2 copies these exact bytes only after acceptance.

- [ ] Save baseline screenshots of light/dark home, components, mods, one open dialog to artifacts/c-glass-ui/before before changing production.
- [ ] Write failing tests naming actual presentation failures. Example real rendering checks:
  ```js
  const layer = await page.locator('.core-components .component-card').first().evaluate(el => {
    const s=getComputedStyle(el);
    return {background:s.backgroundColor,edge:s.boxShadow,rect:el.getBoundingClientRect().toJSON()};
  });
  // Assert a translucent material and rendered edge/depth without relying on a CSS class string.
  assert.ok(layer.background.startsWith('rgba('), 'core surface is still opaque');
  ```
  For motion, navigate in the real DOM, pause the application-owned effect at 35% and 70%, and assert distinct intermediate transforms/bounds and an immediately usable target control. Test nav indicator geometry after resize and rapid navigation, cancellation on hidden/reduced motion, and no re-entry on same-route refresh. Derive expected target alignment from the selected button's measured bounds, not production helper output.
- [ ] Run node Orange3DTool/Web/c-glass-ui-tests.cjs before implementation. Record expected assertion failures, not missing-selector exceptions. Retain raw RED output.
- [ ] Replace the current opaque/flat visual sections with a coherent light/dark glass material system: layered neutral ambient background; translucent navigation and command surfaces; luminous inner/outer edges; bounded specular/lens gradients; separate crisp reading surfaces; consistent component cards and dialogs. Preserve dense layouts and image proportions; avoid stacking another unstructured set of duplicate overrides.
- [ ] Implement finite fluid navigation indicator, 320–420ms route displacement/opacity/scale, 150–260ms press/hover/panel presentation using existing resources and animation lifecycle. Maintain at most one active route transition, cancel obsolete effects, align on resize and keep all interactions immediate. Do not change data/state behavior.
- [ ] Run focused GREEN and existing motion/fluent checks while iterating. Then run node Orange3DTool/Web/full-ui-tests.cjs and motion-quality-ui-tests.cjs once on final task code; record exact counts/output. Any intentional visual decision can update an obsolete layout assertion, but no weakening functional assertions.
- [ ] Implement capture tool that runs only headless fixture. Save two-theme route matrix, responsive/long-dialog examples and 12–18s actual navigation/component/modal/theme video. Decode at least two intermediate frames at distinct real video times, verify non-identical frames and duration, and inspect screenshots. Do not export website assets or deploy.
- [ ] Commit only app resources, focused tests and capture source. Write report with exact RED/GREEN commands, screenshots/video, changed files and concerns. Controller performs independent spec/quality review and visual inspection before Task 2.

## Task 2: Matched glass website, new actual screenshots and online deployment

**Files:**
- Modify: website/site.css, website/site.js, website/index.html and only shared framing in website/help.html / changes.html if necessary.
- Replace: website/assets/launcher-{home,components,mods,servers,maintenance}-{dark,light}.png from accepted Task 1 artifacts, exact bytes.
- Modify tests/website.test.cjs, tests/website-showcase-capture.cjs when needed for actual QA.
- Evidence: artifacts/c-glass-site/; use existing deploy/deploy_site.py and deploy/prepare_release.py without credential/source changes unless a concrete deployment bug is observed.

**Interfaces:**
- Consume accepted Task 1 capture directory and its committed app source SHA, supplied by controller. Verify ten output hashes before copying.
- Keep existing real feature selection, screenshot failure/stale response handling, enlargement, metadata verification and official fallback. Only presentation and screenshot cache version change.
- Produce website current online URL https://orangezl.com/ with same metadata/artifact hashes, four viewport screenshots and actual motion WebM.

- [ ] Verify Task 1 acceptance and copy exact ten captures to matching website asset names. Example:
  ```js
  for (const theme of ['dark','light']) for (const page of ['home','components','mods','servers','maintenance']) {
    fs.copyFileSync(path.join(accepted,`${theme}-${page}-1440.png`),path.join(assets,`launcher-${page}-${theme}.png`));
  }
  ```
  Record both sides' SHA-256; do not fabricate screenshot text or repaint source images.
- [ ] Add failing actual browser tests for the new screenshot URL version and fluid selection/preview presentation, cancellation and new glass framing. Initial and dynamic URLs must use v=20261024-c-glass. Preserve actual failure/retry and stale-race tests.
- [ ] Implement matching glass navigation/feature selector/preview window/dialog surfaces with visibly layered edges and short finite transitions. Keep five introductions visible and large complete 8:5 screenshot; desktop 1440/1280 screenshot width at least 1050px; no horizontal overflow at 1440/1280/768/390. Preserve existing no-JS content and all functional copy/links.
- [ ] Run RED then focused GREEN, full node --test tests/website.test.cjs and Python deployment unit suites. Capture and inspect four widths and five pages/two themes locally; actual motion recording 8–12s must visibly include feature selection, light/dark preview and enlargement.
- [ ] Commit website assets/source/tests after local QA; report ready for controller independent task review. Do not activate online until controller's spec/quality/visual approval.
- [ ] After approval, use existing SSH deployment, fixed host key and in-memory credential consumption; never print the handover document, secrets or key contents. Preserve live metadata/config/immutable attachment identity before/after. Atomically activate and retain prior site for rollback; no unrelated infrastructure changes.
- [ ] Verify actual HTTPS site and ten versioned image response hashes match approved bytes. Run OZL_SITE_URL=https://orangezl.com node --test tests/website.test.cjs (all online assertions, no deployment skip), capture real online screenshots and motion recording. Report activation target and protected metadata/artifact identity, exact tests, source commit and actual media paths.

## Final validation and handoff

- [ ] Controller reads both reports/diffs, views software and actual online screenshots and decoded video frames, and verifies meaningful visual change beyond palette/radius.
- [ ] Whole-branch independent review, one bounded fix wave if required, then fresh app UI/motion and online website tests.
- [ ] Follow existing user preference: local merge to main and source push after checks. No product release. Preserve ignored evidence; do not attempt previously blocked cleanup.
- [ ] Deliver actual software screenshot(s), motion video and online website link; clearly distinguish UI preview from installed/public binaries. Record remaining limits honestly without adding defensive public UI text.
