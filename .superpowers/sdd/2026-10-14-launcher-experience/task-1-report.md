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

## Fix round 1 — review Important 1 and Important 2

Fix base: `08b08c5a09d3091ce28431c7d48c402adbe95166`. Work began on `dff2e94`, which contained only the coordinator's subsequent Task 2 plan clarification. Both numbered findings in `task-1-review.md` were reproduced before production changes. This section supersedes the initial self-review's claim that optional helper calls were already fully budgeted.

### Important 1: optional resolver budgets

Root cause: both Java capture and server retention called the ordinary optional-addon resolver. That resolver independently materialized all candidate/version directories and read candidate metadata outside either caller's budget. Retention could also inspect the preload JAR outside the mod-phase budget.

Fixed by carrying a reusable `LaunchReadBudget` through the existing resolver selection algorithm. Its directory enumeration stops at the shared remaining directory allowance plus one refusal sentinel, and its bounded stream reads charge a shared byte allowance before allocating or reading. Optional-source cache reads, candidate/common/active metadata, and preload inspection consume the same budget as their caller. A budget refusal marks `Incomplete` and cannot pick an uninspected payload.

Server preflight now shares one 4,096-directory/16 MiB budget across retention and dependency catalog scanning. Java capture shares its 4,096-directory/128 MiB budget across inventory, source checks, optional resolution, metadata, and JAR hashing. Unknown optional evidence propagates to advisory report status instead of being called absent or successfully inspected. Ordinary resolution, explicit authorization, actual launch-session retention, and normal catalog behavior continue through null-budget overloads with their prior selection semantics.

New coverage uses an optional addon with 4,097 extra version directories, proves ordinary resolution remains supported, then proves Java preflight refuses that uninspected source and server retention marks it unknown. Another fixture resolves two valid optional sources against one 30,000-byte allowance: the first source is retained with its original selected payload; the second cannot reset the metadata allowance. The same cumulative behavior is exercised through retention. Fixture bytes, timestamps, and directory sets remain unchanged on refusal.

### Important 2: original identities before server filtering

Root cause: the original recognizable mods-block text was parsed, but ID/count/uniqueness validation happened only after ordinary server-excluded IDs had disappeared. In addition, the existing catalog's opaque-ID predicate intentionally accepts embedded backslashes and did not independently reject a traversal-shaped `../invalid` ID.

Added a preflight-specific validator that reuses the existing trimmed ID, maximum-2000, maximum-200-character, control/syntax, and uniqueness checks. It narrowly rejects `.` or `..` path segments while preserving qualified `\\Qualified`, embedded opaque `Opaque\\Value`, and ordinary dotted namespaces. The original list is validated before retention/filtering for every target. The normal catalog's opaque-ID predicate and normal sorting/activation behavior are unchanged.

Tests cover excluded traversal IDs, duplicate ordinary IDs, a 201-character ID, and 2,001 ordinary IDs. All yield a blocking `mods` item before the server controller reaches its real-start prohibition. A valid 2,000-ID original list still permits server exclusion of ordinary mods. Previously covered ordinary dependency exclusions still permit server preparation; malformed original line syntax still blocks.

### Reusable internal signatures

Native `launchPreflight` payloads, the eight report item IDs/routes, and all public report fields are unchanged. Internal adapters available to subsequent PlayProfiles work are:

```csharp
// Returns the existing normalized IDs or throws on proven malformation.
ModConfigurations.ValidatePreflightMods(string[] mods) : string[];

// One instance must be carried through a complete phase; never reset per addon.
new LaunchReadBudget(long maximumBytes);
LaunchReadBudget.Incomplete : bool; // read-only

LaunchProfiles.Retained(Engine e, LaunchReadBudget budget) : string[];
ViewpointAddons.Find(Engine e, ViewpointAddon addon, LaunchReadBudget budget) : AddonFiles;
ViewpointAddons.Resolve(Engine e, ViewpointAddon addon, Version version, LaunchReadBudget budget) : AddonFiles;
ViewpointAddons.Metadata(string path, LaunchReadBudget budget) : Dictionary<string,string>;
ViewpointAddons.Preload(AddonFiles files, LaunchReadBudget budget) : bool;
InstallWorkflow.Source(Engine e, ulong item, LaunchReadBudget budget) : string;
ModConfigurations.SortForPreflight(Engine e, string[] mods, LaunchReadBudget budget = null) : SortResult;
SaveProfiles.Entries(Engine e, List<string> warnings, bool knownPathsOnly = false, LaunchReadBudget budget = null) : List<Entry>;
```

