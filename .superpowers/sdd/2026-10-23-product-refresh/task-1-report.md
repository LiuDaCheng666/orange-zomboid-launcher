# Task 1 report: bounded display snapshots

Scope: catalog/cover shared discovery, component display evidence, explicit controller refresh/mutation invalidation, engine lifecycle invalidation. No UI, component semantics, installation behavior, launch process guards, downloads, published binaries or real game/user/server writes were introduced.

## Implementation and self-review

- `Orange3DTool/DisplayCache.cs`: synchronized cache, at most four engine/source identities and 32 display values per identity, 30-second bounded fallback age (longer than ordinary 15-second polling). Keys canonicalize game, user, data, workshop and selected sources and distinguish test discovery mode. Watch roots are limited to user mods, component receipts/path mappings, workshop/manifest, selected sources and exact game version/deployed files. Recursive source watchers invalidate on file/metadata/rename/delete/add/error notifications. Missing directories watch their nearest safe existing ancestor with event filtering to the intended subtree; unrelated sibling activity does not continually invalidate snapshots. Eviction, game switching and controller disposal dispose watchers. Callbacks only dirty their owning snapshot, so disposed watchers cannot install replacements.
- `SaveProfiles.cs`: Catalog and Cover share a discovery snapshot; responses retain their existing schema and limits. Cover still validates and reads the current resolved image on every call. Entries and all mutation/launch validation remain fresh.
- `ComponentEvidence.cs`: Decorate caches serialized display evidence/inventory/digests, returning independent response dictionaries. Authoritative Fingerprint no longer relies on the prior unbounded length/timestamp digest dictionary; Record hashes current bytes, including equal-length edits with restored timestamps.
- `ModernController.cs`: explicit modCatalog requests invalidate before discovery; every mutation invalidates in finally, including partial/failing mutations. SetEngine and Dispose retire snapshots. Ordinary status reads can reuse display evidence.
- `CatalogCacheTests.cs`, `Program.cs`: native temporary-fixture regression entry integrated into --self-test. Tests exercise real metadata/images, concurrent Catalog/Cover reads and shared snapshot identity, external metadata/poster deletion/add/remove, selected-source changes, engine separation/eviction, actual controller refresh/mutations, fresh Entries, same-size/same-time Fingerprint changes and fresh Record receipts.

## RED / GREEN

RED command: `./Build-Tests.ps1 -Entry PZ3D.CatalogCacheTests -Output artifacts/catalog-cache-tests.exe` followed by `./artifacts/catalog-cache-tests.exe`.

Compilation succeeded; execution exited 1 with `FAIL: catalog and cover must share a discovery snapshot`. This was the expected missing-cache behavior assertion after real Catalog and Cover fixture preparation; production changes followed this failure.

GREEN: same focused command compiled and exited 0, printing `PASS bounded display snapshots, shared cover discovery, external changes, engines, concurrency, controller invalidation, fresh entries and receipts`.

Full native command: `./Build-Tests.ps1 -Output artifacts/cache-regression.exe`, followed by `./artifacts/cache-regression.exe --self-test *> artifacts/cache-self-test.log`. Final source run (session 40972, including the 30-second/narrow-root correction) exited 0, as did three prior completed runs. Final output ended with the retired bridge recoverable cleanup receipt PASS. Commands run through background/piped execution; fixture locations are chosen by existing native test helpers (artifacts/TestFixtures and configured isolated test caches). No actual game launched and no live download performed.

Second RED/GREEN: a new reuse-across-polling assertion failed with `FAIL: display snapshot survives ordinary polling interval` (exit 1) against the initial three-second fallback. Increasing fallback to 30 seconds and narrowing watcher roots made the focused run pass (exit 0), including unrelated game/journal log writes that retain snapshot identity. No ordinary status request forcibly invalidates the cache.

`git diff --check`: clean.

## Read-only real-workshop measurements

Windows PowerShell 5.1 reflection loaded the focused .NET Framework assembly and constructed Engine with game `E:/Steam/steamapps/common/ProjectZomboid`, nonexistent fixture user `E:/Projects/OrangeLauncher/artifacts/catalog-readonly-user`, data `C:/Users/MC233/AppData/Local/PZ3DManager`, test=true. Only Catalog/Cover were invoked; no controller or mutation was constructed/invoked. Output included counts/timings only.

791 entries: cold Catalog 1006.7719 ms; hot Catalog 0.0628 ms. Six Cover reads: 3.8399, 5.6103, 2.8251, 2.4835, 32.0078, 5.6509 ms. Parent-provided baseline: Catalog 942–1111 ms, individual Cover 1004–1299 ms. Discovery is now shared; image decoding still has its own cost.

An initial timing pass during concurrent native fixture writes exposed unrelated ancestor watcher notifications, producing 827–911 ms repeated reads. Filtering missing-root watcher events fixed this; the measurements above are after that correction.

## Limits and concerns

External notifications are asynchronous; a read racing notification delivery can briefly return the preceding display snapshot. The 30-second fallback limits stale display data even when notifications are lost/unavailable. Explicit refresh and all authoritative operations remain fresh. Global cache synchronization serializes cold discoveries, deliberately preventing duplicate concurrent discovery. No persistence or speculative background worker was added.

## Review fix round 1

Finding: watcher coverage omitted workshop roots parsed from libraryfolders.vdf and optional-component directories resolved through workshop-paths.json. These could retain display data until fallback expiry despite functioning notifications.

Changes:

- `GameDiscovery.WorkshopSources` resolves the common catalog/workshop watcher source set and the libraryfolders input files. SaveProfiles discovery and DisplayCache consume this same result. The selected game's own Steam installation root is included along with registry-discovered roots; this also allows isolated alternate-library fixtures without registry writes or a global test hook.
- DisplayCache resolves every supported component item through the same `InstallWorkflow.Source` used by authoritative component fingerprints, including mapped optional sources. Mapping-file edits retire the snapshot so replacement watchers follow the new mapping.
- Reformatted DisplayCache into maintainable methods and grouped targets sharing an existing watcher directory, avoiding duplicate handles for sibling file targets.
- Added real alternate-library catalog edit and mapped optional-component display-evidence edit fixtures. Extended polling reuse to 15.1 seconds, verified fallback expiry by aging the private cached values in test utility code (no production clock/test hook), and verified controller disposal retires its snapshot.

RED/GREEN commands: `./Build-Tests.ps1 -Entry PZ3D.CatalogCacheTests -Output artifacts/catalog-cache-tests.exe`, then `./artifacts/catalog-cache-tests.exe`.

- RED: initial fixture failed `alternate library discovered through shared source set`. After introducing the shared resolver but retaining old watchers, it failed `alternate library edits invalidate catalog` (exit 1), proving actual external-edit coverage was missing.
- Mapped-source mutation check: temporarily omitting resolved component roots failed `mapped optional component edits invalidate display evidence` (exit 1); the roots were restored before final validation.
- GREEN: final focused run exited 0 with the cache fixture PASS, including actual 15-second reuse, both new external-source edits, fallback retirement and lifecycle retirement. A test cleanup issue initially double-disposed ModernController; the fixture was corrected to dispose once, without changing controller behavior.

Focused covering validation is green and `git diff --check` is clean. A full native run was already started in background session 89205 before the request to defer repeated whole-suite runs to branch integration; its log is artifacts/cache-self-test.log and its completion is not required for this scoped handoff. No real source files, registry entries or user/game/server files were modified; all new fixtures are temporary test directories.
