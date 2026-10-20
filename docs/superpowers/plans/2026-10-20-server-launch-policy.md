# Server launch policy implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking. The user approved implementation in the current checkout; do not create an additional worktree or use foreground UI automation.

**Goal:** Keep configured 3D components and Chinese translations during server launch, exclude unrelated local mods, and stop client Buddy ordering from overriding the server's order.

**Architecture:** Reuse the existing temporary launch profile and original game connection/reset flow. Add explicit retention of enabled unified Chinese IDs and derive owned, per-source pure-text translation mods from their installed text assets, because the game's native translation loader rejects any mod containing other media. Call Buddy's existing `setAutoFixModOrder(false)` before connecting; never patch game/Buddy JARs or server settings. Buddy persists this ordinary ordering preference; it is not a Java approval or checksum policy.

**Tech Stack:** C#/.NET Framework, actual Lua 5.1 asset tests, Java 25 installed-library characterization, headless UI regression, signed local build.

## Global constraints

- Preserve existing 3D component eligibility and player choices; do not install or enable unselected components.
- Keep subscriptions, saves, server settings, startup parameters, Java approvals and checksums untouched.
- Do not sort or merge the server list with the player's default list; let the game's dependency resolver and server list drive the final load.
- Only `media/lua/shared/Translate/**/*.txt` and `*.json` are eligible translation overlay assets. Never copy Lua, scripts, maps, textures, JARs or arbitrary files.
- Use installed assets at runtime, not bundled third-party translations; selected version/common precedence follows existing source resolution.
- Restore exact original defaults on ordinary exit and preserve deliberate concurrent edits as before. Only hash-matching owned files may be removed.
- All native testing is background-only and isolated. No full-workshop deletion or production server restart in this task.
- Keep the previous 1.2.10 candidate intact; deliver the new behavior as local 1.2.11, not a claimed GitHub publication.

## Task 1: Chinese-only local extras and lifecycle

**Files:** `Orange3DTool/ServerLaunchTranslations.cs`, `LaunchProfiles.cs`, `ServerLaunchPolicyTests.cs`, `Program.cs`.

- [x] Write a real C# fixture with enabled `B42ModTrans_CN`, `B42Trans_CN`, ordinary local mods, local maps, and synthetic installed translation sources. Assert the staged list excludes ordinary entries, retains chosen Chinese IDs, adds the pure translation overlay, copies only text/JSON, preserves per-version override, and never changes sources.
- [x] Run the test before implementation; expect failure because retention/overlay do not exist.
- [x] Add `ServerLaunchTranslations.Ids`, source-specific generated IDs, and `Build(Engine e, IEnumerable<string> selected)` returning `Dictionary<string,byte[]>` with bounded owned mod.info/translation outputs. Reuse installed-source/version metadata resolution; do not scan unrelated account data.
- [x] Incorporate only enabled Chinese IDs into retained extras independently of whether base 3D is ready. Add derived files to the existing ownership journal and temporary profile only for server targets.
- [x] Extend journal validation narrowly for each overlay’s common/42 mod.info files and text-only subtrees. Preserve old three-file journals. Remove only owned hash-matching files and empty owned directories on recovery.
- [x] Filter the transient overlay ID from persistent named/play/save profile exports. Exercise cleanup after normal exit, player edits, modified owned files and malformed ownership paths; run existing launch/transport/profile regression suites.

## Task 2: Server-authoritative ordering using the original Buddy API

**Files:** `Assets/OrangeLaunchBridge.lua`, `launch-bridge-lua-tests.py`, `tests/java/BuddyOrderProbe.java`.

- [x] Extend the actual Lua boundary test so the fake external connect function rejects connecting while a simulated Buddy pinning policy remains enabled. Assert the real script calls `ZombieBuddy.setAutoFixModOrder(false)` before connect; save launch never changes it; absent or incompatible Buddy does not block vanilla connections or expose credentials.
- [x] Run RED before the Lua change.
- [x] Add the small guarded original-API call inside the existing server branch; log a non-sensitive warning if unavailable. Do not add a new JAR/agent, modify the server list, or invoke any approval API.
- [x] Run both actual Lua 5.1 suites. Characterize installed Buddy's auto-fix on an explicit server fixture `NeatUI_Framework, ZombieBuddy, Viewpoint`; prove its original pinning changes that order and that the existing API disables the policy. Redirect any config writes to an isolated test directory.
- [x] Run full C#, common-tool/PCL/full UI suites and independent code review. Fix concrete findings before delivery.
- [x] Update VERSION/CHANGELOG/guide to 1.2.11. Build to a fresh local candidate directory using the existing signing identity. Validate extracted assets, signature, program version and ownership lifecycle. Record precise evidence and native test limits without claiming a completed world join.

## Verification and delivery

Completed 2026-10-20. Evidence and native-test limits: `docs/releases/2026-10-20-1.2.11-verification.md`. Signed installer and portable ZIP are under `dist/1.2.11-local-candidate`; no GitHub publication was performed.
