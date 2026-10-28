# Launch Configurations Implementation Plan

> **For agentic workers:** Use subagent-driven-development task by task with scoped review and final whole-branch review. User-approved design is docs/superpowers/specs/2026-10-27-launch-configurations-design.md.

**Goal:** Make launcher-owned configuration the default startup path, preserve optional Steam takeover, and expose launcher/Steam/vanilla modes at the existing homepage configuration control.

**Architecture:** Explicit launcher context owns arguments, component state and effective discovery while Engine retains real game/user identity. Launch and component consumers bind the same context. Live isolation uses a dedicated agent contract rather than weakening frozen snapshots.

**Tech Stack:** Existing C# Framework/WebView2, Java game agent/ByteBuddy, PowerShell native fixtures, headless Edge.

## Global Constraints

- Preserve Steam options, original JSON/BAT, original Workshop metadata and user's foreground in launcher/vanilla mode. Only explicit Steam takeover mutates its configured resources.
- No generic global busy blockers, hidden failures, fake percentages, silent fallback to original launch, or hollow UI selectors.
- Component enable state and ordinary mod lists remain separate. Frozen snapshots retain their own contract.
- Preserve all foreign dirty website/deploy files. No publication, VERSION change or server/website edits in this plan.
- Record synthetic/native/JVM/actual-game coverage separately. Actual-game acceptance uses production entry; never claim probes are actual game tests.

### Task 1: Explicit context and stock Java transport

**Files:** Create Orange3DTool/LauncherConfiguration.cs and focused tests; modify build/ProductionSources.ps1 only to register code.

**Interface:** `LauncherConfiguration` represents mode/key/owned paths plus real game/user. Provide parse/default/persist selection and bind/resolve methods without writing original files. Provide stock argument generation/ProcessStartInfo independent of original JSON/BAT/Steam options; signatures finalized in the task report before consumers. Engine binding occurs in Task 3.

- [x] RED fixtures for three exact modes launcher/steam/vanilla; default launcher; persistence per real game/user; malformed/unknown original agents irrelevant; Chinese path Windows quoting; stock B41/B42 main/classpath/options; child JAVA_TOOL_OPTIONS/_JAVA_OPTIONS/JDK_JAVA_OPTIONS removed, parent unchanged.
- [x] Implement explicit owned paths and durable local selection/config initialization, no source mutations or global Steam lookup. Required runtime assets must be resolved concretely; missing files name their problem. Use real game's JRE/native resources with explicit main and Steam IDs, memory from owned preferences.
- [x] Probe actual Buddy JAR through a Chinese owned path and config via system property, background tiny main only; preserve original source hashes. Register production source and run focused native suite.
- [x] Commit owned files and report exact APIs, RED/GREEN results, residual actual-game boundaries for review.

### Task 2: Live discovery, independent component state and approvals

**Files:** New focused live-context Java agent/manifest alongside EnvironmentSource; new owned-component storage class; JavaComponents/JavaComponentWorkflow/CoreComponents/JavaApprovals and dedicated adapters only at context boundaries; build agent packaging/tests.

**Interface:** consume Task1 LauncherConfiguration instance; return effective selected current roots, component enable list and VM arguments. API is instance-scoped so later named configurations can supply their own Root. Preserve original metadata and don't overload FrozenEnvironment. Document context APIs for controller/launch consumers.

- [x] RED file/JVM fixtures with two original Viewpoint roots, an explicitly selected component, unrelated Lua, unknown Java, exact-hash allow/deny, update, disable/uninstall/cancel; originals remain byte-identical. Ordinary default list save cannot enable/add/reorder components.
- [x] Implement owned source selection/state/config/approval directory. Normal configurations reference selected current roots read-only, following current source updates; do not copy full component payloads in normal mode. Snapshot-only copies are Task5. Only small runtime/Buddy files are deployed to owned runtime. Supply generic and known-component backend APIs consumed by Task3 routing before legacy mutations. Reuse existing dependency planning/metadata parsing and exact Workshop resolution; avoid duplicated full resolver. No arbitrary title guess or automatic all-alternative enable.
- [x] Add separate live discovery contract selecting owned Java roots and retaining Lua, owned default/loaded file routing and vanilla exclusion. Keep frozen parser/requirements intact. Initialize Buddy deny-new/no blanket trusts and include enabled components via explicit lists/approvals.
- [x] Execute isolated Java contract probes and native component lifecycle with real copied package bytes; source hashes unchanged. Review/commit with exact unsupported boundaries.

