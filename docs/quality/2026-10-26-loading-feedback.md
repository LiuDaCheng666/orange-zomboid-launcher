# Loading controls, feedback and launch delay — 2026-10-26

## Scope

Version 1.2.24 adds loading-screen enable/remove controls under Help, occasional themed waiting captions, explicit opt-in feedback to the first-party endpoint, a pending Steam submission guard, and reuse of unchanged mod-catalog display data. Feedback has its own sidebar route. The previous 1.2.23 session recovery remains in place.

The final central animation visibly cycles through a 24% seed, elastic fruit growth, hinged leaf unfolding, a settled breathing interval and smooth return. The actual production GL silhouette varies from 29 to 127 pixels in the fixed-clock fixture. The final loading JAR SHA-256 is `60F8D62965CB47BF654EE450770BA50F65C51D290568DD713EDA94535C8FD5EE`. Additional focused motion checks cover 62 model/raster/native-lane cases and 8 actual GL cases; reduced-motion output remains static in all three contexts.

Tests operate on synthetic temporary profiles, inert process identities, headless browsers and hidden rendering contexts. No foreground game/Steam operation or real player feedback upload was used. Only changed and affected functions were tested.

## Findings fixed during integration

- Feedback redraw originally destroyed shared notice/task nodes. Preserve both nodes across redraw; navigate through the actual sidebar and load another page in the regression.
- Draft editing originally discarded an independent availability/history response. Accept current metadata by request/view identity while preserving the edited description and revoking only obsolete previews.
- Prefixed and multiline chat text required additional redaction before preview. LF normalization keeps valid multiline descriptions inside the server limit.
- The actual HTTP transport needs one overall deadline, including a slowly dripping body. The isolated slow HTTP reply terminates at approximately 261 ms with the 250 ms test deadline.
- Initial server deployment rolled back after a post-HUP readiness mismatch. The owned files and empty private store are preserved; the repaired deployer must wait for actual listener readiness before one invalid-schema backend probe. No whole-site rollback is used.

## Targeted evidence

| Area | Verification |
|---|---|
| Consent/preview/redaction/receipts | 21 native checks |
| Upload deadline | Actual loopback slow HTTP response |
| Feedback interface | 10 browser groups; 820/1366 widths; dark/light |
| Feedback navigation | Actual sidebar, held status while typing, shared nodes, explicit catalog refresh |
| Launch submission gap | 27 native checks and 6 browser groups |
| Loading controls | 63 native checks; browser enable/remove/busy/running/retry |
| Prior loading component | 218 owned-component transaction checks |
| Catalog/cache/save validation | Focused cache fixtures, 84 save checks and 312 named configuration checks |
| Private feedback API/deployment | 21 isolated HTTP/store/deployment checks, actual Python 3.6 compile and Nginx syntax check |

Reports and rendering evidence are in ignored `artifacts/{feedback-client,feedback-ui,feedback-server,loading-humor,launch-pending,profile-latency}-20261026/`.

## Performance bounds

On the read-only local 668-Workshop-directory sample, stable full catalog discovery took about 2.4 seconds; retaining the existing bounded display cache avoided that repeated work, with approximately 11 ms still needed to serialize the roughly 587 KiB catalog. These are component measurements, not an end-to-end UI or game-start benchmark. First discovery, explicit refresh and authoritative write/dependency validation remain necessary.

## Publication boundary

The feedback feature uses a new private loopback service and exact additive API location in the existing Nginx guard. It preserves Caddy, current site, website content, software releases, existing guard rules and SSH authentication. Software publication uses the separate scoped publisher.

## Final live verification

- The repaired first install completed with state `/var/backups/ozl-feedback/20261026T093247Z-e34cf1fa/state.json`, phase `verified`. The failed attempt's four exact owned paths were preserved under its original backup before retry; none were deleted.
- The service is active as `ozl-feedback`, loopback health responds, and private directories/files have 0700/0600 modes. Website, Caddy and `current` hashes match the pre-install baseline.
- The packaged production `FeedbackUpload.Send` successfully sent synthetic description-only bytes through the real HTTPS endpoint; an identical retry returned the same receipt/time. SSH read confirmed the exact synthetic body, then that one test report was deleted. No player logs, hardware information or real feedback were uploaded during verification.
- Version 1.2.24 was published to the first-party source, then the 1.2.23 production updater checked and downloaded it to `Ready` before GitHub activation. The 1.2.24 production updater reports `Latest`. Both checks authenticated all 21 bootstrap files and used isolated locators without applying an update, invoking the program entry point or starting a game.
- GitHub public latest is `v1.2.24` (release ID `424060281`), with ten uploaded assets. All twenty anonymous full downloads across both origins matched local size and SHA-256; current feed metadata also matched.
- Final candidate archive/signature/icon checks passed; Defender reported no threats. Post-publication pinned-key SSH and SFTP checks passed. Scoped software publication's protected before/after snapshots match; `current` remains `/var/www/ozl/sites/20261024T195630Z-d3f3c044`.

Publication evidence: ignored `artifacts/publication-1.2.24/`, with `primary-receipt.json`, `old-client-ready.json`, `new-client-latest.json`, `public-download-proof.json`, `feedback-public-proof.json`, `feedback-live-audit.json`, `github-published.json` and `ssh-after.json`. Final package directory: `E:/Projects/OrangeLauncher/dist/1.2.24-animation-feedback-20261026-final`.
