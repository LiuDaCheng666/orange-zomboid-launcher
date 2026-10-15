# Installer Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver approved 1.2.1 reread recovery and a responsive, safe native installer with automatic destination and honest progress.

**Architecture:** Retire stale frontend selection errors only after authoritative current reads; keep launch identity guards. Add focused installer destination/progress/service and native form modules around the existing authenticated installation transaction, without broad deployment refactoring.

**Tech Stack:** C#/.NET Framework 4.8, WinForms, WebView2 frontend, vanilla JS, Playwright, existing Windows release signer.

## Global Constraints

- Real Steam/game/save/pagefile/registry data remain unchanged during development and tests.
- Keep the existing feature/core-self-update checkout; no new worktree, main merge, push or publication.
- Version 1.2.1; preserve accepted 1.2.0 and 1.1.1 artifacts and trust key bytes.
- No registry writes, new updater authority, third-party payloads, automatic game launch or Java authorization.
- Existing signed allowlist, independent-root/reparse/nonempty-directory checks, transactional deployment and scoped uninstall remain authoritative.
- Installer preview and tests use isolated fixtures; no real default destination is created or installed during verification.

## Task 1: Authoritative profile-reread recovery

**Files:** Modify Orange3DTool/Web/app.js and experience-ui-tests.cjs only. No native schema, target apply or unrelated UI change.

**Interfaces:** Existing playProfiles response `{stamp,selectedId,profiles,selectedProfile,pendingRecovery,warnings}`; existing experienceRead profiles branch, token/experienceEpoch and selectPlay guards. Fresh read succeeds only when the current response reconciles the selected ID/profile with actual launch target; no automatic apply/launch.

- [ ] Add actual DOM regressions before implementation: failed select → successful empty native reread → explicit launch reaches fresh preflight; same with a valid selected profile; failure/malformed/stale/old-directory/pending-selection and manual target mismatch cannot authorize preview/apply/launch; clear old feedback on valid recovery; retain drafts.

```javascript
await page.evaluate(()=>fx.failure='playProfileSelect');
await page.locator('#play-profile-select').selectOption('a'.repeat(32));
await page.waitForFunction(()=>!!orange.state.playSelectionError);
await page.evaluate(()=>{fx.failure='';fx.selected='';fx.profiles=[];fx.stamp++;});
await page.locator('[data-play-refresh]').click();
await page.waitForFunction(()=>!Object.keys(orange.state.experienceReads).length);
await page.locator('#launch').click();
await page.waitForFunction(()=>sent.some(x=>x.action==='launchPreflight'));
assert.equal(await page.evaluate(()=>orange.state.playSelectionError||''),'');
```

- [ ] Run node Orange3DTool/Web/experience-ui-tests.cjs; retain expected named RED before changing production.
- [ ] Implement narrow validated reconciliation. Do not blindly clear on refresh click, accept invalid selectedId/profile associations or bypass assertProfileTarget. Prevent stale list/selection results from overwriting newer selection; report mismatches clearly. Preserve launch-intent/config/draft checks and native request payloads.
- [ ] Focused GREEN then all11browser suites once. Node --check, diff --check; scoped commit. Write task report with exact RED/GREEN/output, state/async semantics and constraints.

## Task 2: Native installer, automatic destination, release integration

**Files:** Create focused InstallerExperience.cs (destination/progress/service logic), InstallerForm.cs (native visual layout/state), InstallerExperienceTests.cs; modify InstallerProgram.cs as thin routing/resource adapter, native test integration as needed. Preserve CoreDeployment/PortableDeployment protections; any necessary progress-only additive signature must retain old overload and requires affected regression tests. Update VERSION, CHANGELOG.md, usage/README/release docs. No Web code unless a concrete integration bug is referred to Task1 owner.

**Interfaces:** Existing InstallerProgram.Install(string root) and --verify-setup remain supported; normal Main opens native form instead of folder-dialog/messagebox chain. Add internal progress DTO with phase/message/current/total so known work is measured and unknown deployment work is indeterminate. A pure destination selector accepts supplied drive facts, system/setup paths and fallback directory; platform adapter only reads drive readiness/type/free space. Installation service consumes existing authenticated resources and calls actual CoreDeployment.Install, then verifies manifest-owned files and record. Form is driven by backend delegate for fixture tests; no new arbitrary production CLI/path authority or test-only behavior inside service.

- [ ] Write failing native tests for default selection, fallback, occupied suffix, unchanged real/default paths before click, custom path safety and real service progress/completion. Include ready non-system setup drive preferred, unavailable/removable ignored, largest space/stable tie, only system fallback, existing directory preserved, observer/UI state not reporting completion on failure. Compile via Build-Tests.ps1 and observe missing feature/behavior RED.

```csharp
// Literal drive facts; no real drive writes.
// E is setup drive, D has more free bytes: prefer E, not C and not D.
Check(DefaultDestination(facts, "C:\\", "E:\\Downloads\\setup.exe", fallback)
      == "E:\\OrangeLauncher", "setup non-system drive is preferred");
// Core signed fixture installation: expected selected root equals confined test root.
// Progress cannot say Complete before authenticated record and final owned-file checks.
```

- [ ] Implement small path-policy/progress/service unit. Recommendation order follows spec; use bounded fresh suffix, no pre-click writes. UI shows exact destination and disk fallback reason. Never hardcode developer drive/user folder. Validate roots at install boundary using existing helpers; do not delete nonempty directories. Emit actual archive bytes/file verification counts and indeterminate transaction phase; no invented global percentage.
- [ ] Implement native Form with existing icon, graphite/gold rounded controls, clear version/path, editable full path and browse-parent child folder action. Responsive/scrollable high-DPI layout. Single install CTA, worker task/progress marshal, no auto-start; protect close while running. On success explicit launch of exactly installed launcher; on error copyable details and existing evidence path, no automatic cleanup/retry over nonempty data.
- [ ] Add behavioral native UI fixtures for ready/busy/failure/completed, responsive during held install, exact custom root, failure preserves details and no success/open, explicit open only after successful validation, close protection; draw actual controls for screenshots in normal/small/high-DPI settings. Test utilities/fixture modes belong in tests, not production CLI.
- [ ] Bump VERSION to1.2.1; concise changelog/usage/README explain actual default, custom directory, measured/indeterminate stages, resolved N1 and local-only feed. Preserve historical1.2.0 docs/hashes. Run focused tests, Program self-test, CoreDeployment/CoreUpdateTransaction/CorePackage/CoreFinal and affected installation/package fixtures. Self-review/commit/report; no official signing before source review.
- [ ] Coordinator after task/final review: fresh full13native + InstallerExperienceTests + all11browser, Test-CorePackageBuild ExpectedVersion1.2.1, actual isolated native update lifecycle, official build to fresh dist/1.2.1-installer-verified, signature/18files/20ZIP/threeEXE/bytes/installer/unchanged1.2.0+1.1.1 checks. Capture/inspect actual installer/native fixture stages, both launcher themes and touched flows; no real game/default installation. Final record hashes and constraints, preserve evidence, keep branch.

## Review and handoff

Each task gets spec+quality review before the next production writer. Final whole-branch review gets exact merge-base package and scoped findings; one combined final fix wave and one scoped re-review, then explicit residual adjudication. Root independently verifies source-final artifacts and preserves old candidates. No extra merge/push/worktree or real Steam/game operation is authorized. This new user-requested plan explicitly resolves N1 previously parked in1.2.0; old plan evidence is archived, not active scratch.