### Task 3: Named configuration storage and effective-context resolution

**Files:** focused LaunchConfigurationProfiles.cs with tests; LauncherConfiguration.cs to support explicit named identity/root; build registration.

**Interface:** built-in IDs launcher/steam/vanilla and named live/frozen entries form one inventory. Named normal entries hold name/version ID/ordinary mod IDs, while their component state and memory belong to an instance-scoped LauncherConfiguration. Frozen entries resolve registered SaveEnvironments records read-only. Task4 consumes selection resolution; Task6 consumes native list/edit results.

- [x] RED create/list/select/rename/version selection/mod order of two independent configurations; selection persists across restart; named A component/default/memory state cannot change B; frozen entries expose pinned game+mods without copying another game. Original PlayProfiles storage preserved and not silently applied/migrated.
- [x] Implement small per-user configuration index with exact selected ID; built-ins always available, new named defaults launcher mode. Resolve installed VersionLibrary IDs read-only and exact snapshot registration; do not select global VersionLibrary as a side effect. Named context key incorporates configuration ID at the same directory depth as default to avoid deep nested Windows paths. No arbitrary browser game/source path accepted by the public resolution API.
- [x] Save ordinary list/order only into chosen context, preserving exact bytes on no-op. Preserve safely parsed prior memory setting on initial default configuration when available; arbitrary original VM arguments never imported. Profile create/delete/select/queries do not acquire global game locks; only the chosen index/context writer serializes. Missing selected version reports its exact unavailability without blocking unrelated pages.
- [x] Native/file fixtures verify source/Steam/save/default/other profile bytes and target resolution; commit/report API and tests for review.

### Task 4: Production launch, components, status, memory and recovery integration

**Files:** Program.cs Engine, ModernController/LibraryController/ModernHostPolicy, LaunchProfiles/LauncherLaunch/LaunchPreflight/GameStartupComponent, process/recovery tests.

**Interface:** selected mode persisted from Task1; components/discovery from Task2; new strict host read/change action for homepage. All mode-aware consumers share the bound context, including controller restart.

- [x] RED fixtures: polluted original config doesn't block launcher/vanilla normal/save/server; Steam still reads its settings; owned memory/config status correct; PID/birth journal/restart/forced-stop preserves selected game classification; no unrelated process killed.
- [x] Bind Task3 selected configuration on production engine selection; route explicit launches/preparation to owned assets. No original JSON/BAT component install side effects. Snapshot selection can use its pinned game/mod context for normal/menu, save and server targets without silently changing original mod files. Adjust evidence/preflight to selected configuration rather than unused files. Default launcher save targets retain the save's ordinary choices plus selected components; an explicitly selected named configuration supplies its own ordinary choices, without writing the original save list.
- [x] Route component/Java-list/install/dependency/approval actions to Task2 backend before legacy mutation adapters. Normal configurations follow current selected-source bytes; snapshots use copied sources only. Reuse original workflow exact download/source resolution; metadata sorting cannot become a generic normal-operation/launch gate. Actual missing source/selected component failure is named and actionable.
- [x] Keep Steam takeover optional with single confirmation; force clears this account/game full options and reports unhandled cleanup truthfully. Mode change remains usable during unrelated reads; actual conflicting writes only serialized. Release failure/cancel recovery correctly.
- [x] Run affected native/host/process tests, produce actual generated args/settings preservation proof, review and commit.

### Task 5: Editable frozen configuration contents

