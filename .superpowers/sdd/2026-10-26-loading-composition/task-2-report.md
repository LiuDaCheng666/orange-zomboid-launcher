# Task 2 implementer report

Status: DONE

Current payload: see **Review fix round 1** below. The original full-suite evidence and hash are retained as historical evidence; the review fix has fresh focused verification and a new Jar hash.

## Result

BOOT uses a warm amber background with the growing central brand and meaningful stage subtitle. SERVER uses navy/cyan network activity, a separate information area, full wrapped endpoint, observed population/date/clock/world day, map and mod/project counts. SAVE uses forest/emerald map chunks and save/mode/world status. Known server information remains in the existing model through queue/world handoffs. Diagnostics retain right alignment and the relevant mod name.

LogoGrowth supplies one pure 8.4-second body/leaf growth cycle and one image partition/affine transform to the real GL and raster canvases. The initial entrance starts at seed size; subsequent body scale stays between .8 and 1, with independent leaf unfolding and restrained rotation. Animation uses the existing accepted-draw AnimationClock and never generates progress or events. Reduced motion draws the complete static mark. SceneProbe now honors fractional logo alpha.

Native connection status observation runs only after normal return of a whitelisted OnConnectionStateChanged event. It records the exact bounded localized connectLabel.name once per changed label. Direct known states, known Message suffixes and integer PlaceInQueue 0–255 are supported. Current owner, generation, epoch, environment, exact page/label identities, visibility, passive controls and failure/auth gates must all still match. Unknown/failure/auth payloads are not progress text. TestTCP stays TestTCP; Connected means joining, not world completion.

Read-only status resolution reuses Task1 identity validation but does not require useUiFbo=false: actual events run outside the render interval, where that flag is normally true. This path cannot arm a lease. The Task1 accepted-frame lease, suppression, Abort callbacks, native rendering and queue insertion policy are unchanged.

The original white native status/Abort need a dark backing, so a SERVER scene with validated native control bounds retains navy even when the preference is light. No new panel rectangle, native font/color mutation or native geometry change was introduced. Narrow viewport logs avoid the central Abort lane. Queue tickets inherit connection bounds, so their explicit owned 排队 stage retains its central Esc/controller cancellation hint at initial/immediate position -1 and positioned queues.

## RED / GREEN evidence

All paths below are relative to this checkout.

- `artifacts/context-growth/growth-red.txt`: old component fails sustained body growth at 4/7/11/16 seconds, beyond angle differences.
- `artifacts/native-composition/palette-red.txt`: old component fails distinct primary palettes.
- `artifacts/context-growth/alpha-red.txt`: original SceneProbe fails a known .5-opacity pixel (expected alpha 128).
- `artifacts/context-growth/status-red-final.txt`: final native-status fixture against the pre-status component fails exact localized SteamConnecting observation. The fixture executes the installed OnConnectionStateChanged Lua in the isolated installed Kahlua VM, with a fixture translation function and engine objects.
- `artifacts/context-growth/light-native-red.txt`: pale native backdrop fails the original white status contrast contract.
- `artifacts/context-growth/queue-initial-red.txt` and `queue-positioned-red.txt`: pre-fix Task2 component fails the central cancellation-line contract at -1 and 8. Activity log copies do not satisfy these assertions.
- `artifacts/context-growth/scene-final-green.txt`: final focused scene contracts pass, including queue -1/0/8, late body/leaf growth, continuous boundary, native lanes at 640–3840, full 253-character endpoint, right-aligned mod diagnostics and light native backing.

The native status fixture additionally covers SteamConnecting/AuthPending/UDPConnecting/GettingServerInfo/Connected; known Message(GettingServerInfo/TestTCP); valid/invalid queue format; duplicate labels; unknown states/fields; hidden/auth pages; same-generation owner replacement; changed environment; stale generation; throwing original handler; failure handoff. A proxy counts even silently caught reads of username text and verifies zero. Auth/QR field values are poisoned and never traversed by the observation path.

## Final validation

```powershell
./tests/IngameStartup.test.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid
& E:/AppCaches/Temp/ozl-lc-fc466548/build-tests.ps1 -Entry PZ3D.GameStartupSuiteTests -Output E:/AppCaches/Temp/ozl-t2-queue-final-35a0c178/suite.exe
& E:/AppCaches/Temp/ozl-t2-queue-final-35a0c178/suite.exe --headless
git diff --check -- Orange3DTool/StartupSource/ozl/startup tests/java/startup tests/IngameStartup.test.ps1
```

