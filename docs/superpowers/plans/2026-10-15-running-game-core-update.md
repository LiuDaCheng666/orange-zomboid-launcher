# Game-independent Core Restart Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish 1.2.3 with a one-time download-complete confirmation and automatic verified launcher restart, without a blanket running-game refusal.

**Architecture:** Keep the signed eighteen-file updater and its health/rollback transaction. Isolate the two existing long-lived monitor roles in hash-addressed, authenticated derived executable images; remove only the updater's game-running-alone refusal. A narrow read-only native restart preflight lets the Web UI decide whether to offer its one-time prompt; the existing native apply action remains the final authority.

**Tech Stack:** .NET Framework 4.x/C#, WinForms/WebView2, vanilla JS/HTML/CSS, PowerShell, Playwright, GitHub Releases.

## Global Constraints

- Version: **1.2.3**. Reuse `feature/core-self-update`; no new worktree. Coordinator owns version/release documents/publication.
- Approved spec: `docs/superpowers/specs/2026-10-15-running-game-core-update-design.md`.
- 核心更新不再单独因游戏运行而拒绝。只重启启动器，不关闭、杀死或重新启动游戏、Steam或未知进程。
- 临时启动会话仍未恢复时继续阻止；保留原生队列、进行中的文件修改、未保存编辑、游玩配置事务、待恢复启动会话和旧Buddy接管恢复等保护。
- 游戏运行时安装／卸载模组、改启动参数、恢复清单或修改游戏设置的既有保护不放宽。
- 正式可信核心的两个受管监控角色只能从已认证自有EXE准备副本；哈希、长度、路径、重解析点和并发校验不得省略；可信来源失败不得静默降级。
- No new signed owned paths, trust keys, update URLs, arbitrary program/path/key bridge arguments, game/Steam operations, registry edits, or third-party package assets.
- 每轮准备好的更新最多自动提示一次；历史Ready、过期回复、重复进度或取消后的迟到结果不会弹窗；不自动检查或下载。
- 阻挡时保留Ready，不抢夺其他弹窗、不无限自动重试；处理后玩家手动继续。未核实状态按未知处理。
- Ready copy: **下载完成，等待重启安装**. Explain launcher-only restart and continuing game, with real recovery/draft exceptions.
- Preserve original `Web/home.png`, all existing UI functionality/themes, old release bytes/tags, eighteen-file signature/hash/health/rollback contracts.
- All tests use controlled fixtures under `artifacts/TestFixtures`; never operate a player's installation or launch the actual game/Steam.
- Keep RED/GREEN evidence, scoped test commands/results, commit SHA and limitations in each task report. Use apply_patch for code edits.

## File map and integration

Native task owns `ManagedBackgroundWorker.cs` (new internal role-limited launcher), `PlaytimeTracker.cs`, `LaunchProfiles.cs`, updater-only policy in `ModernController.cs`, a non-mutating queue readiness property in `ModernDispatchQueue.cs`, the narrow preflight bridge in `ModernHost.cs`/`ModernHostPolicy.cs`, focused `CoreRestartPolicyTests.cs` and `ManagedBackgroundWorkerTests.cs`.

Web task owns update-only code in `Web/app.js`, new `Web/core-update-ready-ui-tests.cjs`, and existing focused update UI fixtures if the new interface requires them.

Integration task owns real lifecycle fixture extensions in `CoreUpdateNativeFixtureTests.cs` and `Test-CoreNativeUpdate.ps1`, plus tracked `Test-RunningGameCoreUpdate.ps1` to run the new focused suites. Production updater transaction/helper algorithms stay unchanged unless a demonstrated defect in this scope needs a reviewed fix.

Installer Task 4 is a separately approved follow-up (see desktop shortcut spec); it owns only desktop shortcut creation/ownership cleanup and installer UI integration. No parallel C# compilation/builds.

Coordinator owns VERSION/CHANGELOG/README/使用说明/docs releases, comprehensive regression wrappers, formal build/sign/package verification and public release.

### Task 1: Safe native restart and authenticated monitor isolation

