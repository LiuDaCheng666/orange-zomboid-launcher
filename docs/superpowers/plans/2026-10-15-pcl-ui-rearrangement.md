# PCL-Inspired UI Rearrangement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the approved UI rearrangement, truthful right-bottom cover status, and one-confirmation restore-all components button without redesigning existing features.

**Architecture:** Keep current routes, native actions and game/config semantics. Add a bounded read-only game-version field to status and verify the existing all-uninstall backend. Then rearrange Web HTML/CSS and presentation renderers using the approved inspectable prototype. Preserve the eighteen-file signed update contract. Cover generation is a coordinator-owned independent deliverable, not a change to component artwork or update trust.

**Tech Stack:** C#/.NET Framework 4.8, existing WebView2 JavaScript/CSS, Playwright, built-in image generation, existing RSA-signed core packaging.

## Global Constraints

- 只重新排列既有 UI，不翻新或删除功能；唯一新增操作为组件页“恢复全部组件”。
- 不修改真实游戏、Steam、存档、授权、分页文件或正在运行的启动器；测试只用合成目录和明确的只读检查。
- 保留六个一级路由、二级入口、黑金／浅蓝主题、原作者组件封面、草稿与焦点保护、异步请求代际和启动前体检。
- 游戏版本只读取所选游戏根目录有界 `version.txt`；缺失／畸形／不安全时未知。状态显示当前实际内存与“下次启动已配置”，不把草稿或未验证加载冒充实际状态。
- 恢复全部须一次明确确认，沿用运行中、恢复待处理、屋顶清理、重解析点与未知启动参数保护；保留下载、订阅、普通模组、光影、个人配置、授权和存档，不自动重装或退出 Steam。
- 使用当前 feature/core-self-update 分支，不创建新工作树、不合并、不推送、不上传 Release。保留已发布 1.2.1 及被否决本地候选原样。
- 版本为尚未发布的 1.2.2；正式自有更新路径仍为十八项，不新增信任密钥或给旧客户端增加无法接受的资源路径。

### Task 1: Read-only version status and verified restore-all backend

**Files:** Modify `Orange3DTool/ModernController.cs`; create `Orange3DTool/LauncherStatusMetadata.cs`, `Orange3DTool/LauncherStatusRestoreTests.cs`. No Web ownership in this task.

**Interfaces:** `status` adds `gameVersion` with `{value,state,source,warning}` strings; known value is version only, state `known`, source `version.txt`; unknown value empty, state `unknown`, warning localized when malformed/unreadable. Use existing `componentsAction` payload `{component:"all",operation:"uninstall",confirmed:true}` and conditional `roofRemovalAcknowledged:true`. Do not introduce a second destructive backend or new action.

- [ ] Compile/run current `PZ3D.ModernControllerTests` baseline with `Build-Tests.ps1 -Entry PZ3D.ModernControllerTests -Output artifacts/pcl-ui-122/controller-baseline.exe`, logging results.
- [ ] Add runtime failing tests through serialized `status`: create fixture game root with `version.txt` bytes `42.21.0 4a0e9546ec\nrevision=4a0e9546ec pzbullet=1.0.0.28\n`; assert `gameVersion.value == "42.21.0"`, source root file and state known. Assert metadata does not change config/default list bytes. Current missing field must fail at runtime before implementation.
- [ ] Implement `LauncherStatusMetadata.GameVersion(string gameRoot)` and `GameVersionInfo` with the exact fields above; no reparse path, read at most 4096 bytes using a shared read handle, require a bounded numeric version as the first token of the first line (optional following hex revision), do not scan Workshop/runtime subdirectories or log files. Catch expected I/O/format failure into unknown plus warning; never execute game code. Wire into `Status()`.
- [ ] Test missing/empty/malformed/oversized root version, unrelated runtime version, changed root directory and reparse rejection; first-line metadata beyond version must not be mistaken for game version. Use synthetic directories only.
- [ ] Verify existing all-uninstall with configured base/models/Chinese/shoot and available addon entries in synthetic files: no confirmation and unacknowledged roofs refuse before writes; confirmed operation removes managed enables/loaders, preserves normal mods, heap/other args, downloads, user settings/authorization/save files. Record progress/journal and truthful partial result on a reproducible existing failure. Make only a focused backend correction if tests expose a violation; do not broaden restore behavior or suppress safety errors.
- [ ] Run focused tests plus current controller, component batch/evidence, roof and full integration suites. `Build-Tests.ps1 -Entry PZ3D.LauncherStatusRestoreTests -Output artifacts/pcl-ui-122/status-restore-tests.exe` then run that EXE. Commit only owned native files; report RED/GREEN, exact commands/output and limitations.

### Task 2: Approved UI shell, home status and restore button

**Files:** Modify `Orange3DTool/Web/index.html`, `app.js`, `app.css`; create `Orange3DTool/Web/pcl-layout-ui-tests.cjs`; update existing Web test files only where intentionally rearranged controls now require opening their UI section. Replace the obsolete compact-banner expectation in `home-cover-ui-tests.cjs`. Do not own native files, VERSION, packaging or generated artwork.

