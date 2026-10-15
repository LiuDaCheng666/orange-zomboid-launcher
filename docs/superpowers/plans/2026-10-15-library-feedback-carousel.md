# OZL Library Feedback and Cover Carousel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver the approved restore-feedback, save/server layout, recoverable save deletion, clear launch target, cover rotation and OZL naming changes as a tested local 1.2.4 candidate.

**Architecture:** Keep existing controller, task journal, launch preflight and frontend state ownership. Add one focused native save-deletion boundary and dedicated library/scene presentation helpers without restructuring unrelated app code. Carry extra cover images inside an existing signed Web file to preserve old updater whitelist compatibility.

**Tech Stack:** C# 5, .NET Framework 4.8, Win32 Recycle Bin, existing vanilla JS/CSS WebView2, Node/Playwright/Edge, PowerShell build scripts.

## Global Constraints

- 用户已确认的规格：`docs/superpowers/specs/2026-10-15-library-feedback-carousel-design.md`。
- 程序显示名及左上角改为 **OZL**，全名严格使用 **Orange zombiod launcher**。
- 原封面保持第一张，复用已制作的三张候选图，每 8 秒轮换。
- 右下角实际版本、内存及已配置组件摘要不随图片切换而闪烁或移动。
- 回收站失败不得回退为永久删除。
- 不操作真实用户存档、游戏／Steam 进程、服务器凭据、桌面、注册表、分页文件或运行中的安装目录。
- 保留游玩配置与目标绑定、未保存草稿、预检、运行中禁止重复启动等全部校验。
- 本地候选版本为 1.2.4；正式 1.2.3 包、标签及线上更新不修改。
- 原 `Orange3DTool/Web/home.png` 保持字节不变；不新增第三方图或生成图片。
- 不增加已签名核心白名单路径；保留旧版依赖的内部 EXE 路径和安装所有权格式。
- 使用 `apply_patch` 编辑代码；资源机械嵌入／复制使用可重复脚本。原生编译串行执行。
- 证据放 `artifacts/library-ui-124/`，夹具放独立 `artifacts/TestFixtures/` 子目录；禁止真实删除验证。

## Task 1: Guarded Recycle Bin deletion for local saves

**Files:**
- Create `Orange3DTool/SaveDeletion.cs`, `Orange3DTool/SaveDeletionTests.cs`.
- Modify `Orange3DTool/ModernController.cs`, `Orange3DTool/ModernHostPolicy.cs` and relevant host/controller tests.
- Reuse `SaveProfiles.ResolveSave`, `LauncherMaintenance.NoReparse`, engine guard and existing save mutex naming.

**Interfaces:**
- `SaveDeletion.Preview(Engine e,string id)` returns `{id,name,mode,path,stamp}`.
- `SaveDeletion.Delete(Engine e,string id,string stamp,bool confirmed)` returns `{status:"completed",id,message}` only after directory is absent.
- Native `saveDeletePreview` accepts exactly `{id:string}` and is a read; `saveDelete` accepts exactly `{id:string,stamp:string,confirmed:true}` and is a journalled mutation. No page-supplied paths.
- Injectable recycle boundary belongs to the deletion service, with the public production overload calling Windows recycling and a focused internal overload allowing tests to supply an `Action<string>`. No permanent-delete fallback.

- [ ] **Step 1 — RED tests.** Create isolated saves with marker files. Invoke the new API via reflection so the first test produces an assertion when the class is absent. Assert preview identifies only `Saves/<mode>/<name>`, cancellation leaves marker bytes, changed file/added file/missing target fails, and successful fake recycler moves only the exact folder to an isolated test trash directory. Assert no unrelated directory/default mod/server settings changes.

```csharp
var api=typeof(Engine).Assembly.GetType("PZ3D.SaveDeletion");
Check(api!=null,"guarded save recycling API exists");
```

- [ ] **Step 2 — Run RED.** `./Build-Tests.ps1 -Entry PZ3D.SaveDeletionTests -Output artifacts/library-ui-124/save-delete-tests.exe`; execute it and retain expected failing output before implementation.
- [ ] **Step 3 — Implement.** Resolve IDs through `SaveProfiles.ResolveSave`; bound ID/payload sizes. Preview snapshot includes canonical root identity and a sorted bounded recursive file/directory metadata fingerprint; explicitly reject reparse points before descending. Acquire the existing per-save cross-instance mutex, re-run engine and session/recovery guards, compare fresh stamp and recheck safety immediately before calling the recycler. A changed/disappeared target refuses. Native recycling must be current-user, no shell command, no extra product confirmation and no permanent fallback; surface Win32 error/cancellation. Reject test-mode production recycler use.

```csharp
if(!confirmed)throw new InvalidOperationException("删除存档需要明确确认。");
if(currentStamp!=stamp)throw new InvalidOperationException("存档已变化，请重新核对后删除。");
recycle(path);
if(Directory.Exists(path))throw new IOException("回收站操作未完成；存档仍在原位置。");
```

