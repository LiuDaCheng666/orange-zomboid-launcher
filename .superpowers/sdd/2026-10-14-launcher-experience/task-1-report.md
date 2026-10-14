# Task 1 report — read-only preflight and honest launch stages

Status: COMPLETE. Task review base: `9c4459953aedce2d76455bbc72799cc09b61a5b6`.
Branch: `feature/core-self-update`. Coordinator clarification commit: `6398559`.
No merge, push, package/release work, or additional worktree was performed.

## Result and scope

Added `LaunchPreflight.Check(Engine e, bool busy, bool running)` with a serializable, read-only report. It reuses `LauncherLaunch.Reason`, `Engine.Guard`, Buddy JSON validation, default-list parsing, the existing dependency sorter, and current Java approval capture. Both ModernController launch paths drain network work, rerun preflight, and retain the existing session/configuration/process protections before reaching an external start boundary. Progress distinguishes checks, preparation, and a submitted start request; it never claims successful loading, read/save entry, or server connection.

The coordinator-approved refinement adds a target-aware overload and an optional native `targetKind`. Server preflight validates the original list's staging syntax but checks dependencies only for the client mods that `LaunchProfiles` actually retains. Ordinary local mods excluded by server preparation do not block server launch. Save-target preflight checks the default list; the existing target-specific `SaveProfiles.ValidateForLaunch` still runs on the real save launch path.

## Files

- Added `Orange3DTool/LaunchPreflight.cs`: report DTOs and bounded read-only checks.
- Added `Orange3DTool/LaunchPreflightTests.cs`: synthetic fixtures and real controller-boundary tests.
- Modified `Orange3DTool/ModernController.cs`: native read action, rechecks on both launch paths, and honest progress text.
- Modified `Orange3DTool/ModernHostPolicy.cs`: action allowlist and strict optional target-kind payload validation.
- Modified `Orange3DTool/ModConfigurations.cs`: internal typed preflight adapter reusing `Order`; unresolved sources are advisory rather than fabricated absence.
- Modified `Orange3DTool/SaveProfiles.cs`: optional known-path inventory using native recorded workshop sources; cumulative directory/text budgets; bounded stream reads. Existing full catalog callers retain their discovery behavior.
- Modified `Orange3DTool/JavaApprovals.cs`: no-probe capture mode and summary of current decisions; bounded inventory/file reads. Existing explicit Java authorization operations retain their behavior.
- Modified `Orange3DTool/LaunchProfiles.cs`: `Retained` is internally reusable; its retention algorithm and session/recovery implementation are unchanged.
- Added this report.

## Exact interfaces and native payloads

```csharp
public static LaunchPreflightReport Check(Engine e, bool busy, bool running);
public static LaunchPreflightReport Check(Engine e, bool busy, bool running, string targetKind);
```

The three-argument method defaults to `normal`. The four-argument method accepts only `normal`, `save`, or `server`.

Native action: `launchPreflight`. Accepted payloads are exactly:

```json
{}
```

or:

```json
{"targetKind":"normal"}
{"targetKind":"save"}
{"targetKind":"server"}
```

The property is optional, must be a string, is case-sensitive, and cannot be combined with any extra key. Paths, target IDs, credentials, approvals, and unknown values are rejected at both the host and direct controller boundaries. A null payload at the direct controller boundary is normalized to the empty object. The native host still requires an object payload and its existing trusted origin/request-ID/protocol rules.

Example native request:

```json
{"id":"preflight-1","action":"launchPreflight","payload":{"targetKind":"server"}}
```

Existing host envelope is unchanged: successful native responses contain `{ "id": "...", "ok": true, "data": report }`.

Report DTO fields are exactly:

```json
{
  "canLaunch": true,
  "checkedUtc": "2026-10-14T00:00:00.0000000Z",
  "items": [
    {
      "id": "launch-config",
      "status": "ok",
      "title": "游戏与启动配置",
      "message": "...",
      "route": "components"
    }
  ],
  "message": "..."
}
```

