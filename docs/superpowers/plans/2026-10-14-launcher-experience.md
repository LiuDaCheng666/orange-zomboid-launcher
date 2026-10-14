# Launcher Experience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver the approved configuration-led launcher UI, launch preflight, full play profiles, evidence-based diagnostics and safe configuration sharing.

**Architecture:** Extend existing Engine and native controller with focused service modules. Keep existing launch/session, authorization, total-memory, update and privacy boundaries. A separate browser experience module may extend the current page without a wholesale frontend rewrite.

**Tech Stack:** Windows x64, C#/.NET Framework 4.8, WinForms, WebView2, vanilla JS/CSS, Playwright.

## Global Constraints

- Use current `feature/core-self-update`; no extra worktree, main merge, push or upload.
- All real Steam/game/save/pagefile/registry data remain untouched during implementation and tests.
- Components keep compact original-cover cards; mods keep list plus detail; dark gold and light blue remain.
- Never approve Java files automatically or equate configured/files present/process started with actually loaded or connected.
- Total physical memory determines heap limits; available memory is advisory.
- No PCL source/assets or third-party payloads enter this repository or release.
- No arbitrary filesystem path, shell command, account/password or approval decision accepted in play-profile sharing or page requests.
- Unknown compatibility is explicit; no fabricated retirement notices, automatic addon removal or version-number-only prohibition.
- New candidate version is 1.2.0; don't overwrite accepted 1.1.1 assets or publish online.

## Task 1: Read-only preflight and honest launch stages

**Files:** Create `Orange3DTool/LaunchPreflight.cs`, `Orange3DTool/LaunchPreflightTests.cs`; modify `ModernController.cs`, `ModernHostPolicy.cs`, and focused existing launch tests as required. Existing `LauncherLaunch.cs`, `JavaApprovals.cs`, `ModConfigurations.cs`, `LaunchProfiles.cs` are reused, not duplicated wholesale.

**Interfaces:** `LaunchPreflight.Check(Engine e, bool busy, bool running)` returns serializable report with `canLaunch`, `checkedUtc`, `items` (id/status/title/message/route) and `message`; a target-aware overload also accepts validated `targetKind` (`normal|save|server`). Native action `launchPreflight` accepts empty payload or only optional validated `targetKind`, default normal. `ModernController` emits existing progress events and consumes the report on real launch paths. Server launch must not be blocked by ordinary local-mod dependencies which its existing session mechanism intentionally excludes; malformed/unrecoverable configuration remains fatal.

- [ ] Write tests exercising a clean synthetic Engine, malformed JSON, missing Buddy files, old javaagent, pending recovery, missing mod dependency, pending Java approvals, no-Xmx advisory and stale readiness. Independently assert no writes occur.

```csharp
// Break caught: a failed launch configuration is incorrectly allowed.
var report = LaunchPreflight.Check(engine, false, false);
Assert(!report.canLaunch, "missing Buddy file blocks launch");
Assert(report.items.Any(i => i.id == "launch-config" && i.status == "error"), "specific issue exposed");
```

- [ ] Compile/run `./Build-Tests.ps1 -Entry PZ3D.LaunchPreflightTests -Output artifacts/preflight-tests.exe` then the EXE; record expected red output before implementation.
- [ ] Implement bounded read-only report using existing checks, default-mod sort and current Java approvals. Pending authorization is advisory and has a direct action route; do not require a previously unseen server Java file. Distinguish advisory heap/free-memory conditions from fatal config errors. Check only known paths and guard reparse points; expose no secrets. Real `launch`/`profileLaunch` rerun checks immediately before starting, preserving network drain/session protections. Stage names must not claim game-ready or connection success.
- [ ] Run focused tests plus `ModernTests`/launcher launch regression entrypoints available in this repo; preserve baseline tests and add actual controller-boundary tests. Save TDD commands/results in report.
- [ ] Self-review and commit only task files after fresh passing verification; report report-field contract and exact native action to coordinator.

## Task 2: Play profiles and safe JSON sharing

**Files:** Create `Orange3DTool/PlayProfiles.cs`, `PlayProfileTransaction.cs`, `PlayProfilesTests.cs` as needed; modify native controller/policy and relevant tests. Add narrow pending-transaction guards to `LaunchPreflight.cs`, `LauncherLaunch.cs`, `Program.cs` (Engine.Guard) and updater restart eligibility as needed; no unrelated refactoring. Do not replace the existing mod-configuration store. Keep files focused; transaction belongs in its own module.