Final Java suite: PASS, log `artifacts/context-growth/full-java-suite.txt`. Counts: model/session 28; refinement 23; server information 11; A2S 92; scene contracts PASS; viewport/frame 63; growth raster 48; transformer 51; refinement premain 32; server-info premain 16; native Lua composition 23; lifecycle 17; native status 27; queue 11; hidden native-Lua GL 32; real GL state PASS; queued GL 17; actual no-initialization class/API probe 18; package audit PASS.

Final native headless suite: **889 PASS lines**, exit 0, ending `ALL GAME STARTUP AND RELATED REGRESSIONS PASSED`. Log `artifacts/context-growth/native-suite.txt`; compile log `native-build-final.txt`. It embeds the final Jar below from the fresh short output directory above. Owned diff whitespace check passes.

The initial full Java run found an obsolete queued GL pixel crop centered at x720; the server mark now belongs at x360. The fixture crop and first-visible pixel bound were updated to the new layout, preserving its small-first-frame/threefold-later-growth assertions. Final full suites were rerun after each actual production correction (light native contrast, then queue hint); prior logs are preserved as `full-java-initial.txt`, `full-java-before-light.txt`, `full-java-before-queue-hint.txt`, `native-before-light.txt`, `native-before-queue-hint.txt`. The last test-only strengthening for positioned queue central text was run focused after the final full Java run; it does not alter the tested payload.

Known Byte Buddy/JVM25 Unsafe deprecation warnings remain. The two intentional renderer-failure fixture cases log presentation-disabled NullPointerException and assert native control survival/following-frame restoration. These are expected containment cases, not unhandled test failures.

## Visual review

Root inspected and accepted the three context compositions, late body/leaf differences, native status/Abort spacing, narrow viewport information, observed world day 3 and light-preference native contrast. The GIF was shown to the human. Latest review artifacts:

- `artifacts/context-growth/final-contact-primary.png`
- `artifacts/context-growth/final-contact-growth.png`
- `artifacts/context-growth/final-contact-native-lanes.png`
- `artifacts/context-growth/final-contact-gl.png`
- `artifacts/context-growth/final-orange-growth.gif`
- `artifacts/ingame-startup/tests/native-composition/native-lua-light-1280.png`
- `artifacts/ingame-startup/tests/native-composition/native-lua-light-1920.png`

Full current production raster frames: `artifacts/ingame-startup/tests/scenes/` and `growth/`. Hidden native GL frames: `artifacts/ingame-startup/tests/native-composition/native-lua-growth-1280-t*.png` and `native-lua-growth-1920-t*.png`, including 4/7/11/16 seconds and 8.399/8.401 boundary frames. Reduced-motion pixel identity is checked for all three contexts at 0/4/7/11/16/60 seconds. Original-source pixel partition is checked across every pixel; alpha is checked with an independent half-opacity image.

## Exact payload and scope

Rebuilt ignored `Orange3DTool/Assets/OrangeStartup.jar` SHA-256:

`A2A5E9AD5967329327D9676D6CD1FB2A51A3FCEA7FF15061E75650DCC11C1EFA`

The final full Java accepted-premain fixture copy at `artifacts/ingame-startup/tests/game-4339bb9083f840bf9bc31d451695da1d/OrangeLauncherRuntime/startup/OrangeStartup.jar` has the same SHA-256. The final native executable was compiled afterwards from that unchanged Jar; hashing its actual `OrangeStartup.jar` manifest resource independently gives the same SHA-256. No subsequent rebuild or payload edit was made.

Original `Orange3DTool/Assets/orange-app.png` SHA-256 remains `602D843B5D7FBDAB9B37695B2D2FDABD5B0ADC1E4B26ECB1FD6DB69E75CCBF3D`; its working-tree and HEAD Git blob IDs both equal `ba8f7f50a7595924279a4eee1f308e9ea7310214`. No image/icon source or version metadata changed.

