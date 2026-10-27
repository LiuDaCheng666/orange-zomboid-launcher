# Independent launch configuration checkpoints

These are local implementation checkpoints. The shared configuration editor/UI and actual game acceptance are still pending; no release, VERSION change, website/server operation or foreground game test has occurred.

## Context and stock transport

`cf26c68` supplies explicit launcher/Steam/vanilla context, independent owned paths/memory and clean game-JRE argument generation without importing original JSON/BAT/Steam agents. The 38-assertion native fixture and full production-source compile pass. A real selected-game-JRE/Buddy2.3.4 tiny-main probe accepts a Chinese/spaced owned agent path and reads the prepared Chinese configuration through a system property. The 268 source hashes are unchanged. This proves agent/config transport, not actual game entry. B41 has a concrete inventory fixture but no local real-game run.

## Current-source components and live discovery

`255225b`, corrected by `7e8ee66`, adds instance-scoped selection/approval/ordinary-list storage, source-bound approval stamps and separate live discovery. Ordinary configurations reference current sources read-only; only small runtime payloads are copied. Generic/known component state does not rewrite original Workshop metadata or ordinary lists. Runtime effective choices insert selected components and available Lua requirements while retaining ordinary relative order; author order/conflict/cycle results are nonblocking warnings. Live discovery preserves ordinary Lua, removes duplicate/disabled managed Java roots, and enforces selected source/package/hash/approval at Buddy's actual append boundary.

Task review found two Important defects before production integration: an ordinary public read/save round trip rewrote unchanged imported managed rows; requested ordinary IDs were incorrectly accepted as dependency availability. Both have focused failing/passing regressions and the scoped re-review passes at `7e8ee66`.

Executed evidence:

- Final packaged native backend fixture: 45 assertions. Prior packaged/unpackaged checks, correct metadata/common/version fallback, real copied Viewpoint and pinned Buddy, cancellation, no-op bytes, current-source hash changes and missing/available dependency cases are recorded in local task evidence.
- Separate Java manifest fixture: 15 assertions. Actual instrumentation against fixture game classes: 8 live and 7 vanilla checks, including original-choice isolation. These are not actual game classes/game UI acceptance.
- Actual Buddy2.3.4/game JAR/selected Java25 tiny-main probe: selected package appended, external byte-identical/wrong-package/unknown PREMAIN rejected; three real source hashes unchanged. Evidence `artifacts/owned-launch/live-buddy-gate.json`.
- Existing JavaComponents regression: 265 checks after shared helper extraction. Production Startup/Environment/Live asset build and authenticated nested extraction pass; build temporary paths are shortened for the long worktree, frozen contract semantics unchanged.

Live execution currently verifies pinned Buddy2.3.4 only; B41 live isolation and Buddy3 numbered-only loading explicitly report unsupported rather than silently falling back. The implementation does not claim every Java/native/custom installer is supported. Four known CS0649 compile warnings remain.

At this component-backend checkpoint, named configurations, production consumers, persistent projection, startup presentation and recovery were still pending; subsequent reviewed checkpoints are recorded below. Editable snapshot contents, shared editor UI clicks/screenshots and final production-entry game acceptance remain open. Existing snapshot order/zero-statistics correction and historical player-report checks have separate quality records.

## Named configuration storage

`e32ab76`, corrected by `520d90e`, adds the per-original-user named configuration inventory and exact installed-version/snapshot resolution. Ordinary configurations retain independent component, memory and ordinary-list state; snapshots resolve their registered copied game even when the old source installation is gone. The index is the selection authority, and ordinary no-op saves preserve bytes. Built-in launcher/vanilla follow the existing selected installed version; Steam retains its original installation, while each named configuration selects its own version without changing the global version choice.

