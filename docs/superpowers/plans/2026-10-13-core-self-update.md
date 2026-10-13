# Core-only file-level self-update implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver an installable launcher with versioned, signed, file-level self-updates, restart and rollback, without bundling third-party shader presets.

**Architecture:** VERSION is the single version authority. A signed release manifest describes independently downloadable owned files. A dedicated update coordinator downloads changed files, and a copied helper applies a recoverable transaction after the launcher exits. Installation, updates and uninstallation share the same ownership record.

**Tech Stack:** Existing C# .NET Framework 4.8, WinForms/WebView2, PowerShell build scripts, native fixture tests and Playwright UI tests.

## Global Constraints

- Work at E:/Projects/OrangeLauncher on feature/core-self-update; main remains unchanged. Do not create a worktree without user consent; the existing feature branch is the default.
- User approved docs/superpowers/specs/2026-10-13-core-self-update-design.md and additionally requires recorded version numbers. First formally versioned file-layout build is 1.1.0. Earlier single-EXE builds are legacy unversioned launcher builds, not asserted to be 1.0.0.
- VERSION contains a strict three-part numeric version. All executable version attributes, visible version, install records and release manifests derive from it. Record changes in CHANGELOG.md, never silently reuse a released version for different files.
- Windows x64; no registry writes, no automatic system runtime installation or elevation, no game/mod/saves/Java-authorizations mutation by updater, no third-party shader ZIPs or original-author covers in distributables.
- Default check is manual, asynchronous, cancellable and independent from Steam and the local controller status queue. No auto downloads at startup. Applying waits for game exit and for tool mutations to finish; never kill a game process.
- HTTPS GitHub Releases source is LiuDaCheng666/orange-zomboid-launcher. Verify exact manifest bytes with an embedded trusted RSA SHA-256 public key, then validate full file SHA-256 and length; checksum alone is not publication authentication.
- Accept only owned paths: 橙子启动器.exe, 橙子更新助手.exe, 卸载橙子启动器.exe, VERSION, CHANGELOG.md, 使用说明.txt, release-public-key.xml, Web/index.html, Web/app.js, Web/app.css, Web/home.png, Web/orange-app.ico, Web/WebView2-LICENSE.txt, Web/Microsoft.Web.WebView2.Core.dll, Web/Microsoft.Web.WebView2.WinForms.dll, Web/WebView2Loader.dll, Assets/OrangeShootPresentation.jar, Assets/OrangeLaunchBridge.lua. Runtime data and staging are not release files.
- Manifest schema 1; install schema 2; platform win-x64; updater protocol version 1.0.0. Manifest maximum 1 MiB, at most 32 files, each at most 64 MiB, total at most 128 MiB. Duplicate paths are case-insensitively invalid. Do not accept path traversal, device names, ADS, reparse points or arbitrary execution/URLs.
- Public update models in namespace PZ3D use lower-case JSON fields: CoreFile { string path, sha256, url; long length; }, CoreManifest { int schema; string version, platform, minimumUpdaterVersion, notes; List<CoreFile> files; }, CoreInstallRecord { int schema; string root, installationId, version; List<CoreFile> files; }.
- Current release is usable locally even before a formal GitHub Release exists. A missing/unpublished feed is shown honestly as unavailable; do not disable signature validation to obtain a successful demo. No git push or actual Release publication in this implementation.
- Tests first, real fixtures, focused RED/GREEN evidence. Changes stay in task-owned files; no unrelated hall bug fix or UI redesign.

## Task 1: Version authority and signed release protocol

**Files:** Create VERSION, CHANGELOG.md, Build-Version.ps1, Orange3DTool/CoreReleaseProtocol.cs, Orange3DTool/CoreReleaseProtocolTests.cs; modify Build-Tests.ps1 only for version resource/attributes and invoke the test entry explicitly.

**Interfaces:** Produce AppVersion.Current, AppVersion.ReleaseTag, CoreReleaseProtocol.Verify(byte[] raw, byte[] signature, string publicKeyXml, string currentVersion), CoreReleaseProtocol.Validate(CoreManifest manifest, string currentVersion), CoreReleaseProtocol.Target(string root, string relative), CoreReleaseProtocol.Hash(byte[] data), CoreReleaseProtocol.HashFile(string path), CoreReleaseProtocol.OwnedPaths. Hash returns upper-case hexadecimal. Verify returns CoreManifest. Target returns validated absolute path. Expose shared serializers/strict validation needed by later tasks without trusting raw records.

- [ ] Write runnable tests that first fail for absent version/protocol behavior, not syntax errors. Tests must catch replacing signature verification with checksum-only trust, replaying an older version, and allowing unexpected filenames.