This is offline fixture/hidden-GL acceptance, not an actual interactive game run. No game/Steam UI, process attach, real-path Controller/Prepare/Stage/Recover, game configuration mutation/bootstrap, website/server/deploy action or publishing occurred. Temporary session receipt changes in hidden GL tests are confined to generated fixture directories. Foreign website/deploy/http-guard changes remain untouched. Workshop download implementation is Task3 and is not included here. The 20–40-second observed game/mod initialization interval has not been optimized or remeasured; no startup speedup is claimed.

## Review fix round 1 — native handshakes before Java connection state

Review `task-2-review.md` found a real event-order gap in commit `fbb603f`: statusBefore required a non-null connectionOwner, although serverBegin clears it and ConnectionDetails creates/enters ConnectToServerState only after the initial native handshake. The original native center label remained visible, but early SteamConnecting/AuthPending/UDPConnecting/GettingServerInfo events were absent from the new activity/detail observations. The previous fixture entered the Java owner before those events and missed this ordering.

Added a RED actual-Lua sequence: serverBegin → exact current native panel → SteamConnecting/AuthPending/UDPConnecting/GettingServerInfo with no Java owner → owner establishment → continued statuses. Against the original component it fails `pre-details native status captured before any Java connection owner: SteamConnecting`; log `artifacts/context-growth/early-status-red.txt`.

Minimal production repair: Event advice passes its actual declaring LuaEventManager class via `@Advice.Origin`. statusBefore verifies that event type, obtains its game classloader, and resolves the existing exact passive UI identities without depending on a not-yet-created Java owner. An owner that already exists must belong to the same loader. The observation still captures the optional owner and requires strict identity equality on exit, so a null→owner transition during the original handler is rejected. Existing generation/epoch/environment/page/label/visibility/auth/failure checks remain in force. No render lease, FBO, queue, callback, control or geometry policy changed.

The expanded fixture also rejects pre-owner unknown messages, hidden/auth pages, environment replacement, and new generation while the owner remains null; verifies the next operation's own event works; rejects owner establishment during a handler while accepting subsequent events under that owner; rejects an unverified event type; and confirms early observation never arms a render lease. Existing stale/post-owner/private-field/failure tests remain intact.

Fresh scoped verification:

```powershell
./Build-StartupComponent.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid
./tests/Run-NativeCompositionFixture.ps1 -Probe NativeStatusTest -Output artifacts/context-growth/early-status-green
./tests/Run-NativeCompositionFixture.ps1 -Probe NativeLifecycleTest -Output artifacts/context-growth/early-lifecycle
./tests/Run-NativeCompositionFixture.ps1 -Probe RefinementTransformerTest -Output artifacts/context-growth/early-hook-regression
```

- NativeStatusTest: **45 PASS**, `artifacts/context-growth/early-status-green.txt`.
- NativeLifecycleTest: **17 PASS**, `artifacts/context-growth/early-lifecycle.txt`.
- RefinementTransformerTest: **32 PASS**, `artifacts/context-growth/early-hook-regression.txt`; exercises the other Lua event branches, poison payloads, lifecycle and queue hooks affected by the advice signature.
- Actual GameClassProbe, compiled alone under `artifacts/context-growth/early-actual-classes` and launched with the installed JRE25/projectzomboid.jar/ZombieBuddy.jar against the accepted fresh fixture: **18 actual class/API checks PASS**, no game class initialization/window; `artifacts/context-growth/early-actual-api.txt`. This verifies the new Origin advice on the actual LuaEventManager overloads.
- Owned diff whitespace check passes. Existing JVM Unsafe warnings remain.

Current rebuilt ignored `Orange3DTool/Assets/OrangeStartup.jar` SHA-256:

`FC38249421E2AAC69F7251230636FF009184FACEF443B25EC49D5D13D03DBE6E`

The accepted-premain copy at `artifacts/context-growth/early-status-green/fixture-5c53f443bf254981b2e9fcd1842399ef/OrangeLauncherRuntime/startup/OrangeStartup.jar` has the same hash. Comparing every decompressed entry with the original RED fixture shows only `ozl/startup/Hooks$Event.class` and `ozl/startup/StartupRuntime.class` changed. Renderer/scene/assets and previously reviewed visuals are byte-identical. The earlier 889/native and full Java results apply to the original pre-review payload; this repair deliberately runs the scoped checks above, with the next combined full validation reserved for Task3 as requested. No actual game/UI/server activity or performance claim was added.
