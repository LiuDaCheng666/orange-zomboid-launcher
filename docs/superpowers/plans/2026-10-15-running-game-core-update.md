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

Coordinator owns VERSION/CHANGELOG/README/使用说明/docs releases, comprehensive regression wrappers, formal build/sign/package verification and public release. No parallel C# compilation/builds.

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

## Coordinator release sequence (after reviewed Tasks 1–3)

- [ ] Bump VERSION to 1.2.3 and add precise Chinese change/release notes, including old 1.2.1/1.2.2 first-upgrade limitation; no successful player restart claim.
- [ ] Run all fifteen native and thirteen existing browser gates plus new focused suites on final code; record logs. Run native and browser jobs separately; never compile native jobs concurrently.
- [ ] Broad final branch review against the 1.2.2 source baseline with task reports. One reviewed fix wave for findings; no hidden unreviewed production changes.
- [ ] Build fresh `dist/1.2.3-github-release` with `Build-CoreRelease.ps1` and existing official signing key. Verify RSA, eighteen owned paths/hashes, ZIP twenty entries, four executable metadata/version, installer extraction/uninstall in own fixtures, original cover/public key and immutable 1.2.1/1.2.2 release hashes.
- [ ] Commit final source/docs, normal fast-forward push to `origin/main` plus new annotated `v1.2.3` tag (never move old tags or force push). GitHub draft upload setup/ZIP/manifest/signature/eighteen bins; verify all twenty-two remote names/lengths/digests before publishing latest, then verify public anonymous downloads.
- [ ] Probe a freshly extracted authentic 1.2.2 ZIP with the existing default production release feed: expect Available 1.2.3, then real verified download Ready/Staged and all eighteen old files unchanged. Probe 1.2.3 expect Latest. Record evidence/source SHA/release URL/hashes; do not auto-apply to player's installation.
- [ ] Final concise Chinese handoff: published version/link, actual behavior, first-upgrade caveat, tests scope. Archive this plan's review workspace before removing its exact validated temporary directory; leave sibling plans/old packages untouched.

## Self-review

Spec coverage: native policy/worker trust and failure paths in Task 1; all Ready-generation/modal/draft/consent requirements in Task 2; real replacement/health/rollback and monitor lifetime in Task 3; immutable public release/default-feed verification in coordinator sequence. Global game-mutation guards unchanged. Interface/payload/version names cross-checked. No new trust or owned paths; no unrelated UI changes.
