# Local version library verification — 2026-10-26

Scope: Task 3 of the local preview plan. No game or Steam initialization, branch switch, download, real user profile mutation, server connection, publish, push or tag was performed.

## Implemented contract

- `VersionLibrary(dataRoot, legacyGame, legacyUser)` retains the original Steam row and original user folder by default. Persisted opaque IDs select registered installations. Each imported or preserved version has a new `dataRoot/version-users/<id>` directory. Launcher metadata stays in `dataRoot`; existing journals and backup identities bind the game/user pair.
- Import accepts an existing separate installation. The canonical Steam path must use “保存为独立版本” because its files can change when Steam changes branch.
- Preserve creates a complete byte copy in an absent destination. It copies empty directories, reads and verifies SHA-256, rejects reparse points and crossing destinations, rechecks the source before publication, and rolls back only its new copy. It never creates hard links or junctions.
- `SteamVersionWorkflow.BeginInteractive()` first preserves the current Steam installation in a sibling directory before returning the fixed Steam game-page URL. Steam's own properties UI provides the available branch choices and account entitlement. `State()` observes the local App108600 manifest and actual classfile version. `Complete()` requires a fully installed Steam manifest plus the requested major version and copies into the new destination; the manifest must remain unchanged through the final verification. Cancellation retains the earlier preserved version.
- Installation stages are `awaiting-steam`, `downloading`, `needs-check`, `ready-to-preserve`, `saved`, and `cancelled`. Download percentages alone never add an installed row.
- Managed versions use the selected `ProjectZomboid64.exe`, selected working directory, and one quoted `-cachedir=<UserPath>` argument. Child-only SteamAppId/SteamGameId identify App108600 without modifying global environment or account credentials. The default Steam row retains its prior Steam URI path.
- B41 uses a flat `mod.info` and `media/lua/client` session bridge; B42 keeps `common/42`. B41 catalog selection uses root metadata and excludes B42-only directories. The actual jar version takes precedence over stale text version markers. B42-specific retained components and translation generation are omitted for B41. Session recovery handles either owned layout and workers validate the registered game/user pair.

## Read-only ABI and command-line evidence

Inspected the installed `E:\Steam\steamapps\common\ProjectZomboid\steam_api64.dll` without loading it or calling any export. SHA-256: `e082bf5c9f881c822b1540a76b74f9d15e18019a73ebd206a559595badcb7f65`; file version `09.60.44.10`. Its exported names include `SteamAPI_SteamApps_v008`, `SteamAPI_ISteamApps_GetNumBetas`, `SteamAPI_ISteamApps_GetBetaInfo`, and `SteamAPI_ISteamApps_SetActiveBeta`.

The pinned [Valve SDK header mirror at Steamworks.NET 2025.162.1, commit 76db7889b475f9b31d420055d8ca3b805d8b1301](https://github.com/rlabrecque/Steamworks.NET/blob/76db7889b475f9b31d420055d8ca3b805d8b1301/CodeGen/steam/isteamapps.h) exposes API008. Its `GetBetaInfo` takes index, flags pointer, build ID pointer, name buffer/length and description buffer/length. The current API009 header adds a last-updated pointer, so binding the latest signature blindly to the installed DLL would be unsafe. The corresponding [flat header](https://github.com/rlabrecque/Steamworks.NET/blob/76db7889b475f9b31d420055d8ca3b805d8b1301/CodeGen/steam/steam_api_flat.h) specifies the object pointer first. [Valve's branch documentation](https://partner.steamgames.com/doc/store/application/branches) describes official Steam branch selection.

This preview deliberately delegates the interactive account/branch/download step to Steam. It does not expose an untested native branch-switch binding. Opening Steam alone is an interaction step, not completed installation.

Read-only `javap -c -p` inspection of the installed jar confirms `zombie.gameStates.MainScreenState.main` recognizes the `-cachedir=` prefix at bytecode offsets 67–91, removes it, trims the path and invokes `ZomboidFileSystem.setCacheDir(String)`. The setter stores the absolute folder directly; it does not append another `Zomboid` component. This supports passing the selected user folder itself. No real client process was started.

## Tests and boundaries

`PZ3D.VersionLibrarySuiteTests` combines the filesystem-backed version tests with existing launch, transport, save profile, mod configuration and junction-path regressions. Test files and synthetic Java classfiles live under a disposable runtime; only `javac` and junction fixture creation are executed. New test coverage includes record reload, exact legacy fallback, settings/mod/save isolation, separate writable copies, empty and Chinese paths, cancellation before/during copy, changed source, occupied destination, reparse rejection, worker identity, wrong/incomplete Steam version, manifest change while preserving, foreign/duplicate manifest fields, actual start-info arguments, B41 flat bridge recovery and ordinary B41 launch eligibility.

Not validated here: an actual B41 download, a real B41/42 client launch, Steam entitlement/session behavior for a copied installation, and B41 automatic save/server entry against the real game UI. Those require the user's explicit local candidate run. The UI should present the interactive Steam step and preserve/copy step separately; no completed download is claimed in advance.