Call `ValidatePreflightMods` on the original parsed list before excluding any ordinary IDs. For server checks, reuse one budget for `Retained` and `SortForPreflight`. A null optional budget preserves normal resolver/capture caller behavior; normal `Retained(Engine)` still exists. `LaunchProfilesTests` specifies that one-argument reflection signature explicitly now that a budget-aware overload also exists; its behavioral assertions were not weakened.

### Exact RED/GREEN commands and results

After writing the new tests, before production changes:

```powershell
./Build-Tests.ps1 -Entry PZ3D.LaunchPreflightTests -Output artifacts/preflight-tests.exe
& 'artifacts/preflight-tests.exe' --review-1
& 'artifacts/preflight-tests.exe' --review-cumulative
& 'artifacts/preflight-tests.exe' --review-2
```

Compile exited 0. Each independent RED execution exited 1:

- `--review-1`: `FAIL: optional version directories cannot escape Java preflight's cumulative limit` after confirming the ordinary resolver supported the fixture.
- `--review-cumulative`: `FAIL: optional helpers accept a shared preflight read budget` after confirming ordinary retention kept both valid preload addons.
- `--review-2`: `FAIL: server filtering cannot hide malformed ordinary identities or count`.

After the budget fix, rebuilding with the same build command and rerunning `--review-1` and `--review-cumulative` exited 0 (10 and 5 PASS respectively). After original-identity prevalidation, the same build command and `--review-2` exited 0 (12 PASS). The full final preflight executable exited 0 with 130 PASS, including the preserved initial 106 assertions and the added review/boundary coverage.

Fresh shared verification compiled each entrypoint and ran its EXE:

```powershell
./Build-Tests.ps1 -Entry PZ3D.LaunchPreflightTests -Output artifacts/preflight-tests.exe
& 'artifacts/preflight-tests.exe'
./Build-Tests.ps1 -Entry PZ3D.JavaApprovalsTests -Output artifacts/task1-fix1-JavaApprovalsTests.exe
& 'artifacts/task1-fix1-JavaApprovalsTests.exe'
./Build-Tests.ps1 -Entry PZ3D.ModConfigurationsTests -Output artifacts/task1-fix1-ModConfigurationsTests.exe
& 'artifacts/task1-fix1-ModConfigurationsTests.exe'
./Build-Tests.ps1 -Entry PZ3D.LaunchProfilesTests -Output artifacts/task1-fix1-LaunchProfilesTests.exe
& 'artifacts/task1-fix1-LaunchProfilesTests.exe'
./Build-Tests.ps1 -Entry PZ3D.ModernControllerTests -Output artifacts/task1-fix1-ModernControllerTests.exe
& 'artifacts/task1-fix1-ModernControllerTests.exe'
./Build-Tests.ps1 -Entry PZ3D.AddonTests -Output artifacts/task1-fix1-AddonTests.exe
& 'artifacts/task1-fix1-AddonTests.exe'
git diff --check
```

All six builds and EXEs exited 0 with clean compilation output. Results: preflight 130 PASS; Java approval success; mod configurations 308 assertions/0 failed cases; launch profiles 15 PASS; controller 69 PASS; addon activation, explicit minor-version selection, and path-confinement checks passed. `git diff --check` exited 0. Addon regression was included because this fix modifies its existing resolver; no broad package/release gate was run.

Fix files: `LaunchPreflight.cs`, `LaunchPreflightTests.cs`, `ViewpointAddons.cs`, `InstallWorkflow.cs`, `JavaApprovals.cs`, `LaunchProfiles.cs`, `LaunchProfilesTests.cs`, `ModConfigurations.cs`, `SaveProfiles.cs`, and this report. Self-review confirms the budgets are passed into the nested resolver and source/preload calls instead of restarted there, original identity checks precede server filtering, normal callers retain their selection contracts, and report/launch/network/recovery protections remain unchanged. No real Steam/game/save or registry/pagefile mutation was performed. Both numbered findings are addressed; no blocker remains.

## Fix round 2 — exhausted retention-to-collector handoff

Fix base: `105640eb828149feb705988f7836cfd73aacca44`. The original two findings remain closed. This fix addresses only the new Important finding (item 3) in `task-1-fix1-review.md`.

Root cause: the now-shared budget could be consumed by valid server preload retention before the collector reread a valid `workshop-paths.json`. That cumulative-byte refusal escaped the collector, became the generic fatal `mods` error, and incorrectly blocked a server launch.

