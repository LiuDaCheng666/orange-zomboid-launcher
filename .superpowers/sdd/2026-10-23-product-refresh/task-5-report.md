# Task 5 report — plain copy, neutral covers, official QQ contact

Implemented on `feature/self-hosted-updates`, starting at `9d2d16c`. Integration/delivery remains controller-owned. No website page, site JavaScript, deployment, real game/server/player configuration, Steam session, or foreground browser was changed.

## Changes

- Removed all ten generic procedural route subtitles and the now-unused descriptions map: components, mods (including the exact highlighted “命名、整理与复用本地清单。先预览依赖顺序，再明确保存或启用。”), workshop, saves, servers, settings, shaders, memory, maintenance, help. The heading no longer creates an empty paragraph.
- Removed redundant filler under core components (“加载支持、全新视角…”), optional component disclosure (“只安装你选择的内容…”), both local mod catalog headings (“原作者标题、封面与依赖…” / “原作者封面与元数据…”), the task list (“实际任务事件与结果…”), home cover (“选择配置，开始游戏。”), and diagnostics (“汇总游戏与启动器日志…”). Actual state, counts, authors, descriptions, version metadata, dependencies, field help, permissions, recovery/error notices, and all actions remain.
- Added a compact help-only official QQ row: group `1124858561`, join, copy, and local clipboard success/failure status. No home button cluster was added. Join sends `openContact` with `{}` only after a click.
- Added `LauncherContact` with fixed `InviteUrl = https://qm.qq.com/q/cuLGbwUx4A`, strict empty payload resolver, TestMode launch guard, and a narrowly injected external-open boundary. Both trusted host validation and controller dispatch independently reject supplied targets. Native aggregate registration is through `ModernControllerTests.Run`, already part of self-test.
- Missing covers now use a neutral code-native outlined picture icon and “暂无封面” accessible label. Original author images still use the same `<img>` branch; lazy local cover reads/cache/concurrency and thumbnail logic remain intact. Missing workshop artwork uses the same neutral shape, with distinct pending/error labels where applicable.
- Capture-only current version is `1.2.14`; ordinary fixture defaults remain `1.2.0`. Normal captures wait for fonts and finite animations before screenshotting. Regenerated only the ten owned `website/assets/launcher-*.png` exports from the real launcher DOM.

## TDD evidence

Commands run from `E:/Projects/OrangeLauncher` in PowerShell, with background headless Edge only.

Initial RED command: `node Orange3DTool/Web/copy-contact-ui-tests.cjs`

- Heading test: `FAIL ... components`, assertion `1 !== 0` (procedural paragraph still present).
- Contact test: `FAIL ... locator.innerText: Timeout 4000ms exceeded`, waiting for `[data-contact]` (contact not implemented).
- The first placeholder test draft accidentally referenced the private closure function and errored; corrected it to inspect real rendered mod rows before implementation. The corrected RED run failed waiting for `.cover-missing[role="img"]` (placeholder not implemented).
- Summary: `0/3 copy/contact groups passed`.

Native RED command: `./Build-Tests.ps1 -Entry PZ3D.LauncherContactTests -Output E:/Projects/OrangeLauncher/artifacts/contact-tests.exe`

- Compiler reports CS0103 for missing `LauncherContact`, then `Test compilation failed`. This is an API-addition compile RED; runtime policy checks were executed after the minimal implementation.

Additional filler RED: expanded the actual rendered-route assertion before removing inner section filler, then ran `node Orange3DTool/Web/copy-contact-ui-tests.cjs`.

- `FAIL route headings omit procedural filler while preserving titles and controls`; actual home text still contained “选择配置，开始游戏。”.
- `2/3 copy/contact groups passed`.

GREEN command: `node Orange3DTool/Web/copy-contact-ui-tests.cjs`

```text
PASS route headings omit procedural filler while preserving titles and controls
PASS help contact copies exact official group and joins through an empty native payload only on click
PASS missing covers use a compact accessible native icon and retain original image covers
3/3 copy/contact groups passed
```

Native GREEN commands:

```powershell
./Build-Tests.ps1 -Entry PZ3D.LauncherContactTests -Output E:/Projects/OrangeLauncher/artifacts/contact-tests.exe
./artifacts/contact-tests.exe
```

Build exit 0; executable exit 0. Exact final stdout:

```text
PASS: contact resolves fixed official invitation
PASS: contact rejects supplied url
PASS: contact rejects supplied path
PASS: contact rejects supplied target
PASS: trusted page accepts contact action
PASS: untrusted page rejects contact action
PASS: host rejects contact URL payload
PASS: TestMode blocks contact launch
PASS: TestMode launches no external program
PASS: explicit contact opens only resolved invitation
PASS: controller contact reaches TestMode guard without launching
PASS: controller independently rejects supplied target
```

The clipboard API and native messaging are injected only at the real external boundaries. Assertions exercise actual rendered DOM, click handlers, payloads and copied text. Layout/image checks run at 768/1440 in both themes, including original `<img>` retention.

## Regression verification

- `./Build-Tests.ps1 -Entry PZ3D.ModernControllerTests -Output E:/Projects/OrangeLauncher/artifacts/contact-controller-tests.exe` then `./artifacts/contact-controller-tests.exe`: both exit 0. Includes contact policy plus existing controller/deletion/Buddy/network/cancellation/configuration regression coverage. The final two additional direct controller contact assertions were subsequently rebuilt and passed in the focused executable above.
- `node Orange3DTool/Web/full-ui-tests.cjs`: exit 0, `34/34 full UI test groups passed`. Run twice, with the final run after broader filler cleanup. Includes 31 prior groups and all three new copy/contact groups, rich profile metadata/sort/stale drafts, Steam credential boundaries, task outcomes, theme/route access and startup update behavior.
- `git diff --check`: exit 0, no output.
- `node Orange3DTool/Web/product-refresh-capture.cjs`: exit 0, `PASS 66 route/theme/width screenshots; 10 sanitized new launcher images exported; 6 contact sheets.` All route captures assert no horizontal main overflow. This is synthetic native data, not a live game or server test.

## Screenshot artifacts and personal QA

Actual route screenshots: `E:/Projects/OrangeLauncher/artifacts/product-refresh-ui/{dark,light}-{home,components,mods,workshop,saves,servers,settings,shaders,memory,maintenance,help}-{1440,1280,768}.png`.

Contact sheets: same folder, `contact-{dark,light}-{1440,1280,768}.png`. Additional disclosure/startup captures: `{dark,light}-optional-expanded-1280.png`, `{dark,light}-startup-missing-768.png`.

Personally inspected the two 1440 contact sheets covering all eleven routes and these full-size frames: `dark-help-768.png`, `light-help-768.png`, `dark-mods-768.png`, `light-mods-1440.png`. The help row is compact, readable, and contained in both themes at 768; controls are clearly separated. Mod placeholders fit the small catalog rows without oversized graphics; author/title/version/dependency/status remain legible and original available covers remain visible. Header spacing closes correctly after subtitle removal. Existing home hero image/layout remains intact.

Owned site exports regenerated: `website/assets/launcher-{home,components,mods,servers,maintenance}-{dark,light}.png`. No website source pages were edited.

## Limits / handoff

No actual external QQ/browser opening was performed: trusted target resolution, injected launch argument, TestMode guard and actual UI request are verified without opening external applications. Clipboard copy used an injected API boundary in headless tests; success and exact group content are checked. The native live clipboard remains subject to the host's normal clipboard permission support and displays a concrete manual-copy fallback on failure. Controller owns final whole-branch review, full native/core/deploy/website suites, publication decisions, changelog and Task 6 planning.
