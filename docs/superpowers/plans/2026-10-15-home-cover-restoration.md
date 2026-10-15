# Home Cover Restoration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the approved existing homepage cover while preserving configuration-based launch behavior.

**Architecture:** Replace only the plain homepage heading with an isolated cover banner using the existing local image. Reuse the existing real browser fixture; native I/O remains synthetic in tests. Build a fresh local 1.2.2 signed candidate without overwriting any public release or installed launcher.

**Tech Stack:** Existing JavaScript/CSS, Playwright, .NET Framework release builder.

## Global Constraints

- 沿用 `Orange3DTool/Web/home.png`，不生成或下载新图片。
- 首页顶部使用原图横幅，承载现有标题和一句说明；下方保留游玩配置与唯一的启动游戏按钮，不恢复旧版重复启动按钮。
- 横幅保持紧凑，深浅主题均可读；733×507、1366×768、1920×1080 下图片可见、没有横向溢出，配置区正常可达。
- 不修改实际游戏、Steam、用户配置、Java 授权、存档或运行中的启动器文件。
- 本地修复候选版本为 1.2.2；保留公开 1.2.1 和历史包原样。不推送 GitHub 或发布新的线上更新，除非用户另行要求。

### Task 1: Restore and guard the homepage banner

**Files:** Modify `Orange3DTool/Web/app.js`, `Orange3DTool/Web/app.css`; create `Orange3DTool/Web/home-cover-ui-tests.cjs`.

**Interfaces:** Consume exported `setup`, `route`, `chromium` from `experience-ui-tests.cjs`. Preserve all existing selectors and event handlers, especially `#launch`, `#play-profile-select` and `#launch-target-kind`. Produce a single `[data-home-cover]` banner before `.play-launch`.

- [ ] Run `node Orange3DTool/Web/experience-ui-tests.cjs` as baseline, save evidence to `artifacts/home-cover-122/`.
- [ ] Create a failing browser test that uses the real page and CSS; asserts one visible `[data-home-cover]`, a successfully decoded existing local `home.png` (if background, explicitly decode its computed URL), banner height between 100 and 240 CSS px, banner before config, exactly one launch button and no main horizontal overflow across both themes and the three specified viewport sizes. Save screenshots. Confirm RED against unchanged production code before implementing.
- [ ] Replace the plain heading with `<section class="home-cover" data-home-cover aria-labelledby="home-cover-title">` containing the existing eyebrow, title (with the label ID) and subtitle. Keep all following markup and behavior intact. Add isolated `.home-cover` CSS with local background image, left-to-right readability gradient, rounded corners and compact responsive spacing; do not change shared `.hero` or global card rules.
- [ ] Run the focused test GREEN, existing experience suite and all existing browser suites. Review screenshots yourself. Confirm profile selection does not apply settings and running state still disables only the existing launch control.
- [ ] Commit only the three owned source/test files. Write full RED/GREEN, screenshot paths and any concerns into the assigned task report.

### Task 2: Version, package and handoff (coordinator)

**Files:** Modify `VERSION`, `CHANGELOG.md`; create `docs/releases/1.2.2-home-cover-local.md`; use a fresh `dist/1.2.2-home-cover/` directory.

**Interfaces:** Existing `Build-CoreRelease.ps1 -SigningKey ... -OutputDirectory ...`, existing unchanged official signer, existing `CoreReleaseTool validate` command. No private-key output or new trust material.

- [ ] Record completed Task 1 review and any findings in this plan's ledger.
- [ ] Change canonical VERSION from `1.2.1` to `1.2.2`; add changelog explaining homepage restoration and local-only status.
- [ ] In a fresh process run `Build-CoreRelease.ps1 -SigningKey E:/Projects/OrangeLauncher-Signing/release-private.xml -OutputDirectory E:/Projects/OrangeLauncher/dist/1.2.2-home-cover`, logging output. Validate the signed core with `artifacts/core-release-tool.exe validate dist/1.2.2-home-cover/core dist/1.2.2-home-cover release-public-key.xml`.
- [ ] Verify installer and ZIP exist; ZIP contains restored JS/CSS and unchanged `home.png`; verify all signed file hashes and canonical version 1.2.2. Run focused browser test against packaged `core/Web/index.html` as well as source. Hash-check the formal 1.2.1 installer/ZIP remain unchanged.
- [ ] Obtain final whole-change read-only review, resolve actionable findings, record accurate package hashes and test limitations. Commit source/docs locally. Do not push or change running launcher.
- [ ] Deliver installer, portable ZIP and a checked preview screenshot with explicit local-only/update status.
