# Task 1 review fixwave report

Base: `90d2e7f5b2e44495db438af479b9a68ca5489706`.

Scope: the three Important findings in `task-1-review-result.md`. Read the full review, prior Task 1 report, and approved game-window design before editing. The starting tree already contained website/deployment/HTTP-guard work; those paths were left untouched and are excluded from this commit. Native startup ownership and release/build packagers remain outside this fixwave.

## I1 — actionable-issue clearing and freshness

Root cause: `Session.readIssue()` already represented a fresh explicit empty-primary packet as five blank strings and absence/expiry as an empty array. `LoadingModel.issue()` ignored both shapes after accepting an actionable issue. The renderer therefore continued to receive the old diagnosis.

Changed `LoadingModel.issue()` to clear the stored issue on an empty result, null, or a five-field blank-title result, and retain a cloned five-field actionable result. Invalid other array lengths remain ignored. No protocol, trust boundary, timestamp policy, or analyzer behavior changed.

Tests were added before the production fix using a real temporary session/exchange and the actual parser/model. They accept an arbitrary-mod actionable packet, decode and apply an explicit empty-primary packet, reaccept the issue then apply the real 31-second expiry result, and reaccept it then apply the real missing-file result.

RED: the focused `StartupModelTest` failed with `AssertionError: explicit empty-primary packet clears earlier actionable analysis` (`StartupModelTest.java:74` in that revision). The clear assertion was the first failure; the later expiry/absence assertions were authored in the same test before the fix. GREEN: the focused model/session run passed 28 checks, including all three clear transitions.

## I2 — texture unpack layout isolation

Root cause: the existing saved client pixel-store scope preserved inherited offsets but did not establish the tightly packed upload layout. Alignment/PBO handling alone did not neutralize row length and skip offsets.

Changed `GameRenderer.upload()` to zero `GL_UNPACK_ROW_LENGTH`, `GL_UNPACK_SKIP_ROWS`, and `GL_UNPACK_SKIP_PIXELS` inside the existing `draw()` client-attrib save/restore scope. Existing alignment normalization and PBO unbind/restoration remain in place.

Before changing production, extended the real hidden GLFW/OpenGL regression to set row length, row skip, and pixel skip to **1**. This deliberately short row stride corrupts the image under the old code while remaining within the allocated full image buffers; it does not use a large stride that could overread native memory. The test asserts restoration of all three fields in addition to existing scissor, texture-unit, viewport, PBO, GL-error and rendered-logo pixel/position checks.

RED: the focused hidden-context run failed with `AssertionError: game texture state prevented logo rendering` (`GlProbe.java:47`). GREEN: after the fix, the same probe passed rendered pixel checks and every state-restoration assertion. `GLFW_VISIBLE` remains false; the test never shows a window.

## I3 — locale-independent phase observation

Confirmed the supplied actual 42.21 bytecode ranges in `artifacts/ingame-startup/game-api.txt`: `GameWindow` resolves loading keys through `Translator.getText(String,Object[])` and passes the resolved result to `DoLoadingText`. A read-only `javap` inspection additionally confirmed the real Translator target signature without executing or initializing game code.

Added exit advice only to `zombie.core.Translator.getText` methods whose first argument is String and return type is String. Advice observes the original key and unmodified result; it does not replace the method, alter return values, change arguments, suppress original exceptions, or call the translator again. Only these six actual game keys can register phase labels:

- `UI_Loading_Scripts`
- `UI_Loading_Clothing`
- `UI_Loading_ModelsAnimations`
- `UI_Loading_OnGameBoot`
- `UI_Loading_Texturepack`
- `UI_Loading_Lua`

The synchronized model records exact resolved labels with their phase index, capped at 64 labels and 2048 characters per label. Reobserving a label refreshes its position; older dynamic pack labels are evicted. `DoLoadingText` consults this mapping before the existing literal English recognition. Registration alone never advances the stage. Arbitrary pack/mod names and original translated results remain unchanged. Existing early `Loading Mods` and `Loading Translations` observations still work.

Before the production hook/model change, added a real-agent Translator fixture and assertions for all six Japanese and Chinese phase labels, original returns and invocation counts, no phase movement on translation lookup alone, unrelated UI keys, a String-first String-return overload, original exceptions, an unrelated int overload, arbitrary Chinese mod/pack names, 64-label eviction, and retained current labels.

RED: the actual Java 25 premain/Byte Buddy transformer run failed with `AssertionError: localized Scripts advances via actual transformed Translator and DoLoadingText` (`TransformerTest.java:47`). GREEN: the focused real-agent run passed 51 checks.

Extended `GameClassProbe` from seven to eight actual game classes by adding Translator. It verifies matching methods and the actual String/Object[] -> String target, retaining `Class.forName(..., false, ...)`. The actual game classes are not initialized, so the probe does not launch the game or create a game window.

## Final verification

Ran focused tests while iterating, then ran the full suite **once** after all code/test changes:

```powershell
./tests/IngameStartup.test.ps1 *>&1 | Tee-Object -FilePath artifacts/ingame-startup/java-fixwave-tests.log
```

Exit 0. Results:

- 28 actual model/session/IPC checks.
- 10 shared production scene viewport/animation/reduced-motion checks.
- 51 actual premain/Byte Buddy transformer behavior checks.
- Actual hidden GLFW/OpenGL render and game-state preservation pass, including inherited unpack row/skip state.
- Eight actual 42.21 class transforms and Java 25 verification pass, including one actual Translator target; no class initialization/window.
- Existing package audit passes (no game/framework classes and required Byte Buddy license present).
- Scoped `git diff --check` passes for all changed Java source/tests. A broad check also reported pre-existing CRLF/trailing-whitespace differences in `deploy/README.md`; this unrelated file was not altered.

The existing SHA-input build cache rebuilt the local ignored JAR as needed. No build script or test runner changes were required: recursive test discovery already includes the new fixture. Generated JAR/cache and artifacts remain untracked/ignored rather than being included as release files.

Evidence: `artifacts/ingame-startup/java-fixwave-tests.log`; hidden renderer capture `artifacts/ingame-startup/tests/scenes/actual-gl.png`; shared scene captures in the same folder.

Known minor output remains the upstream Byte Buddy `sun.misc.Unsafe::objectFieldOffset` Java 25 deprecation warning. No JVM warning-suppression flags, security settings, approval policies, or Buddy trust rules were changed.

All work used silent background commands. No Sky/computer use, Steam/game launch, visible window, server operation, website deployment, or release was performed. These tests do not establish actual Steam startup, continuous in-game presentation, real approval/menu handoff, second-launch behavior, or absence of an external native HWND in that real route. Those acceptance items remain separately pending under the user's foreground-use restriction.
