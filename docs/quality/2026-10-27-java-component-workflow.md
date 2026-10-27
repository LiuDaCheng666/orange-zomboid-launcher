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

## Task 4 — preserve uninstall intent across upstream updates

Generic uninstall still retains the bundled Lua/default selection. Reconciliation now includes uninstall intent and its preceding disable/block metadata receipts, even when the latest uninstall has no metadata entries. It preserves freshly downloaded JARs, captures current declarations/hashes, and transactionally retires every contributing intent into a metadata-only suppression receipt. Subsequent enable/restore uses current metadata and never replays obsolete quarantine payloads. Committed enable/metadata restore forms an intent boundary; a later explicit block/uninstall remains effective.

The private receipt field `supersedes` records the owned intents retired atomically at receipt commit. Cancellation and pre-commit crash rollback preserve current upstream files and old intent; committed crash recovery finalizes the new suppression rather than undoing it. Public host actions and production UI are unchanged.

Record growth is handled at the existing 200-record boundary. Before committing compacted records, the exact prior audit bytes are saved as `history-<SHA256>.json` inside the private component folder. Active/restorable suppression receipts, every forced-owner receipt, newest effective enable/metadata-restore barriers and the committing receipt remain live. Original transaction backups are retained. The existing bound still applies if more than 200 simultaneously active/protected records leave nothing safe to compact; no active/restorable records are discarded to force a fit.

Test-first evidence and commands:

- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/JavaComponents.test.ps1 -EvidenceDirectory artifacts/java-components-task4-red` — RED, expected uninstall-update suppression assertion failed.
- Same runner with `artifacts/java-components-task4-capacity-red` — RED, expected normal repeated-update compaction assertion failed at 200 records.
- Same runner with `artifacts/java-components-task4-final` — GREEN, **265 checks**. Includes direct uninstall updates, JAR-only reinstall, disable→uninstall chains, repeated reconcile, fresh-JAR hash preservation through enable, cancellation, subprocess crash/recovery, newer enable/later block intent, exact audit archive, forced-owner/restorable-uninstall preservation and committed compaction crash recovery.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/JavaComponentWorkflow.test.ps1` — GREEN, **55 checks**.
- `powershell -NoProfile -ExecutionPolicy Bypass -File tests/JavaWorkflowNativeUi.test.ps1` — GREEN, actual DOM→production host policy/controller→isolated files.
- `node --check tests/JavaWorkflowNativeUi.cjs` and scoped `git diff --check` — pass.

The native test copies actual ShadowZ (3800671550), Project Viewpoint Minimap (3810179295), and Tactical Grid Inventory (3810674933) packages read-only. ShadowZ is actually disabled/uninstalled via clicks; the harness then copies the source into the isolated Workshop directory and adds a marker entry to the copied JAR plus a current metadata edit. Clicking Refresh preserves the updated JAR and shows 已屏蔽; clicking Enable restores exact current author metadata, keeps the new JAR hash, and a later Refresh is unchanged. The copied active ShadowZ JAR SHA256 is `9f0a494a003343599fedfc642101eed4a3be893de74291e6f0fdb3aaec66f1d4`. Minimap's `42/media/java/pz3dMinimap.jar` layout and TGI's `42/media/java/client/TGIViewpoint.jar` layout both receive actual native install, disable-confirm and enable clicks; Lua/default selection and JAR hashes remain intact, and original metadata bytes are restored.

All **832 original files** compare SHA256-equal before/after: ShadowZ24, Minimap112, TGI696. Per-file maps, exact isolated paths, request/response history, progress, final files and zero browser errors are saved in `artifacts/java-workflow-ui/native-ui-result.json`. Latest isolated root: `E:\AppCaches\Temp\Java界面-4d8a84b93d474e5289926f1947f7bde3`.

