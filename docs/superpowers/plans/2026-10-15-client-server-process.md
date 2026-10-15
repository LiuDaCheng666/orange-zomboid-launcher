# Client/server process distinction hotfix implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct the confirmed dedicated-server false positive and deliver signed OZL 1.2.5 without stopping the server or weakening file guards.

**Architecture:** Preserve the existing broad runtime occupancy guard, introduce a distinct client guard and Java entry-point classification, and route only client-specific consumers to it. Keep file-mutation and recovery consumers conservative. The coordinator handles publication after code and package review.

**Tech Stack:** C#5/.NET Framework, Windows process/WMI evidence, existing WinForms/WebView, Node browser regressions, PowerShell trusted release scripts.

## Global Constraints

- 用户已确认客户端／专用服务器分开识别并要求立即修复及发布；规格 `docs/superpowers/specs/2026-10-15-client-server-process-design.md`。
- 保留原 IsRunning(game) 的保守运行时占用语义，继续保护文件修改；新增 IsClientRunning(game)，仅在证据明确为专用服务端时排除它。
- 不结束玩家游戏、专用服务器或 Steam；不修改真实游戏、存档、凭据、桌面、注册表、分页、运行中的安装目录或 Steam 服务。
- Java 原始命令行不写日志、不上传；信息缺失／PID 身份不匹配不等同无进程。
- 版本1.2.5，OZL / Orange zombiod launcher，内部旧EXE名、18条路径、原封面与正式公钥不变；不覆盖历史资产或标签。
- 用户已有当前目录修复分支偏好；复用当前 checkout 的 fix/client-server-process-125，不额外复制项目。
- 原生编译串行；只使用隔离 TestFixtures 处理副作用。已有私钥仅作为路径交给构建工具，不能读取／输出／复制／生成密钥内容。

## Task 1: Correct process roles, preserve all guards, and prepare 1.2.5

**Files:**
- Modify `Orange3DTool/GameProcessGuard.cs`, `GameProcessGuardTests.cs`, `PlaytimeTracker.cs`.
- Modify narrow consumer locations in `ModernController.cs`, `Program.cs` (Engine), `LaunchPreflight.cs`; preserve mutation consumers in `BuddyTakeover.cs`, `SteamLauncherNative.cs`, `SteamLauncherServices.cs`, `SaveDeletion.cs`, `LaunchProfiles.cs`, `PlayProfiles.cs`.
- Extend real behavioral tests in existing process, preflight/controller, playtime/profile test files as needed; new focused test file allowed if responsibility warrants it.
- Update VERSION, CHANGELOG.md, add `docs/releases/1.2.5-release-notes.md`; do not claim GitHub publication before coordinator actually publishes.
- Create a repeatable runner and package evidence under `artifacts/client-server-125/`, fresh output `dist/1.2.5-github-release/`; no existing output overwrite.

**Interfaces and decisions:**
- `MatchesJava(game,name,image)` stays the existing exact runtime-path predicate; it does not alone prove a client.
- Keep `IsRunning(game)` as broad occupancy. Add `IsClientRunning(game)` for status, duplicate-client protection and normal launch only.
- Introduce a focused pure Java command-line classifier, e.g. `ClassifyJavaCommandLine(string)` returning Client/Server/Unknown. Exact helper type naming is implementation-owned; tests use literal independent expectations.
- Read PID-specific command-line evidence only after matching the runtime image. Validate same process identity/start-time at the exclusion boundary; inaccessible/unknown evidence keeps client launch guarded but never starts playtime.
- Add `Engine.GuardLaunch()` through existing GuardCore checks; only its runtime-running predicate is client-specific. `Guard()` and `GuardPlayProfile()` stay broad.
- `ModernController.Check(bool launch=false)` (or equally narrow helper) must retain cancellation, NoReparse, pending-session and engine guards. Only normal `case "launch"` uses launch=true. `profileLaunch` and mutation cases remain broad.
- `LaunchPreflight.Check` uses `GuardLaunch` only for targetKind normal. Its other checks stay intact. Status running uses IsClientRunning; no UI redesign needed.
- PlaytimeTracker.Find must use confirmed client evidence, not path-only MatchesJava, for Java. Keep native image-path and start-time checks, disposal and accounting.
- Expire/recover/stage and profile transaction callbacks must still use broad occupancy. Existing callers of IsRunning therefore remain safe by default.