**Files:**
- Create: `Orange3DTool/ManagedBackgroundWorker.cs`
- Modify: `Orange3DTool/PlaytimeTracker.cs`, `Orange3DTool/LaunchProfiles.cs`, `Orange3DTool/ModernController.cs`, `Orange3DTool/ModernDispatchQueue.cs`, `Orange3DTool/ModernHost.cs`, `Orange3DTool/ModernHostPolicy.cs`
- Test: `Orange3DTool/CoreRestartPolicyTests.cs`, `Orange3DTool/ManagedBackgroundWorkerTests.cs`

**Interfaces:**
- Keep `ModernController.AssertCanRestart(): void`, now independent of running-game-alone evidence. Do not change `Check()`, `Engine.Guard`, `LaunchProfiles.Pending` or recovery behavior.
- Produce `ModernDispatchQueue.CanRestart: bool`, a locked read-only snapshot (`!restarting && mutations == 0`). No reservation, cancellation, or mutation on reading.
- Add read-only `coreUpdatePreflight` to native core actions. Payload must be exactly `{protocol:1}`; return `{protocol:1,allowed:bool,message:string}`. It reads queue readiness and `AssertCanRestart` without cancelling/draining/reserving, touching recovery files, or starting an updater. Capture mode refuses it. Existing `coreUpdateApply` still performs both native/draft rechecks.
- Produce internal `ManagedBackgroundWorker.Start(string role, string first, string second): Process`. Accept only existing `--playtime-watch` and `--session-watch` roles. Resolve source from the running assembly/image, not caller-supplied paths. Validate arguments using their existing strict identities; preserve existing command-line protocols.
- Use a small internal preparation routine callable in compiled fixture tests with an ephemeral public key/source identity, not a new public CLI/bridge. Production always takes embedded key/current image. Explicit trusted-install evidence with invalid metadata must fail, not become a legacy fallback.
- Reuse `CoreDeployment.ReadAuthenticated/AdoptPortable`, `Safe`, `Matches`, `Acquire` and signed launcher record; prepare `OrangeLauncherRuntime/BackgroundWorkers/<sha256>/橙子启动器.exe`. Acquire root lock for preparation, write once with atomic verified bytes, reject wrong existing bytes/reparse points, then launch hidden. Before execution require exact expected length/hash and source identity. Old untrusted single-EXE mode retains its old launch behavior and no core-update claim.

- [ ] **Step 1: Add failing behavioral tests.** Compile focused entrypoints with `Build-Tests.ps1`; use existing fixture utilities and `CoreFixture` ephemeral signing where useful. Native policy tests exercise a real controller with a controlled game-process snapshot seam or actual fixture game process (not `test=true` bypass alone), asserting restart succeeds while the separate game process remains alive. Assert busy/network/queued mutation, PlayProfile transaction, LaunchProfiles pending and Buddy pending still refuse; assert no journal/default-list bytes change. Test ordinary game-file mutation still refuses in the running-game case.

```powershell
& ./Build-Tests.ps1 -Entry PZ3D.CoreRestartPolicyTests -Output artifacts/r123-restart-policy.exe
& ./artifacts/r123-restart-policy.exe
& ./Build-Tests.ps1 -Entry PZ3D.ManagedBackgroundWorkerTests -Output artifacts/r123-background-workers.exe
& ./artifacts/r123-background-workers.exe
```

Expected RED: running-game-only restart rejected; managed runtime image not prepared. If tests require a new internal method, use reflection to report a behavioral missing-capability failure before implementation rather than accepting a compilation failure as proof.

- [ ] **Step 2: Implement the native interfaces above.** Remove only `if(Running())` in `AssertCanRestart`. Add native preflight routing without relaxing payload policy. Replace the two existing `Process.Start(currentExe,...)` monitor call sites with the shared role-limited launcher. Preserve monitor request creation, exact game PID/start checks, sampling cadence and recovery algorithms.

```csharp
// Updater policy remains the authority at apply time.
controller.AssertCanRestart();
// Two existing callers retain using/Dispose semantics for the process handle.
using (var worker = ManagedBackgroundWorker.Start("--playtime-watch", e.Data, nonce)) { }
using (var worker = ManagedBackgroundWorker.Start("--session-watch", Key(e), nonce)) { }
```

`LaunchProfiles` key helper's actual existing name must be used; do not introduce a second key algorithm. No monitor protocol/path behavior changes.

