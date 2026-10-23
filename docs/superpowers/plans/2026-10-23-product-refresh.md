# OZL Product Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a shorter truthful product site, three independent core component controls, responsive cached lists, and a quiet startup update notice.

**Architecture:** Keep native C# file operations authoritative, add bounded display snapshots without weakening actual writes, and keep the existing WebView and signed update transport. Change the website independently as a static current-version download site with feature tabs and separate help/changes pages. Preserve old combined `base` contracts while exposing the new core component IDs.

**Tech Stack:** .NET Framework C#, WebView2 HTML/CSS/JavaScript, static HTML/CSS/JavaScript site, Playwright headless, Python deployment tooling.

## Global Constraints

- Work only in `E:/Projects/OrangeLauncher`, existing `feature/self-hosted-updates` branch; preserve unrelated files and existing tests.
- All tests run in the background. Do not open a game, take foreground, modify real player/game/server configurations, or invoke live mod downloads.
- Do not add game/server-running configuration guards. Preserve current features and both dark/light-blue themes.
- Core IDs are `buddy`, `viewpoint`, `chinese`; legacy `base` remains compatible. Extra models/shoot/Fix options remain optional.
- Chinese depends on Buddy, not on Viewpoint. Viewpoint depends on Buddy. Individual Viewpoint removal leaves Buddy installed.
- Startup check only notifies; never automatically downloads, installs, or restarts. Reuse own HTTPS signed source and GitHub fallback.
- Use actual sanitized UI screenshots and existing original assets, never AI-generated component covers or invented functionality.
- Website offers current downloads only. Do not delete immutable release assets or change latest version pointers in these tasks.
- Do not publish candidate executables, bypass antivirus, retry Microsoft CAPTCHA, submit identity information, or commit credentials/private data.

## Task 1: Bounded catalog, cover and component display snapshots

**Files:** modify `Orange3DTool/SaveProfiles.cs`, `ComponentEvidence.cs`, `ModernController.cs`; create focused cache types/tests under `Orange3DTool/`; integrate tests through existing native test entry. Do not change component semantics or web layout in this task.

**Interfaces:** keep `SaveProfiles.Catalog(Engine)`, `Cover(Engine,string)` responses intact. Keep fresh `Entries` and `Fingerprint` for writes/validation. Add an internal invalidation entry point usable by controller mutations and game switches. Cache keys include canonical game, user, data and selected source identity.

- [ ] Write a failing native fixture test that calls real Catalog and Cover repeatedly against temporary workshop metadata, then changes `mod.info`/poster/adds/removes a mod and checks actual new outputs. Verify one shared discovery pass via an internal injectable discovery boundary or cache snapshot identity, not timing-only assertions. Test switching engines, concurrent reads, bounded entries, and invalidation after controller operations.

```csharp
var first = SaveProfiles.Catalog(engine);
// Fixture owns real metadata and covers; the same visible catalog is reused.
File.WriteAllText(infoPath, "id=fixture\nname=Changed\n");
// After change notification or explicit refresh, returned title must be Changed.
```

- [ ] Run the focused fixture through `Build-Tests.ps1 -Entry <new test entry> -Output artifacts/catalog-cache-tests.exe`, execute it and record the expected failure before production changes.
- [ ] Implement a bounded, synchronized display cache. Catalog and individual covers share one source index. External changes, invalidation and bounded fallback age retire snapshots. Covers still validate and read their resolved file, and actual install/launch validation stays fresh. Cache component display inventory/digests without moving authoritative Record into stale display data. Dispose/limit watchers and snapshots; never let a disposed watcher install a stale replacement.
- [ ] Re-run focused tests and full native self-tests; record cold/hot read-only times against the actual 791-entry workshop without editing game files. Commit this task and write report with RED/GREEN output, scope and measurements.

## Task 2: Three true core components and upstream audit

