# Task 1 — pinned Velopack preparation

Implemented only in `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher`, branch `codex/velopack-updates`. The delivery consists of `Prepare-Velopack.ps1`, `build/VelopackDependencies.ps1`, `build/velopack.lock.json`, `tests/velopack-dependencies.test.ps1`, and this report. Dependency outputs are ignored under `Dependencies/Velopack`; no global package manager, SDK or tool installation was used.

## Pinned sources and installed inventory

The lock lists every extracted regular file and its SHA-256: SDK 16, Newtonsoft 24, CLI 455 (495 total). It verifies the entire package before opening its ZIP, preflights all entries and their file hashes before extracting, verifies the staged inventory, then promotes the stage. Existing installations are checked again on every preparation/lookup; damaged files are rejected rather than silently repaired. Traversal, absolute paths, Windows ADS/device/ambiguous names, case-insensitive duplicates, undeclared files, nonregular Unix entries and filesystem reparse points are rejected. No dependency package is executed by preparation.

| Package | Exact source | Package SHA-256 |
| --- | --- | --- |
| Velopack 1.2.161 | `https://github.com/velopack/velopack/releases/download/1.2.161/Velopack.1.2.161.nupkg` | `6EEA6CB720651FCA0E28EAE7567A4E228ED977A1B42AB4FDDA748B47756D7CBC` |
| vpk 1.2.161 | `https://github.com/velopack/velopack/releases/download/1.2.161/vpk.1.2.161.nupkg` | `2B56CE117F803FC70C103CB423BD040E395E40370F6FF6E10818E9FF9C26A323` |
| Newtonsoft.Json 13.0.4 | `https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.4/newtonsoft.json.13.0.4.nupkg` | `F09081D457405BAF35A973FA0C50D6BF272ED683F2568C5A620A49DA952F6529` |

