# Java Component Workflow Implementation Plan

> **For agentic workers:** Use subagent-driven-development for isolated implementation and review. Track evidence and completion in artifacts/java-workflow/progress.md.

**Goal:** Complete standard Buddy Java component installation/dependency management and preserve disabled state after Workshop updates.

**Architecture:** Extend JavaComponents receipts for explicit update reconciliation; add a focused workflow using the current download/source resolver and existing dedicated component adapters. Expose it through LibraryController and the current component UI.

**Tech Stack:** C# .NET Framework, WebView2 JavaScript, PowerShell native fixtures, headless Edge.

## Global Constraints

- Only component-related source/tests/docs. Preserve foreign dirty files; no website/server changes or publication.
- No real user file mutations or foreground operation; isolated native files and background browser.
- No arbitrary external installers/agents. No fake progress or hidden failed dependencies. No unconditional full Workshop scan on startup.
- Keep standard mod-list saving independent of component management.
- Record actual test scope and unverified real-game behavior honestly.

### Task 1: Preserve disabled Java state across source updates

Files: JavaComponents.cs, JavaComponentsTests.cs, tests/JavaComponents.test.ps1 as needed.

- [ ] Add a failing fixture: disable standard component, simulate Workshop update replacing metadata/JAR, reconcile, Java declarations absent and Lua fields retained; enable uses NEW metadata and payload, never old files.
- [ ] Implement `internal JavaComponents.Result Reconcile(CancellationToken token)` using existing owned receipts/transactions; no-op without active suppression, preserve changed identities and report specific unresolved items. Inventory can report pending updates; reads do not mutate.
- [ ] Test repeated update, cancellation, changed mod identity, unrelated edits and rollback; existing lifecycle tests pass.
- [ ] Record exact command/results and commit only owned files; task reviewer checks spec and quality.

### Task 2: Install/enable workflow and dependency resolution

Files: focused JavaComponentWorkflow.cs, LibraryController.cs, ModernController.cs/ModernHostPolicy.cs if required, build/ProductionSources.ps1, new focused native tests.

- [ ] Failing fixture exercises a downloaded standard component with a local Java dependency and Lua dependency, missing downloadable dependency, cycle and cancellation. Expected default order puts dependencies first and each requested component enabled once.
- [ ] Reuse SnapshotWorkshopResolver/InstallWorkflow and existing specialized component installer. Introduce `javaComponentsInstall {id,confirmed}` and `javaComponentsEnable {token,confirmed}` as mutations; exact strict payload validation.
- [ ] Report download/recognition/dependency/apply stages and actual item counters; retain cancellation through download/scan. Return typed status/message and missing items on failure, never claim complete for partial apply.
- [ ] Wire Reconcile before production launcher checks with cheap no-record path, and expose explicit `javaComponentsReconcile {confirmed}` for refresh. Frozen environment remains fixed.
- [ ] Register production source, run native/host boundary tests and review.

### Task 3: Human-facing workflow and actual UI acceptance

Files: Web/app.js, Web/app.css if needed, tests/java-workflow-ui-tests.cjs, native UI harness, docs/quality/2026-10-27-java-component-workflow.md.

- [ ] Failing UI tests cover link install, one-step enable with dependencies, cancel, unsupported component and retry.
- [ ] Download dialog starts native install flow, Java enable calls workflow, shows stages/counts and cancellation; stable component cards and original Workshop links. No extra mod-list save gate.
- [ ] Click real app UI into native controller operating isolated real files; inspect desktop/narrow dark/light screenshots and final files.
- [ ] Read-only actual Workshop package verification and isolated production package build. Record real-game coverage separately; don't claim all Java mods supported.
- [ ] Review final diff and commit implementation/evidence docs. Provide local candidate, no publish until acceptance evidence is sufficient.

### Task 4: Preserve uninstall intent after Workshop restores files

Files: JavaComponents.cs, JavaComponentsTests.cs; native UI acceptance harness as necessary.

- [ ] Reproduce uninstall followed by metadata/JAR update: Java currently becomes loadable and stale receipt prevents enable.
- [ ] Include effective uninstall intent in owned-root reconciliation. Refresh into metadata-only suppression using fresh author files/current hashes; never re-delete fresh downloaded JAR or restore obsolete payload bytes. Retire all contributing suppression intents transactionally, including disable then uninstall.
- [ ] Cover normal update, JAR-only reinstall, disable/uninstall/update/enable chain, cancellation/recovery and repeated reconcile; verify fresh payload bytes stay unchanged after enable.
- [ ] Native UI click acceptance covers uninstall/update/refresh/enable and exact file results. Review fixes before building local candidate.