The budget now distinguishes cumulative-byte exhaustion using the internal `LaunchReadBudget.Exhausted : IOException`. Invalid sizes and per-file-size limits retain their ordinary fatal exception classification. `SaveProfiles.Entries` catches only this typed exhaustion around its cache-byte read, records advisory unknown evidence, and continues collecting known/selected/local paths. JSON parsing and cache schema/path/count validation occur outside that catch. Previously safe entries and entries still inspectable within the remaining allowance are retained; there is no blanket catch around the sorter or entire collector, no empty-result shortcut, and no budget reset.

Native payloads/report fields/routes are unchanged. Reusable adapter signatures are unchanged. New internal exception contract: `LaunchReadBudget.Exhausted` means cumulative bytes could not be inspected; it is not evidence that cache content is malformed. Proven malformed JSON, unsupported cache structure, relative recorded paths, and oversized cache files still produce a blocking `mods` item.

### RED/GREEN evidence

Before the fix, after writing the new tests:

```powershell
./Build-Tests.ps1 -Entry PZ3D.LaunchPreflightTests -Output artifacts/preflight-tests.exe
& 'artifacts/preflight-tests.exe' --review-zero
& 'artifacts/preflight-tests.exe' --review-handoff
& 'artifacts/preflight-tests.exe' --review-cache
```

Build exited 0. `--review-zero` exited 1 with `System.IO.IOException: 预检文件超出读取预算。` through `LaunchReadBudget.Read → SaveProfiles.Entries → ModConfigurations.SortForPreflight`; no SortResult was returned. `--review-handoff` confirmed that valid preload retention succeeded, then exited 1 at `FAIL: server preflight keeps exhausted valid cache evidence advisory`. The handoff fixture uses a valid 900,000-byte JSON cache and a valid approximately 15 MB preload ZIP/JAR: retention leaves too few bytes to reread that cache, while small local metadata remains safely inspectable. `--review-cache` passed before the fix (8 assertions), establishing existing malformed-cache behavior rather than replacing it.

After the fix, the same build and flag commands exited 0: zero-budget adapter 3 PASS, actual server handoff/controller 4 PASS, malformed-cache preservation 8 PASS. The zero-budget fixture now returns advisory sort evidence. A 32-byte collector budget still preserves a later inspected `Known` local entry after skipping a larger valid cache. The actual controller's server report has `canLaunch=true` with a warning `mods` item, creates no journal/consent/staged state, and `profileLaunch` reaches the absolute test-mode real-start prohibition. Tests continue beyond `Retained` into the collector, native report, and controller launch boundary.

### Fresh scoped regression commands

```powershell
./Build-Tests.ps1 -Entry PZ3D.LaunchPreflightTests -Output artifacts/preflight-tests.exe
& 'artifacts/preflight-tests.exe'
./Build-Tests.ps1 -Entry PZ3D.ModConfigurationsTests -Output artifacts/task1-fix2-ModConfigurationsTests.exe
& 'artifacts/task1-fix2-ModConfigurationsTests.exe'
./Build-Tests.ps1 -Entry PZ3D.SaveProfilesTests -Output artifacts/task1-fix2-SaveProfilesTests.exe
& 'artifacts/task1-fix2-SaveProfilesTests.exe'
./Build-Tests.ps1 -Entry PZ3D.LaunchProfilesTests -Output artifacts/task1-fix2-LaunchProfilesTests.exe
& 'artifacts/task1-fix2-LaunchProfilesTests.exe'
./Build-Tests.ps1 -Entry PZ3D.JavaApprovalsTests -Output artifacts/task1-fix2-JavaApprovalsTests.exe
& 'artifacts/task1-fix2-JavaApprovalsTests.exe'
./Build-Tests.ps1 -Entry PZ3D.ModernControllerTests -Output artifacts/task1-fix2-ModernControllerTests.exe
& 'artifacts/task1-fix2-ModernControllerTests.exe'
git diff --check
```

All six builds and EXEs exited 0 with clean compile output: preflight 145 PASS, mod configurations 308 assertions/0 failed cases, save profiles 62 assertions, launch profiles 15 PASS, Java approval success, and controller 69 PASS. `git diff --check` exited 0. The prior 130 preflight assertions remain passing.

Only `LaunchPreflight.cs`, `SaveProfiles.cs`, `LaunchPreflightTests.cs`, and this report changed. Self-review confirms cache exhaustion is caught at the byte-collection boundary rather than masking downstream malformation, safely inspectable entries remain available, and no real Steam/game/save mutation or process start was performed. No budget, native contract, memory limit, session guard, or unrelated behavior was relaxed. Item 3 is addressed; no blocker remains.
