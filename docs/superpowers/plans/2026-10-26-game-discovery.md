# Game discovery and launch classification implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Find ordinary Steam installs despite missing registry/library records and stop confirmed Buddy false blocks.

**Architecture:** Keep deterministic discovery APIs and add environment enumeration only at startup. Reuse bounded path resolution for preferences and manual selection. Separate read-only launch classification from cleanup rewriting.

**Tech Stack:** Existing C#/.NET Framework native launcher, PowerShell builds, diagnostic fixture runner.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-26-game-discovery-design.md`.
- Active checkout: `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher`; base `ebe2072`.
- Background only: no app/game/Steam/native SDK invocation or UI, no real-path controller/prepare/stage/recover operations. Temporary fixtures and injected evidence only.
- Preserve all foreign dirty site/deploy files and the completed loading changes. No server/release/version changes. Do not overwrite candidate4.
- Each task records red/green evidence, commits only owned files, and receives a fresh review before the next task.

### Task 1: Bounded automatic and manual game discovery

**Files:** modify `Orange3DTool/GameDiscovery.cs`, modern/legacy startup in `ModernHost.cs` and `Program.cs`, `ModernController.cs` setGame, `DiscoveryTests.cs`; add focused fixture tests only if the existing file becomes unwieldy. Register added diagnostic test source/runner if necessary.

**Interfaces:** retain `Find(string[] steamRoots)` as deterministic explicit-input lookup. Add `Detect()` for registry plus finite fallback roots, `ResolveSelection(string selected)` for exact/root-parent resolution and `InitialGame(string preferencePath)` for bounded preference-first startup; internal overloads may inject environment roots/drive information for tests. No UI action or network dependency is required.

- [ ] Add failing fixture tests. Representative assertions:

```csharp
Check(GameDiscovery.ResolveSelection(library)==game,"selected library resolves its custom install folder");
Check(GameDiscovery.ResolveSelection(Path.Combine(library,"steamapps","common"))==game,"common parent resolves through manifest");
Check(GameDiscovery.ResolveSelection(Path.Combine(game,"ProjectZomboid64.exe"))==game,"exact exe resolves containing game");
Check(GameDiscovery.InitialGame(preference, new[]{steam}, new[]{fallback})==fallbackGame,"stale roots use injected conventional fallback");
```

The overload in the last assertion is internal and must not read host registry/drives. Also cover modern/legacy VDF, custom Chinese/space folder names, missing exe/JSON, oversized/locked VDF then later good root, malformed manifest dot/dotdot/trailing/absolute/separator components, valid saved preference, stale/locked/oversized preference fallback, reparse refusal and no unrestricted descendant search. Test missing registry via injected empty registered roots and valid conventional roots, not the developer machine's actual Steam installation.

- [ ] Compile/run focused tests against the new expectations first; retain the failing output under `artifacts/game-discovery-20261026/`. Then implement minimal shared resolution with the spec's limits. Use a single bounded resolver per request, canonical deduplication, `LauncherMaintenance.NoReparse`, strict/bounded UTF-8 metadata reads, finite roots and Stopwatch checks; keep the old wrapper's return `""` on failure.

```csharp
// Integration shape; helper reads a preference once and falls back safely.
string game = GameDiscovery.InitialGame(Path.Combine(data,"game-path.txt"));
// setGame resolves before atomic persistence and Engine replacement.
string path = GameDiscovery.ResolveSelection(Text(p,"path"));
if(path.Length==0)throw new Exception("未找到游戏，请选择 Project Zomboid 游戏文件夹或 Steam 游戏库。");
```

- [ ] Use the shared helper in actual modern/legacy startup while preserving capture mode's isolated fixture path. Use `Detect()` in `Engine.DetectGame()`. Resolve modern manual selection and align recovery-owner validation with the resolved path without bypassing pending transaction/network guards. Update folder-picker description to allow game/Steam library; no new warning banners or implementation details.
- [ ] Run focused tests green, fixture-controller selection tests (including error does not persist/switch), then native suite if necessary to catch integration regressions. Check status and commit only task files. Write report to this plan's task-1-report.md including actual commands/counts and residual limitations (arbitrary unlisted custom folders still need manual selection).

### Task 2: Read-only Buddy launch validation without cleanup false positives

**Files:** `Orange3DTool/LauncherLaunch.cs`, `LaunchPreflight.cs`, `BuddyLaunchArguments.cs` only if shared validation is needed; relevant `LauncherLaunchTests.cs`, `LaunchPreflightTests.cs`, `BuddyTakeoverTests.cs` or a focused registered diagnostic test class.

**Interfaces:** existing `PreparationReason(Engine,bool,bool)` and `LaunchPreflight.Check` signatures remain. Keep `CleanJson` cleanup behavior unchanged. Any new read-only validator must not rewrite JSON or execute native Steam.

- [ ] Add failing temporary fixture cases using inert EXE/JAR/DLL markers and injected Steam options. Cases: zero loader with two `-D...=zbNative` properties and no Buddy files is allowed; one root native stays allowed; one single Windows branch native is allowed; root duplicates, root+branch native, two branch occurrences, unknown native options, duplicate JSON keys, malformed arrays and noncanonical legacy Buddy remain refused. Snapshot all fixture bytes to prove read-only validation.

```csharp
Check(LauncherLaunch.PreparationReason(e,false,false)=="","ordinary properties are not Buddy loaders");
Check(LaunchPreflight.Check(e,false,false).canLaunch,"one supported branch native loader is valid");
Check(!LaunchPreflight.Check(duplicate,false,false).canLaunch,"actual duplicate remains blocked");
```

- [ ] Capture red output before edits. Replace cleanup `Changed` as launch danger with read-only argument validation. Use `BuddyLaunchArguments.Classify`/exact canonical loader identity; retain syntactic `CleanJson.Safe` or an equivalent shared strict parser. Preserve owned startup agent preparation, required files and all actual guards. Do not claim or implement native alternative-Windows-branch selection.
- [ ] When config fails, keep its error and return a skipped/warning dependent guard message such as `请先修复游戏启动配置，再检查启动参数。`; do not call guard and do not imply an independent Steam failure. Pending recovery retains its existing dependent-warning behavior. Valid configuration still reaches the real guard boundary (test with injected evidence).
- [ ] Run focused red-to-green and native full regression; rerun relevant frontend contract tests only where status shape/message is asserted. Review diff for writes/guard weakening. Commit task files and report actual commands/counts to task-2-report.md.

## Completion

- [ ] Broad combined review over `ebe2072..HEAD`, resolving material findings before packaging.
- [ ] Build a fresh candidate5 using a new copy of the candidate4 artifact builder with a new output path. Verify embedded Java payload hash remains `97F3811414D0FFA2FE19DAC318F06F927BCEFA7A6AD663FB471A586D56656745`, fresh Web/app.js hashes and ZIP EXE hash. Do not run candidate entrypoint.
- [ ] Record quality evidence and concise Chinese delivery. Automatic lookup coverage is improved; exact remote diagnosis and actual user acceptance remain unverified. Public website/software release remains unchanged.
