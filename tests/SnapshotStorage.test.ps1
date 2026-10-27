param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. (Join-Path $repo 'build/ProductionSources.ps1')
. (Join-Path $repo 'build/VelopackDependencies.ps1')
$deps=Get-OzlVelopackDependencies
$evidence=Join-Path $repo ('artifacts/snapshot-storage-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($evidence)
$version=& (Join-Path $repo 'Build-Version.ps1') -OutputPath (Join-Path $evidence 'Version.g.cs')
$project=Join-Path $repo 'Orange3DTool'
$sources=@(Get-OzlVelopackSources -ProjectDirectory $project)+@((Join-Path $project 'SnapshotStorageTests.cs'))
$web=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Web.dll','/reference:System.Xml.dll','/reference:System.Net.Http.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$web/Microsoft.Web.WebView2.Core.dll","/reference:$web/Microsoft.Web.WebView2.WinForms.dll","/reference:$($deps.SdkAssembly)","/reference:$($deps.NewtonsoftAssembly)")
$exe=Join-Path $evidence 'snapshot-storage.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION,OZL_VELOPACK /target:exe /main:PZ3D.SnapshotStorageTests /platform:x64 "/out:$exe" "/resource:$repo/VERSION,Launcher.Version.txt" "/resource:$repo/release-public-key.xml,Launcher.UpdatePublicKey.xml" $refs $version $sources
if($LASTEXITCODE){throw 'Snapshot storage regression compilation failed'}
Copy-Item -LiteralPath $deps.SdkAssembly,$deps.NewtonsoftAssembly -Destination $evidence
& $exe | Tee-Object -FilePath (Join-Path $evidence 'results.log')
if($LASTEXITCODE){throw "Snapshot storage fixture regression failed: $evidence"}
"PASS snapshot storage regression; evidence: $evidence"
