$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$out=Join-Path ([IO.Path]::GetTempPath()) ('ozl-profiles-bin-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($out)|Out-Null
& python (Join-Path $PSScriptRoot 'owned-capture-probe.py') prepare (Join-Path $out 'phase')
if($LASTEXITCODE -ne 0){throw 'Owned phase fixture build failed'}
$env:OZL_REVIEW_PHASE=Join-Path $out 'phase'
$env:OZL_REVIEW_PROBE=Join-Path $PSScriptRoot 'owned-capture-probe.py'
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'OwnedReviewRegressionTests.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.OwnedReviewRegressionTests $refs "/out:$out/Test.exe" $sources
if($LASTEXITCODE -ne 0){throw 'Owned entry fixture compilation failed'}
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe "/out:$out/OwnedNativeProcessFixture.exe" (Join-Path $PSScriptRoot 'OwnedNativeProcessFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Owned process fixture compilation failed'}
$env:OZL_OWNED_PROCESS_FIXTURE=Join-Path $out 'OwnedNativeProcessFixture.exe'
& "$out/Test.exe"
if($LASTEXITCODE -ne 0){throw 'Owned entry fixture assertions failed'}
