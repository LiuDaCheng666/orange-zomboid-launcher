# Configuration editing and dependency-order repair

**Goal:** Honor the user's explicit 2026-10-16 instruction: editing configuration and components must not be refused because a client, server, or Java process is running. Explain dependency failures and provide automatic ordering directly from launch preflight.

**Architecture:** Remove process-state prerequisites from configuration writes, leaving ordinary filesystem errors visible. Reuse the existing dependency graph for a bounded, stable reorder that never enables or removes IDs. The launch page receives concrete dependency details and an actionable repair. Existing file-format, identity, version, and signed-update checks are outside this change.

**Workspace:** Existing checkout, branch `fix/unrestricted-config-and-mod-order`. Do not touch the live game, running server, installed launcher, or account credentials during implementation. Fixtures live under `artifacts/config-order-127` and use owned helper processes only.

## Task 1 — Configuration writes independent of runtime activity

- [x] Add failing integration coverage showing configuration writes succeed beside owned Client, Server, and Unknown Java helper processes; actual file locks still fail truthfully.
- [x] Remove runtime checks from `Engine.Guard` (retain separate duplicate-launch checks), Viewpoint settings, shaders, experimental DLSS switching, and play-profile preview/apply/recovery.
- [x] Audit controller and UI flags so editing, selecting, saving, installing, uninstalling, and Java decisions do not inherit a runtime-disabled state.
- [x] Do not block editing simply because a direct-launch session is active. On session cleanup, preserve deliberate edits instead of restoring an obsolete default or leaving a permanent pending session. Do not stop any real process.
- [x] Update tests whose old assertions deliberately required runtime refusal; run focused backend suites and real owned-process evidence.

## Task 2 — Concrete dependency details and stable automatic repair

**Files:** `Orange3DTool/ModConfigurations.cs`, `Orange3DTool/LaunchPreflight.cs`, their tests, and a dedicated new regression test if useful. Do not edit controller/host policy or Web files (other tasks own them).

**Contract:** `LaunchPreflightItem` gains `string[] details` and `bool canAutoSort`. For the mods item, include actual missing-dependency/conflict/cycle messages and violated order edges. `canAutoSort` is true only when the graph is valid and the current default order differs. `ModConfigurations.AutoSortDefault(Engine e, bool confirmed)` reads the current default, stably reorders the same IDs, preserves BOM/maps/custom blocks, and writes only when changed. Return an object with `message`, `changed`, and `mods`. Invalid graphs leave bytes unchanged. This is exposed as native action `modAutoSort` with `{confirmed:true}` by Task 1.

- [x] Reproduce current LaoYuTranslationJava-before-ViewpointTrueWeathers failure using local fixture metadata.
- [x] Test details, successful same-membership repair, idempotence, invalid-graph refusal, and maps/BOM preservation.
- [x] Implement the contract using existing sorting and atomic-write facilities; no downloads, no automatic addition/removal of mods, no new runtime gate.
- [x] Fix named-profile activation so retained managed IDs cannot reintroduce the unsorted managed prefix after a user sorted the profile. Order the final combined list with the same graph.
- [x] Run ModConfigurations and LaunchPreflight suites and report commands/results.

## Task 3 — Actionable UI feedback

**Files:** `Orange3DTool/Web/app.js`, `styles.css` if needed, new focused UI regression tests. Do not edit C# files.

**Contract:** Render escaped `item.details` under the relevant preflight row. For `mods` with `canAutoSort`, show `自动排序并继续启动`. On explicit click call `modAutoSort` with `{confirmed:true}`, then re-enter the current launch flow with its existing draft/selection checks; do not bypass any remaining genuine dependency error or manufacture launch success. Native backend action returns `{message,changed,mods}`.

- [x] Add failing browser tests for concrete preflight details and the repair-then-launch action.
- [x] Remove runtime-only disabling of component/configuration actions (Java decisions, mod-profile activation, save editing, play-profile application); retain actual in-flight request disabling.
- [x] Keep failed named-profile saves visible next to the editor buttons, preserve the draft and allow retry; clear that error after a successful retry.
- [x] Test successful save receipt, failed/retried save, escaping, and automatic sort errors without false launch.

## Integration and delivery

- [x] Review each task diff and integrate the declared native contract.
- [x] Run focused tests, the full regression suites in proportion to touched paths, and a built native smoke test with fixture paths.
- [x] Record exact changes and verification in the changelog. Build an explicitly versioned candidate; never label an untested build as published or replace a live executable while the user has an unsaved draft.
- [x] After release verification, publish the source and signed update artifacts to the existing GitHub repository under the user's standing release request. Do not disclose or print signing-key contents.

## Final evidence — 2026-10-16

Final coordinator regression: native self-test 1,523 PASS lines, PlayProfiles 759 assertions, owned-process checks 87 PASS lines, dependency-order suite 45 assertions / 9 cases, lifecycle suite 39 assertions; all exit 0. Focused browser suites: configuration/order 15/15, library 14/14, full 21/21, experience 73/73, compact 19/19; Java and update-ready suites pass. Independent scoped final review has zero outstanding findings.

Signed 1.2.7 package verifies 18 owned files, 22 release assets, installer/portable equality, isolated installer/uninstaller and two native preview themes. Published as latest Release ID 414552549. Anonymous official downloads match local hashes; the original 1.2.6 default update service reaches Ready for 1.2.7 with 5,390,138 verified bytes and all 18 original files unchanged. New 1.2.7 returns Latest. No live installation or game/server process was changed. Full publication record: `docs/releases/1.2.7-github-release.md`.
