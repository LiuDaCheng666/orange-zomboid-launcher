# Responsive compact UI release — 2026-10-13

Code revision: `6e53616`, branch `fix/compact-responsive-ui`. Canonical source remains `E:/Projects/OrangeLauncher`. Main is preserved; no public third-party binary release has been made.

## Delivered private binaries

Directory: `E:/Projects/OrangeLauncher/dist/2026-10-13-ui-fix/`. A date-specific directory avoids replacing an older launcher executable that the player may still have open.

- `橙子启动器-便携版.exe`: SHA-256 `3E74B602ECEB5EF88BB2EBA7460D021EB4D8AAFA2F95F6103251B32D911A33A9`.
- `橙子启动器-安装程序.exe`: SHA-256 `E4998FB6434BB08537EA993EF8C9728732D9D926A6D097C3A3552C64E9FAACC6`.
- Registry-free uninstaller and existing usage guide included. All 11 private shader preset ZIPs and original-author covers are retained, not published to Git.

## Changes

Compact component cards retain names, version, configured/update status and install/uninstall controls. Long provenance and descriptions expand on demand. Mod catalogs use dense selectable rows and adjacent details. Home and the remaining routes have reduced empty space and clearer typography/status hierarchy. Green is configured/allowed, red is update/reapply/refused/error, neutral is absent or unknown. Configuration is explicitly not proof of current game loading.

Java review focuses undecided files, collapses green allowed files, prevents repeated grants, queues exact-hash decisions with fresh snapshots and applies the returned authoritative decision before status refresh. Capture-local metadata discovery removes repeated unrelated mod scans without caching stale files, skipping hash checks, or altering author trust.

Hall streams bounded validated batches and supports local name, player-count, latency and password filtering/sorting. Host receipt ordering and exact-request cancellation prevent stale operations from canceling the newest query. Slow Steam reads do not hold the local modification queue. Launch cancels and drains the launcher's own active reads before reaching game startup, so its transient Steam identity is released first.

Unsafe writes remain serialized and guarded. Each editor is locked only during its own save and subsequent authoritative reread, including its replacement selectors. Other editors/navigation/filtering remain usable. Save failures unlock without discarding drafts.

## Fresh evidence

- Source full C# self-test: exit 0, 437 PASS-prefixed checks, plus 308 mod-workspace and 62 save-profile assertions. `artifacts/responsive-native-final.log`.
- Final private EXE full self-test: exit 0, 437 PASS-prefixed checks. `artifacts/responsive-packed-final.log`.
- Final installer `--verify-setup`: exit 0; exact launcher/uninstaller extraction and scoped fixture uninstall verified without game/registry changes.
- Browser compact suite: 19/19; full UI suite: 21/21. Five existing suites also passed (transport/UI, profiles, Java/covers, inherited layout, extensions).
- Both themes and all 11 routes inspected; 22 native WebView captures succeeded. Populated workflow captures under `artifacts/compact-ui-shots/` show 12 complete component cards and 13 full rows in the focused 1080p mod list/detail view.
- Embedded `Modern.Web.zip/app.js` SHA-256 equals final source `Web/app.js`.
- Independent native and whole-branch review found receipt-ordering and pending-save draft regressions; both were corrected and independently re-reviewed with no remaining Critical/Important/Minor findings.

## Real read-only hall probe

Using the selected local game's Steam API, without subscriptions/downloads, game launch or real authorization writes:

- Pre-stream native loop: final result at 25,318 ms, zero incremental batches, 830 final valid servers.
- Stream implementation: first valid server at 2,825 ms; 87 batches; final result at 25,330 ms, 824 final valid servers.
- Parent launcher/probe did not load `steam_api64.dll`; the short-lived worker released its identity.

The different counts come from separate live network queries. 2.8 seconds is this test's measurement, not a latency guarantee. The full bounded scan may still take 25 seconds; already received results are usable during that time. Actual game connection/rendering effects were not tested by launching a real game.

Native source evidence: `docs/hall-stream-report.md`, `docs/approval-native-report.md`. Frontend evidence: `docs/compact-ui-report.md`. All write tests used isolated synthetic fixtures.