**Interfaces:** Consume Task 1 `status.gameVersion` and existing `state.actual.memory.currentGiB`, `status.components[].configured/title`, all existing routes/actions. Keep launch/profile/draft selectors and protocol payloads. Reference exact approved prototype `artifacts/pcl-layout-preview/pcl-layout.html` as spatial guidance; use actual live state, not prototype placeholders.

- [ ] Add runtime RED tests for top navigation + left-launch/right-cover layout at 1366×768, cover height at least 400 CSS px and width greater than launch column, right-bottom actual-state overlay, single launch, existing config controls, all routes and local image decoding. Both themes, 1920×1080 and 733×507 are required. At small size, retain a visible launch control and reachable details, no horizontal overflow or overlay/title collision.
- [ ] Rearrange shell with six existing primary routes across top; retain secondary navigation and game directory/update/task/theme controls, using a compact overflow/menu only if needed on narrow screens. Group home into left `.play-launch` and right `[data-home-cover]` scene; keep current image. Put config management and detailed actual evidence into explicitly labelled disclosure/drawer; real errors/recovery/preflight/approvals remain visible or clearly actionable, not silently hidden.
- [ ] Implement the right-bottom compact overlay with labels 游戏版本 / 启动内存 / 下次启动已配置组件. Render escaped names, count, unknown/pending states and the configured-vs-runtime caveat; invalidated actual memory must not retain stale values. Keep UI readable and image dominant. Avoid adding status API calls on every render.
- [ ] Keep original playtime/recent save/server/hardware/authorization functions accessible via compact home status/details and existing routes. Ensure rerenders preserve disclosure/menu state, draft selection, focus/caret, scroll and stale-reply guards. All previous functionality remains.
- [ ] Components use compact original-cover cards, clear states and one toolbar. Add `[data-components-restore]` labelled 恢复全部组件. Click shows exactly one confirmation describing all scope/preservation and conditional roof cleanup. Cancel emits no mutation. Confirm calls only existing all-uninstall payload and includes roof acknowledgement only after explicit confirmation. Game-running, pending-recovery and conflicting task protections remain; completion refreshes actual state and shows partial failures/log access without auto-install/download/Java approval.
- [ ] Add cancellation, confirmation, roof warning, running lock, native partial/failure and refresh tests. Explicitly open moved sections in legacy tests before using controls; retain their functional assertions and do not weaken tests just to pass. Run all existing Web suites plus new suite once after focused GREEN.
- [ ] Capture and visually inspect both themes for home/components/mods/saves/servers/settings/help and secondary pages; screenshots must come from actual app with synthetic bridge, not only prototype. Commit owned Web changes and tests. Report all results and screenshots.

### Task 3: Three understated cover candidates (coordinator)

**Files:** Create original candidate images under `docs/assets/cover-candidates/` and prompt/provenance notes there. Never overwrite `Web/home.png` or author artwork.

- [ ] Generate separate wide riverside, safehouse and crossroads environment illustrations using built-in image generation. Low-saturation, matte, restrained game-environment aesthetics; no title/logos/film-HDR/hero/gore, right-bottom quiet space for status overlay. Label as original game-inspired illustration, not actual game screenshots.
- [ ] Inspect all outputs, preserve original generated bytes in project, record prompts and tool mode. Deliver three choices without adding a new appearance feature or new signed-owned paths; default remains original until user chooses.

### Task 4: Regression, fresh signed local delivery and review (coordinator)

**Files:** Update `CHANGELOG.md`, `README.md`, release notes; fresh output `dist/1.2.2-pcl-ui/`. Existing uncommitted VERSION 1.2.2 belongs to coordinator.

- [ ] Review Task 1 and Task 2 separately with exact base/head diff packages, record verdicts and resolve actionable findings before continuing. Review final whole task change on highest-capability model.
- [ ] Run focused native status/restore and all existing native/browser regression entrypoints from `artifacts/1.2.1-installer-evidence/coordinator-regressions.ps1` under fresh fixture TEMP, new log prefix. Include new browser/native suite explicitly; retain failures and diagnose, never weaken safety assertions. Native restart/update trust code is unchanged.
- [ ] Update accurate 1.2.2 notes to describe approved UI and restore-all semantics; freeze source bytes before packaging. Do not describe old superseded candidate as accepted.
- [ ] Build with `Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.2-pcl-ui` in a fresh process. Existing signer path only, never print/copy keys. Use a new suffixed output if correction is required after freezing; never overwrite accepted or public packages.
- [ ] Adapt the archived formal verifier only for new local candidate directory/version, verify RSA/hash/18 paths/20 ZIP entries/canonical EXE versions/source byte identity/real installer self-verification, preserve public 1.2.1 hashes. Run new UI focused tests against packaged Web content and view screenshots. Record exact hashes and verification boundaries.
- [ ] Commit local source/docs; archive this plan's task evidence without deleting unrelated work. Deliver installer/ZIP, actual UI preview and cover choices; explicitly state local-only publication and that running old instance was not replaced. No GitHub publication or user-file mutation in this task.
