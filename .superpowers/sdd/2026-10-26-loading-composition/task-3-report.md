# Task 3 — native-compatible Workshop download presentation

Implemented the scoped download phase and the human-requested visual redesign. Root reviewed and accepted the revised 640×480/1280×720 frames, the scrolled narrow frame and light-preference narrow frame before final impact-selected verification. No complete Java suite or native 889 suite was rerun: the human explicitly replaced that earlier requirement with changed-feature/affected-dependency checks only.

## Behavior and ownership

`WorkshopDownload` binds Required IDs, cached Details, native items and Install acceptance to one actual connection owner and generation. Required counts all projects; Details joins public IDs, independent of order. A normal original Install return in WorkshopUpdate and an observed same-owner DownloadPending item are required before the distinct presentation starts. Initial already-ready/metadata-only operations remain native. Once a real transfer has started, native SubscribePending/CheckItemState gaps keep the coherent presentation in preparation mode; all Ready keeps a validation mode until original Success. There is no automatic Install, Retry callback, reenabled Install, new Steam query or presentation-side getState call.

Only current-item Long byte samples are accepted. Unknown total=1 remains indeterminate, including when native received exceeds that sentinel. Invalid types/ranges/IDs and late old-owner samples are rejected. Success clears the transfer observation without inventing final bytes. Ready is read from each original WorkshopItem.state after original update and includes already-installed projects. Measured rate requires a fresh monotonic same-item/stable-total delta, starts unknown, resets for item/total/byte regressions or preparation, and expires after two seconds without fresh observations. Cached titles are bounded and sanitized.

Mode enter/exit advances the existing model epoch so queued old download tickets cannot appear after Success/cancel/reconnect. Same-owner/generation and exact environment/MainScreen/Workshop page, list, prompt, buttons and scrollbar identities are required. Same-type widget replacement cannot inherit a prior layout owner. Unknown children, changed native enable state, malformed/replaced items, missing APIs, unsupported dimensions/FBO or reload yield to native presentation.

## Layout and original native behavior

The scene uses a dark indigo/violet backing even with light preference, retaining contrast for white native labels. Current title, ID, Ready count and server identity have separate lanes. One authoritative rounded bar is at least 20 px high, with percentage above on the right, real current bytes below on the left and measured rate below on the right. Preparation/validation is indeterminate. There is no aggregate server-mod percentage.

The original Workshop parent/create/prerender/render/row/event functions still run. Its exact list is moved into the lower region through the original ISUIElement setY/setHeight functions, verifying agreement of Lua and Java geometry. Native row heights/returns, selection, scrolling, joypad behavior, original disabled Install and Disconnect hitboxes/callbacks/previous screen remain authoritative. Geometry persists through input between frames. Restoration uses the current anchored bottom, including a native +100 px parent resize, rather than stale pre-resize heights; uncertain native geometry is not overwritten.

`NativeWorkshopRows` adapts only immediate primitives on the exact accepted list. Names use native Medium, metadata/status Small; measurement uses native TextManager/UIFont. Names and metadata are ellipsized into a left lane; status has its own padded right lane. The exact two inline progress fills from native drawProgressBar are suppressed so they cannot cover text. Original selection/badge colors become muted violet/green. Unmatched primitives remain native. No Lua row wrapper, row-height ownership, capture/replay framework, new modal/window or parent traversal suppression was introduced. Only the validated parent fill/title and MainScreen logo are retired.

GPU paths do not call Lua/UI setters. GPU FBO rejection marks CPU restoration pending; renderer failure disables the custom drawer, leaves essential native commands available in that frame and restores geometry on the following trusted CPU frame. Tests reject any fixture Lua setter during a GPU draw.

## Actual-Lua and visual evidence

The isolated fixture loads the installed WorkshopItemList doDrawItem/new and Workshop screen create/prerender/render/Required/Details/Progress/Install/Disconnect/event dispatcher, plus original ISUIElement setters and drawProgressBar and native list navigation/wheel functions. It uses fixture metadata and engine UI objects under `IsolatedLuaProbe`, whose loader rejects missing fixture zombie classes instead of falling back to real engine initialization. Original getState is counted exactly once per visible row; the observer adds none.

Native font measurement and GL text rendering now use the same chosen UIFont sizes and Microsoft YaHei metrics. GL commands on the list receive an equivalent list-bound scissor, including partial/scrolled rows. This is explicit fixture clipping, not a claim that a real game UI was launched. No actual Steam object was constructed or queried.

Meaningful RED evidence:

- `artifacts/workshop-download-red.txt`: actual Required confirmation runs, then the original component lacks the downloadPage contract.
- `artifacts/workshop-download-design-red.txt`: the original thin transfer rule fails the >=18 px main-bar contract; the percentage lane is also required.
- `artifacts/workshop-download-widget-red.txt`: same-type prompt replacement incorrectly inherited layout ownership before the identity repair.

The first `gl-redesign` images are **invalid acceptance evidence**: late test Details after Install correctly reenabled the original native Install, causing fail-open fallback, while that old GL assertion checked only native control survival. The fixture was corrected to use actual Required→Details→Install order, and GL now requires accepted owned violet mode before capture. The accepted visual set is `artifacts/workshop-download/gl-readable/`; the final current-payload set is `artifacts/workshop-download/current/gl/`:

