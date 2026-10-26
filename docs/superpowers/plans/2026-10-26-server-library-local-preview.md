# Recommended servers, versions and save backups — local preview plan

> **For agentic workers:** Use subagent-driven development for the three isolated domains, then integrate and review. No publication in this task.

**Goal:** A local executable candidate with administrator-controlled recommendations, coexisting game installations with separate user data, and complete save backup/restore.

**Architecture:** Preserve the current launcher and website. A standalone Python service supplies a public recommendation document and an authenticated management page. Three native C# services expose recommendations, local version installations and save archives to the existing WebView UI. A local-preview compilation connects only the recommendation service to loopback; ordinary builds keep the fixed HTTPS origin.

**Tech Stack:** Existing .NET Framework 4.8/C#/WebView2, HTML/CSS/JavaScript, Python standard library, existing Steam native helper, ZIP streaming.

## Global constraints

- User approves the local candidate visually before any release, GitHub push, live backend installation or website change. Do not publish, tag, push, reload or modify the remote server.
- Preserve all pre-existing website/deploy changes. Do not edit website/ or deploy_site.py.
- No actual game/Steam launch, branch switch, actual profile or game mutation during automated tests. Tests use isolated fixtures and hidden rendering/browser contexts. User runs the candidate personally.
- Straightforward human labels: recommended list, choose version, install/save version, backup, restore. No raw JSON, internal transaction state or filler disclaimer blocks in product pages.
- Steam installs use owned official content/available branches. Never claim a download is complete until actual files are verified. Selecting a version must bind game path AND user-data path; launch must actually use those paths.
- Full save archives are distinct from mod-list configuration backups. Restore validates archive paths/content before swapping directories; retain the displaced save for undo. Never overwrite an unrelated installation/profile.

## Task 1 — Recommendation service and native client

Files: new `deploy/recommendations/` standalone service/admin static assets/README, `tests/test_recommendations.py`, `Orange3DTool/RecommendedServers.cs`, focused C# tests.

Contract: `GET /api/recommended-servers` returns `{schema:1,revision,updatedUtc,servers:[{id,name,host,port,queryPort,version,description,cover,tags,order,useSteamRelay}]}`. Only published entries appear. Administrative login/session/CSRF protects mutations; passwords are hashed and private data is outside public assets. Revision checks prevent overwriting another administrator's edit. Names/descriptions are plain text. Validate records in the admin form before publishing; do not make one bad entry break the whole client display.

Client: fixed HTTPS production origin, caching of last valid document, cancellation and timeout; local-preview build uses fixed loopback service. Existing personal connection and credential storage is reused after recommendation selection. Credentials never appear in the feed.

- [ ] Write and observe failing tests for anonymous write rejection, login/CSRF, add/edit/order/publish, concurrent edits and client cache/network fallback.
- [ ] Implement the service and native reader; test only temporary stores and loopback sockets.
- [ ] Exercise real admin HTTP operations and inspect management page at desktop and narrow widths.

## Task 2 — Complete save backup and restore

Files: new `Orange3DTool/SaveBackups.cs`, focused C# tests; related exit-worker integration only if its existing identity checks can be retained.

Contract: list archives for selected game-user/save identity; create a streamed ZIP with a manifest and per-file hashes; restore a verified archive via staging and directory swap, preserving current save first. Configure backup location, retention and after-exit behavior. Reads remain available while playing; mutations of live saves wait for game exit.

- [ ] Test exact round-trip bytes, nested worlds, empty directories, Chinese paths, cancellation, changed source, running client refusal, symlinks/junctions, ZIP traversal, corrupt content and restore rollback before implementation.
- [ ] Implement with fresh path/identity checks and cancellation; backup jobs expose useful progress.
- [ ] Verify selected-version isolation and automatic retention does not delete a restore's displaced save.

## Task 3 — Local version installations and official installation workflow

Files: new native version library/controller support and focused tests; targeted `LaunchProfiles.cs`, game version discovery and worker integration.

Contract: version rows include opaque ID, detected version, installed location and independent user-data location. Import an existing game, preserve the current Steam installation as a separate version, and install available official branches using Steam. Keep installation/download state separate from installed/launchable state. Steam branch selection replaces its canonical folder, so preserve requested versions as full independent copies. No shared writable hard links. B41 support requires actual version/launch compatibility handling rather than changing a display label.

- [ ] Test record persistence, directory validation, cancelled/failed copy, actual selected-path process arguments and independent saves/settings/mod files.
- [ ] Verify official Steam branch API/ABI before exposing install actions; use explicit user click for Steam-affecting operations. If external download requires an interactive official Steam step, label and track that step honestly.
- [ ] Keep existing default Steam installation behavior; managed versions launch their selected binary with their selected data directory.

## Task 4 — Root integration and local candidate

Files: `ModernController` partials, `ModernHostPolicy`, production source allowlist, focused native bridge tests, `Web/app.js`, `Web/app.css`, `Web/index.html`, local-preview build/start scripts and test report.

- [ ] Connect services with validated host actions, native folder pickers and lifecycle cancellation. Clear stale per-game UI after version selection.
- [ ] Add recommended tab with refresh/connect; version page and home selector; save backup/history/restore controls. Match existing typography and responsive layout.
- [ ] Compile and run affected native/Web tests, perform actual browser clicks against the local backend, and inspect screenshots after each operation.
- [ ] Independent review of selected paths, restore behavior, authentication, UI errors and the candidate build.
- [ ] Build a fresh local candidate, check signed content/icons and Defender. Deliver executable, local admin URL/start script, test evidence and known untested real-game cases. Wait for user review; no release/push/deployment.
