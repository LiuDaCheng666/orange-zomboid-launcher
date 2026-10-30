# Loading Overhead Implementation Plan

> For agentic workers: execute inline using executing-plans; independent screenshot review uses the project-required UI reviewer.

**Goal:** Reduce measured loading-presentation overhead without changing appearance or native connection behavior.
**Architecture:** Keep the shared scene and native hooks; optimize bounded text fitting and move issue-file reads to one demand-driven daemon.
**Tech Stack:** Java 17 component, installed game Java 25/OpenGL/Lua, PowerShell tests, existing C# production launcher.

## Global Constraints
Preserve the approved orange icon/layout/colors, 60 FPS target, native input/authorization/cancel, server mod list and order. No new global busy gate. Do not claim the transient desktop stall is fixed without evidence. Preserve foreign dirty website/deployment files. Use isolated owned test data and a small test window.

### Task 1: Measure and optimize text fitting
- [x] Add a real-font/hidden-GL performance probe exercising boot, server reload, save and workshop with bounded long log lines; save baseline using the published 1.2.43 startup asset.
- [x] Add failing regression: fitting 512-character text uses a bounded number of measurements, preserves a fitting result, keeps text when it fits, and never cuts a Unicode surrogate pair. Verify failure against existing code.
- [x] Change LoadingScene.fit and endpoint wrapping to bounded prefix search, and only add renderer measurement caching if the baseline justifies it.
- [x] Run the new test plus existing GL glyph/tint/layout tests. Repeat the identical benchmark and compare p50/p95 wall time and measurements (Windows thread CPU counters proved too coarse for per-frame percentiles).

### Task 2: Remove issue-file I/O from the render thread
- [x] Add failing behavior tests for a blocked reader: callers return without waiting, receive actual completed data later, refresh after the sampling interval, reject stale results after a connection change, and stop without joining blocked I/O.
- [x] Add IssueSampler with one demand-driven daemon; connect StartupRuntime.draw to its cached result. Existing Session.readIssue retains its validation and file-size bound on the worker.
- [x] Verify error/failure/connection-handoff behavior and real-file reads, and compare frame times while the worker is blocked.

### Task 3: Verify candidate and record scope
- [x] Build the actual startup JAR and candidate application without touching release metadata or live install.
- [x] Run focused native Lua reload, transition, cadence, GL state and scene screenshots with the candidate asset.
- [x] Use an isolated-path host to invoke the candidate production UI/controller/process launcher with TestMode=false; check loading, native terms, main menu and normal exit. Independently inspect real screenshots. This is not a full Program.Main/installed update-chain acceptance.
- [x] Record benchmark numbers, actual operations, files, screenshots and uncovered global-stall/server-specific paths. Review and commit only this task's files; no publication.

### Additional issue found during actual operation
- [x] Record the initial first-run stall; read-only JVM state confirms TermsOfServiceState hidden behind active BOOT overlay.
- [x] Reproduce with a failing native event regression; hand off only BOOT to the original terms page. Leave its handler and acceptance to the game.
- [x] Add BootInteractionTest to the normal native probe list. Rebuild and repeat real terms → menu → normal exit without a diagnostic attach agent.

### Explicit release follow-up, outside this local optimization
- [ ] Before publication, exercise unchanged Program.Main/installed lifecycle and a real server-mod loading session with the final release package. The isolated host's watcher warning is a harness boundary, not a successful installation/recovery acceptance.
- [ ] Reproduce or capture the original transient desktop-wide input stall before assigning its cause or claiming it fixed.

Results and exact boundaries: docs/quality/2026-10-30-loading-overhead-verification.md.

## 1.2.44 发布时的用户后续决定

2026-10-30 用户授权发布，并在正式 Program.Main 窗口已打开后，按 Esc 停止操作，明确要求停止额外验收、直接更新。额外连服未执行，不记通过；按该后续要求完成发布。独立测试服务器已正常退出，三个临时配置索引逐字节恢复；未更新 D 盘安装。最终发行与未覆盖项见 docs/quality/2026-10-30-1.2.44-publication.md。
