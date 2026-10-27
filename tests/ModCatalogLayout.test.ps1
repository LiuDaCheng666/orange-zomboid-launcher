param([string]$RealGame='',[string]$RealUser='',[string]$RealData='',[string]$SaveMods='')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. (Join-Path $repo 'build/ProductionSources.ps1')
. (Join-Path $repo 'build/VelopackDependencies.ps1')
$deps=Get-OzlVelopackDependencies
$evidence=Join-Path $repo ('artifacts/mod-catalog-layout-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($evidence)
$version=& (Join-Path $repo 'Build-Version.ps1') -OutputPath (Join-Path $evidence 'Version.g.cs')
$project=Join-Path $repo 'Orange3DTool'
$sources=@(Get-OzlVelopackSources -ProjectDirectory $project)+@((Join-Path $project 'LauncherTestPaths.cs'),(Join-Path $project 'ModCatalogLayoutTests.cs'))
$web=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Web.dll','/reference:System.Xml.dll','/reference:System.Net.Http.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$web/Microsoft.Web.WebView2.Core.dll","/reference:$web/Microsoft.Web.WebView2.WinForms.dll","/reference:$($deps.SdkAssembly)","/reference:$($deps.NewtonsoftAssembly)")
$exe=Join-Path $evidence 'catalog-layout.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION,OZL_VELOPACK /target:exe /main:PZ3D.ModCatalogLayoutTests /platform:x64 "/out:$exe" "/resource:$repo/VERSION,Launcher.Version.txt" "/resource:$repo/release-public-key.xml,Launcher.UpdatePublicKey.xml" "/resource:$repo/build/velopack-distribution.txt,Launcher.Distribution.Velopack" $refs $version $sources
if($LASTEXITCODE){throw 'Catalog layout regression compilation failed'}
Copy-Item -LiteralPath $deps.SdkAssembly,$deps.NewtonsoftAssembly -Destination $evidence
& $exe | Tee-Object -FilePath (Join-Path $evidence 'fixtures.log')
if($LASTEXITCODE){throw "Catalog layout fixture regression failed: $evidence"}
if($RealGame){
 if(-not $RealUser -or -not $RealData -or -not $SaveMods){throw 'Read-only real catalog check requires explicit game, user, data and save mods.txt paths'}
 & $exe --inspect-real $RealGame $RealUser $RealData $SaveMods | Tee-Object -FilePath (Join-Path $evidence 'real-read-only.log')
 if($LASTEXITCODE){throw "Read-only real catalog regression failed: $evidence"}
}
"PASS catalog layout regression; evidence: $evidence"
