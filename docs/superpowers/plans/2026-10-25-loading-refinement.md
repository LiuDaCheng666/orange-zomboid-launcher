# Loading Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Reduce repeated launch work and provide informative, distinct boot/save/server loading scenes with a better orange animation.

**Architecture:** One authoritative native preflight with selected graph metadata; session-gated Java observations and context-aware renderer; queued server background below original game controls.

**Tech Stack:** Existing C#/.NET Framework controller, Java 17 source, pinned Byte Buddy, local actual 42.21 game API, real hidden GLFW/OpenGL and headless browser tests.

## Global constraints

- Read `docs/superpowers/specs/2026-10-25-loading-refinement-design.md`.
- Existing worktree/branch is isolated. Preserve all pre-existing website/deploy/HTTP changes; no server/website/version/release operations.
- Background tests only. No actual game/Steam initialization, visible window or UI automation.
- All runtime observations and progress must be real. Never inspect credentials or cover original interactive controls.
- Current agent hash, protected paths, process identity and authorization remain authoritative.
- One implementation worker at a time; independent scoped review may run while the next task is implemented.

## Task 1 — one native launch check and focused metadata

Files: `ModernController.cs`, `ModernHostPolicy.cs`/`ModernHost.cs` only if necessary, `Web/app.js`, `LaunchPreflight.cs`, `SaveProfiles.cs`, `ModConfigurations.cs`, relevant existing C# and UI tests.

- [ ] Add failing behavioral tests for exactly one full check per automatic launch, direct backend refusal before staging/transport, structured dependency refusal and auto-sort continuation.
- [ ] Implement one authoritative native preflight; remove the frontend duplicate without accepting JavaScript authorization tokens or bypassing backend validation.
- [ ] Separate identity discovery from selected metadata, preserving every matching source and current version overlays. Read selected bytes fresh and preserve requirements/conflicts/maps/unknown-source semantics. Do not add a persistent security cache.
- [ ] Retain final fresh guard/config/process/agent checks and cancellation. Necessary user-facing failures remain actionable; ordinary launch wording stays brief.
- [ ] Run focused/native/headless workflow regressions, compare the existing controlled read-only local benchmark, self-review and commit exact task paths.

## Task 2 — early activity, animation and three contexts

Files: `StartupSource/ozl/startup/{LoadingModel,LoadingScene,StartupRuntime,Hooks,GameRenderer}.java`, new small queued-render adapter if needed, `tests/java/startup/`, `tests/IngameStartup.test.ps1` only as necessary.

- [ ] Add failing model/transformer/render tests for early real activity, bounded/throttled file observations, grow/rotate animation and reduced motion, and boot/save/server context changes.
- [ ] Hook actual main-thread/resource-index/fonts/init boundaries; count resource scan callbacks with cheap per-entry work and throttled sanitized labels. Improve measured memory precision.
- [ ] Capture local name/mode/map and server friendly name/map/mod counts from proven fields; capture only white-listed workshop progress/connection/queue data and original event names. Add context epochs and genuine completion/failure/cancel/interaction handoffs.
- [ ] Render distinct boot growth/rotation, local map chunks and server network/synchronization scenes with bounded left/right rows and no arbitrary percentage.
- [ ] Add the properly queued server backdrop through an owned runtime GenericDrawer adapter, only on verified non-FBO routes, below native controls. Reject stale queued frames; no server swap cover. Keep fallback to original UI on unverified route/error.
- [ ] Run actual premain fixtures, actual class transformation verification without initialization, hidden queued GL/state/pixel tests, package audit and scene capture review. Commit exact task paths and evidence.

## Task 3 — review and user candidate

- [ ] Review each task and scoped fixes, then the combined change for launch regressions and real-state presentation.
- [ ] Build a new candidate from production inputs, verify exact embedded JAR and ZIP payload hashes, inspect scene captures and record evidence/limits.
- [ ] Deliver the new candidate for the user's own foreground test. Keep real boot/server/save interaction acceptance and publication pending until observed.
