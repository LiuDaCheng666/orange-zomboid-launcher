# Mod workspace backend report

Implemented against `docs/integration-contract.md` and task 1 of `docs/superpowers/plans/2026-10-13-full-launcher.md` on 2026-10-13.

## Owned changes

- `Orange3DTool/ModConfigurations.cs`: named local profile persistence and stable dependency preview.
- `Orange3DTool/ModConfigurationsTests.cs`: synthetic profile, sorting, activation, concurrency, confinement and corruption tests.
- `Orange3DTool/SaveProfiles.cs`: catalog metadata/edge aliases and shared byte-preserving default mods-block helpers.
- `Orange3DTool/SaveProfilesTests.cs`: existing synthetic fixture destination moved into canonical `artifacts`.
- This report.

No controller, Program, host, Web, build script, Steam, Git or server changes were made by this worker.

## Native API and behavior

The six public methods return the agreed DTOs: `List(Engine)`, `Read(Engine,string)`, `Save(Engine,Dictionary<string,object>)`, `Delete(Engine,string,string)`, `Activate(Engine,string,string,bool)`, and `Sort(Engine,string[])`.

Named profiles live under `Engine.Data/mod-profiles/<game-and-user-key>.json`, separate from save snapshots and `mods/default.txt`. IDs are generated lowercase GUID32 tokens, never paths. Profile names are bounded to 100 visible characters, selections to 2,000 distinct syntax-safe opaque mod IDs, and the collection to 200 profiles and 1 MiB. Missing schema, invalid entries, bad UTF-8, oversized files and reparse paths refuse; they do not reset existing data.

Collection stamps hash both stored profile bytes and current default-file bytes, including absence. Every profile write requires the collection stamp. This also rejects activation after another program changes the default file. Writes share the existing per-user `OrangeLaunch` mutex, check the game/independent-loader and pending-session guards, and check the stamp again before atomic replacement. Read and sort operations do not create files.

Saving a named draft preserves the user's selected order and allows unresolved dependencies for editing. Activation requires `confirmed:true`, validates the effective selection, and refuses missing local IDs, duplicate sources, hard dependencies, declared conflicts, cycles and invalid ordering. Active known Viewpoint/Buddy/models/shoot/translation/bridge/addon IDs are retained ahead of ordinary profile selections. The loader-only `ZombieBuddy` ID is accepted without catalog metadata only when it is already active, the Buddy loader argument remains configured, and both original loader files exist on safe paths. This exception applies to sort preview as well; it does not permit selecting a missing new component.

Activation replaces only the ordinary default `mods` block, preserves UTF-8 BOM, newline style and all bytes/layout outside that block (including maps and custom blocks), and retains a default backup with recovery instructions. Unknown syntax inside the mods block refuses before the default write. Save directories and server configuration are never changed.

Sorting uses stable Kahn ordering: among currently eligible nodes it chooses the earliest original selection. `require` is a hard edge; `loadAfter` and `loadBefore` impose edges only when their targets are selected. The author's actual `loadModAfter`/`loadModBefore` keys are also parsed into those DTO fields. Reference values such as B42 `require=\ZombieBuddy,\Viewpoint` resolve against canonical selected IDs; embedded backslashes in opaque IDs remain intact. Raw author metadata is preserved separately. Errors retain the original normalized selection for inspection. Sorting neither adds dependencies nor writes a profile/default/save file. `changed` includes successful whitespace normalization.

Catalog rows retain existing names/shapes and add `title`, `item`, `require`, `loadAfter`, `loadBefore`, `conflicts` and `metadata`. Metadata comes from the author's common/build overlay. Original metadata display is bounded to 64 keys, 100 characters per key and 256 characters per value; exceeding these limits produces a catalog warning. Existing original cover tokens and lazy image-byte behavior remain intact.

## RED/GREEN evidence

All commands ran from `E:/Projects/OrangeLauncher`; executables and fixture data are under its `artifacts` tree. Every engine used synthetic `TestMode=true` fixtures. There were no personal save/default/approval changes, real Steam downloads/subscriptions or real launches.

```powershell
& .\Build-Tests.ps1 -Entry PZ3D.ModConfigurationsTests -Output 'E:\Projects\OrangeLauncher\artifacts\mod-workspace-tests.exe'
& .\artifacts\mod-workspace-tests.exe
& .\Build-Tests.ps1 -Entry PZ3D.SaveProfilesTests -Output 'E:\Projects\OrangeLauncher\artifacts\save-profile-tests.exe'
& .\artifacts\save-profile-tests.exe
```

Initial RED: seven failed cases, with the expected missing named API and missing catalog soft-edge metadata assertions. Initial GREEN: 252 assertions, zero failed cases. Follow-up RED: Buddy preview reported a missing local ID and trimmed IDs failed to report `changed`; both failed their behavior assertions, then passed after correction. Final corruption RED: `{}` was accepted as an empty collection; the schema validation now refuses it and missing `profiles`.

Independent native review identified realistic B42 reference syntax missing from the first fixtures. Added tests were observed RED: qualified hard dependencies did not match selected IDs, `loadModAfter` did not order `[SoftAfter,A]`, and existing save validation rejected `B42Dependency → \A`. The shared metadata-reference parser now resolves the leading qualification backslash, supports both author soft-key aliases, and leaves raw metadata/embedded opaque backslashes unchanged. A managed activation fixture includes the actual `OrangeShootPresentation` bridge requirements and load-order metadata.

Final owned-suite results after those corrections: `ModConfigurations: 308 assertions, 0 failed cases` across ten cases; `SaveProfiles PASS: 62 assertions`. Both freshly rebuilt suites compile all current C# sources. Save regressions include original cover bytes, compatible B42 metadata overlays, duplicate source detection, save reparse refusal, concurrent stale writers, dependency validation, opaque Unicode/namespaced IDs, qualified B42 dependency validation, INI injection refusal and the 900-entry bounded transport fixture.

## Integration and limits

Root still owns route registration, SelfTests registration, Web integration and final independent review. The suites validate synthetic engine guards and pending-session refusal; this worker did not launch a real game or independently exercise the real game-process detector. Existing `LaunchProfilesTests` was inspected but not run by this worker because its old fixture root was outside canonical `artifacts`; root owns that suite. Metadata-based dependency/conflict validation cannot establish actual game compatibility, successful loading or multiplayer behavior.

Earlier all-source rebuilds encountered in-flight changes in the parallel Steam helper and root journal sources. Those transient compilation issues were reported to their owners and had cleared before the final fresh GREEN builds above.
