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
