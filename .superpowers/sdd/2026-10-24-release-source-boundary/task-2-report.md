# Task 2 report — source and diagnostic boundary

Date: 2026-10-24. Branch: fix/release-source-boundary. Starting commit: a1b47c3 (approved Task 1 dependency integrity). Task 2 only.

## Result and changed files

- build/ProductionSources.ps1: explicit list of the 92 original production filenames; validates each leaf and returns absolute paths. Unknown C# files cannot enter production automatically. Diagnostics combines production with the original 84 *Tests.cs files, excluding the new DiagnosticSelfTests.cs from that enumeration, then explicitly adds LauncherTestPaths.cs and both diagnostic files. Current membership is 92 + 84 + 1 + 2 = 179, with no duplicates.
- Orange3DTool/Program.cs: internal Main, compile-time dual-symbol error, eleven diagnostic flags rejected with exit 2 before initialization/UI. Original worker block remains verbatim. Links, Engine and MainForm remain verbatim in place; PreviewMode remains on Program. Normal ModernBootstrap launch remains.
- Orange3DTool/DiagnosticProgram.cs: diagnostic-only entry; runtime/TLS initialization, original diagnostic branches and exact self-test invocation line/order, preview initialization and Program.PreviewMode assignment, internal Program.Main fallback.
- Orange3DTool/DiagnosticSelfTests.cs: original Test class verbatim under OZL_DIAGNOSTICS.
- Build-CoreRelease.ps1: shared production resolver for core, separate production + CoreReleaseTool source array for tooling, OZL_PRODUCTION on both compiler paths.
- Build-Setup.ps1: resolver and production symbol for both setup branches and uninstaller.
- Orange3DTool/Build-Modern.ps1: resolver and production symbol.
- Build-Tests.ps1: diagnostic resolver/symbol, default PZ3D.DiagnosticProgram, explicit -Entry retained.
- tests/production-boundary.test.cjs: real resolver execution, temp-only missing/unknown source fixtures, baseline membership and suite-order checks, flags/workers/non-entry code preservation, PowerShell AST parsing and inspection of every compiler invocation.
- This report.

Generated version source and existing resources stay appended exactly as before. No resources, keys, assets, JARs, website code or release protocol changed.

## Before / RED evidence

Before changing implementation, tests were added and run from E:/Projects/OrangeLauncher:

    node --test tests/production-boundary.test.cjs

Exit 1; tests 8, pass 1, fail 7. The baseline test passed: 84 original test files and 56 distinct invoked test suites. The actual existing Build-CoreRelease source expression was executed in isolation (the build script itself was never executed). The production exclusion test failed:

    AssertionError: production glob includes original 84 test files
    84 !== 0

The production-entry test failed:

    AssertionError: Program still dispatches original 56 suites
    true !== false

Other expected RED failures covered unknown source auto-inclusion, diagnostic membership, diagnostic extraction and build AST integration. These were real behavior failures, not only a missing resolver assertion. Baseline source and invocation order are read from git show a1b47c3:Orange3DTool/Program.cs; the membership baseline uses git ls-tree a1b47c3:Orange3DTool.

## Focused GREEN

After implementation:

    node --test tests/production-boundary.test.cjs

Exit 0; tests 8, pass 8, fail 0.

## Direct C# binder checks

Existing WebView2 files authenticated first using the approved helper. Exact PowerShell command sequence:

~~~powershell
$ErrorActionPreference='Stop'
. ./build/DependencyIntegrity.ps1
Assert-OzlWebView2Dependencies -SdkDirectory (Join-Path (Get-Location) 'Dependencies/WebView2')
. ./build/ProductionSources.ps1
$project=Join-Path (Get-Location) 'Orange3DTool'
$production=@(Get-OzlProductionSources -ProjectDirectory $project)
$diagnostic=@(Get-OzlDiagnosticSources -ProjectDirectory $project)
$tool=@($production)+@(Join-Path $project 'CoreReleaseTool.cs')
$checkDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ozl-boundary-binder-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkDirectory | Out-Null
$sdk=Join-Path (Get-Location) 'Dependencies/WebView2/lib/net462'
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk/Microsoft.Web.WebView2.Core.dll","/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll")
foreach($check in @(@('production','OZL_PRODUCTION',$production),@('diagnostic','OZL_DIAGNOSTICS',$diagnostic),@('tool','OZL_PRODUCTION',$tool),@('mutual','OZL_PRODUCTION;OZL_DIAGNOSTICS',$production))){
 $sourceSet=$check[2]
 "CHECK $($check[0]) sources=$($sourceSet.Count) output=$checkDirectory/$($check[0]).dll"
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:library /platform:x64 "/define:$($check[1])" "/out:$checkDirectory/$($check[0]).dll" $refs $sourceSet
 "EXIT $LASTEXITCODE"
 if($check[0] -ne 'mutual' -and $LASTEXITCODE -ne 0){throw 'Unexpected binder failure'}
 if($check[0] -eq 'mutual' -and $LASTEXITCODE -eq 0){throw 'Mutual symbols unexpectedly accepted'}
}
~~~

Actual unique output directory: E:\AppCaches\Temp\ozl-boundary-binder-115f50d94bc7441b9264a61035c1368a.

- production: 92 sources, exit 0.
- diagnostic: 179 sources, exit 0.
- tool: 93 sources, exit 0.
- mutual: 92 sources, exit 1, Program.cs(2,8) error CS1029 with exact #error text “Production and diagnostic symbols are mutually exclusive”.
- Production and tool reported CS0649 for Program.PreviewMode remaining false in production. This is expected because only diagnostic preview code assigns it.
- Helper succeeded silently. No dependencies downloaded or modified.
- Only three DLL files were produced; never loaded, reflected, or executed. No compiler resources or generated version attributes were added to these isolated binder checks.

## Combined checks and self-review

The final preservation assertion was strengthened to compare all remaining Links/Engine/MainForm text against baseline. The first combined run exposed a one-character trailing-newline difference in the test's sliced strings (24 pass / 1 fail), not a code difference. The assertion was corrected to trim the boundary whitespace, then the combined command was repeated because that test failure required verification:

    node --test tests/production-boundary.test.cjs tests/dependency-integrity.test.cjs

Final exit 0; tests 25, pass 25, fail 0, skipped 0. No broad executable self-tests were invoked. git diff --check passed.

Self-review inspected all build script diffs and diagnostic entry extraction. It confirmed all seven formal compiler commands use their intended symbol and shared resolver-derived source array, CoreReleaseTool is added only for the tool build, the explicit diagnostic -Entry override survives, and resource/version arguments remain unchanged. Main signatures are inspected separately because library compilation does not validate /main. LauncherRuntime.Initialize only sets two AppContext switches, so diagnostic fallback repeating initialization is idempotent.

## Limits and concerns

This demonstrates source boundaries, C# binding and static entry/build configuration. Installer packaging, launcher execution, diagnostic suite execution, UI/preview rendering, worker runtime behavior, signing, and antivirus behavior remain unvalidated. No claim that this changes or resolves antivirus detection is made. No quarantined sample was accessed/restored/run; no Defender settings, CI, release/deployment/feed, VERSION, game, Steam or user configuration were changed. None of Build-CoreRelease, Build-Setup, Build-Tests, Build-Modern or Prepare-Dependencies was invoked during validation.

The diagnostic source enumeration intentionally admits future *Tests.cs additions; the production allowlist requires deliberate review for new production files. All eleven diagnostic flag names are denied by Program even in a diagnostic compilation when explicitly selecting Program as -Entry; the supported default diagnostic entry is DiagnosticProgram.

