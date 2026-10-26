# 1.2.27 startup latency and loading presentation

## Scope

Reduce measured launcher preparation overhead and refine the existing in-game loading presentation. No website, SSH, HTTP guard, feedback service, update transport, real game profile or installed game changes are part of this patch. Tests use isolated filesystem fixtures and hidden OpenGL contexts. The foreground game is left alone.

## Investigation and measured changes

The observed profileLaunch task lasted 25,226 ms. Its existing logs recorded 6,204 ms preflight and 3,753 ms final validation, with an 8,439 ms gap before final validation. At least 18,447 ms elapsed before configuration staging could commit and submit a process request. Task completion is not a measurement of the first visible game frame. The whole slow attempt was not reproduced.

Paired read-only measurements on the actual game sources reduced catalog reads from 696/716 ms to 302/287 ms and evidence verification from 642/636 ms to 281/266 ms. Each ancestor now needs one attribute read instead of repeated existence/attribute reads; reparse rejection and fresh validation remain. Byte comparison short-circuits identical array references and still compares every byte of distinct arrays. Isolated startup component preparation fell from 142–158 ms to 59–64 ms. These are component measurements, not a promised reduction of the whole 25-second delay.

All 137 actual translation files (21,792,164 bytes) staged into disposable profiles in 289–326 ms. No translation file size gate was added. Source inspection found no historical console analysis before a client exists, so the observer was not speculatively changed. New diagnostics separate network drain, recovery, component preparation, validation, staging, observers, Steam/process submission and worker startup to locate any remaining slow attempt.

## Presentation and rendering

The orange grows once, then uses a 30-second repertoire: a full eased turn, controlled leaf wave, anticipatory squash and hop, a settled tilt, and quiet intervals. Reduced motion remains static. Startup/server/save/workshop contexts retain their distinct colors. Server facts use aligned identity/address and three-column groups. The top checkpoint stepper follows actual stages. Thirty occasional PZ captions remain separate from real progress.

Text rasterization now caches a white glyph texture by size and string, applying the requested tint at draw time. In a hidden production GL context, 128 differently tinted draws of one caption changed from 96 cached textures / 44.25 ms to one texture / 1.91–2.22 ms. Pixel assertions verify both red and green tint and no GL error. This measures caption rendering only.

## Focused verification

- Native byte/reference and path tests, including changed bytes, missing ancestors, inaccessible paths and junction/reparse rejection.
- Launch timing callback failure, preflight evidence/focused sources, all-route final commit/cancellation, translation bounds, component bridge and startup presentation.
- Motion contract: 4,690 checks; humor 3,748; layout 9; scene contract; 63 viewport checks from 640×480 to 3840×2160.
- Raster: 228 checks including body and leaf bounds over 180 frames / 36 seconds at native 640×480.
- Hidden production GL: growth, humor, state preservation, tint/cache, full 36-second replay; isolated native Lua composition: 32 checks including status, Abort, failure and fail-open behavior.

Evidence: `artifacts/launch-latency-v127/native-evidence.json`, `source-freeze.json`; `artifacts/loading-refinement-v127/verification.txt`, actual GL GIF and contact sheet; `artifacts/renderer-v127/`.

The final user refinement removes the component's 33,333,333 ns idle-draw gate and uses a 1,000,000,000 / 60 ns target. Only full overlays on the existing render thread use this fallback. Game-produced frames have no added cap; native interactive routes and completed overlays are not repainted. AnimationClock remains monotonic-time based, so gestures/captions do not speed up. Actual local options already specify 60 FPS, uncapped FPS and vsync disabled. A global FPS getter override was deliberately avoided because the game also calls that getter while saving preferences.

The old JAR failed the new premain/hidden-GL 20 ms draw contract. The final JAR passed all 8 cadence/native-handoff checks, 20 scoped Lua-reload checks and the real GL glyph/tint test. Independent review reran the 8 checks and found no blocker. This proves removal of the 30 FPS gate, not sustained real-game frame rate on every system. Earlier unchanged animation/layout evidence used the preceding JAR; the final change only adjusts the runtime idle interval.

Final startup JAR SHA256: `95FAD5172750C94215BF206CF73575C8B9F4FC9013C350CAE55C5F0EDCDA6DD0`. Cadence evidence: `artifacts/loading-fps-v127/`; actual game bytecode inspection: `artifacts/loading-fps-bytecode/`.

No full suite or actual game/Steam launch was run for this focused refinement. Independent read-only review found no actionable blocker. It verified the native source freeze, reran byte/path/timing tests and newly compiled motion/humor/layout/scene contracts, inspected native composition images, and independently checked production GL text caching and tint. Package/publication evidence is recorded separately after completion.