```csharp
// Test inputs are an independently built signed fixture, not production-generated expected output.
// Schema contains the complete fields above and one Web/app.js file with known bytes.
var result = CoreReleaseProtocol.Verify(raw, validSignature, testPublicKey, "1.1.0");
Check(result.version == "1.1.1", "signed newer version is accepted");
Reject(() => CoreReleaseProtocol.Verify(tamperedRaw, validSignature, testPublicKey, "1.1.0"));
Reject(() => CoreReleaseProtocol.Target(root, "../game/ProjectZomboid64.json"));
```

- [ ] Run `./Build-Tests.ps1 -Entry PZ3D.CoreReleaseProtocolTests -Output ./artifacts/core-protocol-tests.exe` and that executable; record the expected failing assertion before implementing.
- [ ] Implement strict manifest validation, RSA signature verification, strict UTF-8/JSON parsing and path ownership. Use resource `Launcher.Version.txt` for runtime version; Build-Version.ps1 reads VERSION, emits generated assembly attributes under artifacts and returns their path. Build-Tests.ps1 embeds VERSION and compiles those attributes. Parsing must reject unknown or ambiguous duplicate JSON fields as well as duplicate file paths. All release file URLs are restricted to HTTPS asset URLs for this repository and the signed version's release tag.

```powershell
$releaseVersion = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()
if($releaseVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'VERSION must use major.minor.patch'}
# AssemblyVersion and AssemblyFileVersion receive $releaseVersion + '.0'.
```

- [ ] Test authentic/tampered/wrong-key signatures, same/older versions, bad UTF-8, duplicate JSON fields, oversized documents, zero/too-large files, invalid platform/updater version, case duplicates, traversal and HTTPS/repository/tag mismatch. Cover runtime build version and tag consistency.
- [ ] Run focused suite and full native baseline once, inspect staged diff and commit with `feat: version launcher and authenticate release manifests`. Write RED/GREEN commands/results to task report.

## Task 2: Owned installation and recoverable update transaction

**Files:** Create Orange3DTool/CoreDeployment.cs, Orange3DTool/CoreUpdateTransaction.cs, Orange3DTool/CoreDeploymentTests.cs, Orange3DTool/CoreUpdateTransactionTests.cs. Modify PortableDeployment.cs only to select v2 records for uninstall and retain legacy v1 compatibility; InstallerProgram.cs integration is in Task 3.

**Interfaces:** Consume Task 1 protocol. Produce CoreDeployment.Managed(root), Read(root), Install(root, CoreManifest manifest, IDictionary<string,byte[]> files), Uninstall(root, string runningExe=null). Record file remains orange-installation.json. Produce CoreUpdatePlan with changed files, removed files, byte total and baseline; CoreUpdateTransaction.Plan(root,target), Stage(root,target,IDictionary<string,byte[]> changed), Apply(root,taskId,Func<string,bool> health,Action<string> checkpoint=null), Recover(root), ConfirmHealth(root,taskId,nonce), HealthArguments(root,taskId), and a validated task-info accessor needed by helper. Stage returns taskId; Apply invokes health with installed executable absolute path, commits on true, otherwise restores and throws. ConfirmHealth validates nonce against active transaction; health startup must not trigger ordinary rollback recovery. Exact method returns needed beyond this interface must be documented in report for Task 4.

- [ ] Write fixture tests for changing only Web/app.js while leaving arbitrary user files unchanged, conflict detection, true owned uninstall after update, and interrupted mixed-file replacement. Fixture root uses LauncherTestPaths.Root; no real game folders.

```csharp
var plan = CoreUpdateTransaction.Plan(root, releaseB);
Check(plan.Changed.Select(f => f.path).SequenceEqual(new[]{"Web/app.js"}), "only changed JS needs download");
string task = CoreUpdateTransaction.Stage(root, releaseB, new Dictionary<string,byte[]>{{"Web/app.js", newJs}});
CoreUpdateTransaction.Apply(root, task, exe => false);
// Independently compare old JS bytes and old installation-record bytes after health failure.
```

