# Native next-stage review

Reviewed 2026-10-13 against `baseline-extensions-2026-10-13` (`a867ec8e9e2b9485f1c64e61f9859d710ecd20b8`), which was also HEAD. Scope: uncommitted native lifecycle, task journal, pictures, favorites, playtime, controller/host/policy/Program integration, and independent mod/save configuration changes. Requirements: `docs/integration-contract.md` and `docs/superpowers/plans/2026-10-13-full-launcher.md`; the approved spec was used for author metadata syntax. Steam helper implementation and Web implementation were outside the primary review scope.

The review did not edit source, index, HEAD, or branch state. Review executables, probes, and all fixture writes are under `artifacts`. No real game launch, login, subscription change, personal default, save, approval, or original DB mutation was performed.

## Strengths

- Profile collection writes use the existing launch mutex, stale stamps include the default-file bytes, and atomic activation retains BOM/newlines plus maps/custom blocks. The suite exercises concurrent writers, corrupt stores, reparse refusal, missing mods, dependency cycles, and preserved managed IDs.
- Favorites list DTOs exclude passwords; explicit imports retain the exact selected role and DPAPI-encrypt credentials in the launcher's own store. Original DB bytes remained unchanged in the synthetic tests.
- Picture selection confines save IDs and uses the exact host/port/username-hash folder for multiplayer. A different role receives no fallback picture. PNG magic, encoded size, dimensions, and decoder validation are present.
- The journal is bounded and durable, marks dead-owner tasks interrupted, preserves partial measured progress on failure, and never forces failed tasks to 100. Playtime uses independent workers, bounded receipts, and omits uncertain heartbeat gaps.
- Host origin/action allowlisting and capture-mode read restrictions were extended consistently to the new actions. `taskGet` and `playtimeGet` bypass the controller's busy gate, as required.

## Issues

### Critical

None demonstrated in this review.

### Important

1. **Snapshot polling cancels pending writes.** `Orange3DTool/ModernHost.cs:38` routes both snapshot actions through `dispatcher.Execute(..., true)`. `Orange3DTool/ModernDispatchQueue.cs:7` uses that same bypass flag to increment the cancellation generation. A read occupying the queue, then a queued mutation, then a harmless snapshot consequently produces `TaskCanceledException` and never executes the write. Periodic task/playtime polling can therefore cancel a user's queued operation without a cancel click. Separate concurrent snapshot execution from cancellation generation; only the explicit cancel action should invalidate queued mutations. Artifact probe result: `SNAPSHOT-CANCELS-QUEUED-WRITE=TaskCanceledException, writes=0`.

2. **Qualified B42 hard dependencies block managed-component activation.** `Orange3DTool/SaveProfiles.cs:49`/`:50` expose `require=\ZombieBuddy,\Viewpoint` literally, and `Orange3DTool/ModConfigurations.cs:44` requires exact membership in the unqualified selected IDs. This is not hypothetical author syntax: `Orange3DTool/BuddyPresentationBridge.cs:27` generates these qualifiers and `ShootClientTests.cs:8` uses them. A profile preserving active Buddy/Viewpoint/shoot/bridge fails activation as missing `\ZombieBuddy` and `\Viewpoint`. Normalize source dependency qualifiers according to B42 semantics, retain raw metadata, and preserve embedded backslashes in opaque IDs. Test the actual generated bridge metadata, preview, activation, and save validation. The mod implementer has reproduced RED and reported a parser correction; this report does not yet certify its GREEN result.

3. **Author soft-order keys are ignored.** `Orange3DTool/SaveProfiles.cs:49` reads `loadAfter`/`loadBefore`, but the approved spec and the repository's generated bridge metadata use `loadModAfter`/`loadModBefore`. The outward DTO aliases can remain `loadAfter`/`loadBefore`; they must be populated from author keys. A synthetic `Soft` mod declaring `loadModAfter=A` with selected order `[Soft,A]` returns that order, no errors, and `changed=false`, allowing activation in the wrong declared order. Support real author keys and add corresponding before/after regressions. The implementer has reproduced RED and reported a correction; final GREEN remained pending at this review handoff.

4. **Credential sanitization misses JSON field syntax.** `Orange3DTool/LauncherJournal.cs:11` requires the sensitive key to be immediately followed by whitespace plus `=` or `:`; the closing quote in `"serverPassword":"..."` prevents a match. `Update` at `:13` consequently persists both the secret and token in task events and the rolling log. The artifact probe passed synthetic JSON credential fields through `Update`; both values survived `Snapshot` and disk persistence. In addition, `ModernController.cs:38` serializes entire result DTOs into a log message, so the sanitizer must handle structured/escaped forms. Prefer an explicit safe message/field projection at controller boundaries and robust sanitization of supported structured/free-text credential formats. Cover JSON keys, escaped JSON inside message strings, quoted values containing whitespace, and cipher/ticket fields. Only synthetic secrets were used for the repro.

5. **Fixed phase milestones are now reported as measured completion.** `Orange3DTool/ModernController.cs:23` forwards all `Engine.Progress` numeric values into the durable journal. `Program.cs:94`–`:131` emits manually chosen 10/25/40/60/72/82/94 milestones, while `LauncherMaintenance.cs:32` emits fixed 80 before replacement. These values have no measured count/byte denominator and violate the contract's measured-only percentage requirement. Keep such phase messages at `progress=-1`, or model phase state separately; preserve numeric percentages from real file/byte counters and the final verified completion.

### Minor

1. **The day-boundary test does not assert day buckets.** `Orange3DTool/LauncherNativeServicesTests.cs:12` samples across Shanghai midnight but checks only total/session seconds. An implementation assigning the whole interval to one day would still pass. Assert each day's `todaySeconds` using `SnapshotAt`, and add UTC-duration/worker-recovery tests. This is a coverage gap, not a demonstrated accounting error.

## Verification

Fresh all-source builds and executions passed for:

- `LauncherNativeServicesTests`: picture-role/confinement, journal failure/redaction/interruption, basic playtime sampling/gap checks.
- `FullLauncherIntegrationTests`: new read actions do not create state, durable success/failure history survives reopening, and settings dispatch works.
- `ModConfigurationsTests`: 280 assertions, zero failed cases.
- `GameFavoritesTests`: readonly original DB, exact account import, DPAPI storage, explicit confirmation, and active journal refusal.
- `SaveProfilesTests`: 61 assertions.

One intermediate all-source build observed a concurrent test-root rewrite error in `StatusTests`; the next fresh build passed. The extra deterministic probes are `artifacts/native-review-probes.cs` and `artifacts/native-review-probes.exe`, using the freshly built `artifacts/review-native-tests.exe`. They reproduced Important findings 1–4 without changing production source. Finding 5 is grounded in the numeric emitters and changed forwarding code.

The original DB schema compatibility and worker/process verification were reviewed from code and fixtures. These checks do not certify real Steam queries, game entry, server login, or real process tracking.

## Assessment

**Ready to merge: No, pending fixes and fresh verification.**

The persistence and confinement foundations are useful and the existing suites pass, but those suites miss a polling-induced cancellation regression and real metadata syntax. The journal and progress contract also need the confirmed repairs above before the new native workflows can be considered ready. This report records the initially reviewed defects; subsequent fixes require rerunning the relevant suites/probes.