- [ ] **Step 3: Verify GREEN and isolation failure branches.** The new tests must reject unknown role/source, signed-record mismatch, modified source/cache, reparse source/runtime path and concurrent conflicting preparation. Successful concurrent preparation reuses one verified immutable image. Hold both fixture workers and a separate fixture game process alive while replacing the root launcher image; ensure neither PID is killed, runtime image hash is unchanged, time totals are not reset and pending launch list is not restored. Use local fixture processes only; clean up only exact owned test PIDs.
- [ ] **Step 4: Run focused existing regression** `CoreUpdateServiceTests`, `CoreUpdaterIntegrationTests`, queue/policy tests and Program `--self-test` once; preserve existing assertions. Record commands/counts. Commit only this task's files.

### Task 2: One-time Ready confirmation and truthful update copy

**Files:**
- Modify: `Orange3DTool/Web/app.js` (update behavior only)
- Create: `Orange3DTool/Web/core-update-ready-ui-tests.cjs`
- Test/fixture adjustments: `Orange3DTool/Web/core-update-ui-tests.cjs`

**Interfaces:**
- Consume native `coreUpdatePreflight` payload `{protocol:1}` and result `{protocol:1,allowed:bool,message:string}` from Task 1.
- Continue using existing `CoreUpdateStatus.protocol/revision/state/taskId/latestVersion`, `hasUnsavedDrafts`, generic confirmation dialog and `coreUpdateApply` payload `{protocol:1,confirmed:true,drafts:false}`.
- Keep download intent session-local: generation, expected version, baseline revision, observed downloading task/Ready task, request success and consumed flag. No persisted auto-consent. No new arbitrary bridge data.
- Manual and automatic paths share one prompt/apply function with captured task/version and in-flight guard. Manual path remains available after choosing later or a blocked automatic prompt.

- [ ] **Step 1: Write and run RED browser cases** using real app DOM and synthetic native boundary messages with complete protocol fields.

```js
// Core behavior: active download + matching Ready offers exactly one confirmation.
await page.getByRole('button', {name:'下载启动器更新',exact:true}).click();
await emitReady({revision:20,taskId:'a'.repeat(32),latestVersion:'1.2.4'});
await page.locator('#confirm[open]').waitFor();
await page.locator('#confirm-no').click();
await emitReady({revision:21,taskId:'a'.repeat(32),latestVersion:'1.2.4'});
assert.equal(await page.locator('#confirm[open]').count(),0);
assert.equal(await applyCalls(page),0);
```

Test helpers live only in test files. Literal independent expected payloads, no source grep assertions. Run `node Orange3DTool/Web/core-update-ready-ui-tests.cjs`; initial failure must be absent one-time prompt, not fixture error.

- [ ] **Step 2: Implement generation-bound intent.** Arm only on explicit download click; accept matching new native Ready and successful acknowledgement in either arrival order (Ready may arrive before the request reply). Require a nonempty valid task ID and exact expected version. Cancel/check/error invalidates intent; ignore duplicates, stale revisions/generations and Ready for a different task/version. Startup/history Ready stays manual. Once eligible Ready is handled, consume intent even if blocked; no timer/retry popup.
- [ ] **Step 3: Gate and reuse consent.** Do not open over any existing `dialog[open]`, draft or local task. Ask native preflight once before offering the automatic prompt, and reject unknown/malformed/failed readiness. Show specific block reason with Ready/manual button intact. Capture task/version; on consent recheck local state, identity, modal/drafts/tasks then call existing apply. Guard double clicks and automatic/manual overlap before awaiting modal. Rejected native apply displays reason and leaves Ready; it never triggers an automatic retry.
- [ ] **Step 4: Update Ready label and copy.** Use `下载完成，等待重启安装`; explain that only the launcher reopens, game may continue, but pending recovery/unsaved edits must be resolved. Do not label staging as success. Keep history/health labels, progress, both themes and keyboard cancel semantics.
- [ ] **Step 5: GREEN cases.** Confirm sends exactly one native apply and does not send download twice; cancel leaves Ready/manual restart; repeated Ready, cancelled-late event, wrong task/version, out-of-order response, malformed protocol/readiness, failed preflight, old startup Ready, other modal, drafts, write task and recovery all cannot auto-apply. State changed during confirmation and double click cannot apply stale consent. Test game-running true alone with native allowed and both themes at 820x600, save screenshots. Run existing `core-update-ui-tests.cjs`, `experience-ui-tests.cjs`, `pcl-layout-ui-tests.cjs` once. Commit task files.