- [ ] Run focused test entry and confirm expected missing transaction/ownership behavior before implementation. Avoid asserting only call counts or source text.
- [ ] Implement per-root mutual exclusion used by install/update/uninstall. Validate actual baseline against record; existing unequal files are conflicts, missing owned files can be restored. Use marker-owned per-task staging and durable transaction journal on the same volume, revalidate target boundaries before every file operation. Copy previous owned files and record before replacement; update record only as part of transaction; remove no unknown file. Recovery handles both removed/new files and interrupted install-record write. No rollback may overwrite an independently modified candidate file without reporting a conflict.
- [ ] Keep the previous complete version until health confirmation. Mark success only after health callback says true. Return distinct successful/rolled-back/conflicted states and persist them under owned install runtime diagnostics. No recursive deletion of install root; cleanup only marked own task folders. Normal startup recovery rolls back incomplete work, health startup uses a validated pending task and nonce.
- [ ] Cover signature-authenticated staging provenance, modified staged bytes, modified old baseline, occupied files, partial writes, disk/permission failure, repeated apply, two competing callers, rollback failure reporting and install/uninstall cross-process locking. Emit fault checkpoints after staging, after each replace, after record commit; tests exercise each checkpoint including simulated process interruption via child fixture executable where necessary.
- [ ] Run focused suites and full native suite once, inspect diff and commit `feat: apply owned core updates with rollback and recovery`. Report interfaces and test evidence.

## Task 3: Split core package and versioned release tooling

**Files:** Create Build-CoreRelease.ps1, Orange3DTool/CoreReleaseTool.cs, Orange3DTool/CorePackageTests.cs and docs/core-release.md; modify Build-Setup.ps1, Orange3DTool/Build-Modern.ps1, ModernAssets.cs, BundledAssets.cs, InstallerProgram.cs, resource readers in Program.cs/PortableDeployment.cs as needed, README.md and 使用说明.md. Do not change the network controller or update UI here.

**Interfaces:** Consume version, manifest and installation contracts. Emit dist/<version>/core/, installation EXE, full core ZIP, signed update-manifest.json plus update-manifest.sig, and content-addressed update assets matching manifest URLs. Read active public key from embedded `Launcher.UpdatePublicKey.xml`; private key never embedded. The updater helper entry is PZ3D.CoreUpdaterProgram, implemented in Task 4; defer compiling that entry until it exists, but write the contract and build tests now. CoreReleaseTool CLI supports key generation, release packing/signing and package validation; log no private key or credentials.

- [ ] Write package fixture tests to run build/pack tooling and assert actual outputs: VERSION/version metadata consistent, no PresetShaders/author-covers, independent JS/CSS/background files, valid signature and no embedded shader ZIP. Add installer test proving exact v2 extraction and owned uninstall. Test existing single-EXE fallback still usable for legacy capture/tests where needed.

```powershell
./Build-CoreRelease.ps1 -VersionOnly
# Must derive 1.1.0 and v1.1.0 from VERSION, never from a second literal.
./Build-Tests.ps1 -Entry PZ3D.CorePackageTests -Output ./artifacts/core-package-tests.exe
./artifacts/core-package-tests.exe
```

- [ ] Observe failing package assertion, then implement sidecar layout from the allowlist. ModernAssets.Prepare chooses an owned validated install Web folder; standalone legacy resource fallback remains only for legacy/test builds. New core build does not embed Modern.Web.zip, private preset bundle, SDK DLLs or original-author covers. Bundled resource readers use owned sidecar Assets for the tool's own bridges. Default shader selection and descriptions must not claim installed preset files that are absent; keep user's imported packs.
- [ ] Installer embeds the complete core file collection plus signed metadata, validates all before writing to a new independent empty directory, and invokes CoreDeployment.Install. Keep legacy installer verification compatibility where applicable. Uninstaller understands updated v2 hashes and its own delayed deletion, no registry or game writes. Changed-file packaging uses stable bytes for unchanged assets so repacking does not cause unnecessary resource downloads.
- [ ] Generate a real publication key outside repo, under an explicit user-owned E-drive signing directory, only if none exists. Export its public portion to tracked release-public-key.xml, embed it in official executables. Never overwrite an existing key; never print or commit private XML. Build script requires explicit signing-key path and fails closed if key/public mismatch. Test keys stay in artifacts fixtures, never in the official feed.
- [ ] Build release assets from one validated file set, RSA-sign exact raw manifest bytes, validate package for unexpected files, include required dependency notices. Full ZIP uses same installation schema and is directly runnable after extraction. A draft-publication command may be documented, but do not push/upload anything.
- [ ] Run focused package tests, pack the 1.1.0 release once Task 4 helper exists, and retain evidence of exact file names/sizes/hashes/signature and no third-party presets. Commit `feat: package versioned core files without shader presets` and report build limitations if helper is not yet available.

## Task 4: Update coordinator, helper lifecycle and native UI