`canLaunch` is a boolean; `checkedUtc` is an ISO-8601 UTC string; `items` is an array of exactly eight items, each with five string fields; `message` is a string. Item status is `ok`, `warning`, or `error`. Any error makes `canLaunch` false. Warnings remain advisory. The overall error message is the first blocking item's message. The overall success message explicitly defers authorization, loading, target entry, and connection results to the game.

| Item ID | Route | Meaning |
| --- | --- | --- |
| `activity` | `home` | Busy task/network state or an already-running process |
| `recovery` | `maintenance` | Pending profile/Buddy recovery, or unsafe checked paths |
| `workshop` | `maintenance` | Unfinished workshop rebuild |
| `launch-config` | `components` | Game files, launch JSON, heap syntax, old/duplicate Buddy arguments |
| `launch-guard` | `maintenance` | Existing Buddy, BAT, and scoped Steam launch-option protections |
| `mods` | `mods` | Parsed default list and known dependencies/order; server uses retained client list |
| `java-approvals` | `java` | Current known Java decisions; unknown/pending/denied are advisory |
| `memory` | `memory` | Missing Xmx, heap budget, and current free-memory advice |

`java` is an action route: frontend integration should invoke the existing Java review/authorization entry (`openJavaReview`), not simply call the current page navigator with `java`.

Progress wire schema is unchanged: `{ "event": "progress", "data": { "value": -1, "text": "..." } }` for checks/preparation, and value `100` after a handled start request. No new progress event type is required.

## Read-only and bounded behavior

- Native preflight bypasses mutation journaling and produces no preferences, backups, consent, staged session files, or recovery writes. It can report busy state without cancelling the active network query.
- Known paths and ancestors are checked for reparse points. A fixture junction is rejected without reading or changing its target through the junction.
- Launch JSON is capped at 2 MiB and uses existing duplicate-key/Buddy safety validation. Default text is capped at 1 MiB and unfamiliar mods-block syntax is rejected without normalization writes.
- The preflight mod inventory uses game-adjacent workshop roots, user mods, explicitly selected native sources, and up to 2,000 paths from bounded `workshop-paths.json`. It skips registry-discovered, unrecorded Steam libraries. It inspects at most 4,096 directories, caps each preflight metadata file at 256 KiB, and applies a 16 MiB cumulative metadata/cache read budget.
- Sources/metadata not inspected because of missing known evidence or the budget are reported as advisory unknown. Known declared missing selected dependencies, conflicts, cycles, invalid IDs, and invalid order remain blocking. Normal sorting/activation behavior is preserved.
- Java preflight reuses the existing effective-directory/identity/SHA-256 algorithm with process probing disabled. Unknown default Java home is advisory; it is never guessed. Current known file decisions are summarized without disclosing paths, hashes, source text, or private Steam options. Directory inventory is bounded to 4,096 entries cumulatively and Java reads have a 128 MiB cumulative budget, in addition to existing per-text limits.
- Pending Java authorization does not block launching, does not imply consent, and does not require unseen server Java files. Changed JAR bytes become pending again.
- Memory advice reads physical-memory information only. No Xmx/free-memory issue is turned into a launch-configuration error. The existing total-memory setter limit (reserve at least 4 GiB or 25%) remains unchanged; no pagefile or registry setting is changed.

## TDD evidence

The TDD skill and its complete `writing-good-tests.md` reference were read before writing tests. Each scenario names the behavioral break it catches and uses real algorithms/controllers with synthetic paths. No production stub was used to obtain the initial RED.

Initial RED commands:

```powershell
./Build-Tests.ps1 -Entry PZ3D.LaunchPreflightTests -Output artifacts/preflight-tests.exe
& 'artifacts/preflight-tests.exe'
```

Compilation exited 0. Execution exited 1 with the expected `FAIL: read-only launch preflight exists`. Implementation then passed the initial suite.