### Task 3: Real native lifecycle coverage and reproducible release gate

**Files:**
- Modify: `Orange3DTool/CoreUpdateNativeFixtureTests.cs`, `Test-CoreNativeUpdate.ps1`
- Create: `Test-RunningGameCoreUpdate.ps1`
- Tests consume: Task 1 focused native entrypoints, Task 2 focused browser entrypoint, existing native update fixtures.

**Interfaces:**
- Production core restart/preflight and derived worker interfaces from Task 1; Ready interaction from Task 2. Test fixture CLI additions belong only to `CoreUpdateNativeFixtureTests`, not production `Program`.
- `Test-RunningGameCoreUpdate.ps1` runs focused native entries sequentially, then browser Ready suite, then `Test-CoreNativeUpdate.ps1`; supports a fixture-root/evidence directory confined below repository `artifacts/TestFixtures` and no game/Steam discovery side effects.

- [ ] **Step 1: Extend the controlled fixture first.** Add a running-game/long-lived-monitor case that would fail with root-bound monitor images: run real signed fixture launcher/helper WebView flow, keep separate owned game-like process and derived monitor running, and replace the three core EXEs in a full update. Preserve existing sparse/full success, bad page rollback and interrupted recovery assertions. Do not test by changing real game's process or player configuration.
- [ ] **Step 2: Run the new fixture assertion against pre-isolation production in an isolated source build or use Task 1 saved RED evidence if exactly the same root-lock defect is exercised.** Record concrete old image replacement failure and new success, not a source-text test. If the existing fixture hardcodes manual-click behavior, update only fixture user confirmation for the one-time modal and preserve all health/receipt checks.
- [ ] **Step 3: Run all real lifecycle cases.** At least full healthy apply/reopen and full failed-health rollback/reopen must run, with monitor/game PID survival and exact executable hashes. Verify sample playtime store survives launcher restart and active LaunchProfiles journal blocks preflight without premature restore. No test-only calls may be added to production solely for assertions.
- [ ] **Step 4: Add the reproducible gate script**, strictly scoped output directory checks, explicit `$LASTEXITCODE` checks, exact-PID cleanup and bounded waits. Run it and report each sub-suite result. Commit tracked fixture/gate files, not generated binaries/logs/private fixture keys.

### Task 4: Opt-out per-user desktop shortcut with owned cleanup

**Files:**
- Create: `Orange3DTool/InstallerShortcut.cs`, `Orange3DTool/InstallerShortcutTests.cs`
- Modify: `Orange3DTool/InstallerForm.cs`, `Orange3DTool/InstallerProgram.cs`
- Test/fixture adjustments: `Orange3DTool/InstallerExperienceTests.cs`, `Test-RunningGameCoreUpdate.ps1`
- Approved additional spec: `docs/superpowers/specs/2026-10-15-desktop-shortcut-design.md` (user accepted recommended option on 2026-10-15).

**Interfaces:**
- Add a default-checked native control named `installer-desktop-shortcut`, labelled `创建桌面快捷方式（当前用户）`. Disable it with other install options during the worker. Capture its value at install click. Do not silently create shortcuts when merely opening the installer.
- Preserve existing 3-argument `InstallerForm` constructor for isolated tests; add an internal overload accepting `Func<InstallerInstallResult,InstallerShortcutResult>` as a fourth dependency. Production `InstallerProgram.Main` must pass the actual creation function. Existing synthetic 3-argument test forms must not touch a real desktop.
- `InstallerShortcutResult` contains read-only `Created`, `Path`, `Message`. A failure is not an installation failure; completion displays the independent shortcut outcome and appends it to copyable details/log evidence. Install/open/finish behavior stays unchanged.
- `InstallerShortcut.Create(InstallerInstallResult result): InstallerShortcutResult` uses current-user known DesktopDirectory and authenticated installed EXE. Internal constructor/dependencies can inject fixture desktop/key/COM operations but are not exposed by CLI/Web payload. Use Windows IShellLink/IPersistFile or the installed Windows shell COM object; no registry writes or external PowerShell/scripts.
- Persist a bounded owned shortcut receipt below the installation runtime, binding installationId, exact direct-child desktop path, exact target EXE/working directory/empty arguments/icon and file hash. Desktop may be a system-returned redirected/OneDrive location; validate the destination and link itself without following an attacker-controlled reparse link. Use bounded unique filename suffixes; never overwrite a pre-existing file.
- `InstallerShortcut.RemoveOwned(string root): string` must authenticate installation before core uninstall deletes its record. Only delete when receipt/install identity, known current desktop direct-child path, exact file hash and parsed shell link target/workdir/args/icon all match; preserve modified/replaced/foreign files. Returned warning is appended to uninstall outcome and diagnostics. No recursive desktop operations, no COM resolve that launches targets, no extra owned update paths.
- Keep `InstallerProgram.Install/--verify-setup`, portable ZIP startup and core updater free of implicit real desktop writes. Actual normal interactive installation passes Create; ordinary updater never does. Legacy interactive installer either verifies its own existing deployment identity equivalently or clearly reports unsupported shortcut creation; do not weaken official authentication to accommodate fixtures.

