# OZL game-window loading component

User approved the game-side Java component on 2026-10-25, including a refined orange animation and live left/right logs. This extends the already approved layout; it does not introduce another native loading window.

## User-visible behavior

The launcher submits the normal Steam launch. Once the game creates its graphics context, OZL draws inside that game's window through its loading stages. The same window proceeds to the main menu. World loading uses the same presentation while preserving connection errors, authorization, and required game input. Steam's own launch dialog remains Steam-controlled.

The center uses the existing orange logo, a restrained rotating arc and breathing light, with stage transitions. A thin segmented stage track and a separate measured Java heap meter sit above it. Left-aligned lower-left rows show actual stage/mod/script work; right-aligned lower-right rows show only actionable problems, their mod, Chinese meaning, and a short original excerpt. Both areas stay below one quarter of the screen height. No placeholder warnings, raw JSON, fabricated completion percentage, or synthetic log scrolling. Normal loading without actionable issues leaves the right area empty. Animation continues while a long operation emits no logs; reduced motion uses a static mark and discrete stage changes.

## Early loading and trust

ZombieBuddy's supported early mod preload requires a ZBS author identity published on Steam plus explicit user approval. This project currently has no established OZL ZBS author signing identity. Do not publish profile content, invent an author identity, alter approvals, or weaken Buddy policy.

Use a launcher-owned Java startup agent for the OZL code only. It is bundled with the launcher, hash-verified when deployed, installed as one exact owned JVM argument, and gated by a fresh session file. It uses the pinned compile-time Byte Buddy dependency already present in ZombieBuddy's development dependency, packaged with its upstream license. It does not load arbitrary mod JARs. Buddy and its ordinary mod approvals retain their existing behavior. This achieves the user-approved early Java-component architecture without making a Steam identity setup a prerequisite.

## Runtime boundaries

Java source: `Orange3DTool/StartupSource/ozl/startup/`. Asset: `Orange3DTool/Assets/OrangeStartup.jar`. Build: `Build-StartupComponent.ps1`.

Owned deployment folder: `<game>/OrangeLauncherRuntime/startup/`. JAR: `OrangeStartup.jar`; session: `session.properties`; analysis exchange: `issues.txt`. JVM argument: `-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar`. Never allow a same-named arbitrary path or arbitrary javaagent through launch validation. Asset/file conflicts fail the component preparation without overwriting unknown user data. No game JAR/classes are shipped or replaced.

Session properties are ASCII, version `OZL1`: `session` is 32 lowercase hex; `created` and `expires` are Unix seconds (maximum lifetime 600 seconds); `game`, `user`, and `issues` are Base64 UTF-8 absolute paths; `theme` is `dark` or `light`; `reducedMotion` is a boolean. Validate identity, lifetime, canonical path containment, symlinks/reparse points, and file sizes. No server credentials appear in these files. Agent obtains its folder from its own JAR, not a command-line path supplied by a mod.

Issue exchange has eight UTF-8 lines: `OZL1`, session nonce, Unix milliseconds, Base64 title, Base64 Chinese meaning, Base64 mod names, Base64 original evidence, Base64 advice. At most 16 KiB, valid only for this session and recent timestamp. The existing C# analyzer supplies primary/actionable findings from the current launch's console. Writes are atomic and conditional on a matching current session. Java stage and mod/script activity come directly from instrumentation hooks; they do not depend on the launcher staying open.

Use the actual game render context and restore OpenGL state. Render on swap; permit a bounded idle-frame pump while boot loading so animation does not depend on new console lines. Never create another HWND or change foreground/z-order. Stop covering when the real main-menu state is entered; resume on actual world-load entry; release on exit or required interactive state. Any renderer/instrumentation failure disables only this presentation and leaves the game functioning.

## Verification

Test freshness/path boundaries, event aggregation, real stage counts, throttling, log privacy, actionability, terminal-state handoff, and exception fallback. Build against the local 42.21 game API as compile-only. Test production transformers against controlled game-shaped fixtures with actual Byte Buddy instrumentation. Verify actual game-window rendering in a real Steam launch, menu handoff, a second launch, and no external loading window. Capture and inspect animation frames and text placement at real resolution. Do not claim all-stage coverage from fixture screenshots. Do not publish a new release until the real startup route is verified. Do not touch website or server configuration.