Additional observed RED/GREEN cycles used the same build/run commands:

1. A fixture with unreadable selected mod metadata failed with `FAIL: unreadable mod metadata is advisory unknown rather than invented missing evidence`. The preflight adapter was corrected to distinguish unknown source evidence from proven dependency errors; GREEN followed.
2. Target-aware server tests failed because the old empty-only native payload validator rejected `targetKind`. After the coordinator-approved payload/retention refinement, normal launch remained blocked by the ordinary local dependency, server preflight passed, and real server start remained prohibited in test mode.
3. Cumulative metadata-budget testing was refined so the final file actually required more bytes than the remaining budget (a small file can legitimately fit the remainder). The final test was then independently proven with a mutation: temporarily replacing only the 16 MiB budget by `long.MaxValue` yielded exit 1 and `FAIL: metadata beyond the cumulative read budget stays uninspected and advisory`. The 16 MiB guard was immediately restored. Mutation executable: `artifacts/preflight-budget-red.exe`. Final rebuild and execution of `artifacts/preflight-tests.exe` exited 0 with 106 PASS assertions.

The final fresh gate compiled and executed all entrypoints below, using:

```powershell
./Build-Tests.ps1 -Entry PZ3D.<Entry> -Output artifacts/task1-<Entry>.exe
& 'artifacts/task1-<Entry>.exe'
```

For `LaunchPreflightTests`, the exact output path was `artifacts/preflight-tests.exe` as required by the brief.

| Entrypoint | Compile | Execute | Result |
| --- | --- | --- | --- |
| `LaunchPreflightTests` | 0 | 0 | 106 PASS; `LAUNCH PREFLIGHT TESTS PASSED` |
| `ModernControllerTests` | 0 | 0 | 69 PASS |
| `ModernHostTests` | 0 | 0 | 44 PASS |
| `LauncherFeaturesTests` | 0 | 0 | 29 PASS |
| `LaunchProfilesTests` | 0 | 0 | 15 PASS |
| `JavaApprovalsTests` | 0 | 0 | `JAVA APPROVAL TESTS PASSED` |
| `ModConfigurationsTests` | 0 | 0 | 308 assertions; 0 failed cases |
| `MemoryPolicyTests` | 0 | 0 | 65 PASS; existing total-memory constraints preserved |
| `BuddyLaunchArgumentsTests` | 0 | 0 | 579 PASS; `BUDDY ARGUMENTS PASSED` |

All compile outputs were clean. `git diff --check` exited 0.

New tests independently compare the complete fixture file set, bytes, last-write timestamps, and directory set before/after reads. Controller preflight also proves no journal/preferences are created. The default-home fixture places a synthetic test executable at the Java path; it would create a fixture marker if probed, and no marker appears. Network tests use a cancellable fixture read, then assert release occurs before the launch preflight fails. Both actual game/Steam start boundaries remain absolutely prohibited by test mode. Tests used no real Steam/game/save data or pagefile/registry mutation; fixtures are retained under ignored artifacts for inspection.

## Self-review and limits

Reviewed the changed files against the Task 1 brief and the approved server-target clarification. The implementation retains existing BAT/Steam guards, JSON/Buddy safety, cancellation/network drain, session protection, target validation, recovery ownership, and memory bounds. No existing regression test was weakened or deleted. The report contains bounded, generic public messages rather than raw exceptions/paths/credentials. Server retention calls the existing algorithm rather than introducing a second selection policy.

Meaningful limits are intentional and exposed: unknown Java home, unrecorded external Steam sources, uninspected metadata, and unseen server files remain advisory. A report is a point-in-time check; both actual launch actions obtain fresh reports rather than consuming prior page readiness. Actual game load/read/connection success was not tested or claimed. Broad release/package gates and subsequent frontend/profile tasks remain coordinator-owned. No unresolved blocker was found.