**Files:** modify native `InstallWorkflow.cs`, `Program.cs`, `ChinesePatch.cs`, `ModernController.cs`, `ComponentEvidence.cs`, `ModernHostPolicy.cs` as required; add a focused `CoreComponents.cs` and native regression tests; add `docs/components/2026-10-23-upstream-audit.md`. Do not change Web files yet.

**Interfaces:** status exposes `buddy`, `viewpoint`, `chinese` cards in addition to legacy compatibility where needed; existing component action `{id,op,confirmed}` accepts the new IDs. Legacy `base` and existing play-profile formats retain combined behavior. The three cards expose existing title/item/version/configured/update fields. Buddy version is runtime JAR manifest, not installer tag. Task 3 consumes these cards.

- [ ] Write native controller/engine fixture tests: Buddy-only installation does not enable Viewpoint; Chinese with Buddy alone succeeds; Viewpoint auto-fetches only missing Buddy/Viewpoint sources; Viewpoint uninstall retains Buddy native files and launch args; Buddy uninstall leaves downloaded optional files intact; legacy base/profiles and restore-all still work. All fixtures and fake fetches must be isolated.

```csharp
// Test consumer-observable state, not source strings.
controller.Execute("component", new Dictionary<string,object>{{"id","buddy"},{"op","install"},{"confirmed",true}});
// Expect framework configured, Viewpoint absent from temporary default mods.
```

- [ ] Compile/run focused tests and observe missing-ID or forced-Viewpoint-dependency failure; adjust the action name in this example to the actual existing native dispatch name, without inventing a new protocol.
- [ ] Implement independent framework and Viewpoint operations by extracting shared file/argument handling from Engine, not copy-pasting the combined installer. Preserve source validation, existing backups, rollback and actual I/O error reporting; no process-running restrictions. Add new evidence item mappings and version detection, migrate/display old receipts without silently marking unrelated files as verified.
- [ ] Document author URLs, read date, installed runtime versions (Buddy 2.3.4, Viewpoint 0.1.6.1, Chinese 1.19.0), latest published information where verified, current paths/IDs/approval names, and query limitations. Buddy installer `windows_installer_4.4` is not runtime 4.4. No live install/update/download during audit.
- [ ] Run focused and full native regression; commit and report RED/GREEN, compatibility and any upstream uncertainty.

## Task 3: Launcher hierarchy and startup update notification

**Files:** modify `Orange3DTool/Web/app.js`, `style.css`, `index.html` as needed, create focused UI modules only if embedded/deployment resources are also updated; extend headless tests in `Web/` and existing full suite. Keep user-facing feature routes/actions intact.

**Interfaces:** consume Task 2 native cards/IDs and existing `coreUpdateCheck`, `coreUpdateStatus`, `coreUpdateProgress` protocol. Preserve legacy fixture compatibility if useful but primary UI shows exactly three core cards. Optional addons stay separated. Do not modify update signature/transport.

- [ ] Write failing headless tests for exactly three independent core actions, optional additions collapsed/separate, Chinese-only workflow, clear next step only when game/core missing, and accessible existing memory/maintenance/mod/save/server routes in both themes. Add startup tests with controlled native responses: one silent check after readiness; Latest/Error produce no modal/global error; Available yields dismissible notice; manual in-flight/Ready state not overwritten; dirty drafts survive.

```js
await page.goto(base);
await page.waitForFunction(() => window.orange.state.status);
await page.getByRole('button', { name: /安装 ZombieBuddy/ }).click();
// Inspect actual bridge action payload and UI result against fixture response.
```

