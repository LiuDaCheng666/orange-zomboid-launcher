# 1.2.20 workflow verification

Software-only corrective release based on 537564b. The separate website and HTTP-guard work was excluded from the application changes and staging.

## Acceptance evidence

| Area | Verification | Result |
| --- | --- | --- |
| All existing Web suites | `artifacts/mod-workshop-repair/all-web-final/results-reviewed.json` | 33/33 suites passed, no timeouts. The original compact refresh-lock failure remains recorded; its corrected expectation passed 19/19. |
| Corrected workflows | `tests/workflow-repair-ui-tests.cjs` | 16/16 groups: quiet launch, process-stop consent/cancel/error/no-client, Workshop cursors and BBCode, mod drafts/save/sort races, inline details, accurate startup-parameter scan presentation. |
| Actual native mod persistence behind the page | `tests/mod-management-native-ui.cjs` | 5/5 flows, including repeated saves, save/enable and external-edit rejection. External launches are synthetic in this fixture. |
| Native mod/configuration validation | Fresh compiled `WorkflowRegressionRunner` | ModConfigurations 312, SaveProfiles 84 and PlayProfiles 759 assertions passed; launch, Java, process, cache and Steam suites also passed. |
| Startup regression | `tests/GameStartup.test.ps1`, output `startup-final3` | Native lifecycle, original launch/controller/memory regressions and real Lua harness passed. |
| Immediate loading and concurrency | `immediate-startup-review.md` and its referenced logs | Independent review and isolated lifecycle/presenter/controller tests passed, including delayed preflight paint, cancel/lease/target identity and Workshop reads concurrent with status. |
| Final production assembly file effects | `production-file-effects-final3.log` | Actual packaged controller saved 8 then 12 GiB across volumes, retained exact backup bytes, cleared both map formats, and fully cleared multiplayer fixture data while preserving single-player/mod data. |
| Logs | `logs-final3.log`, `production-logs-final3.log` | Real-log corpus checks passed. Actual production assembly distinguished four imported failure/entered-with-issues cases and located an unseen mod's stack frame. |
| Visual inspection | `page-visual-frozen.log`, `pages/`, real-catalog and details screenshots | 12 routes × 2 themes × 2 widths produced 48 captures with no horizontal overflow. Real local catalog and final expanded last-row details were additionally inspected. |
| Release inventory | `final3-package-tests.log` | Full and portable package signatures, exact shipped inventory and absence of retired auxiliaries passed. Update manifests use the project RSA key; this does not assert Windows Authenticode reputation. |
| SSH | `deploy/check_ssh.py` | Pinned host fingerprint, explicit Ed25519 key, SSH and SFTP checks passed; the physical website current path was unchanged. |

## Real desktop acceptance

The user explicitly authorized computer-use testing. The final3 portable candidate was launched using its exact `current/橙子启动器.exe` process path, rather than the separately installed D: application.

- Clicking Launch created the native OZL loading surface and started the actual selected game's executable.
- The loading surface yielded for ZombieBuddy Java approval. The human made the trust decision; the agent did not automate security approval.
- The actual Project Zomboid 42.21.0 main menu was observed with the enabled Java components loaded. No OZL loading surface remained over it.
- Force-close first displayed only the selected game's verified executable. Cancelling preserved the game. Confirming at the main menu closed the one test game; the button unlocked and the launch button became available. A repeat check reported no game process.
- Workshop opened without a keyword and returned approximately 70,011 entries, original covers and dates. Next requested a genuinely different second page; no false local-write admission error occurred in the final3 run. Author descriptions no longer exposed BBCode.

Windows separately displayed an `explorer.exe` system-error popup during acceptance (System event 26). It was not dismissed by automation. This observation does not establish its cause; it is not counted as a clean Windows-environment test or as a diagnosed launcher fault.

## Additional blocker reported before publication

The user correctly identified the old-parameter scan's false Steam-login message. Publication was paused: the original takeover path relied on registry account/PID fields even though official Steam SDK queries worked. This blocker was resolved before publication. Fresh native SDK evidence now binds the exact client root/PID/birth, App108600 and current account; account switching, invalid configuration and local-only scope remain guarded. Periodic status performs no identity probes and grants no recovery receipt. Independent native and Web re-review approved the fixes.

The final5 production UI was actually scanned on this registry-zero machine and reported “启动参数正常。” without a login/offline claim. The selected JSON/BAT were safe and unchanged; the verified Steam scope contained no legacy parameter conflict. Subsequent final6 Web recovery-discovery correction passed 16/16 focused groups and independent 2/2 discovery/consent probes; existing Buddy UI passed 11/11. Final6 options/Buddy/Steam/preflight/launch/settings-controller suites and exact release inventory passed. The packaged Web/app.js SHA-256 matches the frozen source.

A cross-process PowerShell reflection experiment cannot exercise this SDK transport because it deliberately starts workers using the current executable. That discarded helper was not added to the release or source; actual launcher UI scan supplied runtime SDK evidence. No production test hook or account-verification bypass was introduced.

## Coverage boundaries

Browser suites exercise real page code with synthetic native replies; the native persistence and packaged-assembly checks supply actual file-effect evidence. Deletion and failure branches operate on isolated fixtures. Live Steam browsing and the actual main-menu/force-close flow were tested separately. No claim is made that arbitrary third-party mods, every server, every Windows installation or every possible concurrent schedule has been exhaustively tested.