Opened and visually inspected through view_image: `artifacts/java-workflow-ui/real-package-updated-disabled.png`, `real-package-reenabled.png`, and `real-standard-layouts-enabled.png`. The screenshots show the expected blocked/enabled badges and continuing controls; technical details remain collapsed. Existing dark/light and narrow screenshots are also regenerated by the native run.

The extended harness initially caught its own SHA256 case mismatch (native uppercase versus Node lowercase), then its existing 2200 ms wait was too short once it copied and hashed the larger TGI package. Hash case is normalized and only test waits use 15/30 seconds; product behavior and timeouts were not altered. Final native run passes.

Boundaries: source packages are real read-only copies; upstream change is simulated only in the isolated copy. Game/Buddy bytes, external Steam download/search, process boundary and TestMode Steam-library resolution remain controlled fixtures. No live Steam action, original Workshop write, real user-game/save mutation, foreground control, actual game entry, JVM execution, publication, signing or deployment occurred. These UI/native/file checks do not establish real game-loading acceptance. Four existing CS0649 warnings remain.

Task 4 final native rerun: `node tests/JavaWorkflowNativeUi.cjs` — pass after adding a direct `existsSync` assertion that the actual ShadowZ `42.0` active JAR is absent following confirmed uninstall, before simulating the upstream copy. This supplements the prior row-state/file-list assertions. Source hashes remain unchanged and browser errors remain zero.


# Task 5 — Stable task-center progress, 2026-10-27

Implemented progress snapshot coalescing (120 ms), at most one `taskGet` in flight, and a trailing refresh whenever progress/manual reopen arrives during an outstanding read. The task dialog/list shell stays mounted. Cards are keyed by task ID and move from active to history without replacing the card; changed progress/text/events are patched in place, unchanged history signatures are skipped. User details.open and dialog scrollTop persist. Refresh failures retain the last successful list with an explicit retry. Initial loading/error/retry remain explicit. Java inline/global progress behavior is unchanged.

## RED / GREEN

`node tests/task-center-stability-ui-tests.cjs` first failed:

```text
AssertionError: progress must never blank the existing task list
0 !== 1
```

The old loader replaced task-center-content with its loading placeholder during an 80-event burst. The new test passes with the same card/details/history nodes, preserved scroll and collapsed details, completion moved into history, one pending request at a time, and six total reads across the initial request, burst, pending-close/reopen/trailing completion, intentional failure, and explicit retry. It also clicks through first-read loading -> failure -> retry -> empty successful snapshot. Burst messages and failures are controlled UI fixtures, honestly separate from native deletion.

Final executed commands (all exit 0):

```text
node tests/task-center-stability-ui-tests.cjs
node Orange3DTool/Web/cache-cleanup-ui-tests.cjs
node tests/java-workflow-ui-tests.cjs
node tests/component-discovery-ui-tests.cjs
pwsh -NoProfile -File tests/JavaWorkflowNativeUi.test.ps1
node --check Orange3DTool/Web/app.js
git diff --check -- Orange3DTool/Web/app.js tests/task-center-stability-ui-tests.cjs tests/JavaWorkflowNativeUi.cs tests/JavaWorkflowNativeUi.cjs
```

Native compile retains four pre-existing CS0649 warnings in GameLogModels/GameStartupPresenter/Program. This is an isolated harness compilation, not a production publication build.

## Actual native cleanup

The existing real DOM -> production ModernHostPolicy -> ModernController harness now creates exactly 80 owned `map_x_0.bin` files and `players.db` in an isolated Multiplayer server directory. Actual UI clicks choose ordinary cache cleanup, confirm once, open task center, and refresh the actual production journal. No deletion or file-result success is injected. Production ClearMultiplayerCache deletes the 80 owned chunks and reports all 80 per-file events. Final native file enumeration: 0 chunks; players.db still exists. Task-center snapshot calls during this native run: 6.

Isolated cleanup root: `E:\AppCaches\Temp\Java界面-f356f81c0fa349059d57a5e4f25554bc\用户\Saves\Multiplayer`.