**Files:** Create Orange3DTool/CoreUpdateService.cs, CoreUpdaterProgram.cs, CoreUpdateServiceTests.cs, CoreUpdaterIntegrationTests.cs, Web/core-update-ui-tests.cjs; modify ModernHost.cs, Program.cs startup, Web/app.js, Web/app.css, Web/index.html and focused host-policy tests. Existing component updatesGet remains unchanged.

**Interfaces:** Consume protocols and transactions; expose native actions coreUpdateStatus, coreUpdateCheck, coreUpdateDownload, coreUpdateCancel, coreUpdateApply. These use their own coordinator/async lane, not SteamLauncherServices and not ModernController.Execute. ModernHostPolicy must allow exact actions and payload constraints. Service events coreUpdateProgress carry only native-authoritative task/version/progress fields. Runtime trust comes from embedded public key, not an untrusted editable local key file. Request payloads cannot choose URLs, output paths or executables.

- [ ] Write protocol/native/UI tests first. Demonstrate status read + blocked controller does not prevent update checking; unauthenticated publication is not offered; download fetches only JS in A/B fixtures; cancellation leaves current program unchanged; repeated click starts one job.

```javascript
await page.getByRole('button', {name:'检查启动器更新',exact:true}).click();
await expect(page.getByText('可更新至 1.1.1', {exact:true})).toBeVisible();
// Capture only the host messages at the external boundary; real page renders its own status.
// A source request id from an earlier check must not replace current update state.
```

- [ ] Run focused tests and record RED. Implement HTTPS release lookup with bounded/cancellable requests, restricted redirects, raw signed manifest download, verified metadata before UI offers a release, file-length/streaming hash validation and retry reuse of already verified staged files. UI version always comes from native version authority. No Java/Steam credentials and no mod downloads.
- [ ] Coordinator holds a per-install cross-process lock during preparation/download. Safely release and reacquire for helper handoff while verifying task identity/baseline; do not allow a second update/installer/uninstaller to invalidate a prepared transaction unnoticed. Log check, download, file verify, cancellation, apply, restart, success and rollback under owned diagnostics; preserve durable task history without overwriting unrelated task state.
- [ ] Apply gates actual tool writes, unfinished launch session restoration, active game and unsaved UI drafts. After explicit restart confirmation, safely cancel/drain own reads, copy and hash-verify the current helper, pass strictly validated task and original process identity, then close launcher normally. Helper waits only for matching launcher process, applies transaction, starts installed EXE using generated health arguments, verifies fresh health marker with task nonce and candidate process; startup health acknowledgement occurs only after native init, page load and one valid host communication. Health failure restores old version and restarts it once. Normal startup recovers incomplete transaction before UI loads. No force killing arbitrary process.
- [ ] Add compact update panel to help and top entry, colored textual states, current/latest versions, checked time, notes, byte totals, current file, speed, cancellation/retry and restart action. Warn about unsaved editors before quit; leave unrelated navigation/read controls available. Both themes and small windows must be visually inspected via screenshots. Use a fixture feed for native automated update/restart/rollback closed loop; do not loosen official HTTPS trust for fixture convenience.
- [ ] Add explicit protocol/file compatibility check so a mixed page/core set cannot silently invoke incompatible actions. Preserve current component update behavior, importable shaders and all existing profile/native tests.
- [ ] Run focused native/UI suites, all native baseline and browser suites. Run Task 3 real signed 1.1.0 pack and fixture A→B update with native restart/healthy acknowledgement plus failed candidate rollback and scoped uninstall. Record that formal feed is not published. Commit `feat: update core files in-app with progress and safe restart`.

## Final verification and handoff

- [ ] Fresh C# self-tests plus focused update entries all green; Playwright existing and new suites all green. Review any warnings as findings.
- [ ] Inspect signed 1.1.0 artifacts, verify installer extraction, direct ZIP startup, key provenance, sparse download evidence, native healthy restart and rollback evidence, both UI themes and exact uninstall behavior. No real game, Steam subscription or authorizations are modified during tests.
- [ ] Run task-scoped reviews after each task and a whole-branch review once all tasks complete. Resolve critical/important findings through the implementer; keep an identity-specific ledger, no speculative completed claims.
- [ ] Update CHANGELOG.md and release report with version 1.1.0, paths, SHA-256, tested boundaries, remaining unpublished channel state and migration steps. Keep source branch local; do not merge main, push or create a GitHub Release without user direction.

**Coverage map:** Task 1 covers version authority/authentication; Task 2 covers ownership and transaction recovery; Task 3 covers installation/resources/no-shaders/release assets; Task 4 covers networking/UI/progress/concurrency/helper/health handshake. Final verification exercises cross-task integration and distinguishes local fixtures from official publication.
