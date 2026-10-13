# Native review follow-up

Reviewed the current uncommitted native changes on 2026-10-13 after the repairs to `docs/native-review.md`. Baseline remains `baseline-extensions-2026-10-13` (`a867ec8e9e2b9485f1c64e61f9859d710ecd20b8`). This report supersedes the earlier native readiness assessment. Scope also includes `SteamLauncherServices.cs` and `SteamLauncherNative.cs` for API boundaries, x64 layouts, worker persistence, cancellation, and resource release; Web and final installer/package verification remain separate reviews.

## Strengths and closed findings

- `ModernDispatchQueue.cs:7` provides a distinct `ExecuteSnapshot` path, and `ModernHost.cs:38` uses it for task/playtime reads. Fresh `ConcurrentSnapshotTests` confirms a snapshot completes while the queue is occupied and the queued mutation subsequently executes once. Explicit cancellation retains its separate generation path.
- `SaveProfiles.cs:49`–`:50` now normalizes B42 dependency qualifiers and reads the author `loadModAfter`/`loadModBefore` keys into the agreed DTO aliases. Fresh managed Buddy/Viewpoint/shoot/bridge activation and ordering regressions pass. The mod suite reports 308 assertions with zero failed cases; the save suite reports 62 assertions.
- `Program.cs:66` and `LauncherMaintenance.cs:32` report unknown progress for fixed phases; actual completion and measured counters retain numeric values. `PhaseProgressTests` passes.
- `LauncherJournal.cs:11`–`:15` parses and recursively scrubs structured JSON, including nested JSON string values and credentials containing embedded quotes/whitespace. Plain-text fallback redacts the rest of the sensitive line. `ModernController.cs:38`/`:122` extracts a safe message instead of logging full result/profile DTOs. The fresh volume test passes with bounded task records, and the extra probes return `ESCAPED-JOURNAL-LEAK=False`, `ESCAPED-DURABLE-LEAK=False`, and `QUOTED-JOURNAL-LEAK=False`.
- `GameFavorites.cs:18`–`:21` holds a read-only main-DB handle that denies concurrent writers, checks sidecars again after native queries, and retains source/stamp only inside native records. Import reopens and locks the exact source, verifies its stamp and sidecars, and keeps that lock through the DPAPI-backed profile write. The 15 MB synthetic fixture previously reproduced a new WAL being ignored during a read. After repair, 1/2/3 ms WAL-emergence probes each return zero favorites and an explicit writing warning; `FAVORITE-WAL-RACE-OBSERVED=False`. The original DB is never written by this code.
- `PlaytimeDayTests` now asserts five seconds on the current side of local midnight and ten seconds total, and checks the explicit waiting state. Existing sampling tests still confirm heartbeat gaps do not fabricate offline hours.

## Steam review

`SteamLauncherServices.cs:23`–`:34` projects request fields into a small action-specific payload; credentials/tickets are omitted. `DownloadVerified` at `:51`–`:53` requires explicit confirmation, exact item ID, successful metadata, both App108600 IDs, and a downloadable ordinary item before reaching the mutation delegate. The inaccessible subscribed-item fallback remains display-only and cannot authorize download.

Worker transport uses an owned directory with a protected current-user DACL, nonce-bound responses, bounded response size/time, cancellation, and cleanup. The dedicated Steam test executable freshly passes success, progress, wrong nonce, failure propagation, timeout, cancellation, and owned-directory cleanup. These transport fixtures start synthetic helper processes and make no Steam connection or mutation.

The reviewed delegates use x64 pointer widths, cdecl flat exports, one-byte native booleans, the 9,776-byte UGC detail buffer, and 376-byte server rows. The subscription count/list delegates include the documented locally-disabled boolean. UGC query handles release in `finally`. Hall requests cancel and release before the callback sink is freed, keeping its delegates alive through release. These lifecycle requirements agree with the official [UGC reference](https://partner.steamgames.com/doc/api/ISteamUGC) and [matchmaking-server reference](https://partner.steamgames.com/doc/api/ISteamMatchmakingServers).

The worker disposes native Steam before emitting its result; `SteamLauncherNative.Dispose` calls shutdown, restores process environment values, and frees the DLL. Parent request code does not load the Steam DLL. No further Important ABI, persistence, or deferred-release defect was demonstrated by code inspection or fixtures. This review did not repeat the root's actual read-only Steam connection/search/subscription/hall queries and does not independently certify their live results.

## Remaining issues

### Critical

None found in the reviewed scope.

### Important

None found after the verified repairs.

### Minor

No additional actionable Minor finding. The earlier midnight-assertion gap is closed.

## Fresh verification

Direct C# compilation to `artifacts/review-followup-tests.exe`, followed by execution, returned exit 0 and passed:

- native services, full launcher integration, favorites, mod configurations (308 assertions), and save profiles (62 assertions);
- concurrent snapshots, JSON/nested/embedded-quote privacy, bounded journal volume, phase progress, local-day split/waiting state;
- Steam unit boundary/ABI parsers and inaccessible-item display fallback;
- added escaped/quoted durable-journal probes and the synthetic concurrent-WAL probe.

A separate direct compilation of `artifacts/review-followup-steam-tests.exe` and execution returned exit 0, including the bounded worker transport cases. The additional review fixture source is `artifacts/native-followup-runner.cs`. No tracked implementation files or Git state were changed by the reviewer, and no real launch, login, Steam mutation, personal save/default/approval, or personal DB operation was performed.

An initial follow-up test-build invocation discovered that the updated build helper regenerated the ignored embedded-assets ZIP and raced with a second build. This was reported immediately to root; subsequent review builds used the compiler directly and wrote only artifact executables/fixtures. Final package assets must come from root's separately verified packaging build.

## Assessment

**Ready to merge within this reviewed native scope: Yes.**

The original five Important findings, the midnight Minor finding, and the follow-up journal/favorites edge cases have fresh passing regressions or artifact probes. This verdict does not replace final Web, packaging, licensed-asset/secret-publication checks, or claim real game entry/server login/process-tracking success.
