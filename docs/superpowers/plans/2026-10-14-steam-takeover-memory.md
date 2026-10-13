# Launcher 1.1.1 Steam and Memory Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver 1.1.1 with reliable bounded subscription confirmation, explicitly consented takeover of old Buddy launch parameters, and total-physical-memory-based heap settings.

**Architecture:** Keep the current native C# / local WebView2 boundaries. Introduce small shared memory and launch-options policy units, then build the confirmed takeover transaction on the scoped parser; Steam account/process and operating-system calls stay at injectable native boundaries. Preserve the existing signed program-core update layout and existing component installers.

**Tech Stack:** .NET Framework C# compiled with the repository PowerShell scripts, WinForms/WebView2, plain JavaScript/CSS, synthetic native and Playwright regression fixtures, RSA-SHA256 core manifests.

## Global Constraints

- 1.1.1 修复三个问题：Steam 在线时订阅状态刷新稍晚而安装失败，手动安装 ZombieBuddy 后启动参数冲突导致工具不能安装，以及实时剩余内存限制下次启动的 Java 堆设置。
- 保留游戏本体、存档、模组清单、Java 授权和第三方光影文件；只修改本规格涉及的启动参数，不修改注册表或系统分页文件。
- 普通安装和下载不要求退出。
- 结果成功后的标志刷新等待最多15秒，每次检查间隔不超过100毫秒，逐次检查取消，进度文字最多每秒更新一次。
- 不能简单删除订阅检查，不能以文件夹存在或成功发起下载代替完成。回调处理不得丢失同时到达的所选条目下载结果。
- 只有确切识别的旧 Buddy Java 代理、原生加载器参数和重复项可处理；其他 Java 代理、游戏参数、内存参数、其他游戏及其他账号配置必须保留。
- 不能在启动软件时自行接管，也不能通过页面传入的任意文件路径、账号或命令选择修改目标。
- Steam 未运行时不需要退出；无法确定当前账号时不猜选另一个账号，不能把未核实的 Steam 项标记为已接管。
- 开发测试不改变真实Steam订阅、不退出用户正在运行的Steam、不修改真实游戏配置或授权。
- 总量采用操作系统识别的总物理内存，不加上分页文件或显存。
- 维持当前可用内存的读取与显示，但它只能产生风险提示，不能参与推荐值、输入上限、预设按钮可用性或保存拒绝规则。
- 已有Xmx按实际值显示，不因可用内存下降悄悄截低编辑值。
- 原有Java批准仍须玩家逐项决定。
- 源码保留在当前本地功能分支，不额外创建工作树，不合并或推送main。
- 不改用测试公钥、不覆盖正式私钥、不重新附带第三方光影。

## Execution and evidence

Repository: `E:/Projects/OrangeLauncher`; existing branch: `feature/core-self-update`; approved design: `docs/superpowers/specs/2026-10-14-steam-takeover-memory-design.md`. Base before this plan: `a5385ba732c36f277c05580600af750dc8f6fbcb`. The three fixes form one release but tasks below are separately reviewable. All implementers read the approved design and their own extracted task brief, not the entire plan. The coordinator uses sequential fresh implementers and scoped reviews; no concurrent production edits.

Store task reports and exact commit-range diff packages in this plan's `.superpowers/sdd/2026-10-14-steam-takeover-memory/` directory. Tests, captures and failed evidence go under uniquely named `artifacts/1.1.1-*` paths. Preserve existing 1.1.0 output and other plans' evidence. Each implementer records RED commands/failures before production edits, GREEN commands/results, changed files, commits and concerns. Each reviewer returns both specification and quality verdicts. Development fixtures must make accidental real Steam/process/game operations impossible, not merely promise not to call them.

## File responsibilities