- [ ] **Step 1: Behavioral RED.** Add a controlled installer UI test asserting default checked control exists, unchecked install never calls creation boundary, successful install invokes creation once with exact result, failed install never invokes it, and shortcut error still leaves installed/open usable with a visible warning. Run new entry `PZ3D.InstallerShortcutTests` via `Build-Tests.ps1`; capture missing capability failures, not compilation errors.
- [ ] **Step 2: Implement the isolated shortcut service.** Create real .lnk files only inside test fixture desktop via Windows shell COM. Verify TargetPath, WorkingDirectory, IconLocation and empty arguments by parsing, not by launching. Validate installed EXE and identity first; create with collision-safe semantics; write receipt only for verified owned file. If any step fails, do not claim success or remove an external/changed file. An unwritable/redirected destination must result in an independent creation warning, not delete/reinstall the program.
- [ ] **Step 3: Integrate interactive completion and scoped uninstall.** Run shortcut creation off the UI thread with suitable STA/COM handling, after successful install verification and before complete page shows outcome. Show actual path or warning. Uninstall cleanup happens only after normal core uninstall safety preflight allows removal, but before installation identity disappears; avoid deleting a working shortcut when uninstall preflight fails. Validate all action ordering with fixtures; no changes to updater transaction/owned-file trust.

```powershell
& ./Build-Tests.ps1 -Entry PZ3D.InstallerShortcutTests -Output artifacts/r123-shortcut.exe
& ./artifacts/r123-shortcut.exe
& ./Build-Tests.ps1 -Entry PZ3D.InstallerExperienceTests -Output artifacts/r123-installer.exe
& ./artifacts/r123-installer.exe
```

- [ ] **Step 4: GREEN coverage.** Native default/unchecked/failure cases; real fixture .lnk parsing; same-name conflicts and bounded exhaustion; redirected desktop selection; source or receipt tamper; unknown/malformed receipt; reparse links; foreign replacement and user edits preserved; exact owned link removal; uninstall preflight failure retains link. Verify creation and cleanup do not write outside fixture desktop/installation or start launcher/game. Save normal/small/scaled UI screenshots.
- [ ] **Step 5: Run existing installer/core deployment/package regressions and add shortcut suite to tracked integration gate.** Commit task-owned files, report commands/counts and any known desktop platform limits. Do not modify artwork or public release assets in this task.

### Task 5: Approved original geometric icon integration

**Files:**
- Replace approved own artwork: `Orange3DTool/Assets/orange-app.png`, `Orange3DTool/Assets/orange-app.ico`, `Orange3DTool/Web/orange-app.ico`
- Create provenance: `docs/assets/icon-123/README.md`
- Existing converter: `Orange3DTool/Build-Icon.ps1` (reuse, no editing needed)
- Approved spec: `docs/superpowers/specs/2026-10-15-geometric-orange-icon-design.md`.

