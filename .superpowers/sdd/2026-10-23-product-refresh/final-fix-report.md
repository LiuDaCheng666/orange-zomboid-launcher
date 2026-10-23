# Final fix wave — 2026-10-23

Base: d75c6fe. Implementation and regression tests: 4c5a85aa857b76bb5490aed224c38aac28a59443. Scope was the three Important findings and sensible Minor findings in final-review-findings.md; approved product-refresh and server-workshop-preparation specs remain binding.

## Results

- Recovery records the endpoint catalog revision at staging and compares it under the catalog persistence lock before accepting capture. A later explicit import wins. Unknown/legacy revision cannot overwrite an existing manifest; absent legacy catalogs can still be seeded. Missing events, stale capture and malformed UTF-8 leave existing records intact and do not prevent session recovery.
- Browser covers invalidate on catalog reload and at a 30-second fallback interval. Inflight responses carry their generation and cannot populate a later generation. The existing two-request limit, 30-entry bound, lazy visibility and original author images remain. Empty/failure entries also expire.
- Import acknowledgment clears only the submitted edit revision. Nonempty import text participates in hasUnsavedDrafts; only the import button is disabled while that request is pending. Ordinary navigation and editing remain usable.
- Backup operation labels now follow actual configuration/pending-loader paths, including Viewpoint-only filtered targets and full base targets.
- Capture-only Available version is 1.2.15 above candidate 1.2.14; normal fixture defaults stay unchanged. This is synthetic preview data, not a public release claim.
- Added actual watcher lifecycle/direct engine-switch checks and injected clipboard rejection tests for both site and launcher. Website server copy now states the concrete capability.
- Expanded amended catalog import/capture persistence and preparation Start transitions, plus browser import handling, into readable blocks.

No nonblocking finding was silently dropped. Broader formatting of untouched preparation Run/constructor methods is deliberately deferred: their behavior was not changed by this fix, and a whole-file rewrite would widen the final scoped review. No known functional defect remains from the supplied findings.

## RED → GREEN evidence

All Important regressions ran against unfixed production code first.

1. Native real temp-file session: import A, stage connection capture, explicitly import B, recover. Command:
   `./Build-Tests.ps1 -Entry PZ3D.ServerWorkshopTests -Output E:/Projects/OrangeLauncher/artifacts/final-server-tests.exe; & ./artifacts/final-server-tests.exe`
   RED: `System.Exception: FAIL recovery preserves newer explicit import`.
   GREEN after revision fix and added legacy/no-event/decode-failure checks: `PASS ServerWorkshopTests 81 checks`, exit 0.
2. Actual headless DOM with a delayed native acknowledgment, textarea changed from 123 to 789. Command: `node Orange3DTool/Web/final-fix-ui-tests.cjs`.
   RED: expected 789, actual empty string. GREEN: `PASS pending import preserves newer text and protects its draft`.
3. Actual DOM catalog reload with stable cover token and outstanding older request. Same UI command.
   RED: `obsolete pixels must never be installed`, actual old image count 2, expected 0. GREEN: `PASS manual catalog refresh replaces stable cover identity and ignores outstanding old response`. Final version uses real decoded 2×2 PNGs generated only in test memory, verifies naturalWidth, changes cached image again, advances the clock through expiry, and proves empty results retry after expiry.

An initial UI selector referred to a nonexistent refresh button and was corrected before accepting RED evidence: the test now rereads through the real route/load path. Expiry coverage initially timed out because refresh-fixture replaces setInterval; the isolated clock option now preserves real timers. These were test harness corrections, not accepted product failures.

## Focused verification

Run from E:/Projects/OrangeLauncher:

- `./Build-Tests.ps1 -Entry PZ3D.ServerWorkshopTests -Output E:/Projects/OrangeLauncher/artifacts/final-server-tests.exe; & ./artifacts/final-server-tests.exe`: 81 checks PASS.
- `./Build-Tests.ps1 -Entry PZ3D.CoreComponentsTests -Output E:/Projects/OrangeLauncher/artifacts/final-core-tests.exe; & ./artifacts/final-core-tests.exe`: five new report-label assertions and existing independent-core cases PASS; final `CORE COMPONENTS PASSED`.
- `./Build-Tests.ps1 -Entry PZ3D.CatalogCacheTests -Output E:/Projects/OrangeLauncher/artifacts/final-cache-tests.exe; & ./artifacts/final-cache-tests.exe`: PASS bounded display snapshots/shared discovery/external changes/engines/concurrency/controller invalidation/fresh receipts, including new active-watcher disposal and direct SetEngine assertions.
- `node Orange3DTool/Web/final-fix-ui-tests.cjs`: both temporal groups PASS.
- `node Orange3DTool/Web/server-workshop-ui-tests.cjs`: 4/4 groups PASS.
- `node Orange3DTool/Web/copy-contact-ui-tests.cjs`: 4/4 groups PASS.
- `python Orange3DTool/launch-bridge-lua-tests.py`: 7 PASS groups, including Required callback, malformed/no-event/writer failures and unchanged connection behavior.
- `node --test tests/website.test.cjs`: 10 PASS, 0 FAIL, 1 expected skip (live deployment metadata check requires OZL_SITE_URL).

## Final full regression

Performed once after all source fixes settled, on 4c5a85a:

- `./Build-Tests.ps1 -Output E:/Projects/OrangeLauncher/artifacts/final-native-tests.exe; & ./artifacts/final-native-tests.exe --self-test`
  Output saved in artifacts/final-native-tests.log. Completed with exit 0 and 1744 PASS-prefixed lines, including `PASS ServerWorkshopTests 81 checks` and `CORE COMPONENTS PASSED`.
- `node Orange3DTool/Web/full-ui-tests.cjs`
  Output saved in artifacts/final-full-ui-tests.log. `41/41 full UI test groups passed`, exit 0.
- `git diff --check`: clean.

## Visual check and boundaries

Only affected dark/light startup-missing-768 frames were recaptured with the real refresh fixture using {theme,capture:true,update:'Available',missing:true}, 768×900 viewport, #startup-update-notice wait and page.screenshot. Both PNGs under artifacts/product-refresh-ui were opened and visually inspected: readable 1.2.15 offer, no horizontal clipping, existing cover retained. No website PNG export was regenerated, so Task 6 server drawer exports remain intact.

VERSION remains candidate 14; public download metadata remains 13. No Steam/game/server/player operations, foreground windows, credentials, signing changes, publication or deployment occurred. Test executables and isolated headless browser/Lua fixtures are validation artifacts, not a release binary safety claim.