Status: COMPLETE — native backend reviewed at 175c36c; scoped spec/quality PASS. Parent native rerun PASS66 at o5-5ef7e836. Shared editor UI/clicks and actual packaged game remain Task6/7.

**Files:** SaveEnvironments/SaveEnvironmentRuntime transaction boundary, focused snapshot configuration editor service, controller/host backend actions, affected native/file/JVM tests. Shared editor UI/click acceptance follows Task6.

- [x] RED creation with original reverse order then edit order/add/remove: only chosen snapshot changes, original save/default/Workshop/other snapshot/gameversion bytes unchanged. Adding current source copies exact files and records hashes/source ID, then source update leaves copied mod unchanged. Explicit order/conflict warning never blocks save/start.
- [x] Add atomic snapshot revision transaction under same per-snapshot resource lease. Use manifest/index hash publication with recoverable rollback on failure/cancel; do not copy whole game again, auto backup user saves or introduce unrelated global tasks. Keep existing identity/manifest invariants and active-process file ownership. Copied new root remains inside chosen snapshot; removal only drops selected IDs and unreferenced owned root. Existing IDs/order untouched except user's explicit edits.
- [x] Rebuild effective discovery/default/preload/approval configuration for that snapshot from current revised copied roots; list edits consistent through next launch/save/exit/reopen. Apply explicit replacement/skips consistently. Original source metadata warnings may inform but never gate unchanged available content.
- [x] Native backend edit→real copied files/manifest/runtime; verify progress/count/cancel and retry final files. Verify source changes do not alter copy and exact gameversion stays pinned. Shared editor DOM clicks and reorder animation are Task6; actual production-entry game acceptance is Task7 and remains required before publication.

### Task 6: Homepage choices and native click/visual acceptance

Status: COMPLETE — scoped spec/quality PASS at 1fa6142 after four findings fixed. Parent final focused native R1–R4 PASS; prior full native360 responses/830 events at 5a467b8. Actual packaged-game acceptance remains Task7.

**Files:** Web/app.js/app.css, focused mode UI suite and production native harness, usage/quality docs.

- [x] RED actual DOM cases show 启动配置 with 启动器配置(default)/Steam 配置/原版配置 plus named normal and snapshot configurations. Create/edit uses one configuration workflow for version/components/mod list/order; keep existing ordinary named mod-list compatibility without merging their storage semantics silently. Only selected-mode description and actual memory/components appear; Steam takeover/force actions only in its mode.
- [x] Wire strict native actions, persisted selection and single force confirmation. All targets use selection; no hollow selector, raw JSON, duplicate progress or irrelevant old-parameter gate in launcher mode.
- [x] Actual clicks -> production controller -> real owned files for select/install/enable/approval/launch preparation, failure/cancel/retry and Steam confirmation; inspect desktop/narrow dark/light and final statuses. Tests may not claim injected ProcessStart results are actual game entry.
- [x] Run affected DOM/native suites, inspect screenshots, review and commit. Build/game acceptance follows Task7 after the shared editor and frozen backend are integrated.

### Task 7: Final local candidate and production-entry acceptance

- [x] Review the whole branch and resolve Critical/Important findings. Run affected checks after final changes; retain per-task evidence instead of mechanically repeating every project suite. Final N3 scoped review passed at f4834e93; original Runtime377 observation remains unclassified with recurrence diagnostics retained.
- [x] Build immutable local candidate, verify bootstrap RSA/all listed file hashes and Windows NotSigned status truthfully; no VERSION/publication change by this plan. Candidate: E:\Projects\OrangeLauncher\dist\launch-configurations-local-20261028-f4834e9\app; RSA and 21 listed files verified, Windows NotSigned.
- [ ] Use candidate production entry/exact arguments for actual menu, selected Java/Lua provenance, named configuration, Chinese-path edited snapshot save/save-exit/relaunch, multiplayer, process/recovery/force-stop. Keep real user data and foreground intact. If actual foreground interaction is indispensable, prepare the concrete candidate first and request that limited permission; no game acceptance/completion/publication claim before it passes.
