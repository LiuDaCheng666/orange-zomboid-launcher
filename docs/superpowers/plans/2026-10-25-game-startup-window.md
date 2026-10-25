# OZL Game Startup Window Implementation Plan

> **For agentic workers:** Use superpowers:subagent-driven-development for the independent monitor and native view tasks; primary agent integrates and verifies. Preserve all unrelated dirty changes.

**Goal:** Display a truthful independent full-screen OZL startup canvas for all three game launch paths. User clarified: Minecraft/Forge-style full screen, top progress/memory, animated centered existing logo, a few bottom-left log lines, no utility-window layout.

**Architecture:** ModernController emits lifecycle requests around existing launch operations. ModernForm marshals them to its UI thread and owns a read-only asynchronous monitor plus a non-topmost WinForms window. An identity-bound log reducer provides measured memory and actual loading stages; ready and failure never come from arbitrary timers or old logs.

**Tech Stack:** Existing .NET Framework C# compiler / WinForms / System.Diagnostics / System.Management; no new runtime dependencies or game modifications.

## Global constraints

- Work in existing `codex/velopack-updates` worktree based on 1.2.16. Do not overwrite website/deployment/HTTP/SSH changes or publish anything.
- Use the approved design at `docs/superpowers/specs/2026-10-25-game-startup-window-design.md`.
- No fabricated percent, completion on process/window appearance, automatic process termination, Java helper, or game log mutation. Reuse the existing one-shot Lua bridge for actual nonce-bound menu readiness; restore its temporary launch configuration after exit.
- Keep process memory separate from configured Java heap limit. Keep streams, timers and UI disposal bounded.

## Shared contracts (PZ3D, fields use these exact names)

`GameStartupLaunch`: string `SessionId, GamePath, UserPath, DataPath, TargetKind, Stage, Message`; DateTime `StartedUtc`. Stage is `preparing`, `submitted`, or `failed`. UI models never contain server credentials.

`GameStartupSnapshot`: string `SessionId, Stage, Message`; string[] `Lines`; double `ElapsedSeconds`; int? `ProcessId, CompletedItems, TotalItems`; long? `WorkingSetBytes, TotalPhysicalBytes, HeapLimitBytes`; bool `Complete, Failed, WaitingForInput`.

`GameStartupMonitor(GameStartupLaunch launch, Action<GameStartupSnapshot> changed)` implements IDisposable, methods `Start()`, `Submitted()`, `Fail(string message)`. Callbacks may occur off UI thread; Dispose stops observation only.

`GameStartupWindow(GameStartupLaunch launch, LauncherTheme theme)` extends Form, method `UpdateSnapshot(GameStartupSnapshot snapshot)`. Parent uses FormClosed to detach monitor. No global process side effects.

### Task 1: Read-only launch monitor and genuine evidence

Files: create `Orange3DTool/GameStartupModels.cs`, `GameStartupMonitor.cs`, `GameStartupMonitorTests.cs`; monitor implementer owns them.

- [x] Write focused failing tests for stale logs/old process rejection, fresh loading phases, exact readiness and authorization evidence, unknown progress, bounded lines, append/rotation/truncation, graceful observer disposal and memory distinction.
- [x] Inspect existing actual fixture logs under `E:/Projects/OrangeLauncher/artifacts/` for startup markers. Do not interpret JVM command line or stack frames as readiness.
- [x] Implement monitor with a cancellable background task, per-session initial log boundary and exact client executable identity plus birth time; Java requires existing classifier. Read at most 256 KiB per tick, 4 KiB per line, retain at most 80 lines; never write game files.
- [x] Run focused .NET tests; include evidence for actual markers. Notify UI implementer when shared DTOs exist.

### Task 2: Native startup window

Files: create `Orange3DTool/GameStartupWindow.cs`, `GameStartupWindowTests.cs`; native-view implementer owns them.

- [x] Write failing native tests for independent non-topmost/resizable window, safe text, indeterminate progress, measured count progress, labelled memory and failure/input states.
- [x] Implement a borderless full-screen OZL canvas with the original embedded hexagon logo and central loading animation, top progress/memory, 4–6 bottom-left lines and a restrained elapsed/dismiss control. Reuse dark/light palettes; verify 1920x1080 and 1280x720. Do not steal focus on periodic updates.
- [x] Add actual native rendering captures in own artifact directory for both themes and constrained sizes; inspect those captures.
- [x] Verify disposal/close has no game effects; expose only the shared contract constructor/update method. Report tests and paths.

### Task 3: Integration, test harness and build

Files: modify `Orange3DTool/ModernController.cs`, `ModernHost.cs`, `build/ProductionSources.ps1`; create `GameStartupPresentationTests.cs` and a focused build/test runner; primary agent owns these files.

- [x] Add a controller lifecycle boundary around `launch` and `profileLaunch`, emitting preparing before original work, submitted after success, failed after exception. Safe observer errors cannot abort launch. Test with controlled delegates without starting a game; nonlaunch actions emit nothing.
- [x] Host marshals events to UI thread, creates one window and monitor, forwards snapshots, disposes after actual readiness or user close, retains failure info, ignores stale sessions, and disposes during launcher close. Preserve capture/test no-mutation policy.
- [x] Add explicit new production sources to the source allowlist; tests remain diagnostics only. Build focused native test executable and current production app with unchanged release metadata.
- [x] Run new suites plus relevant original preflight/transport/profile/controller/UI regressions. Verify no UI calls leaked into controller background thread and no monitors survive close.
- [x] Independently review combined changes, fix substantive findings, record verification and show final screenshots/test build. Preserve production publishing for a subsequent explicit release request.
