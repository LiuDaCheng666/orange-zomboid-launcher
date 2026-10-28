$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$out=Join-Path $repo 'artifacts/owned-launch/task6'
[IO.Directory]::CreateDirectory($out)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('o6-bin-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'UnifiedConfigurationNative.cs')
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.UnifiedConfigurationNative "/out:$bin/NativeUi.exe" $refs $sources 2>&1 | Tee-Object -FilePath "$out/compile.log"
if($LASTEXITCODE -ne 0){throw 'native compilation failed'}
node (Join-Path $PSScriptRoot 'UnifiedConfigurationNative.cjs') $bin $out
if($LASTEXITCODE -ne 0){throw 'unified configuration acceptance failed'}
