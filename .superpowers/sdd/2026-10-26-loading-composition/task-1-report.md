# Task 1 report — native connection and queue composition

## Acceptance limits

This is offline fixture and hidden-GL verification, not a real interactive game acceptance run. The installed 42.21 Lua functions execute unmodified in the installed Kahlua VM. Engine/UI targets are isolated fixture objects; texture pixels are fixture stubs (the native logo draw uses a stub logotype), not extracted packed game artwork. No game/Steam UI, process attach, controller against a real game directory, staging/recovery, server operation, version/release or website change was performed.

An initial fixture experiment, before adding the rejecting loader, discovered that registering the game's StringLib transitively initializes ObjectPool/statistics helper classes; it failed on the fixture LuaManager.platform field before running Lua. No engine bootstrap/window was called. Subsequent executions use IsolatedLuaProbe, which rejects every real zombie class before loading its bytes, plus a small explicit fixture dependency surface. StringLib is not registered. This distinction is recorded rather than claiming the earliest experiment had no game helper initialization.

## Implementation

- NativeConnectionPresentation resolves explicit MainScreen/ConnectToServer tables, inherited Type via UIManager.tableget, actual javaObject/getTable identity, native controls and visibility. No Lua table enumeration or username/auth text is read. Unknown controls, pages, roots, types, missing fields, changed environments and unsupported FBO routes keep native decoration.
- A lease activates only after QueuedBackdrop reports a non-null accepted draw, and is restricted to generation, epoch, CPU frame sequence, environment and exact native identities. Exit/throwable clears it. Parent Lua callbacks, child traversal, status, Abort, input and native callback return/exception behavior remain native.
- Only exact MainScreen logo calls, ConnectToServer fill/borders/background/arrows and its six known decorative children are retired. Native status and Abort bounds become normalized immutable draw-ticket observations; LoadingScene reserves these lanes. GameRenderer only adds the forwarding overload needed for those bounds.
- Required yields before original dispatch. WorkshopUpdate stays native. Success must originate inside the current connection state's update, match the operation, and be followed by visible passive connection plus closed native pages before resuming. Previous connection Success cannot resume a new interaction. Auth/QR/character/spawn still hand off.
- Queue initial place=-1/immediate gets one conditional EndFrameUI-entry slot after original black clear. Positioned queue uses its normal background slot, exact passive LoadingQueueUI render is retired, and EndFrameUI cannot duplicate it. Native requests, update, Esc/controller A edge, lost connection and redirects remain unchanged. Queue scene includes its actual cancel hint.
- Existing world/save errors, original Done and multiplayer SendDone behavior remain intact; an extracted OnConnected saved-player fixture covers deferred queue request, queue entry, world entry, SendDone and Done.

## TDD evidence

The saved pre-change component is local-only artifacts/native-composition/old-startup.jar. With an accepted real premain receipt:

```powershell
./tests/Run-NativeCompositionFixture.ps1 -AgentJar (Join-Path $PWD 'artifacts/native-composition/old-startup.jar')
./tests/Run-NativeCompositionFixture.ps1 -Probe QueueCompositionTest -AgentJar (Join-Path $PWD 'artifacts/native-composition/old-startup.jar')
```

Both fail for the intended missing behavior: native MainScreen Lua logo remains, and initial queue place=-1 lacks a scene. Logs: artifacts/native-composition/red.txt and queue-red.txt. Lifecycle RED before the interaction change: lifecycle-red.txt, Required did not yield. The current component passes these behaviors.

Pre-change hidden GL reproduction:

```powershell
./tests/Run-NativeCompositionFixture.ps1 -Probe NativeLuaGlProbe -AgentJar (Join-Path $PWD 'artifacts/native-composition/old-startup.jar')
```

10 checks passed, explicitly confirming the three-layer pre-fix reproduction, rather than asserting it coherent.

## Verification

