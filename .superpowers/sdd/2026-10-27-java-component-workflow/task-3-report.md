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

Background headless Edge executes actual app.js DOM clicks → production `ModernHostPolicy.Validate` → production `ModernController.Execute` → isolated real files. Native evidence: `artifacts/java-workflow-ui/native-ui-result.json` (request/response history, stage events, file lengths/hashes, zero browser page errors). Latest isolated root: `E:\AppCaches\Temp\Java界面-6e9bcf48dd734966b8b965d5c911c4d3`.

- Bundle download creates actual metadata/JAR files. Selection changes no default mods. Enabling AlternativeA adds LocalLua plus the selected Java mod; AlternativeB remains absent. Existing Unrelated mod, KeepMap, and custom fields remain.
- MissingDependency returns unresolved with its concrete ID; NeedsMissing is absent from default mods. ManualOnly returns unsupported, without enabling it.
- A cancellable fixture download reports actual fixture byte count (128 B); cancelling preserves the downloaded file without adding Cancelled to defaults. Clicking enable afterward succeeds, and refresh reaches reconciliation before list.
- ShadowZ item3800671550 is copied from real Workshop data read-only. Direct Java-tab installation enables ShadowZ; disable removes actual Java metadata declarations while preserving Lua/default selection. Cancelled uninstall preserves active JARs. Confirmed uninstall quarantines the active B42 payload, shows 已卸载, and preserves separate manual-install content. No fake deletion result is injected.
- The source package SHA256 map is captured before and after, compared equal, and saved as sourceProof. Original `E:/Steam/steamapps/workshop/content/108600/3800671550` bytes are unchanged.

## Screenshot inspection

Manually opened through view_image: `artifacts/java-workflow-ui/dark-desktop.png`, `dark-narrow.png`, `light-desktop.png`, `light-narrow.png`, `cancel-progress.png`, `unresolved.png`, and `real-package-uninstalled.png`. Desktop is 1100×850, narrow is 390×850. No horizontal overflow. A first narrow screenshot exposed actions squeezing names; CSS now gives the text and buttons separate rows. Cards, counts, progress/cancel, closed technical details and post-uninstall status were visually inspected. Screenshots are local evidence artifacts, not committed binary assets.

## Coverage boundaries

External Steam download/search results, process/game/Buddy bytes are controlled fixtures; `TestMode=true` permits isolated Steam-library resolution. Production controller/workflow/metadata/file operations are real. ShadowZ package bytes are real. No live Steam subscription/download, real game mutation, foreground control, actual game startup, server multiplayer loading, publication build, VERSION change, deployment, or signing was performed. Existing real Workshop metadata/JAR survey is separate evidence (`artifacts/java-workflow/real-workshop-metadata.json`, `real-jar-proof.json`); this UI run does not imply every Java mod is supported. Parent will validate a local candidate and address uninstall/update reconciliation separately.
