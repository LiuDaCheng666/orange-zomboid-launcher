param([string]$RunTag='current')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$output=Join-Path $repo 'artifacts/forced-chinese-native'
[IO.Directory]::CreateDirectory($output)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-forced-chinese-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'ForcedChineseNative.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/VERSION,Launcher.Version.txt"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.ForcedChineseNative "/out:$bin/Test.exe" $refs $sources 2>&1 | Tee-Object (Join-Path $output ($RunTag+'-compile.log'))
if($LASTEXITCODE -ne 0){throw 'Forced Chinese fixture compilation failed'}
& "$bin/Test.exe" (Join-Path $bin 'fixture') 2>&1 | Tee-Object (Join-Path $output ($RunTag+'-results.log'))
if($LASTEXITCODE -ne 0){throw 'Forced Chinese assertions failed'}[IO.File]::WriteAllText((Join-Path $output ($RunTag+'-scope.txt')),"Full production C# compilation; actual controller Get/Save and isolated preference files; transient Stage+Recover normal/save/server; no game process, native desktop interaction, screenshots or real player files.",[Text.UTF8Encoding]::new($false))