- `workshop-native-640x480.png`, `workshop-native-1280x720.png`, `workshop-native-1920x1080.png`: known 30% current item, long Chinese/Latin name and fresh measured rate.
- `workshop-native-scrolled-640.png`: original wheel path plus a fixture smoothing step; row content clipped above original buttons.
- `workshop-native-light-640.png`, `-1280.png`, `-1920.png`: dark native-control backing under light preference.
- `workshop-stale-success-ticket.png`: queued old-mode frame rejected after native Success.
- `workshop-renderer-failure-native.png`: native fallback after contained owned renderer failure.

## Impact-selected verification

Initial final impact command: `./tests/WorkshopDownload.test.ps1`, log `artifacts/workshop-download/final-impact.txt`. The helper builds once and selects transfer/layout and shared native primitive/lifecycle dependencies; it is not the complete Java/native suite. Passed before the final widget-identity-only repair:

- Workshop main-bar/percentage contract PASS.
- Workshop sequence/layout 105 assertions PASS and hidden Workshop GL PASS.
- NativeCompositionTest 23 PASS; NativeLifecycleTest 17 PASS; NativeStatusTest 45 PASS, including Task2's early pre-owner statuses; QueueCompositionTest 11 PASS.

The run then correctly failed the old RefinementTransformerTest assertion that unowned Progress could update bytes. That assertion was updated to reject samples without accepted owner/Install; real owned progress remains exercised through the actual Workshop callbacks. Already-passing unchanged dependency scopes were retained rather than rerun.

After meaningful widget-replacement RED and the narrow identity repair, rebuilt the **current** component and ran only its covering sequence/GL tests, the previously failed refinement test, actual API checks and package/hash audit:

```powershell
./Build-StartupComponent.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid
./tests/Run-NativeCompositionFixture.ps1 -Probe WorkshopDownloadTest -Output artifacts/workshop-download/current/sequence
./tests/Run-NativeCompositionFixture.ps1 -Probe RefinementTransformerTest -Output artifacts/workshop-download/current/refinement
./tests/Run-NativeCompositionFixture.ps1 -Probe WorkshopDownloadGlProbe -Output artifacts/workshop-download/current/gl
```

- Current Workshop sequence/layout/identity: **109 PASS**, `artifacts/workshop-download-current-sequence.txt`.
- Current refinement premain/lifecycle/queue: **32 PASS**, `artifacts/workshop-download-current-refinement.txt`.
- Current owned Workshop hidden GL, narrow/light/scrolled/native-control/GPU stale/FBO/failure checks: PASS, `artifacts/workshop-download-current-gl.txt`, no GL errors.
- Actual GameClassProbe compiled alone with javac21 `--release 17`, then run with the installed JRE25, actual projectzomboid.jar and ZombieBuddy.jar from the accepted generated fixture directory: **18 class/API checks PASS**, `artifacts/workshop-download/current/actual-api.txt`. All actual loads use Class.forName(...,false,...); no actual constructor, getState, UI/Steam initialization or game window.
- Current package audit PASS, `artifacts/workshop-download/current/package-audit.txt`: no zombie/LWJGL/Kahlua/engine/fixture classes shipped, Byte Buddy license present, all three fresh accepted fixture Jar copies hash-match.
- Owned whitespace diff check PASS. javac is 21.0.12.1, generated production target is Java17; actual probe runtime is installed Zulu JRE25.0.1. No ECJ was required.

Known JVM25 Byte Buddy Unsafe deprecation warnings remain. The deliberate renderer-failure case logs `presentation disabled: NullPointerException`; it is an asserted containment test, not an unhandled failure. No native 889 executable was rebuilt/run for this revised test scope, and this report does not claim a newly verified native embedded resource. The parent candidate builder can audit its eventual embedded Jar against this exact payload.

## Exact payload and scope

Current ignored `Orange3DTool/Assets/OrangeStartup.jar` SHA-256:

`C641CF5A44276002694B0C955020AAF8DB95F18E13EE8644927BA9C73CABE4E9`

The three `current/` accepted fixture copies have that same hash. No later payload rebuild occurred before this report/commit.

Unchanged source PNG SHA-256: `602D843B5D7FBDAB9B37695B2D2FDABD5B0ADC1E4B26ECB1FD6DB69E75CCBF3D`; working tree and HEAD blob both `ba8f7f50a7595924279a4eee1f308e9ea7310214`. Unchanged ICO SHA-256: `78D9EB39880C023E9C37E367CCED77FB401B03CF67BB61B0720CC9CFB1A1E180`; working tree and HEAD blob both `5cda8b3552236d016b46a430bae265fd042d07d6`.

Only Task3 production helpers/integration, isolated fixtures, focused test runner and this report are owned by this task. Root's quality document, candidate builder, earlier candidates/public directories and foreign website/deploy/http-guard changes were preserved. No version/release/web/server operation, actual game/Steam/UI launch, process attach, real-path Controller/Prepare/Stage/Recover, game configuration mutation or performance measurement occurred. This is offline fixture/hidden-GL verification, not interactive game acceptance, and it makes no game-startup speedup claim.