- [ ] **Step 4 — GREEN edge coverage.** Cover traversal/extra nesting, root or nested junction, missing confirmation, game-running guard through a controllable boundary, pending launch/Buddy/profile recovery, parallel writer mutex, recycler exception and cancellation, valid singleplayer and local Multiplayer deletion scope. Host tests reject extra path fields and malformed stamps; journal tests verify success/failure states without credentials. Run SaveDeletionTests, SaveProfilesTests, ModernControllerTests, ModernHostPolicyTests (use actual existing entry names).
- [ ] **Step 5 — Commit focused implementation and test evidence.** `git add` explicit changed paths; commit `feat: safely recycle local saves from launcher`.

## Task 2: Compact libraries, one restore result, and explicit launch intent

**Files:**
- Modify `Orange3DTool/Web/app.js`, `Orange3DTool/Web/app.css`.
- Create `Orange3DTool/Web/library-ui-tests.cjs`; extend `experience-ui-tests.cjs` synthetic boundary only as necessary.
- Adapt existing profile/home/PCL test expectations when wording intentionally changes; do not weaken behavioral checks.

**Interfaces:**
- Consume native `saveDeletePreview` / `saveDelete` as defined in Task 1.
- Keep DOM target controls `#launch-target-kind`, `#launch-target-id`, `#launch` and state `launchKind`, `launchId` for existing profile binding.
- Add dedicated `.save-card`, `.server-card`, `.library-grid`, `.library-body` styles rather than changing generic author cover dimensions.
- Restore action result owns one compact `[data-restore-result]` with collapsed `<details>` and dismiss control. Generic notification must not contain the same full report.

- [ ] **Step 1 — RED browser regressions.** Use existing real app setup and controlled native bridge. Assert batch restore response produces exactly one detailed report, collapsed by default, success/failure counts visible, cancellation sends no mutation, refresh failure preserves result, and dismiss stays dismissed through polling. Assert save Delete button exists, cancel emits no delete, confirm sends exact ID/stamp (not path), errors preserve card, success removes only target and invalidates stale edit/launch selection while respecting unsaved drafts.

```js
assert.equal(await page.locator('[data-restore-result] details[open]').count(),0);
assert.equal(await page.locator('#notice').innerText().then(x=>x.includes('[完成] base')),false);
```

- [ ] **Step 2 — Run RED** using `node Orange3DTool/Web/library-ui-tests.cjs`; record the expected missing-control/duplicate-report failure.
- [ ] **Step 3 — Implement restore ownership.** Add a narrowly scoped run option to suppress only the restore-all generic notice; preserve standard errors and task journal. Parse native `[完成]` / `[失败]` headers into summary counts and handle plain base-only success without claiming zero work. Refresh must not erase returned outcome. Escape raw results; bounded collapsed display; one confirmation only.
- [ ] **Step 4 — Implement libraries and deletion UI.** Dedicated horizontal cards use roughly 112px square un-stretched original thumbnails and min-width-zero content; 733px view wraps safely. Save shows name/type/modified/mod count/Continue/View Mods/Delete. Local Multiplayer labels deletion as local cache only. Confirm dialog includes native path/name/type and Recycle Bin semantics; disables reentry, checks identity/guards after confirmation, handles stale and native failures honestly, refreshes visible and actual lists. Server personal cards show address, status, compact stats, collapsed long description and clear Connect/Query/Edit/Delete. Preserve favorites, hall filtering and descriptions.
- [ ] **Step 5 — Implement explicit launch labels.** Display “进入主菜单 / 继续单人存档 / 连接服务器” with contextual help. Button text depends on selected target, showing “启动到主菜单 / 继续：<name> / 连接：<name>”; missing target is actionable. Preserve experimental direct-load warning and every existing call path/guard. Do not issue extra launch actions on changing selection.
- [ ] **Step 6 — GREEN and commit.** Run new suite plus profile, experience, PCL/home UI and compact UI; inspect actual screenshots in both themes at 733×507, 1366×768 and 1920×1080. Repair affected intentional-wording expectations without reducing coverage. Commit `fix: compact save server and restore workflows`.

## Task 3: Cover rotation with backward-compatible signed assets

**Files:**
- Modify `Orange3DTool/Web/app.js`, `Orange3DTool/Web/app.css`, `Orange3DTool/Web/index.html`.
- Create repeatable `Build-CoverGallery.ps1` and `Orange3DTool/Web/carousel-ui-tests.cjs`.
- Reuse `docs/assets/cover-candidates/{crossroads,riverside,safehouse}.png` and unchanged `Web/home.png`.
- Existing build scripts may invoke the deterministic gallery builder; do not add CoreReleaseProtocol.OwnedPaths entries.

**Interfaces:**
- Known cover IDs `original`, `crossroads`, `riverside`, `safehouse`; first resolves existing `home.png`.
- Extra PNG bytes embedded as JSON data resource inside existing signed `index.html`, not executable inline JS (keep CSP).
- `syncHomeScene()` called after frontend render and route changes owns a single timer and updates only scene image/control nodes. Existing launch DOM and `[data-home-actual]` must not be replaced for a slide transition.
- Preferences live in launcher localStorage with defensive parsing; no native mutations.

