# Game discovery and launch classification verification

Source range: `ebe2072..029773b`. Candidate5 was built from `029773b91a5d38c3ee320bdaed404316e34dd318` on 2026-10-26 Shanghai time. The remote player's exact installation and JSON remain unavailable; the screenshots alone do not establish their root cause.

## Changes verified

- Preference-first startup now uses one bounded resolver. Registered Steam libraries precede conventional Program Files/ready fixed-drive Steam locations. Manual game/library/steamapps/common parent selection resolves to a validated game root; no recursive disk scan was introduced.
- Missing, stale, locked and malformed preferences/metadata fall back without crashing startup. Manifest components cannot escape the direct common folder. Root, metadata and elapsed-work limits and reparse refusal apply.
- Manual selection resolves once before recovery-owner admission and persists the same resolved path. Failed selection preserves the old preference and engine. Network, busy and pending recovery checks remain.
- Read-only Buddy validation counts actual loader arguments rather than arbitrary `zbNative` substrings. A single canonical native loader in a Windows branch is accepted; duplicates, unsupported custom/legacy forms, missing files and unknown Java agents remain refused. Cleanup rewriting is unchanged.
- A failed configuration check now leaves its dependent guard skipped/warning instead of implying a separate Steam failure. Actual guard checks still run for valid configuration. Selected-mod ambiguity and Steam/ownership boundaries remain.

## Evidence

All game/controller fixtures use fresh temporary paths and fake Steam evidence. No candidate/app/game entry point, Steam SDK initialization or foreground operation was performed.

| Gate | Recorded result | Evidence under `artifacts/game-discovery-20261026/` |
|---|---|---|
| Discovery initial red | 40 failures | `task1-red.log` |
| Priority/admission red | 2 failures | `task1-priority-red.log` |
| Final discovery fixtures | 53 assertions, zero failures | `task1-final.log` |
| Classification red | 12 failures | `task2-classification-red.log` |
| Dependent guard red | 1 failure | `task2-guard-red.log` |
| Classification/guard green | 55 + 11 checks, zero failures | `task2-classification-green.log`, `task2-guard-green.log` |
| Exact-final headless native suite | 889 PASS, zero FAIL; exit 0 | `task2-commit-check.log` |
| Current PC readonly discovery | Valid `e:\steam\steamapps\common\ProjectZomboid`, 26 ms for the resolver call | `current-pc-discovery.json` |
| Production candidate | Compilation/resource checks passed, exit 0 | `candidate5-build.log` |
| ZIP integrity | One EXE; packed hash equals built EXE | `candidate5-archive-verification.json` |

Commands are recorded in task reports under `.superpowers/sdd/2026-10-26-game-discovery/`. The full suite uses `PZ3D.GameStartupSuiteTests --headless`; the focused entries use `PZ3D.DiscoveryTests` and `PZ3D.LaunchPreflightTests --launch-classification/--dependent-guard`. Independent Task 1, Task 2 and combined reviews approved without material, deferred or parked findings. Existing unused-field CS0649 warnings remain; no warning-driven unrelated source changes were made.

## Delivered candidate

Folder: `E:\Projects\OrangeLauncher\dist\loading-refinement-20261026-candidate5`

- EXE: `橙子启动器.exe`
- ZIP: `橙子启动器-加载与目录识别修正版.zip` (17,132,095 bytes)
- Internal development version remains `1.2.20.0`; this is not a published release.

| Content | SHA-256 |
|---|---|
| EXE, including archived copy | `9DAE20FB9EA6822B4DB5101921F6D1BB367755419BC0F361ED711FFE7482ABD2` |
| ZIP | `62ED54F8D1E758EAD84D2CBB40E06C7243E5E06E48EEBD45FB2C55C5317647F8` |
| Embedded/current OrangeStartup.jar | `97F3811414D0FFA2FE19DAC318F06F927BCEFA7A6AD663FB471A586D56656745` |
| Freshly bundled/current app.js | `EA4BBC24528B5BC421ECD798336EC165A834D5EA1B0D26E02AE74280E90A84E2` |

Java loading source/payload and frontend source did not change in this repair. Candidate5 includes the earlier boot/save/server loading refinement and server information implementation; embedded resource hashes match current inputs. Unchanged Java and frontend suites were not repeated. Candidate4 remains untouched.

## Limits and publication state

Conventional fallback cannot locate an arbitrary unlisted custom directory; manual selection remains available. Unknown `version.txt` display is still honest and does not invalidate a complete game directory. Multiple native Buddy occurrences remain conservatively blocked until actual Windows branch combination semantics are established.

The current-PC observation proves only local readonly discovery. Candidate5 actual game/server loading acceptance and the other player's installation remain unverified. Candidate3's earlier user-confirmed game entry does not establish candidate5 acceptance. No software release, version bump, server connection or website/deploy/configuration operation occurred; all existing foreign site/deploy changes were retained. Keep this repair's review workspace until pending user acceptance is resolved.