- `LauncherMaintenance.cs`: OS memory probe, pure budget policy and guarded heap save.
- `LauncherFeatures.cs`, `Web/app.js`, scoped browser tests: memory representation in both interfaces.
- `SteamWorkshop.cs`, `SteamLauncherNative.cs`: bounded subscription flag confirmation and preserved callback results.
- `WorkshopWorker.cs`: structured, bounded, privacy-safe worker failures and correct primary error ordering.
- New `SteamLaunchOptions.cs`: scoped, edit-preserving VDF parser and current-account discovery.
- New `BuddyLaunchArguments.cs`: exact Buddy argument classification and conservative JSON/BAT/command-line transforms.
- New `BuddyTakeover.cs`: preview, identity binding, graceful process boundary and recoverable atomic transaction.
- `Program.cs`, `JavaApprovals.cs`, `ModernController.cs`: shared scoped guards, snapshots, controller integration.
- `ModernHostPolicy.cs`, `ModernHost.cs`, `Web/app.js`, `Web/style.css`, `LauncherFeatures.cs`: explicit confirmation and visible takeover states.
- `VERSION`, `CHANGELOG.md`, core build/test scripts and release evidence: versioned deliverables and inherited self-update regression.

### Task 1: Total-memory heap policy and honest UI

**Files:**
- Modify: `Orange3DTool/LauncherMaintenance.cs`, `Orange3DTool/LauncherFeatures.cs`, `Orange3DTool/Web/app.js`.
- Modify/Test: `Orange3DTool/LauncherMaintenanceTests.cs`, `Orange3DTool/LauncherFeaturesTests.cs`, `Orange3DTool/Web/ui-tests.cjs`, `Orange3DTool/Web/full-ui-tests.cjs` where memory fixtures exist.
- Create/Test: `Orange3DTool/MemoryPolicyTests.cs`, `Orange3DTool/Web/memory-policy-ui-tests.cjs`.
- Modify: `Orange3DTool/Program.cs` self-test registration only.

**Interfaces:**
- Produces: `internal static LauncherMemoryInfo CalculateMemoryInfo(int totalGiB, int availableGiB)` in `LauncherMaintenance`.
- Keep: public `MemoryInfo()`, `ReadHeapGiB(Engine)` and `SetHeapGiB(Engine,int)` signatures.
- Add a native-policy provider overload used by the normal save implementation and synthetic save tests, without mutable global test switches. It must preserve all existing guards and the atomic transaction. Record its exact signature in the task report.
- Modern `memoryGet` retains `currentGiB,totalGiB,availableGiB,maximumGiB,recommendedGiB,stamp` fields.

- [ ] **Step 1: Add failing policy, real-file save and browser regressions.** First reproduce that total64 with available12 permits manual28, and that reading current28 does not clamp it. Add table cases total16→recommend4/max12,32→8/24,64→16/48,128→16/96,63→16/47,0→0/0; equal totals with available1/12/50 must have equal recommendation/max. A representative native assertion is:

```csharp
var low = LauncherMaintenance.CalculateMemoryInfo(64, 1);
Check(low.MaximumHeapGiB == 48 && low.RecommendedHeapGiB == 16,
      "low available memory cannot reduce next-launch budget");
```

Before that new interface exists, a reflection regression can fail with an explicit missing-policy assertion rather than a compilation error. Real save fixtures retain `-Dcustom=keep`, another Java agent, all Windows branches, exact old backup, duplicate Xmx normalization and Xms lowering. Browser test renders low-available memory, selects28, verifies save enabled and sends `{value:28,stamp:'memory-low'}`. Refresh does not truncate dirty or current values; an existing over-total-budget heap is shown unchanged with a warning and cannot be saved outside the fixed budget.

- [ ] **Step 2: Run RED and save evidence.** Build/run `Build-Tests.ps1 -Entry PZ3D.MemoryPolicyTests -Output artifacts/1.1.1-memory-tests.exe`; run the focused browser regression. Capture the actual failing behavior before modifying production code.
- [ ] **Step 3: Implement the total-only policy and retain save safety.** Use the following independently specified numerical contract:

```csharp
int reserve = Math.Max(4, (int)Math.Ceiling(totalGiB * .25));
int maximum = Math.Max(0, totalGiB - reserve);
int starting = (int)Math.Round(totalGiB / 4.0, MidpointRounding.AwayFromZero);
int recommended = maximum == 0 ? 0 : Math.Min(maximum, Math.Max(1, Math.Min(16, starting)));
```