- [ ] **Step 1 — RED.** Browser fake-clock test expects first original image, next after 8000ms, previous/manual/pause controls, keyboard and pointer pauses, hidden/route pause, single timer through repeated status renders, failure fallback, reduced-motion default pause and reload-persisted selection. Track focus and launch selections unchanged across image changes; decode candidate resources and assert actual image dimensions.

```js
await page.clock.install();
await page.clock.fastForward(8000);
assert.notEqual(await page.locator('[data-home-cover]').getAttribute('data-scene-id'),'original');
```

- [ ] **Step 2 — Run RED** and retain output; choose clock APIs compatible with installed Playwright.
- [ ] **Step 3 — Implement gallery builder.** Use deterministic input order and base64 of original bytes; inject one marked `type="application/json"` data block into existing HTML with a patch-created script. Re-running yields identical bytes and no duplicated blocks. This is resource embedding, not image editing; no external download or generation. Keep full signed file limits and old path whitelist intact.
- [ ] **Step 4 — Implement scene lifecycle.** One interval/timeout at 8000ms when eligible; teardown on route leave/page hidden. Pauses for pointer/focus and user pause, no full render on tick. Retain state overlay nodes. Load candidate before paint, guard late load completions, skip failed assets with original fallback, accessible controls and restrained/no transition under reduced motion. Persist validated ID/pause only; never let localStorage values become arbitrary URLs.
- [ ] **Step 5 — GREEN and commit.** Run carousel and homepage/PCL suites; check original SHA unchanged; run deterministic builder twice and compare. Assert exact existing `OwnedPaths` set remains compatible with 1.2.3. Commit `feat: rotate bundled cover scenes without updater path changes`.

## Task 4: OZL naming, version, full regression and local candidate delivery

**Files:**
- Modify explicit user-visible strings in `Web/index.html`, frontend heading/help, `ModernHost.cs`, installer/uninstaller UI, shortcut display default, `VERSION`, `CHANGELOG.md`, `使用说明.md`, `README.md`, release notes.
- Add `docs/releases/1.2.4-local-candidate.md` and an isolated verification runner under `artifacts/library-ui-124/`.
- Update only tests whose intentional product-name expectation changes; preserve old shortcut ownership cleanup compatibility and old internal EXE names.

**Interfaces:**
- Display name `OZL`, full name `Orange zombiod launcher`; no guessed spelling correction.
- Version `1.2.4`, no GitHub push/tag/release/feed mutation.
- Generate installable and portable local candidate in a fresh dist directory using existing trusted build process; never inspect or print private key contents. If real signing is not authorized/available, hand off compiled unsigned local preview and clearly state online-update validation limit rather than using a fixture key as trusted product.

- [ ] **Step 1 — RED branding tests.** Assert user-visible document/window/installer/shortcut names use OZL while old owned executable target stays compatible. Assert uninstall still recognizes unchanged existing owned shortcuts and respects modified/unowned ones.
- [ ] **Step 2 — Implement branding/version/docs.** Change narrowly located visible strings. Preserve original home image, package path ownership, update identity and saved settings. Explain local save recycling, local Multiplayer scope, launch experimental limitations and scene controls in help.
- [ ] **Step 3 — Run covering native gates serially.** SaveDeletion, SaveProfiles, ModernController/Policy, LauncherStatusRestore, LaunchProfiles, ServerProfiles, LauncherMaintenance, desktop shortcut, CoreReleaseProtocol, CorePackage, CoreUpdateService, CoreUpdateTransaction and updater integration tests. Commands use `Build-Tests.ps1 -Entry PZ3D.<Name> -Output artifacts/library-ui-124/<Name>.exe` followed by execution; capture all exit codes, don’t treat script completion as pass if child failed.
- [ ] **Step 4 — Browser and visual gates.** Run all tracked `*-ui-tests.cjs` entry suites once (exclude helper-only imported modules); do not concurrently run multiple shared native builds. Capture and inspect save/server/home screenshots at required sizes/themes, including loaded square previews, empty lists, long names and descriptions. Check screenshot regions for actual text/image fit, not just absence of overflow.
- [ ] **Step 5 — Build and verify local package.** Fresh output directory only. Verify version, original image SHA, embedded gallery images decode, installer/portable contain same Web bytes, immutable published 1.2.3 output remains untouched, owned path count matches previous release and latest old updater can parse target manifest where signing permits. No user installation/test desktop creation.
- [ ] **Step 6 — Document evidence and commit.** Record exact tests/counts and package paths, gaps honestly; commit `chore: prepare OZL 1.2.4 local candidate`. Final independent whole-branch review before handing off; do not merge/push/release without explicit authorization.

## Plan self-review

- Spec sections 1–4 map to Tasks 1–2, section 5 to Task 3, appended naming/version/delivery to Task 4.
- All native/browser contracts are named above; no dependency on unbuilt extra services.
- Recycling is only performed in isolated test trash through an injected boundary; product path uses Windows recycling with no permanent fallback.
- Additional covers deliberately ride an existing signed file, preserving 1.2.3 allowlist verification. No new updater protocol or key is introduced.