Proof: `artifacts/task-center-stability/native-cache-proof.json`; synthetic DOM/request proof: `artifacts/task-center-stability/ui-proof.json`. The full native request/response/stage history remains `artifacts/java-workflow-ui/native-ui-result.json`.

Opened and inspected through view_image: `artifacts/task-center-stability/task-center-stable.png` and `artifacts/task-center-stability/native-cache-task-center.png`. The finished count, completed state, readable event history, buttons and retained collapsed details are visible; no loading-placeholder replacement, overlap or extraneous JSON is shown.

## Boundaries

Native cleanup operates only on harness-owned temporary files. The controlled burst validates DOM stability and request coalescing independently of real cleanup speed. Steam download/search and game/Buddy fixture boundaries from Task 3 remain; this change does not modify cleanup policy or any production C#. No real user cache, foreground app, live Steam state, website/deployment, VERSION, publication package or actual game startup was touched. The native harness also reruns the existing Java install/cancel/update/reconcile/layout paths and verifies the original 832 Workshop files are unchanged. Parent performs review and any candidate build separately.

## Final review: framework dependency ordering

Added an implicit Buddy DFS prerequisite for every generic Java component, covering Java dependencies whose author omitted require even when the selected target explicitly lists Buddy later. Buddy's own declared prerequisites and cycles stay in the same traversal; the former root-only post-processing insertion was removed.

Observed RED before production edits: disabled Buddy failed before its installer; an already-ready Buddy appeared after LocalJava; an implicit cycle through Buddy's own Java dependency was missed. GREEN: `pwsh -NoProfile -File tests/JavaComponentWorkflow.test.ps1 -EvidenceDirectory artifacts/java-workflow/final-fix-green` exits 0 with **68 checks**, including declared Lua prerequisite before Buddy, disabled/ready cases, one-time install/application and no writes on cycle. Exact RED probes and test boundaries are in `.superpowers/sdd/2026-10-27-java-component-workflow/final-fix-report.md`.

Rebuilt and reran `pwsh -NoProfile -File tests/JavaWorkflowNativeUi.test.ps1`: actual isolated native UI click flows pass; all 832 original Workshop source files remain unchanged. Result/history: `artifacts/java-workflow-ui/native-ui-result.json`; new isolated root `E:\AppCaches\Temp\Java界面-d0b522dedbd54d229216702cd15a47d9`. Viewed the regenerated `real-standard-layouts-enabled.png`: enabled state/action buttons readable, AlternativeB still unselected. These are isolated native/UI fixtures with copied real packages, not an actual foreground game launch or live Steam download. Manager code unchanged; its complete suite was not repeated for this workflow-only fix. Four existing CS0649 compile warnings remain.

## Local candidate checkpoint

Final whole-branch review and the scoped dependency-order correction review pass at `1364a3f`; evidence is `artifacts/java-workflow/final-review.md`. Parent verification also passed manager 265 checks, the affected UI suites and native harness. The final workflow suite has 68 checks.

`Build-VelopackLauncher.ps1` built the local preview into `E:\Projects\OrangeLauncher\dist\java-components-local-20261027\app`. It retains the existing 1.2.36 version label and is not a new published release. Independent verification with the bundled public RSA key confirms the bootstrap signature and all 21 listed file lengths/SHA256 values, with zero mismatches. Executable SHA256: `693FE6E729656601CB2551350AAF0B661F0C94AF38E07433FDC3642C4E1D879D`. Windows Authenticode status is `NotSigned`; RSA update signing does not change that status. Build/proof: `artifacts/java-workflow/local-candidate-build.log`, `local-candidate-proof.json`.

This checkpoint has no real-game startup acceptance, live Steam download, website/server/GitHub publication, or independent-launch-configuration implementation. The requested launcher/Steam/vanilla launch modes are a separate implementation and must not be represented as already present in this candidate.
