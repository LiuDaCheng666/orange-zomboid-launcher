param([string]$EvidenceDirectory='',[string]$RunTag='current')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/mod-detail-native-1239'}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-mod-detail-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'ModDetailNative.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$exe=Join-Path $bin 'ModDetailNative.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.ModDetailNative "/out:$exe" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Native mod-detail fixture compilation failed'}
& $exe (Join-Path $bin 'isolated-fixture') 2>&1|Tee-Object (Join-Path $EvidenceDirectory "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Native mod-detail regression failed'}
