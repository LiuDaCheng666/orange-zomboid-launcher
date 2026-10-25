# Unified native server loading and sustained orange growth

User feedback on published 1.2.21 shows the native MainScreen logo and connecting panel over the OZL scene, game mod loading still takes 20–40 seconds, and the orange appears to rotate only. This repair must reproduce the real composition, preserve working game interaction, and describe performance honestly. Existing authorization covers fixing these issues; keep all testing hidden because the foreground is in use.

## Evidence

Read-only findings: `artifacts/loading-repair-20261026/server-composition-research.md` and the latency investigation in that directory. Native MainScreen Lua draws its logo after our queued backdrop; ConnectToServer draws a .7W×.8H panel, identity/reminder decoration, status and Abort. Prior green-rectangle queue tests did not model this UI. Queue rendering omits its normal backdrop before the first place packet. Existing orange growth lasts about 1.1 seconds, followed by rotation.

Current logs show 40.374 seconds from Loading Mods to translations, including a 3.549-second approval wait. Buddy reports 30.776 seconds in its loader; a Shelves class-resolution/initialization/callback bracket accounts for 16.425 seconds. These observations do not establish CPU or I/O attribution or the overhead of OZL. Previous ~1.7-second benchmark covers launcher preflight and evidence validation only. Do not call it game startup timing or skip third-party initialization to manufacture a faster result.

## One coherent connection presentation

Keep native connection UI objects, traversal, input, callbacks and state authoritative. Add a Java-only adapter, active only for an accepted same-frame owned server backdrop and exact current native page identities. Do not rewrite game Lua or files. Suppress only the exact MainScreen logo, exact ConnectToServer decorative fill/borders/background/arrows and its known decorative title/server/user/reminder children. Retain native `connectLabel` and `backBtn`, reserving their existing center-status and bottom-control lanes in the custom scene.

Resolve current environment/table/element identities via explicit known field names and verified APIs; never scrape username/auth text or enumerate Lua tables. Validate current operation, page visibility, expected element/texture identities and FBO mode. Unknown extra/modded controls or required native pages veto suppression. A CPU-frame lease begins only when enqueue succeeds, ends on every exit, and cannot carry across Lua reload, generation or epoch. GPU tickets retain existing stale/FBO/context checks. A renderer failure restores native drawing next frame; essential controls are never suppressed even in the failed frame.

Yield before native workshop Required/Details interactions, authentication/QR, failures and character/spawn pages. Preserve original focus, parent visibility, event ordering and FromLua behavior. Resume only from the current operation after native success and confirmed page closure; do not infer readiness solely from a WorkshopUpdate enum or `connecting=false`.

For queue position >=0, replace only the known passive LoadingQueueUI decoration after accepted background insertion; keep original update/packets/Esc/controller cancellation. Show the actual cancellation hint. Before first position/immediate branch, enqueue once at Core.EndFrameUI entry after native black clear, while the QUEUE frame is still active. Preserve one original Core frame lifecycle; no nested frames, window, swap or forced background call. Unknown/unsupported routes remain native.

Preserve existing world/save error, done, click prompt and multiplayer SendDone behavior. Add transitions through connecting → queue → world → Done and failed save/world. No blanket UIManager skip or swap overlay over interactions.

## Growth that remains visible during a long wait

Retain the existing brand image and three scene layouts. Use a gentle repeating seed → body expansion → leaf unfold → settle/breathe cycle (about 7–9 seconds) rather than a one-second whole-image scale followed only by rotation. Rotation is secondary. Body and leaf can be drawn as code-rendered regions of the unchanged authenticated image; no new raster generation or unrelated icon redesign. Geometry/reveal is bounded and independent of progress. Reduced motion draws the complete stable logo, with no growth/rotation/pulse.

Production GL and offscreen canvases must share actual animation geometry and alpha/reveal semantics. Existing SceneProbe currently ignores logo alpha; correct that adapter before trusting the new contact sheet. Verify beginning, late-cycle and loop-boundary frames, including a stationary log, server/native status/Cancel composition, 640×480 through 3840×2160 and reduced motion.

## Performance and delivery

Keep a precise timing report of observed native/game stages and attributable limits. Do not silently disable/reorder Shelves/Viewpoint/other mods, alter memory or trust settings, cache executable authorization, or claim the game mod-loading interval is reduced without a comparable actual run. Fixing composition and animation is independent of optimizing third-party initialization.

Use TDD with real Lua-function fixtures plus actual API/bytecode probes and hidden GL, not only layout-only mocks. Source/payload tests and integration review precede a fresh local candidate6 containing the repaired agent. No version bump, server operation, website edit or automatic release in this repair; actual native interactive acceptance remains explicitly distinct from offline fixture verification.