Validate nonsensical provider values rather than allowing integer overflow. OS total remains floor integerGiB; available display is not rounded into installed capacity. Remove the available-minus2 branch and the pre-replace free-memory refusal. Maintain validation of total budget at save time, exit requirement, all argument normalization, external-byte checks, reparse checks and exact atomic backup. No memory allocation or Windows pagefile change.

- [ ] **Step 4: Update both interfaces.** Modern metrics separately show “系统可见总内存”, “当前系统可用”, “基础推荐”, “设置上限（按总量）”. Low available memory adds a nonblocking advisory, no mandatory modal and no disabling. Initial modern/legacy input always shows actual existing heap even if above budget; legacy NumericUpDown may have a display maximum of `max(current,budget)` but saving remains native-budget-validated. On normal legacy page load, automatically read without applying anything. Existing over-budget modern slider may display a bounded thumb but must not write its bounded value into the number/current/draft. Preview32 recommends8, not12. Preset enablement is total-only, dirty/conflict rules retained.
- [ ] **Step 5: Run focused GREEN, inherited maintenance and browser suites, then native full self-test once.** Do not parallelize full browser/native processes because the old baseline depended on machine free memory. Preserve output under task-specific files. Commit scoped changes as `fix: budget game heap from total physical memory` and write the report.

### Task 2: Bounded Steam subscription propagation and download errors

**Files:**
- Modify: `Orange3DTool/SteamWorkshop.cs`, `Orange3DTool/SteamLauncherNative.cs`, `Orange3DTool/WorkshopWorker.cs`.
- Modify/Test: `Orange3DTool/WorkshopSubscriptionTests.cs`, `Orange3DTool/SteamLauncherServicesTests.cs`, `Orange3DTool/UiUpdateTests.cs`, `Orange3DTool/JournalPrivacyTests.cs` where relevant.
- Create/Test: `Orange3DTool/SteamSubscriptionWaitTests.cs`.
- Modify: `Orange3DTool/Program.cs` self-test registration only.

**Interfaces:**
- Keep `SteamWorkshop.EnsureSubscribed(ulong,Func<ulong,uint>,Action<ulong>)` for compatibility, routing it through the shared bounded implementation.
- Produce overload consuming `Action pump,CancellationToken cancel,Action<int,string> progress`; production wait uses monotonic elapsed time,15,000ms deadline and waits at most100ms per iteration. A small injectable elapsed/wait boundary may accelerate tests; no mutable global test hook.
- Native `SteamLauncherNative.Pump` retains its download result dictionary; `SteamWorkshop.Fetch` needs a bounded dictionary of matched selected-item results consumed by the later download phase. Both entry points use the shared subscription wait.
- Worker emits JSON `kind:error` with sanitized explicit failure information on stdout; non-JSON native stdout/stderr is not propagated/persisted wholesale. Existing structured `progress`/`paths` messages remain compatible.

- [ ] **Step 1: RED with delayed state and callback preservation.** Before modifying implementation, extend the current state/subscribe delegates so successful subscription returns before state bit1 appears. Ensure existing subscribed state calls no subscribe, delayed bit needs callback pump, result rejection/mismatchedid remains rejection, cancellation stops, a never-set flag times out and does not return paths. Representative regression:

```csharp
int reads = 0, requests = 0;
SteamWorkshop.EnsureSubscribed(3810637496UL,
    id => ++reads >= 3 ? 5U : 4U,
    id => requests++);
Check(requests == 1 && reads >= 3, "successful subscribe tolerates delayed client flag");
```

