# Server shader and Buddy order implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. User approved in-place branch execution and GitHub publication; no new worktree or foreground testing.

**Goal:** Retain the selected TrueWeathers visual initializer and persist Buddy's disabled order option before multiplayer connection.

**Architecture:** Extend only the existing retained-client whitelist; keep native server list replacement unchanged. Synchronize original ModOptions plus Buddy API before the existing one-shot connection. Reuse signed packaging and the official update channel.

**Tech Stack:** C#/.NET Framework, Lua 5.1, original installed Lua/Java libraries, background native game, GitHub CLI.

## Global constraints

- No new Java bridge, third-party JAR/mod.info changes, checksum bypass, server changes, foreground focus/input, or extra launch gates.
- Preserve unselected components and ordinary local-mod exclusion. TrueWeathersGameplay is not retained.
- Persist only the ordinary Buddy order preference via original APIs; no authorization changes except already-approved isolated test decisions.
- Version 1.2.12, signed existing trust root; do not overwrite historical release assets.

## Task 1: Retention regression and minimum fix

**Files:** `Orange3DTool/ServerLaunchPolicyTests.cs`, `Orange3DTool/LaunchProfiles.cs`.

- [x] Add enabled visual weather and gameplay/ordinary mod fixtures with a plain client JAR and no preload declarations. Expected staged list includes `ViewpointTrueWeathers`, excludes gameplay/ordinary IDs, preserves shader property/pack bytes, and recovers exact default bytes.
- [x] Run `./Build-Tests.ps1 -Entry PZ3D.ServerLaunchPolicyTests -Output artifacts/server-policy-1212.exe` and that executable; observe failure on missing weather.
- [x] Retain weather only after resolving its enabled client JAR; keep existing preload checks for other addons. Test disabled/missing/base-not-ready branches and single-player behavior.
- [x] Re-run the policy executable; all checks pass.

## Task 2: Real ModOptions reload regression and fix

**Files:** `Orange3DTool/Assets/OrangeLaunchBridge.lua`, `Orange3DTool/launch-bridge-lua-tests.py`, `tests/java/BuddyOrderProbe.java`.

- [x] Extend the Lua test fixture with `getOptions('ZombieBuddy'):getOption('autoFixModOrder')`, saved option state and reload reapplication. Assert the real bridge prevents the observed true overwrite and never changes save-target options. Observe RED.
- [x] Before connection set that original option false, persist only its option row through native file APIs when changed, then call `ZombieBuddy.setAutoFixModOrder(false)`. Do not use global ModOptions save, which joins unknown option rows in the installed version. Log failures without blocking original connections or printing credentials.
- [x] Run actual Lua 5.1 tests with `PYTHONPATH=Dependencies/Python`; additionally run against installed original ModOptions and ZombieBuddy_Options in isolated Lua state/file storage, including reload and unchanged unrelated options.
- [x] Compile/run installed Buddy characterization under isolated config; characterize preserved reverse order while the ordinary auto-fix preference is false.

## Task 3: Native background proof and regression

**Files:** ignored `artifacts/background-native-1.2.12/` helpers/logs, `docs/releases/2026-10-20-1.2.12-verification.md`.

- [x] Build candidate with new Lua asset; copy real custom shader directories and mod option settings into the isolated client, never replace real files.
- [x] Use the approved separate-desktop process helper and original game launch arguments with isolated cache/Buddy/temp/home; obtain actual 127.0.0.1:16261 mod order and checksum evidence, shader pack initialization, 3D and Chinese hooks.
- [x] Stop only identified owned test process; run original recovery; compare production hashes/server identity. Record full-world-input limitation if applicable.
- [x] Run full C# self-tests, both Lua suites and headless common-tool/PCL/full UI tests. Review exact diff and fix findings before packaging.

## Task 4: Versioned publication

**Files:** `VERSION`, `CHANGELOG.md`, release notes/README index, signed `dist/1.2.12-github-release`.

- [x] Set 1.2.12 and accurate release notes; compile/sign via `Build-CoreRelease.ps1` using the existing private key outside the repository. Verify signed 18-file ownership, installer extraction and app version.
- [ ] Commit reviewed source; fast-forward main, tag v1.2.12 and push without force.
- [ ] Create GitHub draft, upload all assets, compare server digest/length to local bytes, then publish stable latest.
- [ ] Verify anonymous official manifest/signature/package downloads. Use an isolated formal old 1.2.11 updater on the unchanged default official channel to check/download 1.2.12 to Ready; do not restart the user's real launcher.
- [ ] Save publication evidence and report concise results plus remaining test limits.
