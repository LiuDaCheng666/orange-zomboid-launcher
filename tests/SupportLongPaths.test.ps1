param([string]$RunTag='current')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$evidence=Join-Path $repo 'artifacts/support-long-paths-1240'
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-support-path-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'SupportLongPaths.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.SupportLongPaths "/out:$bin/Test.exe" $refs $sources 2>&1|Tee-Object (Join-Path $evidence "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Support long-path fixture compilation failed'}
& "$bin/Test.exe" (Join-Path $bin '中文测试') 2>&1|Tee-Object (Join-Path $evidence "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Support long-path regression failed'}