Add real shared wait-driver fixtures for pumping matching download completion during subscription and retaining it for download consumption; reject foreign app/id/result. Worker process fixture returns deliberate UTF8 validation failure and injected native stderr SteamID; primary error must remain readable and diagnostic store must not contain the full ID. Avoid assertions that merely grep source strings.
- [ ] **Step 2: Run targeted RED tests and record output.** Compile/run `PZ3D.WorkshopSubscriptionTests` and new wait tests under `artifacts/1.1.1-subscription-*`; no SteamAPI connection or actual item mutation.
- [ ] **Step 3: Implement bounded propagation.** Subscription callback validates EResult1 and correctid before flag wait. Pump before subsequent checks, call cancel before each check/wait and inside callback drain, throttle “等待 Steam 同步订阅状态” to once/sec. Timeout is a specific subscription synchronization failure; preserve existing45/30-second request-result limits. Use an elapsed deadline rather than assuming a fixed sleep caused success:

```csharp
cancel.ThrowIfCancellationRequested();
if ((state(id) & 1U) != 0) return;
subscribe(id); // validated successful result at the native boundary
// Until bit1, pump selected callbacks; deadline, progress and cancellation govern every pass.
```

Don't lose simultaneous DownloadItemResult callbacks. Capture only relevant requested IDs/App108600, validate actual result before accepting installation; calling DownloadItem again must not erase an already-received valid result without another confirmed completion. Existing cached installed+subscribed+notpending item may be reused without a fresh download; display “使用已存在的本地文件”. `subscriptionsOnly` must not claim download complete or invent unavailable installinfo. End summary distinguishes actual download vs local reuse. Existing component structure and native service ownership/InstallInfo root checks remain.
- [ ] **Step 4: Preserve specific worker failures.** Bounded structured exception first, append child-exit/released-identity secondary. Don't store raw native stderr or SteamIDs. Exceptions, progress and task diagnostics include safe itemid/phase/state/elapsed, never credentials. A nonprotocol child exit gets a clear generic native-worker phase failure, not unbounded stderr. Keep cancellation cleanup scoped only to the owned worker.
- [ ] **Step 5: GREEN, targeted worker/privacy/native service tests and full native suite once.** Commit `fix: wait for Steam subscription state propagation`; report RED/GREEN and explain that synthetic API boundaries are not real affected-player Steam validation.

### Task 3: Scoped launch-options parser and exact Buddy conflict policy

**Files:**
- Create: `Orange3DTool/SteamLaunchOptions.cs`, `Orange3DTool/BuddyLaunchArguments.cs`.
- Modify: `Orange3DTool/Program.cs` (Engine.Guard/Agent and self-test registration), `Orange3DTool/JavaApprovals.cs`, `Orange3DTool/ModernController.cs` (LaunchStamp only).
- Create/Test: `Orange3DTool/SteamLaunchOptionsTests.cs`, `Orange3DTool/BuddyLaunchArgumentsTests.cs`.
- Modify/Test: `Orange3DTool/JavaApprovalsTests.cs`, `Orange3DTool/ModernControllerTests.cs`, `Orange3DTool/Test.cs` where existing guard assumptions require adjustment.

**Interfaces:**
- Produce `SteamLaunchOptionsSnapshot` with internally bounded Steam root, active account id, fixed config path, app108600 field span, decoded LaunchOptions, raw file snapshot/encoding and known-identity state. Never serialize raw options/accountid/fullVDF into the UI or ordinary logs.
- Produce `SteamLaunchOptions.Current()` for registry/process-backed read-only current-account discovery and `SteamLaunchOptions.Read(string root,uint account)` for native bound fixtures. Root/account inputs belong only to native code/tests, not UI payload.
- Produce `SteamLaunchOptions.Replace(SteamLaunchOptionsSnapshot snapshot,string replacement)` returning rewritten bytes using the captured file, preserving every byte outside that exact field. Fresh Steam exit content is re-read by Task4 before constructing the snapshot to replace.
- Produce `BuddyLaunchArguments.Classify(string argument)` returning `None`, `Known`, or `Unknown`; produce `BuddyLaunchArguments.CleanCommandLine(string commandLine)` with safe changed result plus sanitized Buddy-only summary, refusing ambiguous syntax/unknown Buddy options. Produce conservative JSON/BAT cleanup transforms with the same classification. Record precise return types/signatures for Task4 in report.
- Produce current-game scoped guard evidence used by Engine/JavaApprovals and native startup stamp. Tests use native injected snapshots/read-only roots and must not read other real accounts.

