# Task 3 report — launcher refresh and startup update notice

Implemented on `feature/self-hosted-updates`, starting from `90e6435`. No C# or website HTML/CSS changes. No game execution, real configuration writes, Steam operations, downloads, publishing, foreground browser, or antivirus workarounds.

## Result and native handoff contract

- The actual `app.js` / `app.css` / `index.html` launcher uses the existing top navigation, left launch controls and original dominant carousel. Compact control/profile cards, coordinated typography and spacing, readable solid lists, glass navigation/launch surfaces/hero overlay, 180 ms route/control transitions. Route animation runs only on a route change; status/progress renders do not restart it. Reduced motion disables transitions/route animation; unsupported backdrop blur has opaque fallbacks.
- Exactly three primary cards when the independent native contract is present: `buddy` / `3619862853`, `viewpoint` / `3809306528`, `chinese` / `3810637496`. Each consumes `{id,title,description,item,downloaded,configured,optional,version,...evidence}` directly. `version` remains native evidence; empty Buddy version displays “未声明”, never the combined base version. An accompanying compatibility `base` card is excluded from displayed status/counts. A legacy-only base fixture retains its existing combined card/action, without fabricating split evidence.
- Each selected card sends `componentsAction` with `{component:id,operation:'install'|'uninstall'|'update',confirmed:true}`. Existing warning/Java review flows remain. Chinese-only installation dispatches only `chinese`. Models, guns and fixes are a separate, collapsed optional disclosure. Existing all-component restore remains explicit; there is no automatic install-all action.
- The next-step card appears only for a missing game directory or unconfigured core component, and links to selection/component management without assuming every optional addon is needed. Memory/cache/workshop rebuild/mod/save/server/help access remains.
- Startup waits for a successful native `status`, then reads `coreUpdateStatus` and makes one background `coreUpdateCheck`. Existing Checking/Downloading/Ready/Restarting/PendingHealth work is observed without beginning another check. Invalid/failed/Latest results produce no global error or dialog. Available produces a dismissible notice that opens the existing updater; nothing automatically downloads/applies/restarts.
- Automatic discovery does not call `coreRequest`, increment the manual generation, or clear the manual download intent. Generation and native revision checks ignore late automatic responses; discovery progress cannot regress Downloading/Ready/Restarting. A dismissed same-version notice remains dismissed during this session. Dirty drafts remain in place.

Task 2 still needs to supply/integrate the independent native rows and operations. This task verifies the real UI against an isolated native boundary, not native game mutation behavior. Signature/transport code was not changed.

## RED / GREEN

Added `product-refresh-ui-tests.cjs` before production implementation. Initial run failed all five groups for missing functionality:

1. Primary core selectors returned `[]`, expected buddy/viewpoint/chinese.
2. Contextual next-step element was absent (4 s timeout).
3. No startup `coreUpdateCheck` was dispatched (4 s timeout).
4. Held automatic check never existed for the notice/draft test (4 s timeout).
5. Held automatic check never existed for the manual/Ready race test (4 s timeout).

Implemented until all five groups passed. Extended the same groups with independent update/uninstall payloads, combined-version rejection, passive Ready/Downloading startup behavior and malformed-protocol silence. Corrected the synthetic transport to `structuredClone` responses, matching native message isolation rather than sharing mutable fixture objects.

Final focused run also passed with `OZL_TEST_COVERS_DIR` set to an intentionally nonexistent directory. Author covers are optional: no ignored-directory dependency in a clean checkout. `OZL_CAPTURE_BUDDY_ICON` is an optional capture-only file supplied externally; no private path is committed.

```
PASS three independent core cards dispatch only the selected component; Chinese installs alone
PASS next step is contextual and existing tool routes remain accessible in both themes
PASS startup checks once after native readiness, silently handles Latest and Error without locking editors
PASS available startup notice is dismissible, preserves a dirty draft, and opens the updater only explicitly
PASS late automatic responses cannot replace manual download or Ready state, including progress events
```

Full `npm test` was run once after implementation, as requested: 30/30 groups. Subsequent compact-card/copy/fixture portability refinements were rechecked with focused tests, retired-gun tests and the entire screenshot matrix; the full suite was not repeatedly rerun.

### Full npm test output

