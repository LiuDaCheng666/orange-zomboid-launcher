$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$out=Join-Path $repo 'artifacts/version-preserve-20261029'
[IO.Directory]::CreateDirectory($out)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-version-ui-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'VersionPreserveNativeUi.cs')
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.VersionPreserveNativeUi "/out:$bin/NativeUi.exe" $refs $sources 2>&1 | Tee-Object (Join-Path $out 'native-ui-compile.log')
if($LASTEXITCODE -ne 0){throw 'Native UI compilation failed'}
Set-Content (Join-Path $out 'runtime-path.txt') $bin
node (Join-Path $PSScriptRoot 'VersionPreserveNativeUi.cjs')
if($LASTEXITCODE -ne 0){throw 'Native UI regression failed'}