- [ ] **Step 1: Fail behavioral parser and guard regressions.** Fixtures must include current accountApp108600, another account108600, current accountanotherapp, unrelated text mentioningZombieBuddy, comments/escaped quotes/spaces, missingLaunchOptions, duplicate keys, malformed/braceless/oversizedVDF and reparse paths. Expected edited bytes are literal fixtures, not generated by the parser under test:

```csharp
string vdf = "\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { "
  + "\"999\" { \"LaunchOptions\" \"-javaagent:ZombieBuddy.jar\" } "
  + "\"108600\" { \"LaunchOptions\" \"-Xmx28g -javaagent:ZombieBuddy.jar -Dkeep=yes\" } } } } } }";
// Replace only app108600 field; app999, layout and memory bytes remain exact.
```

Classify true `-javaagent:ZombieBuddy.jar`, quoted exactbasenameabsolute paths, `-agentlib:zbNative`, exact nativeDLLagentpath and duplicates; reject or preserve unknown customagent options/directories, lookalikeJar names, `-Dnote=ZombieBuddy`, unrelatedagentlib/Javaagent and filenames merely containingzbNative. Legitimate already-normal JSON `-agentlib:zbNative` with no external conflict is not refused. Unknown own user.home/configdir override still blocks Javaapproval; another app's agent/override does not. UnrelatedVDFrefresh leaves startup snapshot stable, changed own LaunchOptions changes it.
- [ ] **Step 2: Record RED before parser/guard implementation.** New tests may use reflection to assert missing interfaces; once compiles, tests must exercise actual parser and transforms. Run focused tests with `Build-Tests.ps1`, no real registry writes or Steamfile edits.
- [ ] **Step 3: Implement a bounded, edit-preserving VDF reader.** Parse hierarchy case-insensitively with strict quoted tokens, braces, supported comments/escapes and span offsets. Fixed path is `UserLocalConfigStore/Software/Valve/Steam/apps/108600/LaunchOptions`; reject duplicate ambiguous hierarchy/fields instead of selecting one. Preserve encoding/BOM and all untouched content. Bound existing16MiBfile limit, byte/text parsing and path reparse guards. Discover ActiveProcess.ActiveUser read-only from the correct validated Steamclientroot; unknown/changed identity not guessed from folder enumeration. Distinguish absentfield from unsupported/malformed/unknownaccount, don't fabricate a cleared result.
- [ ] **Step 4: Implement narrow Buddy policy.** Recognize exact filenames/tokens case-insensitively, parse quoting without interpreting commands, preserve nonBuddy args. Only known optionless legacyBuddyJava/native args are removable automatically; nonempty/custom options that can alter authorization directories are Unknown and refused. Existing normal JSON nativearg remains valid and Engine.Switch still installs/manages it. BAT transform only unambiguous active invocation/known variableassignment; preserve comments and reject `%`/delayed expansion/caret/pipes/multiple ambiguous commands involving Buddy rather than guessing. Keep unrelated Javaagent/native params intact.
- [ ] **Step 5: Integrate read-only scoped guards and stamp.** Replace all-account regex scans in Engine.Guard and JavaApprovals.Guard. The latter still refuses actual own unknownauthdirectoryoverride; don't authorize a jar automatically. `LaunchStamp` includes exact bounded current-game LaunchOptions plus account/root identity, not wholeotheraccountVDFbytes. Engine.Guard reports actionable Buddy conflict; actual takeover arrives Task4. Scope existing Agent-removal to recognizedBuddy args, refusingUnknown before mutation. Code example boundary:

```csharp
SteamLaunchOptionsSnapshot options = SteamLaunchOptions.Current();
// Read own app only; callers must distinguish KnownIdentity from unavailable discovery.
// Hash own app value + native identity, never another game's option field.
```

- [ ] **Step 6: GREEN and inherited guard/approval/controller/installer tests plus full native self-test.** Commit `fix: scope Buddy launch conflict detection to active Steam game`; report all exact interfaces for Task4 and explicitly note any remaining UI integration boundary.

