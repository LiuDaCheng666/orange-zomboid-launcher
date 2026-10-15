# Final fix wave — M1 and M2

Branch `fix/ozl-library-scenes`, starting HEAD `efc114efc7156d9e6078c5ac791444715342c1fb`. Read the approved spec and the complete final-review.md before work. This is the one requested final fix wave: exactly two Minor findings, no production scope expansion. The progress ledger was not edited.

## Changes and regression evidence

M1: `richSaves` now renders `Multiplayer · 模组只读`. The local-cache explanation, delete action, disabled experimental launch and all native guards remain unchanged. Added a real rendered DOM regression to library-ui-tests.cjs: it checks the scoped badge, explanation, enabled local-cache deletion, disabled Multiplayer launch, and unchanged Sandbox badge. Only native I/O is synthetic; the real app renders and handles events.

M2: montage.py computes rows as `(len(files)+2)//3`. Added montage_tests.py, which copies the real script into an owned temporary TestFixtures child, creates 18 controlled images per theme, runs the script and verifies literal output dimensions 1500×1980 plus the actual pixels of captures 15, 16 and 17. Temporary fixture cleanup is limited to that owned child. Both production defects were tested before implementation, following the TDD and verification skills and writing-good-tests reference.

| Command | Before fix | After fix | Evidence under artifacts/library-ui-124/ |
| --- | --- | --- | --- |
| `node Orange3DTool/Web/library-ui-tests.cjs` | Exit 1, expected `Multiplayer · 模组只读`, actual `Multiplayer · 只读`; remaining 13 groups pass | Exit 0, 14/14 groups | final-M1-red.log, final-M1-green.log |
| `python artifacts/library-ui-124/montage_tests.py` | Exit 1, both themes actual 1500×1650 vs expected 1500×1980 | Exit 0, one test with both theme subtests and all six final-row pixel assertions | final-M2-red.log, final-M2-green.log |

## Fresh covering verification

Commands ran from `E:/Projects/OrangeLauncher`. All PowerShell gates use the existing runner under PowerShell 7.6.5; each runner records command arguments and child exit codes in commands.txt. No extra broad native rerun was performed by this wave because native sources are unchanged and the coordinator owns that gate.

```powershell
pwsh -NoProfile -File artifacts/library-ui-124/verify.ps1 -Gate Browser
pwsh -NoProfile -File artifacts/library-ui-124/verify.ps1 -Gate Visual
python artifacts/library-ui-124/montage.py
pwsh -NoProfile -File artifacts/library-ui-124/verify.ps1 -Gate Candidate -Candidate E:/Projects/OrangeLauncher/dist/1.2.4-ozl-local-final-20261015
pwsh -NoProfile -File artifacts/library-ui-124/verify.ps1 -Gate Proof -Candidate E:/Projects/OrangeLauncher/dist/1.2.4-ozl-local-final-20261015
```

- Browser: `logs-Browser-20261015-122833`, runner exit 0, exactly 17 entry commands and 17 `EXIT 0` records. All entries ran once in this final full gate. Includes library 14/14, carousel 19/19, home-cover 8/8, experience 73/73, compact 19/19, full 21/21, Buddy 11/11, memory 7/7, PCL 11/11, branding 3 assertions, plus seven unnumbered contract suites. Total numbered groups: 183. Intentional copy/layout contracts pass; no older wording assertions broke.
- Visual: `logs-Visual-20261015-122833`, runner exit 0, 36 refreshed screenshots across dark/light, 733×507, 1366×768, 1920×1080. Montage exit 0. Inspected both complete sheets, including full sixth row `*-servers-long-{1366,1920,733}.png`: all three cells per theme visible, square previews and wrapped long-name content retained. Compact source screenshots can require vertical scrolling, as documented previously; the sheet does not clip their images. Dark sheet SHA256 `C73B7099638CFEC67663F41F8316D345E7FCC229226AD5BC27E9B1C478B5FBD9`; light `5815BB3F854F10E304756EEF0CEB54FFC7EE1A662E0FDDE2B232047839829657`.
- Candidate: `logs-Candidate-20261015-122918`, runner and signed-local-build exit 0. New directory was absent before build; no old candidate or published directory overwritten. Existing approved signing key supplied by path only to the existing Candidate gate. Its contents were never inspected, printed, copied or regenerated.
- Proof: `logs-Proof-20261015-123024`, runner and candidate-proof exit 0. Transcript `candidate-proof-20261015-123025.log`. Uses actual old published 1.2.3 EXE byte loading without executing Main, accepts the new signed manifest, checks the exact old 18 owned paths, unchanged original home, final source app.js/app.css/guide/changelog, embedded scene PNG byte identity and 1672×941 decoding, ZIP equality, installer embedded ZIP equality and OZL metadata. Actual trusted candidate `--verify-setup` exited 0 in owned fixture TEMP/TMP, extraction and scoped uninstall with shortcut callback skipped.
- Actual final native candidate: isolated copy `artifacts/TestFixtures/t124-native-db3839a1`; native harness compiled and exited 0. JSON and screenshots in `artifacts/library-ui-124/native-t124-native-db3839a1`. Inspected native-data-image.png: crossroads scene displayed, OZL branding and controls visible. JSON: nativeTitle OZL, document title OZL, brand OZL, scene crossroads, dataImage true, width 1672, height 941. Preview paths remain isolated; no live-read.
- Native regression evidence is attributed to the coordinator: final-source `logs-Native-20261015-123137/commands.txt` was checked and contains 36 successful exits (18 builds + 18 entries). This supersedes the same-source earlier `logs-Native-20261015-121708` run, also checked with 36 successful exits. Task 4 records 1374 reported assertions/checks across these 18 entries. This wave did not modify C# or native/build inputs. A diff against the starting HEAD for `*.cs`, Build*.ps1 and signed payload documents was empty. The native compiler was explicitly released to the coordinator immediately after final Proof completed; this wave performs no further compilation.