**Interfaces:**
- User selected the cleaned generated preview at `C:/Users/MC233/.codex/generated_images/01a036c9-80e6-7cd3-ac51-35d8c78e44c5/exec-88b6fc89-9031-470b-b581-881644f79db2.png`. Copy this exact image to Assets/orange-app.png; preserve the original generated file. Do not redraw, recolor, crop or use a different variant.
- Existing build scripts embed Assets/orange-app.ico in all core EXEs and setup; Web/index.html reads Web/orange-app.ico. Existing window icon extraction and Task 4 shortcut EXE icon location must keep this single identity. No new owned core paths or page layout changes.

- [ ] **Step 1: Inspect inputs and current build wiring.** Read selected PNG with view_image if not already visible; check converter and build win32icon paths, both source ICO files and header image reference. This task is generated-artwork/format conversion, not a runtime code behavior change; no production test seam or new feature code is needed.
- [ ] **Step 2: Copy selected artwork and run existing multi-size converter.** Use native file copy for image assets, not shell text writes or image redraw scripts.

```powershell
Copy-Item -LiteralPath 'C:/Users/MC233/.codex/generated_images/01a036c9-80e6-7cd3-ac51-35d8c78e44c5/exec-88b6fc89-9031-470b-b581-881644f79db2.png' -Destination Orange3DTool/Assets/orange-app.png
& ./Orange3DTool/Build-Icon.ps1
Copy-Item -LiteralPath Orange3DTool/Assets/orange-app.ico -Destination Orange3DTool/Web/orange-app.ico
```

- [ ] **Step 3: Validate actual artifacts.** Decode ICO header and every PNG frame, verify 16/32/48/64/128/256 sizes and alpha; ensure both ICO hashes match. Browser renders the actual ICO on dark/light backgrounds at 16/32/48/256; save screenshots in own fixture directory and inspect them. Original home.png hash remains `82FCDE2FDAF188629D9ED9903CEEB21FD9C1C99EA16CCFFB123E9A51FA529D01`. Final coordinator package validation checks actual EXE/setup icon and frame extraction from the built binaries.
- [ ] **Step 4: Write provenance via apply_patch.** Record exact selected source, generation/cleanup prompts from the coordinator's prompt artifact, built-in imagegen mode, sourcePNG/ICO SHA256 and dimensions, original design copyright scope and PCL conceptual reference only. No PCL assets/code copied. Commit own asset/docs files, not screenshots or generated binaries. Report exact checks and screenshot paths.

## Coordinator release sequence (after reviewed Tasks 1–5)

- [ ] Bump VERSION to 1.2.3 and add precise Chinese change/release notes, including old 1.2.1/1.2.2 first-upgrade limitation; no successful player restart claim.
- [ ] Run all fifteen native and thirteen existing browser gates plus new focused suites on final code; record logs. Run native and browser jobs separately; never compile native jobs concurrently.
- [ ] Broad final branch review against the 1.2.2 source baseline with task reports. One reviewed fix wave for findings; no hidden unreviewed production changes.
- [ ] Build fresh `dist/1.2.3-github-release` with `Build-CoreRelease.ps1` and existing official signing key. Verify RSA, eighteen owned paths/hashes, ZIP twenty entries, four executable metadata/version, installer extraction/uninstall in own fixtures, original cover/public key and immutable 1.2.1/1.2.2 release hashes.
- [ ] Commit final source/docs, normal fast-forward push to `origin/main` plus new annotated `v1.2.3` tag (never move old tags or force push). GitHub draft upload setup/ZIP/manifest/signature/eighteen bins; verify all twenty-two remote names/lengths/digests before publishing latest, then verify public anonymous downloads.
- [ ] Probe a freshly extracted authentic 1.2.2 ZIP with the existing default production release feed: expect Available 1.2.3, then real verified download Ready/Staged and all eighteen old files unchanged. Probe 1.2.3 expect Latest. Record evidence/source SHA/release URL/hashes; do not auto-apply to player's installation.
- [ ] Final concise Chinese handoff: published version/link, actual behavior, first-upgrade caveat, tests scope. Archive this plan's review workspace before removing its exact validated temporary directory; leave sibling plans/old packages untouched.

## Self-review

Spec coverage: native policy/worker trust and failure paths in Task 1; all Ready-generation/modal/draft/consent requirements in Task 2; real replacement/health/rollback and monitor lifetime in Task 3; immutable public release/default-feed verification in coordinator sequence. Global game-mutation guards unchanged. Interface/payload/version names cross-checked. No new trust or owned paths; no unrelated UI changes.