### Task 4: Confirmed recoverable Buddy takeover and visible workflow

**Files:**
- Create: `Orange3DTool/BuddyTakeover.cs`, `Orange3DTool/BuddyTakeoverTests.cs`.
- Modify: `Orange3DTool/ModernController.cs`, `Orange3DTool/ModernHostPolicy.cs`, `Orange3DTool/LauncherFeatures.cs`, `Orange3DTool/Program.cs` (help/guard/selftest), `Orange3DTool/ModernHost.cs` only if confirmation belongs at native boundary.
- Modify: `Orange3DTool/Web/app.js`, `Orange3DTool/Web/style.css`.
- Create/Test: `Orange3DTool/Web/buddy-takeover-ui-tests.cjs`.
- Modify/Test: `Orange3DTool/ModernHostTests.cs`, `Orange3DTool/ModernControllerTests.cs`, `Orange3DTool/LaunchProfilesTests.cs` as needed.

**Interfaces:**
- Consumes exact Task3 scoped parser/classifier interfaces from its report.
- Produce `BuddyTakeover.Preview(Engine)` and `BuddyTakeover.Apply(Engine,string stamp,bool confirmed,bool steamExitConfirmed,Action<int,string> progress,CancellationToken cancel)`. Preview returns only opaque stamp, safe conflicts/locations, `requiresSteamExit`, knownidentity/support/recoverystate and safe explanation. Native service owns actual paths/account/process details.
- Produce `BuddyTakeover.Pending(Engine)` / read-only recovery state for startup and `BuddyTakeover.Recover(...)` explicit guarded retry/recovery using current files. No silent overwrite or Steamexit at startup.
- Controller actions `buddyPreview`, `buddyTakeover`, `buddyRecover`; only `buddyPreview` is a NativeRead. Apply/recover are local mutations, disallowed concurrently with otherlocalwrites/networkSteamworkshopmaintenance or pendinglaunch; payloads accept only stamp and explicit bool confirmations, never paths/account/executable/commands. NativeHost rejects extra fields and wrongtypes.
- A small Steam process environment boundary injects activeidentity capture, correctexe/processbirth/path, gracefulexit/reopen and monotonic wait into fixture execution. Default production implementation uses validated Steamclientexe with fixed `-shutdown`, at most30,000ms, never Kill; reopening uses samevalidatedexe without login/gamearguments.

- [ ] **Step 1: RED transaction fixtures.** Test cancel/falseconfirmation nofilewrite/noSteamexit; onlygameJSON/BAT neverexitsSteam; currentappVDFedit causes exitbeforefreshread then reopen; unrelatedcontentflushedduringexit retained; targetlaunchoption/account/processidentitychange rejectsstalepreview; timeoutdoesnotwrite; gamestillrunning/pendinglaunch/workshopmaintenance refuses; wrongUIfieldcannotchooseanotherpath. Realfixturefiles prove partialwritefault rollsbackexactownbytes, outsidechangedbytes survive, interruptionjournal isdetectableafterserviceconstruction. Backups privatetoowner retainexactbytes, regularUI/logsneverexposepasswords/accountid/fulllaunchoptions. Representative contract:

```csharp
// Fake only OS/Steam boundaries. Real Preview/Apply write real isolated fixture files.
var preview = BuddyTakeover.Preview(engine);
// confirmed=false => unchanged game JSON/VDF and zero process shutdown/reopen events.
// confirmed=true + required exit approval => scoped cleanup; unrelated args remain exact.
```

