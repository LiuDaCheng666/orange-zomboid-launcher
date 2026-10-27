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

- [ ] RED fixtures for three exact modes launcher/steam/vanilla; default launcher; persistence per real game/user; malformed/unknown original agents irrelevant; Chinese path Windows quoting; stock B41/B42 main/classpath/options; child JAVA_TOOL_OPTIONS/_JAVA_OPTIONS/JDK_JAVA_OPTIONS removed, parent unchanged.
- [ ] Implement explicit owned paths and durable local selection/config initialization, no source mutations or global Steam lookup. Required runtime assets must be resolved concretely; missing files name their problem. Use real game's JRE/native resources with explicit main and Steam IDs, memory from owned preferences.
- [ ] Probe actual Buddy JAR through a Chinese owned path and config via system property, background tiny main only; preserve original source hashes. Register production source and run focused native suite.
- [ ] Commit owned files and report exact APIs, RED/GREEN results, residual actual-game boundaries for review.

### Task 2: Live discovery, independent component state and approvals

**Files:** New focused live-context Java agent/manifest alongside EnvironmentSource; new owned-component storage class; JavaComponents/JavaComponentWorkflow/CoreComponents/JavaApprovals and dedicated adapters only at context boundaries; build agent packaging/tests.

**Interface:** consume Task1 context; return owned effective roots, component enable list and VM arguments. Preserve original metadata and don't overload FrozenEnvironment. Document context APIs for controller/launch consumers.

- [ ] RED file/JVM fixtures with two original Viewpoint roots, a staged selected component, unrelated Lua, unknown Java, exact-hash allow/deny, update, disable/uninstall/cancel; originals remain byte-identical. Ordinary default list save cannot enable/add/reorder components.
- [ ] Implement staged roots only on explicit component writes, owned state/config/approval directory, generic and known-component routing. Reuse existing dependency planning and exact Workshop resolution. No arbitrary title guess or automatic all-alternative enable.
- [ ] Add separate live discovery contract selecting owned Java roots and retaining Lua, owned default/loaded file routing and vanilla exclusion. Keep frozen parser/requirements intact. Initialize Buddy deny-new/no blanket trusts and include enabled components via explicit lists/approvals.
- [ ] Execute isolated Java contract probes and native component lifecycle with real copied package bytes; source hashes unchanged. Review/commit with exact unsupported boundaries.

### Task 3: Production launch, status, memory and recovery integration

**Files:** Program.cs Engine, ModernController/LibraryController/ModernHostPolicy, LaunchProfiles/LauncherLaunch/LaunchPreflight/GameStartupComponent, process/recovery tests.

**Interface:** selected mode persisted from Task1; components/discovery from Task2; new strict host read/change action for homepage. All mode-aware consumers share the bound context, including controller restart.

- [ ] RED fixtures: polluted original config doesn't block launcher/vanilla normal/save/server; Steam still reads its settings; owned memory/config status correct; PID/birth journal/restart/forced-stop preserves selected game classification; no unrelated process killed.
- [ ] Bind production default launcher configuration on engine selection; route explicit launches and preparation to owned assets. No original JSON/BAT component install side effects. Keep snapshot paths unchanged. Adjust actual evidence/preflight to selected mode rather than unused files.
- [ ] Keep Steam takeover optional with single confirmation; force clears this account/game full options and reports unhandled cleanup truthfully. Mode change remains usable during unrelated reads; actual conflicting writes only serialized. Release failure/cancel recovery correctly.
- [ ] Run affected native/host/process tests, produce actual generated args/settings preservation proof, review and commit.

### Task 4: Homepage choices and native click/visual acceptance

**Files:** Web/app.js/app.css, focused mode UI suite and production native harness, usage/quality docs.

- [ ] RED actual DOM cases show 启动配置 with 启动器配置(default)/Steam 配置/原版配置. Existing ordinary mod profile remains separate. Only selected-mode description and actual memory/components appear; Steam takeover/force actions only in its mode.
- [ ] Wire strict native actions, persisted selection and single force confirmation. All targets use selection; no hollow selector, raw JSON, duplicate progress or irrelevant old-parameter gate in launcher mode.
- [ ] Actual clicks -> production controller -> real owned files for select/install/enable/approval/launch preparation, failure/cancel/retry and Steam confirmation; inspect desktop/narrow dark/light and final statuses. Tests may not claim injected ProcessStart results are actual game entry.
- [ ] Build local immutable candidate, verify bootstrap RSA/file hashes and NotSigned status. Review code; perform production-entry actual game acceptance if feasible within foreground constraint. If foreground required, preserve candidate and report concrete unverified paths, no publication/completion claim until accepted.
