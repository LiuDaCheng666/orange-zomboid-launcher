# Task 1 — explicit live context and stock Java transport

Implemented standalone API only. Engine/controller have not been bound; their legacy/frozen behavior remains for Tasks 2/3. No website, deployment, game, Steam, Workshop or user-save source file was edited. No real game or foreground UI was launched.

## Consumer API

`PZ3D.LauncherConfiguration` (same-assembly class):

- `Resolve(string data, string game, string user)`: read-only resolution; canonical full real Game/User, SHA256 key of their Windows case-folded identities; root `data/launcher-configurations/<key>`. Missing selection defaults to `launcher`. No runtime validation during resolution/page browsing.
- `ParseMode(string)`: null/empty defaults to launcher; exactly `launcher`, `steam`, `vanilla` accepted; unknown values produce an explicit argument error.
- `Bind(string mode)`: immutable clone, no writes. `Select(string mode)`: persist selection atomically and return clone. Consumers must use the returned object.
- Properties: `Mode`, `Key`, `Game`, `User`, `Root`, `ConfigDirectory` (`buddy`), `RuntimeDirectory` (`runtime`), `ComponentsDirectory` (`components`), `DefaultModsPath` (`mods/default.txt`), `PreferencesPath`, `SelectionPath`.
- `Initialize()`: creates owned directories and missing preferences/Buddy config/default list only. Original ordinary `User/mods/default.txt` is copied byte-for-byte once; later changes do not overwrite the owned list. No original JSON/agent parsing. Initial Buddy config has empty trusted authors and preload, auto_fix_mod_order=false, fix_approval_dialog_cursor=false. Component split/import belongs to Task 2.
- `MemoryMb` (default 3072); `SetMemoryMb(int)`: owned persisted max heap (512–262144 MB); retains other preference fields. Initial heap is min(1024,max), no AlwaysPreTouch.
- `StockArguments(int buildMajor)`: resolved token array for 41 or 42. Explicit main `zombie.gameStates.MainScreenState`, real cachedir, Steam/native flags, owned memory. Steam mode rejects this transport; use existing Steam path. Vanilla has no optional Java agents in these tokens; effective zero-mod discovery is Task 2/3's responsibility.
- `BuildStartInfo(int buildMajor)`: selected real game's JRE, real game cwd, no shell/window; token quoting; SteamAppId/SteamGameId 108600; JRE bin and game prepend child PATH; child JAVA_TOOL_OPTIONS/_JAVA_OPTIONS/JDK_JAVA_OPTIONS/ZB_VERBOSITY removed, parent untouched.
- `QuoteArgument(string)`: Windows backslash/quote escaping, including trailing slashes and Chinese. To compose trusted owned agents, obtain stock tokens, insert owned JVM arguments **before** `-cp`/main, and assign the quoted complete token array to the sanitized ProcessStartInfo. Do not append JVM options after main. Do not import original vmArgs.

Owned writes reject placement inside real game/user and reparse ancestors. Atomic files use same-directory temporary + replace. Mode switching does not initialize component config or mutate original files. Consumers must explicitly Initialize before component writes. Missing JRE/JVM/native/classpath file errors occur at transport creation and name the missing file.

## Baseline evidence and limits

Actual read-only installation: `E:/Steam/steamapps/common/ProjectZomboid`, version.txt 42.21.0; jre64/release Zulu25.30+17-CA, Java25.0.1. Actual layout is projectzomboid.jar with root DLLs. Existing original JSON is visibly polluted with zbNative/startup agents and forced heap settings; it is **not** used as the baseline or parsed by this API.

B42 resolves only `projectzomboid.jar`, excluding the stock launcher's loose `.` extension to prevent stale loose classes overriding the packaged game. It sets native access/add-exports explicitly. Shared game JAR/native/JRE bytes are not authenticated by this task. Missing `win64` is allowed when required native files exist in game root. Required platform checks: java.exe, server/jvm.dll, steam_api64.dll, ZNetJNI64.dll, Lighting64.dll.

B41.78 uses the concrete 19-JAR Windows alternate-launch inventory plus required loose `zombie/gameStates/MainScreenState.class`; it omits Java25-specific native-access flags. Inventory corroborated by firsthand stock-launch console transcripts on the developer's support forum: [2023 support transcript](https://theindiestone.com/forums/topic/65229-cant-run-the-game/) and [another Windows transcript](https://theindiestone.com/forums/topic/69159-game-wont-launch/). These are user-provided stock-launch logs, not an authenticated depot manifest. No actual local B41 client installation was available for an actual game launch. B41 loose classes remain inherent to that game's distribution. Older B41 layouts and pre-fat-JAR B42 builds are not advertised as verified; missing required files return actionable errors rather than importing arbitrary original config or fallback agents. G1 is chosen explicitly for both baselines rather than copying OS-version-specific original GC options.

## RED/GREEN and real evidence

- RED: `powershell -NoProfile -ExecutionPolicy Bypass -File tests/LauncherConfiguration.test.ps1` failed with **Missing explicit launcher configuration context** before the implementation existed. The executable assertion (reflection) runs even when the new file is absent, rather than treating a missing source compiler error as RED.
- GREEN: same focused native C# fixture: 38 assertions. Three exact modes/default/persistence identity, byte-preserving one-time ordinary list import, owned memory/Buddy defaults, malformed foreign original JSON ignored and unchanged, B41 concrete files, B42 classpath/main/options, missing-asset naming, Windows Chinese/space/quote/trailing-slash parsing, all three child Java-option variables and parent preservation, vanilla no optional agents, Steam transport refusal.
- `... -File tests/LauncherConfiguration.test.ps1 -Production`: full production source registration compiles and the same focused fixture passes. Four existing CS0649 warnings in GameLogModels/GameStartupPresenter/Program; no new compiler warnings. This is a native process/file fixture, not native UI clicking.
- `python tests/launcher-context-buddy-probe.py`: actual installed Buddy2.3.4 copied to an owned **Chinese and spaced JAR pathname**, tiny compiled reflection main on selected game Java25, config via `-Dzb.config_dir` and agent `prop_prefix=zb,policy=deny-new`. Exit0; reflection reads auto_fix_mod_order=false and fix_approval_dialog_cursor=false from prepared owned config. Native wrapper is not used. 268 hashes covering Buddy source tree, game root files, game java.exe unchanged. Full argv/output/hashes: `artifacts/owned-launch/unicode-context-probe.json`. Compiler output/classes/config/logs remain under owned random temp root, user.home/cwd redirected there for the probe only. This probe deliberately uses a tiny main, not production game arguments.

## Remaining acceptance

Task 2 must implement effective component/mod discovery and hash-bound authorization; Task 3 must bind all entry targets, optional agents, startup/recovery/PID handling to this context. No actual production game menu, graphics, save/save-exit/relaunch, multiplayer, zero-mod vanilla, original loose-class binary provenance, UI clicks/screenshots, cancellation or launch recovery was accepted here. No release/publication authorized by these checks. The Unicode JVM probe proves transport/config reading only.

Foreign dirty deploy/site/test paths and untracked gallery/http_guard files were inspected and left unchanged. Only named Task1 implementation/test/probe/report paths and the one-line production registration are committed.
