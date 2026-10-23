# Task 1 — Fluent shell and cover-first home

Implemented in the canonical checkout on `redesign/fluent-workspace`, based on `05f0b72`.

## Structure and scope

- Six existing primary route buttons now occupy a persistent 216px sidebar; below 900px it becomes a 72px accessible icon rail. Original inline SVG paths, visible active marker, aria labels and title hints are included.
- Secondary navigation moved into main. Actual game status and choose-directory remain in the toolbar. Desktop utilities stay inline; smaller windows expose the existing keyboard-operable menu.
- `arrangeHome` now orders the existing cover before the existing launch section. Configuration and target/launch controls form a horizontal work strip; actual summary and management disclosures follow below. Existing Java, recent targets, warnings, preview panels, next-step hints and carousel lifecycle remain intact.
- Consolidated old shell/top-nav/side-launch CSS selectors into one Fluent workspace section. Rule boundaries were normalized while filtering these superseded rules; unrelated feature declarations remain. CSS is smaller overall (69,940 to 63,570 bytes). Both themes use layered neutral canvas/chrome/cards and orange emphasis; bounded chrome/scene blur has solid fallback.
- No native contracts, dependencies, runtime assets, original image files, versions, game/Steam/config data, website assets or publication state changed.

## Tests and actual results

- RED before production: `node Orange3DTool/Web/fluent-ui-tests.cjs` — 1 failed, 2 passed. Geometry failed with `navigation must not overlap the work area`; this was a real DOM assertion against the old top navigation, not setup failure. Dirty focused memory editor and keyboard/resize baseline passed.
- Initial GREEN: same command — 3 passed. Checks cover both themes, navigation bounds at 1440×900,1280×900,768×900,1100×650,733×507,500×760, keyboard settings/help/theme/task access, and focused dirty state refresh.
- During full verification an open desktop dropdown intercepted startup-dismiss and restore clicks: initial `npm test` was 43/44 and motion was 5/6. The legacy desktop auto-open expects inline header utilities. Restored desktop inline presentation while keeping compact dropdown behavior; no original test was weakened.
- Final `npm test` — **44/44 full UI groups passed**, including the previous 41 plus 3 Fluent groups.
- Final `node Orange3DTool/Web/motion-quality-ui-tests.cjs` — **6 passed, 0 failed**.
- `git diff --check` — passed.
- Measured at 1440×900: nav right203, main left216/width1224; cover width1180 (96.4% of main), height432; launch begins16px below cover and shares its width.

## Capture and visual review

Command: `$env:OZL_CAPTURE_BUDDY_ICON='E:/Steam/steamapps/workshop/content/108600/3619862853/mods/ZombieBuddy/icon_256.png'; node Orange3DTool/Web/fluent-capture.cjs`

Final exit0: 66 route/theme/width captures, 6 contact sheets, 6 short-window open-tool captures, 2 optional-expanded and 2 startup-missing captures. Default capture writes only `artifacts/fluent-ui`; website copy is guarded by explicit `--export-website` and restricted to the existing ten filenames from1440 captures. No export flag used.

Inspected original baseline `artifacts/product-refresh-ui/dark-home-1440.png` and `light-home-768.png`. Inspected new full-resolution dark/light home/components/mods at1440, dark home at768, light733×507 tools and dark500×760 tools. New captures show the sidebar, broad cover above launch controls, correct original art and compact rail. The first capture exposed the dropdown interception; final screenshots show the corrected desktop inline utilities. Small-window open menus fit the viewport; the scrolling content retains access to the launch area below the cover.

## Files and self-review

`Orange3DTool/Web/index.html`, `app.css`, `app.js`, `full-ui-tests.cjs`, new `fluent-ui-tests.cjs`, new `fluent-capture.cjs`; this report.

Reviewed HTML and JS diffs: route selectors and native handlers unchanged; only two existing DOM placement statements changed in JS. Reviewed captures and test failures to correct the utility-overlay issue. Feature-page density is intentionally left to Task2. No known Task1 blocker. Short windows require vertical scrolling to reach the strip, by design; persistent navigation and toolbar remain accessible.

Commit: recorded by parent from the scoped Task1 implementation commit (report committed with implementation).