**Interfaces:** Native actions `playProfiles` (list + stamp + selected ID + pending recovery summary), `playProfileCapture` (capture current known settings, no game writes), `playProfileSave`, `playProfileDelete`, `playProfileSelect`, `playProfilePreview`, `playProfileApply`, `playProfileExport`, `playProfileImport`, `playProfileRecover` (explicit recovery confirmation, bound to a fresh native recovery stamp). Use native-generated IDs/stamps and expose JSON objects with lower-case fields. Return a profile with `id,name,targetKind,targetId,mods,heapGiB,shaderId,settings` and list summary. TargetKind is `normal|save|server`; settings map keys use existing `file|key` identifiers. Publish exact payloads in report for Task 4. Pending recovery blocks launch, mutation and updater restart; no raw page paths or unconditional startup restoration. Base-component installation is global and is not automatically installed/uninstalled by profile selection or apply.

- [ ] Add red tests for capture, independent profiles, persistence and stale stamp, invalid/readonly settings, missing shader, invalid mod/target IDs, save and server-specific behavior, malformed/unknown-version or secret/path-bearing import, no auto-apply, game-running rejection and transaction rollback/external modification recovery.

```csharp
// Break caught: selecting/importing a profile silently edits game files.
var original = File.ReadAllBytes(engine.Config);
service.Import(json, stamp, true);
Assert(File.ReadAllBytes(engine.Config).SequenceEqual(original), "import never applies");
```

- [ ] Run `Build-Tests.ps1 -Entry PZ3D.PlayProfilesTests -Output artifacts/play-profile-tests.exe` and record red failure; use isolated test fixtures.
- [ ] Persist bounded versioned profile JSON under Engine.Data with atomic writes, snapshot stamps and reparse guards. Capture actual editable existing settings, current heap/selected shader and ordinary mod IDs. If actual Xmx/graphics.pack is absent, null heapGiB/empty shaderId means retain current with explicit warnings, not invented defaults. Server profiles require mods=[] and apply only heap/settings/shader, leaving default mods for the existing launch session to isolate. Selection/save/import modify only launcher data and may be used while a game runs; apply/recovery require game exit. Save requires valid existing local targets; missing imported mods remain visible, not automatically downloaded. Imported target associations are cleared to normal. Export excludes absolute paths, credentials, author trust, Java decisions and machine-specific target ID. Preserve legitimate opaque slash-containing/B42-qualified mod IDs; reject actual absolute/traversal-shaped IDs, not every slash.
- [ ] Prevalidate all changes before apply. Build a bounded, allowlisted, recoverable transaction over config JSON, actual properties, default mods or editable target save mods.txt only. Reuse parsing/validation/total-memory limits, preserve unrelated bytes/fields and client-managed mods. Apply only after explicit confirmation with current preview stamp; reject running game, pending recovery, stale files, invalid dependencies or selected shader. Server profile must not apply ordinary single-player mods. Atomic write/rollback must not overwrite a file altered by another program; preserve evidence for manual recovery. Ensure interrupted transaction has a startup recovery path rather than silently continuing. Do not run a game or Steam in tests.
- [ ] Expose named profile selection/list/preview/import/export through strict native policy. Run focused and existing mod/save/memory/setting/session tests, commit task files, report exact payload shapes and recovery boundaries.

## Task 3: Evidence-based game diagnostics

**Files:** Create `Orange3DTool/GameDiagnostics.cs`, `GameDiagnosticsTests.cs`; modify controller/policy/diagnostic journal integration only as needed. Keep pure rule analysis separate from bounded file collection inside this module or a focused helper.

**Interfaces:** `GameDiagnostics.Read(Engine e)` returns `checkedUtc`, `findings`, `warnings`, `sources`, `reportText`. Each finding includes stable `id`, `severity`, `confidence` (`confirmed|suspected`), `title`, `message`, `evidence`, `source`, `historical`, `nextStep`. Native action `gameDiagnostics` accepts empty payload; export text is already redacted. Read-only, no uploads/deletions or automatic fixes.