Task review identified a permanent first-use memory regression: importing only root VM arguments ignored the effective Windows-branch heap. The correction follows the existing selected Windows-branch precedence and imports only the final complete numeric Xmx setting. The focused native fixture reports 72 assertions and the scoped review passes. This is storage/resolution coverage; it does not establish UI or gameplay acceptance.

## Runtime phase correction during integration

Production integration exposed a flaw in the earlier live agent fixture contract: mutating every `ActiveMods.getMods()` return can disrupt parser row accumulation and raw-field bookkeeping, and a process-wide override cannot distinguish a later server join from menu/single-player loading. `8f9f47a` replaces this with verified phase boundaries and keeps the getter pure. It uses completed default reads and current-game selection, preserves server ordinary IDs/order at the client loading call, and redirects only existing source-save mod-list metadata into the owning session.

The preliminary read-only review then found a concrete B42 confirmation loop: save validation rereads `getSaveInfo().activeMods`, and a redirected write without a corresponding read path exposed the old missing IDs repeatedly. `4da70a1` adds scoped completed-save reads and owned metadata readback. The scoped re-review closes that Important finding without new Important/Critical findings. Reported native JVM fixture checks are 15 normal, 15 named and 14 vanilla, with original metadata byte checks; the separate live manifest/contract fixture reports 23 checks. Actual installed B42 bytecode/Lua inspection supports the identified flow, but these fixtures do not run the game's real confirmation UI, world save or multiplayer session.

`0d263d6` adds a separate captured frozen active-component policy, retaining legacy frozen manifest semantics. This Java checkpoint did not establish controller integration; that is covered below. Revision-aware snapshot editing, UI and actual packaged-game acceptance remain open. None of these commits is a publication approval or a claim that the final configuration workflow is complete.

## Reviewed production integration

`118502b` / `2b8867f`, corrected by `643d271` and `794d2c8`, bind production dispatch, launch preparation, components, memory, status and recovery to the selected configuration. Host requests capture the owner before scheduling; unrelated selections/reads do not wait behind a component download. Counted per-owner work and explicit cancellation retain the shared Steam SDK lane and updater drain bookkeeping. User-only saves/logs/server/version repair remain available when the selected game is unavailable. Original Steam/JSON/BAT mutations remain confined to explicit Steam actions.

The full task review found three Important gaps: fresh-root runtime recovery omitted imported versions no longer referenced by current configuration rows; named snapshot capture incorrectly included inactive source-save mods; explicit replacement/skip choices were inconsistent across frozen active/default/component arrays. The first fix closed recovery and choice consistency. Scoped review then caught the remaining original-save union in production frozen launch preflight. `794d2c8` closes that path while retaining strict source provenance for automatic bound-save launches. Final Task4 spec and quality reviews pass; all findings are closed.

Coordinator verification actually executed:

- Owned production integration fixture: 79 assertions, including host capture/selection, two producers/cancellation, unavailable-version repair and native PID/birth recovery. The process is an isolated compiled executable at an owned Java-image path, not the game/JVM.
- Review regression fixture at `794d2c8`: 38 assertions, including production dispatcher startup/preflight/staging for selected and automatic-bound snapshots, generated arguments/journal identity, imported-version runtime recovery, effective capture and exact source bytes. Captured runtime manifests run through the JVM twice, with native revalidation after persistence.
- Owned optional startup receipt JVM fixture: 4 assertions. Production embedded live/frozen JARs extracted from the actual Startup asset: live 15 normal / 15 named / 14 vanilla plus tamper rejection, frozen selected-phase 7 assertions.
- Commit whitespace verification passed. Four existing CS0649 compiler warnings remain. Implementer-run affected suites and exact commands are retained in the task handoff, including frozen runtime 377, frozen launch 29 and controller 158 checks.

Task5 revision-aware frozen editing is now in progress. Task6 shared UI/native click/screenshots and Task7 actual packaged production-entry game/save/server acceptance remain required. No actual game/foreground operation, VERSION change, publication or website/server modification is covered by this checkpoint.