```
> orange-zomboid-launcher-tests@1.0.0 test
> node Orange3DTool/Web/full-ui-tests.cjs

PASS eleven navigable routes remain visible in both themes and narrow windows
PASS named profiles keep rich metadata, stable sorting preview and stale dirty drafts
PASS profile copy into save stays a draft and dependency errors block applying a sort
PASS server sources import explicitly, hall adds a credential draft, and metric values are separated
PASS Steam browse never asks for credentials and download requires explicit confirmation
PASS global durable tasks retain unknown and partial progress and home reminder dismisses explicitly
PASS server editor retains its opening snapshot when list refresh arrives during editing
PASS read failure offers retry and stale search replies cannot replace latest query results
PASS a dismissed reminder returns only after native due state clears and a new threshold arrives
PASS a profile read uses its returned collection snapshot even after an unrelated list refresh
PASS loading a profile cannot replace manual save edits made after its request started
PASS durable history distinguishes completed failed cancelled interrupted and unknown event progress
PASS navigating to another page starts its content at the top of the scroll panel
PASS native Steam Unix seconds render the actual update year rather than milliseconds in 1970
PASS hall search ignores replies from older requests after a newer result is visible
PASS help source buttons treat native component titles as text instead of active HTML
PASS next elapsed reminder threshold appears even if polling misses the intermediate false state
PASS refreshing existing hall data visibly shows an indeterminate read until the response arrives
PASS original thumbnail reads follow the viewport with at most two in flight rather than an entire save collection
PASS global due reminder appears on other pages once per threshold and preserves dirty configuration
PASS rich ordered lists show actual one-based positions after a reorder
PASS gun help exposes author controls without launcher aim editing or persistence
PASS old preference data cannot restore the retired mode controls
PASS retirement waits for explicit gun reapply and uses the normal component install route
PASS read-only gun help stays contained with author covers in both themes and narrow windows
PASS three independent core cards dispatch only the selected component; Chinese installs alone
PASS next step is contextual and existing tool routes remain accessible in both themes
PASS startup checks once after native readiness, silently handles Latest and Error without locking editors
PASS available startup notice is dismissible, preserves a dirty draft, and opens the updater only explicitly
PASS late automatic responses cannot replace manual download or Ready state, including progress events
30/30 full UI test groups passed
```

Existing dedicated updater regressions also pass:

```
CORE UPDATE UI: PASS independent lane, native versions, stale events, drafts, both small themes, durable outcomes/history
CORE UPDATE READY UI: PASS one-time/manual consent, native boundary payloads, identity/generation/async guards, local/native blockers, stale consent, double clicks, game running, Escape, both 820x600 themes
4/4 retired aim UI groups passed
```

## Screenshots and personal inspection

`node Orange3DTool/Web/product-refresh-capture.cjs` captures the actual launcher in headless Edge through `refresh-fixture.cjs`. The fixture supplies Chinese demo names, reserved `example.invalid` connections and no real identity/password/path. Original local author cover bytes remain unchanged. Game version is 42.21.0; the illustrative component evidence is Buddy 2.3.4, Viewpoint 0.1.6.1, Chinese 1.19.0, matching the independently audited versions provided by the main task.

- 66 screenshots: all 11 routes at 1440 / 1280 / 768, dark and light, with horizontal overflow assertions and browser-error checks.
- Six contact sheets: `artifacts/product-refresh-ui/contact-{dark,light}-{1440,1280,768}.png`.
- Additional expanded optional components and available-update/missing-core screenshots in each theme.
- All route compositions were personally reviewed through the six contact sheets; full-size home/components/mods/maintenance/startup examples were inspected. Corrections included removing tall empty control/profile areas, moving help/maintenance headings ahead of their panels, and replacing wide poster strips with 96 px square thumbnails and ~234 px desktop core cards. Three core actions, optional addons and Java entry fit the desktop first viewport. Text and controls remain legible in narrow screenshots; long pages use the existing scroll panel.
- Ten sanitized **new actual UI** website images: `website/assets/launcher-{home,components,mods,servers,maintenance}-{dark,light}.png`. These replace no website markup and use no old UI screenshots. Task 4 should consume these files.

## Scoped files

- `Orange3DTool/Web/app.js`, `app.css`, `index.html`: production UI and startup coordinator.
- `Orange3DTool/Web/product-refresh-ui-tests.cjs`, `refresh-fixture.cjs`, `product-refresh-capture.cjs`: tests, isolated fixture and screenshot exporter.
- `Orange3DTool/Web/full-ui-tests.cjs`: registers five new groups.
- `Orange3DTool/Web/retire-aim-ui-tests.cjs`: explicitly opens the newly collapsed optional section before existing gun-help assertions.
- Ten `website/assets/launcher-*.png` exports listed above.
- This report.

## Self-review and limits

`git diff --check` is clean. Existing callbacks, mutation boundaries, menu routes and Java/launch/restore safeguards are retained. No extra JS deployment resource was added; production changes stay in the already embedded files. Capture-only native mocks cannot prove native independent installation/uninstallation; Task 2 integration and final release review remain with the main task. Original covers are displayed with `object-fit:contain`; missing local covers remain explicit neutral placeholders. No native version values are inferred from screenshots or combined legacy records.