```powershell
./Build-StartupComponent.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid
./tests/IngameStartup.test.ps1 -GameDirectory E:/Steam/steamapps/common/ProjectZomboid
./tests/Run-NativeCompositionFixture.ps1 -Probe NativeLifecycleTest
& E:/AppCaches/Temp/ozl-lc-fc466548/build-tests.ps1 -Entry PZ3D.GameStartupSuiteTests -Output E:/AppCaches/Temp/ozl-t1-69880c5d/suite.exe
& E:/AppCaches/Temp/ozl-t1-69880c5d/suite.exe --headless
git diff --check -- Orange3DTool/StartupSource tests/IngameStartup.test.ps1 tests/java tests/Run-NativeCompositionFixture.ps1
```

- Full Java suite: PASS. Counts printed: model/session 28; refinement model 23; server information 11; A2S 92; scene contract PASS; viewport/frame 63; agent transformer 51; refinement premain 32; server-info premain 16; actual native Lua composition 17; extracted workshop/auth/character lifecycle 17; queue 11; hidden native-Lua GL 14; real GL/state PASS; queued GL 17; actual game no-initialization probe 18 class targets plus the added exact UI/environment/queue APIs; package audit PASS. Log artifacts/native-composition/full-suite.txt.
- The final saved-player → queue → world → SendDone → Done assertions passed in both the focused NativeLifecycleTest and the refreshed full Java suite: **17 checks PASS**.
- Native headless suite: **889 PASS lines**, final ALL GAME STARTUP AND RELATED REGRESSIONS PASSED; fresh short fixture directory E:/AppCaches/Temp/ozl-t1-69880c5d. Log artifacts/native-composition/native-suite.txt.
- Owned diff whitespace check passed. Whole-tree diff check reports existing foreign deploy/README.md CRLF whitespace; those files were not edited or staged.
- Known Byte Buddy/JVM25 sun.misc.Unsafe deprecation warnings remain. The two `presentation disabled: NullPointerException` lines are intentional renderer-failure containment cases (null fixture receipt), asserted to preserve native controls and restore following-frame decoration.
- Compile-only API declarations under tests/java/kahlua-api let javac21 target Java17 without trying to read Java25 Kahlua class files. They are absent from all runtime classpaths and the component. Original Kahlua is loaded at runtime by the game's JRE25. A temporary ECJ compiler was used once during investigation; no ECJ dependency/install is needed by committed tests.

## Inspected frames

All paths are relative to this checkout; both worker and root inspected the before/after composition.

- artifacts/native-composition/native-lua-before-1280x720.png
- artifacts/native-composition/native-lua-before-1920x1080.png
- artifacts/native-composition/native-lua-after-1280x720.png
- artifacts/native-composition/native-lua-after-1920x1080.png
- artifacts/native-composition/native-lua-renderer-failure.png
- artifacts/native-composition/native-lua-failure-next-native.png

The fresh full-suite copies are under artifacts/ingame-startup/tests/native-composition/. Status and Abort pixels are measured independently in both 720p and 1080p captures. The tiny orange in these frames is the first accepted animation frame; sustained animation remains Task 2. A future deferred GPU failure cannot retroactively restore already-filtered same-frame decoration; essential native status/Abort are never filtered, and failure disables subsequent filtering. No contrary same-frame decoration recovery claim is made.

## Component and handoff

Built ignored local artifact Orange3DTool/Assets/OrangeStartup.jar SHA-256:

`0927A42311D7682E19A96D9CBD4F72403E8887455AAA32F1C623FED3F29C0329`

The repository already ignores generated startup JAR/build-stamp outputs; rebuilt locally for downstream candidate packaging, without force-tracking binaries. The package audit excludes zombie/, se/krka/, org/lwjgl and Buddy runtime classes. No source from game classes is packaged.

Task 3 read-only native workshop pointers: media/lua/client/OptionScreens/ServerWorkshopItemScreen.lua has installButton/disconnectButton/label/listbox; onButtonInstall around 143, onButtonDisconnect 148, setRequiredItems 154, updateItemDetails 174, progress 190, local OnServerWorkshopItems dispatch 216. Native list/Install/Disconnect remain untouched in Task 1. A distinct custom download scene remains separate authorized work.

After removing trailing blank lines in three new files, the full Java suite was rerun and passed. The rebuilt final JAR has identical decompressed entry bytes to the JAR embedded in the headless-tested suite (verified across every ZIP entry); only ZIP metadata and the container hash changed. The 889 native headless checks therefore cover the same payload bytes. Staged owned whitespace check then passed.
