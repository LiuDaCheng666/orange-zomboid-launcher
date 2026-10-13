# Java approval native responsiveness

Date: 2026-10-13. Scope: `JavaApprovals.cs` and `JavaApprovalsTests.cs`, with isolated synthetic fixtures. No real game, authorization store, author trust, mod configuration, or Steam account was modified.

## Findings

`Save` already returns `List(e)` after the atomic write. The returned DTO contains the persisted decision and a fresh stamp; a new regression assertion verifies both without requiring another client list request. No native correctness defect was found explaining an apparently unchanged allowed row.

The native path nevertheless performed redundant source discovery. Each capture read every folder's root/common/42 metadata separately for every enabled managed Java component and Java addon. A successful save performs four captures (initial target, under mutex, with selected JAR read locks, and final DTO). With 665 local mods, unrelated metadata therefore multiplied with the number of managed components.

## Change and safety

Each capture now builds an identity-to-folder index once. It retains only identity matches for that capture; it does not retain metadata, directory state, or hashes across captures. Root/common/42 declarations for the same folder count as one source. Selected identity/metadata validation continues to reread files, and addon-only captures use the same index when an actual Java addon is found.

All four captures, full JAR SHA-256 reads, snapshot comparisons, approval-file byte comparisons, launch/approval mutexes, selected JAR writer exclusion, process/profile/maintenance guards, source ambiguity checks, atomic private writes, and author trust behavior remain. `JavaHome` still launches the bundled Java afresh per capture. There is no cross-capture Java/JRE result cache: executable existence or file timestamps alone would not establish that a runtime or environment cannot change its effective home.

## RED / GREEN

Commands, from `E:/Projects/OrangeLauncher`:

```powershell
.\Build-Tests.ps1 -Entry PZ3D.JavaApprovalsTests -Output E:\Projects\OrangeLauncher\artifacts\approval-native.exe
& .\artifacts\approval-native.exe --inventory-budget
& .\artifacts\approval-native.exe
.\Build-Tests.ps1 -Entry PZ3D.Program -Output E:\Projects\OrangeLauncher\artifacts\approval-native-full.exe
& .\artifacts\approval-native-full.exe --self-test
git diff --check
```

The inventory test uses 665 independent local mod folders and real file reads, first with one enabled component and then four. It discards warm-up measurements and compares the median of three measurements; the four-component path must take at most 2.5 times the one-component path. This is a relative performance check, not a fixed machine-speed deadline.

Before production changes, RED exited 1 with the expected repeated-inventory assertion: single-component listing 257 ms, four-component listing 925 ms, save 3,749 ms. The save measurement still confirmed that the returned decision was persisted before the performance assertion failed.

After changes, GREEN exited 0: single-component listing 271 ms, four-component listing 269 ms, save 1,262 ms. An earlier GREEN run measured 262 / 268 / 1,210 ms. These fixtures explicitly configure their Java home to isolate source discovery; these timings do not measure the user's real JRE startup or real JAR sizes, and no real approval was saved for measurement.

The ordinary Java suite exited 0. It covers the authoritative returned decision/stamp, changed JAR bytes, external approval edits, changed metadata, added source inventory, addon-only approval, duplicate component/addon identities, common/42 conflicts, unknown schema preservation/refusal, author trust preservation, override refusal, path confinement, pending profile guard, and absent bundled Java refusal.

The full C# `--self-test` suite completed with exit 0 using `approval-native-full.exe`, including Java approvals, concurrent snapshot/dispatch, Steam synthetic transport, modern controller, game-process guard, configuration conflict, installation rollback, and UI regression fixtures. `git diff --check` also exited 0 before commit.