Browser fixtures simulate pending conflict→confirmation→waitingSteam→writing→completed, cancelled/failed/stale/partial recovery. Verify noapplyonpageopen, twoexplicitconfirmationswhenSteamexitrequired, installationretriesonlyaftervalidatedtakeover and re-read, noautoJavaauthorization. Assert actualsentpayload/resultstates, notsourcegreps.
- [ ] **Step 2: Run RED tests before native/UI implementation.** Add host-policy regressions first against missing actions; compile new native fixtures and run browserfixture. Keep realSteam boundary disabled in tests even for syntacticallyvalidgamefolders.
- [ ] **Step 3: Implement preview/identity binding and preflight.** Takeover bypasses conflicting Engine.Guard only by a narrow own preflight which enforces configexists, selectedgamerunning, pendinglaunch and workshopmaintenance, NoReparse paths and a named per-selectedgame mutex. Bind previewstamp to gamebytes/knownBuddyarguments/currentaccount/appLaunchOptions/Steamclientidentity; UI supplies noarbitraryidentity. UnknownBuddycustomoptions/ambiguousBAT marked unsupported and neverremoved. Preserve normalJSONnativeBuddyconfiguration. Limit supportedtargets to selectedgameProjectZomboid32/64.json/.bat and boundactiveaccountVDF. No deletingmodpayload or memorysettings.
- [ ] **Step 4: Implement graceful exit and atomic transaction.** Before anyexit revalidate identity; noSteamexitifonlygamefiles. After correctclientexits, re-readVDF and ensure targetvalue unchanged, retainunrelatednewbytes. Check game/identity/externalchanges again before writes. Store private exactbackups and durable bounded stagejournal under owneddata/backups, use File.Replace/Move asappropriate, verifynewbytes. On anyfault restoreonlyfilesstillmatchingourwrittenbytes; foreignchanges become recoveryconflicts. Treatpartialchange/reopenfailure honestly; reopening sameclient is attempted onlyifthistransaction actuallyrequestedexit, evenonpost-exiterror, noautogamelaunch. Monotonicwait30s with cancellationchecks. No blocking cancellation after unsafehalfwrite: finish rollback/safephase then cancelledstatus. Record stages/results through existingjournal with sanitizedsummaries. Interruptedtransactionstartuponlyreports guardedrecovery/manualsteps; don't autoexit or autoauthorize.
- [ ] **Step 5: Add concise UI/legacy entry and install conflict affordance.** Maintenance shows “扫描旧启动参数” and actionable “接管旧启动参数”; componentpage installconflict alsooffersit, notonlygenericerror. PreviewlistsrecognizedBuddyarguments/location butnotunrelatedrawLaunchOptions. Confirm migration scope, then separately confirm one-timeSteamexitifrequired. Display waiting/cleanup/reopen/completed/partial/recovery and cancelviaexisting taskcenter/progress. Freshread aftereachend; “旧参数已接管” doesnotmeancomponentsfullyinstalled; actualinstaller/statusvalidation governsconfiguration. Offer continueoriginalinstall aftersuccessfultakeoverwithoutautostartgame. Both themes,820x600/highresolution maintainreadability/accessibility; memorytask1labels remaincorrect. Update helpmanual removalguidance forsafe takeover vsunknownmanualcase.
- [ ] **Step 6: GREEN native transaction/host/controller, focusedbrowser and inheritedfullnative/browser suites once.** Commit `feat: safely take over legacy Buddy launch parameters`; report precise failure/recovery behavior and screenshot paths.

### Task 5: Version 1.1.1, inherited self-update acceptance and deliverables

**Files:**
- Modify: `VERSION`, `CHANGELOG.md`, `使用说明.md`, `docs/core-release-verification.md` (append new version evidence, preserve1.1.0).
- Modify/Test: `Test-CoreNativeUpdate.ps1`, `Test-CoreLegacyStartup.ps1`, `Test-CorePackageBuild.ps1` only to remove old hardcodedfixtureversions or include new assets/tests safely.
- Modify: release allowlist/build scripts only if genuinely required by compiled-native additions. Parser/migration C# compiles intoexistingmain/helper/uninstaller EXEs; no thirdparty DLL introduced.
- Create: `docs/releases/1.1.1-verification.md` and `artifacts/1.1.1-*` evidence/output.