## Final local candidate and hashes

Final directory: `E:/Projects/OrangeLauncher/dist/1.2.4-ozl-local-final-20261015/`. Earlier t4, t4-r2 and t4-r3 remain historical evidence, not final handoff. docs/releases/1.2.4-local-candidate.md now identifies this final directory, hashes, source fingerprint and proof. Signed payload documents were not modified after build; only the nonpayload candidate note and this report were written afterward.

| File | Bytes | SHA256 |
| --- | ---: | --- |
| orange-launcher-setup-v1.2.4.exe | 15157248 | 832D5A5A58D31E53990392907806E3B8010BF816384C4A7658D366510F38FE62 |
| orange-launcher-v1.2.4-win-x64.zip | 13497865 | E6E2CBA78A0D3D9044E3ABCE26A8FF07748DC1A9864EA8432142B69358998D6B |
| update-manifest.json | 15522 | 36312EA74CA4D315BA5502F84AB416FFD63B7ED60166B401EB4B3C5DD203031F |
| update-manifest.sig | 384 | E45404F602DD2008F33EF248DC3D4245FF1A24E28F3A7C776F31A7D7DCE5F308 |

Core EXEs retain internal filenames: 橙子启动器.exe `40D542119901D73DC3E3CDCD0AB22B0E263DDCFC2E5F9343AD046E9352411C76`; 橙子更新助手.exe `4F0E48C15DB49B182558BBD8FA825F627CC007F7CDBB0652E5674C5E539C366D`; 卸载橙子启动器.exe `78A51BE9313AF7A0BB01923AB20B9A9B8D69179C92D6C1CB7C9FCFE0862FDFE6`. All three and setup have FileVersion 1.2.4.0, description OZL, exact full product name `Orange zombiod launcher`.

Updated source-inputs.sha256 only for final app.js (`C48BFC98BA90745791E4575BD23AD5DDA75B0ED4E4F5058A0C54D3125028F6A8`). Every fingerprint entry was freshly checked against disk with no mismatches. Fingerprint file SHA256 `927119192DB256543B3B0D8E520167F66CFC9478ED03CD628D2341068C4A7F27`. Original home SHA256 remains `82FCDE2FDAF188629D9ED9903CEEB21FD9C1C99EA16CCFFB123E9A51FA529D01`. Published 1.2.3 manifest `6F03EF618F3C4AF86FD59078EC612B7E047CD7F56636D3DBFC8CA828AC1A0749` and signature `65B203D0803EB2D36CF898DD4ECFCB3A5AA99FADC4AD4E295A0CFD2DDC77AB6F` are unchanged and checked by final Proof.

## Limits and handoff

No real saves, server worlds, credentials, Steam/game processes, desktop, registry, paging, current install, or actual Windows recycling were accessed or modified. All preview/install operations used owned TestFixtures. No push, tag, feed, GitHub/release mutation or merge. Actual recycling, real installed upgrades, real game/server direct launch and online rollout remain untested as required. No new Critical/Important finding was introduced or claimed; independent scoped rereview remains the coordinator's next step. Full sheets and test logs remain local ignored evidence, matching prior artifact practice; the repeatable script, focused tests, fingerprint, candidate note and this report are committed explicitly.
