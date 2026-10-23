# Release Source Boundary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development task-by-task, read-only task and final review. User already authorized repair/background execution and waived repeated confirmation.

**Goal:** Separate production from test/dev C# inputs and authenticate every shipped WebView2 dependency, without building or executing a new product package.

**Architecture:** Shared explicit source resolver plus diagnostic-only entrypoint; one pinned dependency manifest with read-only PowerShell validators reused by preparation and CI. Preserve existing runtime and update protocols.

**Tech Stack:** Windows PowerShell, .NET Framework csc library-only compile, existing Node test runner. No added dependency.

## Global Constraints

- Work E:/Projects/OrangeLauncher, branch fix/release-source-boundary, base9e12c76. Existing preference current directory/no extra worktree. Background only.
- No product EXE/installer build/run/repack, no quarantined sample access/restore/run, no Defender exclusion/settings changes, no CI execution/release/deploy/feed/version changes. Do not call Build-CoreRelease, Build-Setup, Build-Tests, Build-Modern or Prepare-Dependencies during validation. Source edits to those scripts are allowed.
- Tests may run existing PowerShell/csc with synthetic ordinary byte fixtures and compile source directly to temporary library DLLs only. Never load/execute generated DLLs. No private keys, game/Steam/user configuration writes or dependency download during tests.
- Preserve all production worker flags and UI behavior, 18-file protocol, original diagnostic suite invocation order and test entry selection. No third-party feature, JAR, asset or website changes.
- Use apply_patch, scoped commits. Report only verified engineering fixes; no claim Defender root cause or permanent no-detection guarantee.

## Task 1: Authenticate all shipped WebView2 dependencies including cached paths

**Files:** Create build/dependencies.lock.json, build/DependencyIntegrity.ps1, tests/dependency-integrity.test.cjs. Modify Prepare-Dependencies.ps1, Build-Setup.ps1, .github/workflows/build-release-installer.yml. Test fixtures temporary ordinary bytes, no PE execution.

**Interfaces:** Dot-source build/DependencyIntegrity.ps1. Functions `Assert-OzlWebView2Package -PackagePath <string> -LockPath <string>` and `Assert-OzlWebView2Dependencies -SdkDirectory <string> -LockPath <string>` throw descriptive errors on failure, no output/write/download on success. LockPath defaults adjacent dependencies.lock.json. `Get-OzlWebView2Lock` may expose validated metadata for download URL reuse.

- [ ] Write Node tests invoking existing PowerShell validators against generated three-file fixtures and controlled lock JSON. Exact fixed relative paths are below. Assert success for all three; each missing file, changed byte in each file, duplicate/unknown/escaping entry, malformed SHA and mismatched package fails. Validate cache path with existing files, not merely first-download path. A genuine RED should show current coverage fails to reject altered WinForms/Loader before changing implementation; source fixture for existing Prepare script can isolate its dependency-validation block without executing download/bridge work. Do not accept a missing-new-function error as sole defect reproduction.
- [ ] Add lock schema1 package id/version/url/catalog/packageSha512 plus exact three paths and SHA256. Use values from spec2026-10-24-release-source-boundary-design.md; main authenticated existing nupkg against official catalog and each entry. Validate schema, exact three unique safe names and well-formed hashes. Compare computed digests ordinal-ignorecase (Base64 package digest ordinal). Helper only reads given local files.

```powershell
$packageHash=[Convert]::ToBase64String([Security.Cryptography.SHA512]::Create().ComputeHash([IO.File]::ReadAllBytes($PackagePath)))
# Dispose hash instance in implementation; compare with authenticated lock, throw on mismatch.
foreach($entry in $lock.files) {
  $actual=(Get-FileHash -LiteralPath (Join-Path $SdkDirectory $entry.path) -Algorithm SHA256).Hash
  if($actual -ine $entry.sha256){throw "WebView2 dependency hash mismatch: $($entry.path)"}
}
```

- [ ] Prepare-Dependencies: use lock URL; authenticate nupkg before extraction; always authenticate all three after extraction OR existing cache before old bridge handling. Do not change bridge behavior in this task. Build-Setup core branch must validate SDK even though it does not call preparation. CI download uses same lock/helper: package first, extracted three second, remove duplicated single-Core hash. No workflow run or security bypass.
- [ ] Focused then complete `node --test tests/dependency-integrity.test.cjs`; AST-parse changed PowerShell without executing scripts; read-only validator against existing actual SDK and official-verified nupkg. Self-review, scoped commit and report .superpowers/sdd/2026-10-24-release-source-boundary/task-1-report.md with real RED/GREEN outputs. Return status/commit/testsummary/concerns.

## Task 2: Isolate production source inputs from diagnostics without deleting tests

**Files:** Create build/ProductionSources.ps1, Orange3DTool/DiagnosticProgram.cs, Orange3DTool/DiagnosticSelfTests.cs, tests/production-boundary.test.cjs. Modify Orange3DTool/Program.cs, Build-CoreRelease.ps1, Build-Setup.ps1, Build-Tests.ps1, Orange3DTool/Build-Modern.ps1. Keep non-entry Program classes in place.

**Interfaces:** Dot-source build/ProductionSources.ps1. `Get-OzlProductionSources -ProjectDirectory <string>` returns explicit validated92 original C# absolute paths, excludes every *Tests.cs, LauncherTestPaths.cs, CoreReleaseTool.cs and new diagnostics. `Get-OzlDiagnosticSources -ProjectDirectory <string>` adds original84 testfiles, LauncherTestPaths.cs and two Diagnostic files. Release tool adds CoreReleaseTool.cs only. Production `/define:OZL_PRODUCTION`; diagnostics `/define:OZL_DIAGNOSTICS`; reject both simultaneously at compiletime. Build-Tests default Entry PZ3D.DiagnosticProgram, explicit -Entry preserved.