**Interfaces:**
- `VERSION` authoritative `1.1.1`; EXE assembly/file/UI/installrecord/manifest version follows existingBuild-Version/Release scripts.
- Keep official embedded publictrust `release-public-key.xml`; sign with existing external `E:/Projects/OrangeLauncher-Signing/release-private.xml` without reading/printing/changing it. Publickey SHA256 stays `943D7FC795E52E9C43AD1587CEF15457C1B8153130496C47BED4EB58044C355C`.
- Output unique releasefolder initially `dist/1.1.1-verified`; if occupiedbyfailedattempt keep it and selectuniquesuffix, neveroverwrite acceptedorfailed evidence.

- [ ] **Step 1: Version/release regression.** Before versionchange buildcurrentcode and runfocusedmetadata/read fixture confirmingcurrent1.1.0 source. Add acceptance expecting1.1.1 acrossrequiredartifacts; recordexpectedRED. Avoid testinghumanprose bygreppinganexactline. Changelog/help are reviewed ratherthanunit-testedastext.
- [ ] **Step 2: Update version and concise Chinese changelog/help.** Explain total-memoryrecommendationisstartingpoint, lowfreewarningonly; onlinesubscriptionboundedwait; takeoveruserconfirmationone-timeSteamexitonlywhenneeded. State ordinaryinstalldoesnotrequireSteamexit. Keep thirdpartyshader exclusion and scopepreservation. Source stays featurebranch; noremoteupload.
- [ ] **Step 3: Run complete native and browserregression, captureboth themes.** BuildfullConsole --self-test plus newtests; run eightexisting browserfilesandnewmemory/takeoverfiles sequentially. Use actualdefault modern andlegacyBuildoutputs/capturetests, verifyexeversion. Inspect actualmemory/migrationcaptureimages for layoutnotjustexitcodes. No realSteam/accountmutation.
- [ ] **Step 4: Run inheritedsignedupdatefixtures.** GeneralizeTest-CoreNativeUpdate A=currentVERSION, B=nextpatchfixtureversion; adaptnativehealth/captureassertionsratherthanforcingpublished1.1.0. Run signedfixturemanifest/install/scopeduninstall, sparse/fullupgradehealthy/failed andinterruptedordinary-startuprecovery, originalprocessbirth/oneoldrestart assertions. Preserveexistingownership/trustguardsandoutcomehistory. Same officialtrust remainsnativeembedded; syntheticreleasekeys areconfinedfixturesonly.
- [ ] **Step 5: Build new formalassets.** Use Build-CoreRelease.ps1 withofficialexternalSigningKey andnewOutputDirectory. Validate signedmanifest/package usingCoreReleaseTool, inspect ZIPforonlyownedallowlistfiles/no shaderZIP/nooriginalmodpayload/userdata. Verify installeranddirectfolderdeployment,uninstallpreservation, metadata1.1.1 andSHA256record, deltafileassets present. Confirmprivatekeypathnotinsideoutputandrepopublickeyunchanged. Read ordinary officialRelease stateonlyifneeded; neverupload/push/mergeaspartofthistask.
- [ ] **Step 6: Commit and write evidence.** Commit `release: prepare verified launcher 1.1.1`; reportcommands/exits/assertioncounts/screenshots/packagepaths/hashes. If realaffectedplayerSteamonlinereproductionwasnotrun, sayso explicitly. Finalwholeworkreview mayrequireoneconsolidatedfixwave; anycodechangeafterpackaginginvalidatespackageacceptance andmustberebuilt/reverified intoanotheruniquefolder.

## Coordinator final acceptance

Taskreviews are scoped. One final broad review uses the complete newworkrange beginning at `a5385ba`, approveddesign and deferredfindingledger, on the mostcapableavailablemodel. Existing1.1.0 workalreadyaccepted is inheritedcontext, not a secondoldtaskloop. Resolve finalfindings in oneconsolidatedfixwave and one scopedrereview, then independently rerun coveringtests and refreshallassets affectedbyanyfix. No successclaim beforefreshoutputs. Preserveplanreportsevidence recoverably inartifacts afterfinalacceptance, retainingdurablecommitrecord; leaveolderplansalone. No mainmerge/push/Releaseupload is authorized in this repair scope.