- [ ] Run focused new tests and observe failure; implement the requested presentation/notification. Avoid another monolithic duplicate of existing functions: extract a small startup-update coordinator or integrate existing lifecycle clearly. Notice opens existing updater; no auto download/restart and no full-page busy lock. Late startup results cannot undo explicit user work or revive dismissed identical notices.
- [ ] Refine compact card/row spacing, hierarchy, status colors plus text, image ratios and responsive layout; preserve large original carousel cover and a concise lower-right status summary. Retain strong contrast in dark/light-blue, keyboard focus and reduced motion.
- [ ] Run `npm test` plus new focused tests. Capture headless screenshots with sanitized fixture data at 1440/1280/768 widths in both themes and inspect them; correct overflow/huge empty panels. Export sanitized actual components/mods/servers/maintenance UI screenshots under `website/assets/` for Task 4, with no private data. Commit and report.

## Task 4: Short truthful website and preparation docs

**Files:** modify `website/index.html`, `site.css`, `site.js`; create `help.html`, `changes.html`, screenshot assets; extend `tests/website.test.cjs`, `deploy/deploy_site.py` and deployment tests for the exact new file allowlist. Add `docs/operations/public-security-filing-preparation.md`. Keep `website/release.json` at public latest until an independently validated final release.

**Interfaces:** preserve validated release.json current-version downloads and official GitHub backup; feature tabs reference actual Task 3 images. Help and changes are linked local pages. No analytics/login, no historical version download links, no new hosting system.

- [ ] Add failing headless tests: keyboard-selectable feature tabs reveal relevant screenshot/text; current downloads unchanged; help/changes page navigation; no historical download links; homepage scroll height <=2700 at 1440×900; no horizontal overflow at 390/768/1280/1440; reduced-motion rendering; hostile/failed release metadata still uses existing safe fallback. Add deploy allowlist tests that accept intended pages/assets and reject arbitrary extras.

```js
await page.getByRole('tab', { name: '模组清单' }).click();
assert.ok(await page.getByRole('tabpanel').filter({hasText:'模组清单'}).isVisible());
assert.ok(await page.evaluate(() => document.documentElement.scrollHeight <= 2700));
```

- [ ] Run tests and observe expected missing-tab/page/length failures.
- [ ] Rebuild the short homepage using existing logo, existing scene cover and actual sanitized UI images, a single clear current download area, lightweight 160–220 ms transitions respecting reduced motion. Use concrete Chinese functionality copy, no slogan blocks, fabricated claims, historical download CTAs or artificial app windows. Separate practical help and text-only changes. Preserve source and ICP footer links; do not invent a public security registration number.
- [ ] Prepare a non-sensitive filing checklist: website name OZL/Orange zombiod launcher; orangezl.com/www; IP 101.201.143.163; Aliyun; HTTPS; ICP 辽ICP备2026024866号-1; public GitHub; no 公安 number or submitted application. Identify identity/address/responsibility fields user must provide on official systems, distinguishing ICP/SSL from 公安备案; link official guidance, no identity collection in repository.
- [ ] Run website/deployment tests, inspect screenshots of all pages/feature panels at desktop/mobile, correct visual issues. Commit and report RED/GREEN and screenshot paths. Deployment is performed by controller only after integration review; do not change remote latest metadata or delete release assets.

## Integration and delivery

- [ ] Review each task diff for spec and quality, fix Important/Critical findings and re-review.
- [ ] Run full native self-test, core-update-specific suite, complete Web and website tests, Python deploy/mirror tests. Use background screenshot inspection and repeat cold/hot catalog benchmark.
- [ ] Record completed features/test evidence in changelog without marking unreleased functionality as live. Keep version candidate 1.2.14 until final publication decision. Existing 1.2.13 downloads remain valid on the redesigned site.
- [ ] Perform whole-branch review; fix its complete findings in one wave and scoped re-review.
- [ ] Deploy only approved website static files through the existing host-key-pinned helper, preserving release.json and immutable binaries; verify live HTTPS pages/download destinations/cache headers. No game tests or foreground browsers.
- [ ] Build/scan one final launcher candidate if feasible; a detection is reported accurately and not bypassed. Deliver verified website and source work with exact release state, not an unverified public binary.
