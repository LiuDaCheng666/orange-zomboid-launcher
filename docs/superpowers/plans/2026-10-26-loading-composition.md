# Loading composition and growth repair implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Render one coherent server loading screen with functioning native interactions and a sustained growth animation, while honestly attributing observed loading time.

**Architecture:** Exact-identity Java presentation filtering is gated by accepted per-frame backdrop leases. Native states/controls continue unchanged. Shared scene animation remains code-native and independent of loading progress.

**Tech Stack:** Existing Java17 agent/Byte Buddy, game42.21 compile-only APIs, Kahlua fixture VM, hidden OpenGL and .NET launcher packaging.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-10-26-loading-composition-design.md`; source base `3337d10` in existing checkout `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher`.
- Background only: no real game/Steam/UI launch, process attach, real-path controller/Prepare/Stage/Recover or configuration mutation. Actual game classes/Lua may be read; tests run fixture classes and hidden GL only.
- Preserve all foreign dirty website/deploy/HTTP guard files. No server, release, version change or modifications to existing published/candidate directories.
- TDD evidence first; scoped fresh review after each task. One implementation worker at a time. Generated Jar is an owned task output; no game/framework classes may enter it.

### Task 1: Coherent native connection and queue composition

**Files:** create focused `Orange3DTool/StartupSource/ozl/startup/NativeConnectionPresentation.java`; modify `Hooks.java`, `StartupRuntime.java`, `QueuedBackdrop.java`, `LoadingModel.java` only where lifecycle requires, `LoadingScene.java` for reserved native status/control lanes; add Java fixture/native Lua composition tests under `tests/java/startup` and register in `tests/IngameStartup.test.ps1`. Rebuild `Assets/OrangeStartup.jar` with existing builder.

**Interfaces:** retain native observation APIs and GPU safeguards. Make `QueuedBackdrop.enqueue(loader,ticket)` report acceptance; introduce one CPU-frame lease encapsulating current generation/epoch/frame/environment and exact page identities. `NativeConnectionPresentation` supplies read-only `skipElement(Object)` and `skipTexture(Object,Object,kind)` decisions only within that lease. Exact signatures may be adjusted to actual APIs, but no global suppression flag survives a frame.

- [ ] Reproduce the screenshot using extracted actual `MainScreen.prerender`, `ConnectToServer.prerender/render` and `ISPanelJoypad.prerender` functions in an isolated Kahlua VM with stub UI/texture draws. Shadow/reject real zombie class initialization. Render order includes native panel, logo, status and Abort, not a generic green rectangle. Capture failing single-scene assertion/image before changes.
- [ ] Add targeted fixture cases and RED evidence for exact-identity filtering, native status/Cancel callback preservation, foreign UI/texture untouched, missing APIs/env reload/FBO/unknown child fail-open, no prior-frame lease reuse and renderer failure. Representative contract:

```java
check(!presentation.skipElement(nativeCancel), "original Cancel always renders");
check(presentation.skipTexture(mainScreen, knownLogo, SCALED), "exact original logo is retired only in accepted passive frame");
endFrame();
check(!presentation.skipTexture(mainScreen, knownLogo, SCALED), "lease does not survive frame exit");
```

- [ ] Implement the smallest exact whitelist adapter based on `artifacts/loading-repair-20261026/server-composition-research.md` (actual signatures and field names are there). Hook only documented UIElement drawing/child-render boundaries; do not skip the parent traversal. Preserve native `connectLabel` and `backBtn`, reserve their lanes. Resolve current Lua environment on main frame, not GL thread; no arbitrary auth/username reads or table enumeration.
- [ ] Add actual lifecycle fixtures for workshop Required→Details→Install/Progress→Success/error, auth/QR and character/spawn handoff, ResetLua replacement and cancel/reconnect stale events. Current workshop interaction must remain native until proven closed. Keep native callbacks/returns/exceptions and done behavior unchanged.
- [ ] Match native queue initial place=-1/immediate branches (no background/UI). Add the conditional Core.EndFrameUI entry fallback after clear, once per accepted frame. Narrowly retire passive LoadingQueueUI decoration when own queue scene is accepted, preserve update/Esc/controller cancel and accurate hint. Assert original Core lifecycle counts and command order; cover lost connection and redirect.
- [ ] Run focused green; actual 42.21 no-initialization API probes; hidden GL composition captures (including native status and button), full existing Java suite with new tests, and native GameStartupSuiteTests --headless if Jar changes affect it. Inspect frames at 1280×720 and 1920×1080. Build with `Build-StartupComponent.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid`; no real game initialization. Commit owned files and report commands/results/remaining limits to task-1-report.md.

### Task 2: Sustained orange growth and visual verification

**Files:** focused animation helper if needed in `StartupSource/ozl/startup`, `LoadingScene.java`, `GameRenderer.java`, `AnimationClock.java`; `tests/java/startup/SceneContractTest.java`, `SceneProbe.java`, hidden GL animation fixtures. Rebuild owned Jar.

**Interfaces:** scene animation takes accepted draw time; it never creates progress/log events. Extend Canvas with bounded body/leaf region drawing only if required, with equivalent GL and raster alpha/geometry. The existing image and icon assets remain byte-identical.

- [ ] Add RED tests that fail the old one-second-only growth: long-wait frames at 4, 7, 11 and 16 seconds must include changing body/leaf growth beyond a mere angle difference; reduced motion must be pixel-identical at all times. Verify a continuous loop boundary without a one-frame disappearance or jump. Test alpha in the raster adapter with a known fractional-opacity pixel.
- [ ] Implement a restrained 7–9-second seed→body→leaf→settle/breathe cycle with rotation secondary. Keep the full brand recognizable through the loop; avoid blinking/resetting the complete screen or moving information. Scene/background-specific map/network activity remains. No animation timing masquerades as completion.

```java
GrowthFrame a = growth.at(4.0, false), b = growth.at(7.0, false);
check(a.bodyScale()!=b.bodyScale() || a.leafScale()!=b.leafScale(), "growth remains visible after startup entrance");
check(growth.at(0,true).equals(growth.at(60,true)), "reduced motion is stable");
```

- [ ] Make production and raster canvases share body/leaf geometry and alpha. Render contact sheets from actual production scene commands at entrance, late-loop and boundaries, for boot/save/server with current native connection status/Cancel, and reduced motion. Root views contact sheets and hidden GL frames; do not treat numeric pixel changes alone as visual approval.
- [ ] Run focused green and full Java suite once for final Jar, then the native suite if final payload changed since Task1. Record counts without claiming actual game speedup. Commit owned changes and task-2-report.md with exact image paths and Jar hash.

## Completion

- [ ] Final combined review over `3337d10..HEAD`, resolve material findings with focused re-review.
- [ ] Record latency evidence (40.374s native Loading Mods→translations; 30.776s Buddy loader; 16.425s Shelves bracket; approval overlap and CPU attribution limits). Any additional optimization requires comparable evidence; do not alter third-party behavior to claim speed.
- [ ] Build fresh candidate6 from current source with the tested Jar and current frontend; verify embedded Jar/Web/ZIP hashes and preserve candidate5 and published1.2.21. Do not run its entry point. Save quality evidence and deliver the concrete package/preview with actual acceptance limits.
