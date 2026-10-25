# Mod management, workshop browsing and startup repair

> Execute with subagent-driven-development. Continue without design/permission pauses: the user has specified the desired list workflow and launch handoff and has already authorized tested software publication.

**Goal:** Make mod management a usable list, restore saving and launching without unrelated draft blockers, browse workshop immediately, and hand the loading screen over to the game when ready.

**Architecture:** Keep existing native mod storage and concurrency checks. Use one mod workspace with a configuration selector, searchable checkbox list, compact enabled ordering and explicit save/apply controls. Use existing Steam native catalog query, adding deep pagination only when required and verified. Startup is a launch-owned presentation with readiness/recovery handling, not an unrelated observer.

## Global constraints

- Worktree: C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher; preserve unrelated website/deploy work.
- Software publication only, primary own server then GitHub; pinned SSH helper; no website/current/Web configuration changes.
- Initial foreground restriction was superseded by the user's explicit “你自己操作测试” authorization. Use the computer-use skill for real candidate/game acceptance; the human handles security and Java trust prompts. Destructive file cases stay in isolated fixtures.
- No raw JSON, internal transaction/nonce/session explanations, generic disclaimers or resource metadata warnings on the normal screen. Keep actual actionable failures and optional technical detail.
- Preserve saved profiles, game files and source/version guards; no automatic stale overwrite or silent draft deletion.
- Candidate version1.2.20 only after meaningful native/UI/integration tests. Fixed AUMID OrangeZomboidLauncher.Launcher.

### Task 1: Native startup flow and typed launch outcome

Files: GameStartupPresenter/Host/Monitor/Window/Models/Presentation and their existing tests, LaunchProfiles bridge only if required; ModernController launch branches/tests for typed outcome. No Web files.

- [x] Reproduce JSON serialization and strict-marker readiness/lifetime defects with isolated fixtures.
- [x] Show loading immediately from launch initiation, synchronize game presentation with confirmed readiness, then close loading and hand off once. Preserve authorization access and restore owned hidden windows on close/failure/cancellation; never hide unrelated windows or leave game inaccessible after launcher failure.
- [x] Use valid readiness signals bound to this process/session and a tested bounded fallback when the bridge is missing; do not infer completion merely from process creation or a visible blank game window.
- [x] Return readable structured launch messages, not serialized JSON plus timers. Remove internal session/recovery noise from normal success path; meaningful failures remain accessible.
- [x] Native/offscreen regression and implementation report; independent review.

### Task 2: Simple mod workspace and draft behavior (root)

Files: Web/app.js mod/home/draft/launch/workshop functions; Web/app.css scoped mod rules; meaningful UI tests and native-boundary fixture tests.

- [x] Reproduce cached-list stale stamp and post-activation stale editor with actual native mod storage. Refresh appropriate snapshots without disabling native stale guards; reject same-profile external edits while preserving the draft.
- [x] Default page shows actual enabled mod list and full searchable catalog; select/create named configurations in the same list, checkbox membership, compact ordering arrows and automatic dependency sort. Primary save/apply action must actually persist and enable the displayed list, with accurate partial-failure handling.
- [x] Named unapplied drafts do not block launching saved/current game configuration after leaving page; preserve drafts for returning. Keep blockers for actual target settings and in-flight mutations.
- [x] Remove huge default-name paragraphs, empty naming prompts, metadata-limit/cover warning banners and dominant side panels; metadata and dependency details on demand. Missing covers use stable compact icons.
- [x] Empty startup status has no yellow banner; valid native Chinese stages display correctly. No raw JSON/session/timer boilerplate on home.
- [x] Workshop opens with paginated browse results; keyword search refines, clear restores browse; preserve latest-request race handling/retry.
- [x] Test actual flows and screenshots in both themes at820/1920, malicious labels, stale writes, partial failures, save→apply→launch and leave-draft→launch.

### Task 3: Native workshop query and final integration/publication

Files: SteamLauncherServices/Native/WorkshopWorker/ModernHostPolicy and existing tests only where required by confirmed query contract.

- [x] Empty keywords browse popular mods through native Steam APIs. Preserve optional search/filter, pagination and legitimate errors. No new server/API key/login flow.
- [x] If using deep pagination, verify official cursor ABI/DTO and tests; do not promise all pages from a1000-page-limited endpoint.
- [x] Review full changes and test native/UI boundary against the final production artifact; keep real Steam/network evidence separate from synthetic fixtures.
- [ ] Build signed1.2.20, preserve installer/path/branding/update identity. Publish only immutable artifacts/latest metadata/notes through existing safe publisher. Verify20anonymous downloads, actual old client Ready, protected website equality and postpublish SSH.