- [ ] Add failing tests for actual instrument/load-agent lines, Java heap error vs system swap/commit error, missing class/dependency, duplicate mod, EXCEPTION_ACCESS_VIOLATION, subscription-not-confirmed, unrelated lines containing only a mod name, historical timestamps, bounded UTF-8/legacy logs and malformed/reparse/oversized sources. Include password/SteamID/token/IP/path redaction assertions.

```csharp
// Break caught: ordinary mod mention is falsely blamed for a crash.
var report = Analyze("Loading Viewpoint successfully", DateTime.UtcNow);
Assert(report.findings.Count == 0, "successful load is not a culprit");
```

- [ ] Run `Build-Tests.ps1 -Entry PZ3D.GameDiagnosticsTests -Output artifacts/game-diagnostics-tests.exe`, record red output.
- [ ] Collect only known console/logs/hs_err and tool diagnostic files, bounded file count/bytes with tail retention. Do not recursively crawl saves or read arbitrary supplied paths. Distinguish game log errors from launcher task errors, explicitly mark old sources, surface unreadable source warnings, and apply privacy sanitization before findings/report exposure. Each conclusion retains actual supporting lines and narrowly qualified next steps.
- [ ] Run diagnostic/privacy regression tests, commit and report boundary schema for frontend. Do not claim the rule can diagnose network latency or GPU bottlenecks from insufficient logs.

## Task 4: Unified experience UI and integration

**Files:** Modify `Orange3DTool/Web/index.html`, `app.js`, `app.css`; create a focused `experience.js` module if needed and update resource/build allowlists when adding files. Create `experience-ui-tests.cjs`; update existing browser tests only for deliberate route/layout changes, retaining their behavior protections. Version/docs/build integration belongs to this task. Native integration defects discovered in previous tasks are sent to their owners rather than silently worked around.

**Interfaces:** Consume Task 1–3 exact reports and native action contracts. Preserve existing host protocol, update trust boundaries, hasUnsavedDrafts, directory identity and component safety. Mock only the native external I/O boundary in Playwright; exercise real DOM/events and assert requested payload semantics.

- [ ] Write and run failing browser tests for homepage profile selection without mutation, clear single launch CTA, preflight failure preventing launch, advisory allowing confirmed launch, save/server target selection, apply confirmation and stale preview, safe JSON import/export, evidence/report display, filters and multiselect bulk add/remove, drafts preserved during async reads, task-local busy state and semantic colors in both themes.

```javascript
await page.getByRole('button', {name:'启动游戏', exact:true}).click();
await expect(page.getByText('缺少 Buddy 启动文件')).toBeVisible();
assert.equal(sent.filter(x=>x.action==='launch').length,0);
```

- [ ] Build configuration-led home, overview summaries, recent saves/servers and pending items; add full-profile capture/editor/preview/share UX with explicit global-setting scope. Re-group sidebar into six primary categories using secondary tabs and preserve direct old hash routes. Maintain compact cards and list/detail workspace; keep official covers. Add status filters and multi-select action bar that edits drafts only; update-based filters must show unknown rather than fake update metadata.
- [ ] Keep task/query feedback on local portions of the page; preserve input focus, scroll and unsaved drafts. Show launch stages with no fabricated game-load percentage or success. Integrate diagnostics into maintenance/help with copyable redacted report. Present component configured/local/workshop/compatibility evidence separately, retaining unknown retirement status unless confirmed.
- [ ] Normalize typography (14px body, >=12px supporting text except compact technical chips), spacing, status colors + wording, fold long evidence/advanced settings; verify dark/light and responsive layout. Test all existing routes and native WebView resource loading. Newly split JS/CSS must be authenticated and included in release allowlists, helpers and update tests; avoid bundle inflation.
- [ ] Run all existing C# regression and browser suites; run signed-core packaging and native preview smoke checks. Capture and inspect homepage, components, mods, saves, servers, settings/memory, maintenance/diagnostics and help in both themes at target resolutions using synthetic preview data.
- [ ] Update VERSION to 1.2.0 and concise CHANGELOG/usage/README with exact implemented behavior and verified limits, PCL conceptual reference attribution only. Build into fresh `dist/1.2.0-experience-verified` or distinct verification-suffixed folder if retries require; never overwrite 1.1.1. Use existing external signer by path only, never inspect private key contents. Commit only after fresh tests and report package hashes/verification.