The first two digests were independently confirmed in the [official release assets](https://github.com/velopack/velopack/releases/expanded_assets/1.2.161). Newtonsoft was acquired from the official NuGet URL linked by its [package page](https://www.nuget.org/packages/Newtonsoft.Json/13.0.4). **NuGet and GitHub Velopack package bytes differ because NuGet adds repository signing. Do not replace these GitHub URLs with NuGet URLs while retaining the GitHub digests.** The supplied SDK file in `Dependencies/velopack.1.2.161.nupkg` was the NuGet variant; a fresh GitHub asset was acquired for the pinned preparation.

Full downloads initially stalled in this environment. Eight bounded 20 MiB range requests to the same official GitHub vpk asset (cache-busting query parameters only) were concatenated to 146,884,263 bytes, and the complete result passed the required SHA-256 before it was used. Verified packages were seeded into `Dependencies/Velopack/packages/`; actual preparation then extracted and checked them. Local HTTP tests separately exercise the downloader, so the checked path includes both real downloads and real extraction. Direct preparation downloads use a 120-second request timeout and delete incomplete downloads.

## Public preparation interface

```powershell
# Both accept the following optional parameters:
Prepare-Velopack.ps1 [-DependencyRoot <string>] [-LockPath <string>] [-DotNetPath <string>]
Get-OzlVelopackDependencies [-DependencyRoot <string>] [-LockPath <string>] [-DotNetPath <string>]

. ./build/VelopackDependencies.ps1
$deps = Get-OzlVelopackDependencies
$vpk = $deps.VpkExecutable
$prefix = @($deps.VpkArguments)
& $vpk @prefix --help
```

Preparation and lookup return one object with `SdkAssembly`, `NewtonsoftAssembly`, `VpkExecutable`, `VpkArguments` (`string[]`), `VpkEntryPoint`, `Runtime`, `RuntimeVersion`, and `DependencyRoot`. Lookup is read-only and checks all installed bytes before returning executable paths. Explicit alternate lock/root parameters support local fixture tests. An explicitly supplied `DotNetPath` permits a project-local host when needed; no undeclared local runtime is selected implicitly.

Default root: `C:/Users/MC233/.codex/worktrees/velopack-updates/OrangeLauncher/Dependencies/Velopack`.

- SDK: `sdk/lib/net472/Velopack.dll` below that root.
- Newtonsoft: `newtonsoft/lib/net45/Newtonsoft.Json.dll`.
- CLI: `vpk/tools/net8.0/any/vpk.dll`.
- CLI executable in this environment: `C:/Program Files/dotnet/dotnet.exe`.
- CLI argument prefix: the absolute CLI DLL path above.

## Runtime/compiler and concrete SDK APIs

The selected SDK targets .NET Framework 4.7.2. Its nuspec declares Newtonsoft.Json 13.0.4 plus framework assemblies `System.IO.Compression`, `System.IO.Compression.FileSystem`, `System.Net.Http`, and `System.Web`. The selected Newtonsoft net45 assembly has no external dependency packages. Existing compiler `C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe` compiled the probe successfully. Actual loaded assembly identities are `Velopack, Version=1.2.0.0` and `Newtonsoft.Json, Version=13.0.0.0`; these stable assembly versions are distinct from package versions.

The selected vpk entry requires `Microsoft.NETCore.App` 8.0.0 via its shipped runtimeconfig. Installed 8.0.21 satisfies this requirement. No full .NET SDK, global vpk tool, local runtime download, or Windows Desktop runtime was needed. Other bundled CLI TFMs are net9.0/net10.0; do not accidentally select those on this machine. vpk's optional Azure signing helper has a separate net6.0 runtimeconfig; it was not invoked.

Probe source and reflection output are retained in ignored `artifacts/velopack-sdk-probe/VelopackSdkProbe.cs` and `api.txt`. The probe builds a `VelopackApp` with auto-apply disabled and reflects types; it does not run bootstrap, contact a feed, download an update, or hand off an update. Its unused compile-contract method type-checks the asynchronous discovery/download and apply signatures.

Key public signatures confirmed from the pinned net472 assembly:

```csharp
VelopackApp VelopackApp.Build();
VelopackApp VelopackApp.SetArgs(string[] args);
VelopackApp VelopackApp.SetAutoApplyOnStartup(bool enabled);
void VelopackApp.Run();
UpdateManager(UpdateOptions options, IVelopackLocator locator);
UpdateManager(string urlOrPath, UpdateOptions options, IVelopackLocator locator);
UpdateManager(IUpdateSource source, UpdateOptions options, IVelopackLocator locator);
Task<UpdateInfo> UpdateManager.CheckForUpdatesAsync();
Task UpdateManager.DownloadUpdatesAsync(UpdateInfo updates, Action<int> progress, CancellationToken cancelToken);
void UpdateManager.ApplyUpdatesAndRestart(VelopackAsset toApply, string[] restartArgs);
void UpdateManager.ApplyUpdatesAndExit(VelopackAsset toApply);
void UpdateManager.WaitExitThenApplyUpdates(VelopackAsset toApply, bool silent, bool restart, string[] restartArgs);
Task<VelopackAssetFeed> IUpdateSource.GetReleaseFeed(IVelopackLogger logger, string appId, string channel, Guid? stagingId, VelopackAsset latestLocalRelease);
Task IUpdateSource.DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken);
```

`UpdateInfo.TargetFullRelease` exposes the full `VelopackAsset`; asset properties include `PackageId`, `Version`, `Type`, `FileName`, `SHA1`, `SHA256`, `Size`, and notes. `UpdateManager` exposes `IsInstalled`, `IsPortable`, `CurrentVersion`, and `UpdatePendingRestart`. `CheckForUpdatesAsync` has no cancellation-token parameter, so the authenticated transport boundary must retain explicit cancellation/stale-result handling. The public custom `IUpdateSource` is suitable for enforcing the signed envelope and server-first policy before offering a release. Nothing here changes update hosts or enables Velopack Flow.

## TDD and verification evidence

| Command/check | Exit/result |
| --- | --- |
| First `pwsh -NoProfile -NonInteractive -File tests/velopack-dependencies.test.ps1` before production code | 1, expected RED: missing preparation implementation |
| Initial real loopback HTTP/ZIP suite after implementation | 0, 13 tests passed |
| Added regression for undeclared implicit local dotnet host | 1, expected RED: selected undeclared host; after removing implicit selection, GREEN |
| Final focused suite under PowerShell 7 and Windows PowerShell 5.1 | 0, 14 tests passed on each host |
| `Prepare-Velopack.ps1` from seeded verified packages; repeat under Windows PowerShell | 0, exact 495-file installation and usable returned paths |
| `dotnet <verified-vpk.dll> --version` | 1, upstream CLI does not support this flag; prints unrecognized argument |
| `dotnet <verified-vpk.dll> --help` | 0, banner `Velopack CLI 1.2.161`; file product version `1.2.161+92d6a1c`, file version `1.2.161.37590` |
| Existing `csc.exe /nologo /target:exe /platform:x64`, references verified SDK/Newtonsoft and framework HTTP/compression assemblies, probe source above | 0, then probe execution 0; SDK/bootstrap construction and public API reflection succeed |
| `node --test tests/dependency-integrity.test.cjs tests/license-guide.test.cjs tests/production-boundary.test.cjs tests/website.test.cjs` | 0, 59 passed, 1 existing deployed-metadata check skipped |
| `node Orange3DTool/Web/full-ui-tests.cjs` | 0, 52/52 groups passed headlessly |
| Bundled Python `-m unittest discover -s tests -p test_prepare_release.py` | 0, 6 passed |
| `git diff --check` | 0 |

The initial UI run was 51/52 because the ignored author-cover JPEGs were absent. Only the trusted `.jpg` files were copied from the original checkout's cover directory, then all 52 groups passed. The initial system-Python package test could not import `cryptography`; rerunning with the existing bundled Python succeeded without installing anything. Windows PowerShell initially exposed two compatibility issues: it required explicit loading of `System.IO.Compression`, and `-File` evaluated script parameter defaults before `$PSScriptRoot` was available. Loading the assembly explicitly and assigning default paths in the script body fixed both; the complete Windows PowerShell fixture suite and default preparation/lookup then passed. Known trusted WebView2/JAR/resource ZIP dependencies were already present. No quarantined executable was copied or rebuilt, and the legacy package build/lifecycle script was deliberately not run because it compiles those custom auxiliary executables. The Python package and production-boundary checks supply unchanged package-baseline coverage for this preparation task.

## Self-review and handoff concerns

Self-review checked full file inventories, cache authentication, preflight-before-extraction ordering, path confinement, reparse-point rejection, cleanup confinement, CLI prefix quoting, accidental global installations and generated-file exclusions. The regression above removed an unauthenticated implicit local runtime-host selection. Only the focused source/test/lock/report files are committed. No edits were made to `E:/Projects/OrangeLauncher`; no antivirus scan/restore/settings, visible application windows, deployment, signing key output, live feed changes, cloud service use or website publication occurred.

Task 1 is preparation only. Actual signed discovery/download/cancellation/apply, stable portable data storage, installer/portable packaging and isolated lifecycle validation remain required in Tasks 2–3. Preserve the user's explicit **orangezl.com first, GitHub fallback on network failure**, in-app one-click update/restart, installer and portable ZIP requirements. Dependency acquisition from upstream GitHub is separate from the launcher's future update-feed origin policy. vpk `--version` remains unsupported upstream; use the successful help banner or product metadata. Fresh uncached CLI downloads are large (~140 MiB) and may hit this environment's intermittent full-download stalls; bounded acquisition plus the exact package hash was the successful workaround here. Preparation intentionally fails closed on an invalid existing install/cache; an operator must remove only the affected dependency directory/cache file before retrying.