- [ ] **Step 1: RED real role regression.** Extend GameProcessGuardTests through reflection if the new classifier is initially absent; retain all existing path cases. Include literal full commands with actual main class and decoy appearances in -D / classpath. Example behavior:

```csharp
// Literal expectations, never computed by the classifier.
// "java.exe -cp projectzomboid.jar zombie.network.GameServer" => Server
// "java.exe -Dnote=zombie.network.GameServer -cp projectzomboid.jar zombie.gameStates.MainScreenState" => Client
// "java.exe -cp zombie.network.GameServer zombie.gameStates.MainScreenState" => Client
// "java.exe -jar unknown.jar" / null / unknown options => Unknown
```

Run `./Build-Tests.ps1 -Entry PZ3D.GameProcessGuardTests -Output artifacts/client-server-125/process-tests.exe`, then execute it. Preserve expected RED output, not just compilation failure.

- [ ] **Step 2: Minimal classifier and safe OS evidence.** Use a bounded Windows-argument parser or existing equivalent; correctly skip Java options and their operands, identify the actual main class, and refuse ambiguous forms. Do not treat arbitrary substring matches as server evidence. Never log the raw command. Preserve broad occupancy as-is and implement client-specific checks plus confirmed-client tracking. Tests cover inaccessible/unknown evidence and path aliases without creating test-only production utilities.
- [ ] **Step 3: Integrate normal-launch checks.** Add the launch-only engine/controller/preflight path above, leaving mutation and recovery paths broad. Add behavioral tests that would fail if broad occupancy were replaced globally, server were again counted as a client, preflight bypassed safety checks, or playtime accepted a server. Do not launch real Steam/game. Controlled OS helper processes are allowed only inside owned TestFixtures with clear identity and natural/scoped cleanup.
- [ ] **Step 4: Covering GREEN and full regression.** During edits run focused suites; once stable run serial GameProcessGuardTests, LaunchPreflightTests, ModernControllerTests, ModernHostTests, PlayProfileTests (actual entry name), launch/profile/recovery, SaveDeletionTests, LauncherMaintenanceTests, workshop and native services/playtime suites. Run the existing 18-suite native gate plus added changed-area entries, and all17 browser entry suites once. Record commands and child exit codes. No repeated whole-suite runs per small edit.
- [ ] **Step 5: Version and package.** VERSION1.2.5, concise formal changelog/release notes explaining client/server distinction and retained file-occupancy restrictions. Build new signed `dist/1.2.5-github-release` via existing `Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory ...`. Existing trusted key path only, no private content access. Adapt the prior reviewed candidate-proof helper into a fresh artifacts/client-server-125 helper for1.2.5 and old1.2.4: verify RSA, all18 lengths/hashes, original image, source equality, byte-identical portable/installer payloads, real isolated --verify-setup, actual old compiled parser acceptance. All process starts are fixtures/previews only. No external publication from implementer.
- [ ] **Step 6: Report and commit.** Write own workspace task-1-report.md with RED/GREEN, all counts/commands/outputs, exact modified files, live-read boundaries, final artifact paths/hashes and concerns. Explicit-path commit, no whole-directory staging of ignored evidence or raw logs. Release compiler ownership when done. If no safe interpretation exists for an OS identity race or a consumer contract, ask coordinator before weakening protection.

## Coordinator after task gates

Task-scoped review then whole-branch final review; fixes follow the selected workflow. Re-run final verification on reviewed source. Then normal main integration, immutable v1.2.5 annotated tag/source push, draft22asset upload, remote name/size/digest check, publish latest, anonymous public downloads and actual signed old1.2.4 default service query/download-to-Ready in fixtures. Document truthful boundaries; never apply a real player update or stop server. User explicitly requested this fix and publication, so this is not an inferred new external action.
