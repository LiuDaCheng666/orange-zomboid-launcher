# Game-window startup implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement task by task.

**Goal:** Render OZL loading and live activity inside Project Zomboid's game window, with a refined animation and truthful progress.

**Architecture:** A bundled session-gated Java agent instruments early loading and the existing game render thread. A native deployment/observer service stages the owned component and sends only actionable analysis. The external WinForms loading presenter is removed from the production launch path.

**Tech Stack:** Java 17 source, Byte Buddy from hash-pinned development dependency, LWJGL game API compile-only, C# .NET Framework, PowerShell builds, existing log analyzer.

## Global Constraints

- Scope and protocol: `docs/superpowers/specs/2026-10-25-ingame-startup-design.md`.
- Game target for actual acceptance is local 42.21; no modified original game classes.
- Preserve unrelated dirty website/deployment files. No server/site publication in this implementation phase.
- Preserve all Buddy approvals and policy. Own agent may execute only its bundled code.
- Native code must not create a second loading window or change game focus/z-order.
- No fake progress/logs, generic explanation panels, or meaningless error floods.

## Task 1: Java component and real renderer (root)

Files: `Orange3DTool/StartupSource/ozl/startup/{Agent,Hooks,Session,LoadingModel,GameRenderer,Runtime}.java`, `tests/java/startup/`, `Build-StartupComponent.ps1`, `tests/IngameStartup.test.ps1`.

- [x] Write behavioral tests first: expired/wrong-root sessions stay inactive; mod/script events update bounded activity and real counters; menu ends boot; world entry starts a new loading operation; interactive state releases; an unrelated/noise record never appears on the right. Run and record expected RED before implementation.
- [x] Implement session parser and synchronized immutable snapshots. Example consumer: `model.phase("Loading Mods"); model.mod("ExampleMod"); model.menuReady(); assert !model.snapshot().active;`.
- [x] Instrument `GameWindow.DoLoadingText(String)`, `ZomboidFileSystem.loadMod(String)`, script execution, `MainScreenState.enter`, `GameLoadingState` lifecycle, and display swaps. Advice suppresses its own failures; game originals and security flows remain intact.
- [x] Render on the game context with explicit GL save/restore, bounded text textures, logo and restrained arcs; per-operation counters only where a real total exists. Pump idle frames at a bounded rate during boot.
- [x] Build using `javac --release 17`, copy only Byte Buddy runtime entries and its license from the pinned dependency, package own logo. No Project Zomboid classes in output.
- [x] Run headless model/session tests, transformer fixture JVMs, jar-content audit, and actual render-context integration checks. Commit only task files after passing.

## Task 2: Owned deployment, analysis exchange and production launch wiring

Files: new `Orange3DTool/GameStartupComponent.cs`, `GameStartupComponentTests.cs`; modify `GameStartupHost.cs`, `GameStartupPresentation.cs` or launch commit wiring as necessary, `LauncherLaunch.cs`, `BundledAssets.cs`, `build/ProductionSources.ps1`, and existing build/resource packagers.

Interfaces: bundled asset `OrangeStartup.jar`; `GameStartupComponent.Prepare(Engine e,string sessionId)` deploys/gates the component and adds the exact owned argument once. `GameStartupComponent.IsOwnedArgument(Engine e,string argument)` validates its identity and resource hash. Session and issue formats are defined verbatim in the spec. Production observer starts at committed launch, writes primary issue exchange, and does not instantiate `GameStartupWindow`.

- [x] Write and run failing file-effect tests for idempotent deployment, preserved foreign args, stale/foreign file refusal, current-session exchange, and absence of production external windows.
- [x] Implement transactional owned deployment with bounded files and no reparse traversal, preserving all unknown data, user defaults, memory, and other JVM branches. Known missing/edited owned payload must not become an unrestricted javaagent allowlist.
- [x] Wire component preparation into actual launch before Steam transport and before startup validation can reject the owned argument. Wire a headless observer for analysis, with cancellation/disposal and no artificial 90-second completion for heavy loading.
- [x] Include new asset in every production build/package/resource allowlist; invoke `Build-StartupComponent.ps1` when preparing dependencies without changing website scripts.
- [x] Run focused C# tests and relevant launch/startup/package regressions; record RED/GREEN and commit exact files only.

## Task 3: Actual launch acceptance and review

- [x] Build a candidate with the production source/resource path and verify its embedded Java payload. Do not open it while the user requires silent background testing.
- [ ] Click launch through the candidate, inspect actual game-window identity and screenshots throughout startup, verify direct menu handoff and no extra native loading HWND. Record observed intervals and unsupported phases honestly.
- [ ] Inspect multiple animation frames, log changes and right-side quiet state, ordinary mod attribution, resized window, and core error fixture. Repeat startup and verify safe exit.
- [x] Review complete change set for session safety, GL state, compatibility and launch regressions. Fix findings and rerun affected checks.
- [x] Update verification document with actual evidence. Keep software release pending if real acceptance fails or required human trust approval remains unresolved.

2026-10-25 verification status: Java fixwave `02a96e0` and native ownership/approval fix `90d2e7f` passed focused re-review. Whole-feature review findings F1/F2 were fixed in `6b7cb5a` and `e3f0bb6`; the latest run passed 218 component, 747 headless integration and 186 analyzer assertions. Final scoped re-review approved the implementation with no new Important/Critical findings. Candidate3 was rebuilt and its embedded Java resource hash matches the tested component. Real Steam acceptance is pending because the user is using the foreground and explicitly requires silent testing. This restriction supersedes the plan's earlier interactive acceptance timing. Detailed evidence: `docs/quality/2026-10-25-ingame-startup-verification.md`.