- [ ] Before edits capture current production-glob inclusion and suite invocation list. Add behavior tests which demonstrate real existing production source set includes84testfiles and Program dispatches56 suites, not just a missing resolver. Then assert new explicit resolver excludes an added unknown dummy .cs and fails for missing listed source. Test production vs diagnostics memberships and no accidental duplicated paths.
- [ ] Move Program test/dev commands into DiagnosticProgram, original Test class into DiagnosticSelfTests. Preserve all tests and their invocation order verbatim. DiagnosticMain initializes LauncherRuntime/TLS, dispatches test/dev flags then falls back to internal `Program.Main(args)`. Keep Program type, PreviewMode, Links/Engine/MainForm. Preview commands also migrate but normal ModernBootstrap stays. Diagnostic files `#if OZL_DIAGNOSTICS`; enforce mutuallyexclusive symbols via `#error` in shared Program.

```csharp
#if OZL_PRODUCTION && OZL_DIAGNOSTICS
#error Production and diagnostic symbols are mutually exclusive
#endif
// Production Main rejects diagnostic flags with a nonzero result, before normal UI.
```

- [ ] Diagnostic flags: --buddy-crash,--buddy-steam-crash,--check-workshop,--subscribe-workshop,--verify-bundle,--verify-cache,--prepare-dlss,--fetch-workshop,--self-test,--preview,--preview-help. Production rejects these, preserves --steam-service-worker,--playtime-watch,--session-watch,--launcher-workshop-snapshot,--launcher-workshop-rebuild,--workshop-worker dispatch. No duplication of full test dispatcher in production.
- [ ] Replace C# glob at every formal compile path in Build-CoreRelease (core and tool), both Build-Setup branches, Build-Modern. Tool adds CoreReleaseTool; diagnostics use combined resolver/defaultDiagnosticProgram and diagnostics symbol. Version attributes remain additional generated source exactly as before. Do not invoke these scripts for testing.
- [ ] Real C# binder checks via direct csc `/target:library /platform:x64` to unique temporary directory: production92/OZL_PRODUCTION, diagnostic179/OZL_DIAGNOSTICS, production+CoreReleaseTool93/OZL_PRODUCTION all compile. Explicit diagnostic tool selector adds CoreReleaseTool for180 inputs. Bothsymbols must fail with intended #error. Verify existing WebView2 via task1helper first. No DLL execution, assembly reflection/load or private resources. Separately inspect Main signatures and script AST /main and symbols since library mode does not validate entry choice.
- [ ] Compare preserved suite calls/order and diagnostic flags to baseline git Program; test worker preservation and AST shared-source usage across scripts. Focused iterations then full `node --test tests/production-boundary.test.cjs tests/dependency-integrity.test.cjs` once; no broad executable selftests. Self-review/commit/report task-2-report.md; clearly list unvalidated installer/runtime/AV behavior.

## Task 3: Review, evidence and source integration

**Files:** docs/quality/2026-10-24-release-boundary.md, plan results. No native implementation by controller.

- [ ] Read every report; per-task independent reviews, one broad final review, scoped fix loops per SDD. Resolve any unverified items explicitly.
- [ ] Main freshly run both new Node suites and PowerShell AST checks; inspect no resources/protocol/VERSION/deploy/website changed. Record all actual counts/exits and boundaries. Do not equate library compilation with AV approval or installation validation.
- [ ] Document historical detection names/events and missing exact-sample verdict; future authorized normal release requires frozen source/dependencies, pre-execution hashes, final artifact scanning/vendor determination. No new package in this task. Keep unrelated legacy/DLSS/JAR follow-up visible, not silently declared fixed.
- [ ] After verified review merge/push source main per existing authorization, no force push. Preserve local website preview3837. Tell user what changed, what remains unproven and that no new installer was distributed.

## Execution results

Task1 a1b47c3, Task2 4a795a3+29d8b3e completed and independently approved. Main fresh25/25tests and three library-only C# binding checks passed; mutuallyexclusive symbols rejected. Expected PreviewMode defaultfalse CS0649 recorded. Task3 wholebranchreview and sourceintegration pending. See docs/quality/2026-10-24-release-boundary.md; no productbuild/run/distribution or AVresolution claim.

## Final integration findings requiring the single fix wave

Controller cross-caller check found existing Test-CorePackageBuild.ps1 and Test-CoreNativeUpdate.ps1 select Build-Tests -Entry PZ3D.CoreReleaseTool, now excluded from diagnostic inputs. Those scripts also directly compile all C# without OZL_DIAGNOSTICS, causing real CS0103 Test errors after extraction. Test-LegacySetupBuild.ps1 copies scripts to an isolated fixture but omits new build helpers/lock. Preserve these existing test callers by adapting their inputs/defines and copying build support files; do not run packaging/EXE tests. Add AST/membership/copy-input regression coverage and library-only binding. Also untrack only this plan's accidentally committed task-2-report.md, preserve local evidence and never force-add scratch again. These are compatibility/scope fixes, not a new feature or AV workaround.

Final fixwave9e167a7 completed, scopedreviewallfourfindingsADDRESSED/nobreakage. Mainfresh29/29tests and180inputlibrarycompileexit0. Tasks1–3 verification/review complete; sourceintegrationmayproceedperexistingauthorization. NoAVresolution/productreleaseclaim.
