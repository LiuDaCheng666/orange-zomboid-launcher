# Java component workflow UI acceptance — 2026-10-27

## Implemented

The component installation dialog submits once to `javaComponentsInstall`. Generic enable submits directly to `javaComponentsEnable`; native workflow owns download, recognition, dependency handling, and actual enable. Multi-component bundles display returned package rows with individual enable buttons. Java page also exposes a direct “安装工坊组件” button. Refresh invokes `javaComponentsReconcile` before rereading inventory. Managed framework controls retain their specialized native adapter.

Cards show name, enabled state, version/source, original Workshop link, and actions. Namespace, preload, and technical limitations are collapsed; actionable failures identify specific missing IDs/reasons. Native stage text and actual counts/bytes are forwarded without fabricated scan percentages. Cancel, search, and navigation remain usable. Uninstall retains one confirmation. Subsequent Java management clears stale install success summaries. The optional preview receipt ID is always sent as a string because production controller requires that field.

## Test-first evidence

- `node tests/java-workflow-ui-tests.cjs` RED: expected new install request never arrived (2500 ms timeout) because old UI required another confirmation and used steamDownload.
- Same command GREEN: single submit, bundle choice, one-click enable, original Workshop bridge, reconcile-before-read, unsupported/unresolved reasons, indeterminate real counts, cancel/retry, modal close, search/navigation during work.
- Native regression RED: production Preview failed `缺少有效参数：receiptId`; sending empty receipt ID fixes real integration.
- Native regression RED: old install success summary remained after actual uninstall (`1 !== 0`). Clearing that summary fixes the misleading page.
- Java-tab installation affordance RED: missing `[data-component-discover]` timed out; same browser test GREEN after adding the direct button.

## Executed commands

```text
node tests/java-workflow-ui-tests.cjs
node tests/component-discovery-ui-tests.cjs
node tests/java-components-ui-tests.cjs
node tests/java-block-ui-tests.cjs
pwsh -NoProfile -File tests/JavaWorkflowNativeUi.test.ps1
node tests/JavaWorkflowNativeUi.cjs
node --check Orange3DTool/Web/app.js
git diff --check -- Orange3DTool/Web/app.js Orange3DTool/Web/app.css tests/component-discovery-ui-tests.cjs
```

All targeted UI tests pass. Native compile emits four existing CS0649 unused-field warnings in GameLogModels/GameStartupPresenter/Program. It compiles production sources for this isolated harness, not a publication package.

## Native click and file results

Background headless Edge executes actual app.js DOM clicks → production `ModernHostPolicy.Validate` → production `ModernController.Execute` → isolated real files. Native evidence: `artifacts/java-workflow-ui/native-ui-result.json` (request/response history, stage events, file lengths/hashes, zero browser page errors). Latest isolated root: `E:\AppCaches\Temp\Java界面-475130804ef4444b9c1ea2fe9ce3fcf6`.

- Bundle download creates actual metadata/JAR files. Selection changes no default mods. Enabling AlternativeA adds LocalLua plus the selected Java mod; AlternativeB remains absent. Existing Unrelated mod, KeepMap, and custom fields remain.
- MissingDependency returns unresolved with its concrete ID; NeedsMissing is absent from default mods. ManualOnly returns unsupported, without enabling it.
- A cancellable fixture download reports actual fixture byte count (128 B); cancelling preserves the downloaded file without adding Cancelled to defaults. Clicking enable afterward succeeds, and refresh reaches reconciliation before list.
- ShadowZ item3800671550 is copied from real Workshop data read-only. Direct Java-tab installation enables ShadowZ; disable removes actual Java metadata declarations while preserving Lua/default selection. Cancelled uninstall preserves active JARs. Confirmed uninstall quarantines the active B42 payload, shows 已卸载, and preserves separate manual-install content. No fake deletion result is injected.
- The source package SHA256 map is captured before and after, compared equal, and saved as sourceProof. Original `E:/Steam/steamapps/workshop/content/108600/3800671550` bytes are unchanged.

## Screenshot inspection

Manually opened through view_image: `artifacts/java-workflow-ui/dark-desktop.png`, `dark-narrow.png`, `light-desktop.png`, `light-narrow.png`, `cancel-progress.png`, `unresolved.png`, and `real-package-uninstalled.png`. Desktop is 1100×850, narrow is 390×850. No horizontal overflow. A first narrow screenshot exposed actions squeezing names; CSS now gives the text and buttons separate rows. Cards, counts, progress/cancel, closed technical details and post-uninstall status were visually inspected. Screenshots are local evidence artifacts, not committed binary assets.

## Coverage boundaries

External Steam download/search results, process/game/Buddy bytes are controlled fixtures; `TestMode=true` permits isolated Steam-library resolution. Production controller/workflow/metadata/file operations are real. ShadowZ package bytes are real. No live Steam subscription/download, real game mutation, foreground control, actual game startup, server multiplayer loading, publication build, VERSION change, deployment, or signing was performed. Existing real Workshop metadata/JAR survey is separate evidence (`artifacts/java-workflow/real-workshop-metadata.json`, `real-jar-proof.json`); this UI run does not imply every Java mod is supported. Parent will validate a local candidate and address uninstall/update reconciliation separately.

## Task 3 review correction round 1

- RED: `node tests/java-workflow-ui-tests.cjs` failed `conflicting install is unavailable during workflow` (`true !== false`). GREEN after disabling only conflicting install entry points during local writes, and guarding dialog submit before closing it. A dialog opened before work begins retains `999777` and shows a concrete busy reason; no second request is sent until the occupied operation ends. Retrying submits the retained link exactly once.
- Java page shows one inline stage/count/cancel panel. Its global task display is hidden only while this Java workflow is active on the Java page; leaving for Home restores the global panel and returning hides the duplicate. Durable task-center records remain untouched. Search and navigation remain usable.
- Browser bridge rejection (`下载连接中断，请重试`) now has explicit clicked regression evidence, with controls released for retry. A partial native result shows actual enabled DependencyDone, B write failure, downloaded item, and pending recovery, and does not claim complete success. The next successful operation clears old errors.
- GREEN commands: `node tests/java-workflow-ui-tests.cjs`, `node tests/component-discovery-ui-tests.cjs`, `node tests/java-components-ui-tests.cjs`, `node tests/java-block-ui-tests.cjs`, `pwsh -NoProfile -File tests/JavaWorkflowNativeUi.test.ps1`, `node --check Orange3DTool/Web/app.js`, and targeted `git diff --check`. The native harness was rebuilt; its cancellation click also asserts one visible progress panel, disabled conflicting install, usable search, and restored global progress when navigating away. Four existing CS0649 warnings remain unchanged.
- Reopened `artifacts/java-workflow-ui/cancel-progress.png` through view_image after the fix: one visible indeterminate bar with actual 128 B fixture count and one cancel action; navigation and search are visible and usable. No production C# was changed. Coverage boundaries above still apply.
