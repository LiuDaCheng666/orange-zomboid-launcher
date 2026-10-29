param([string]$RunTag='current',[switch]$NativeUi,[string]$PreviousBridge)
$ErrorActionPreference='Stop'
if($PreviousBridge){$env:OZL_PREVIOUS_BRIDGE=[IO.Path]::GetFullPath($PreviousBridge)}
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$output=Join-Path $repo 'artifacts/server-session-native'
[IO.Directory]::CreateDirectory($output)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-server-session-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$versionClasses=Join-Path $bin 'version-classes'
[IO.Directory]::CreateDirectory($versionClasses)|Out-Null
& javac -encoding UTF-8 --release 8 -Xlint:-options -d $versionClasses (Join-Path $PSScriptRoot 'java/server-version/zombie/core/Core.java') (Join-Path $PSScriptRoot 'java/server-version/zombie/core/GameVersion.java')
if($LASTEXITCODE -ne 0){throw 'B41 metadata fixture compilation failed'}
$env:OZL_SERVER_VERSION_CLASSES=$versionClasses
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'ServerSessionNative.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/VERSION,Launcher.Version.txt"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
$inputs=@($sources)+(Join-Path $repo 'VERSION')+(Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar')+(Join-Path $repo 'Orange3DTool/Assets/OrangeLaunchBridge.lua')
$binding=@($inputs|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()}})
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.ServerSessionNative "/out:$bin/Test.exe" $refs $sources 2>&1|Tee-Object (Join-Path $output "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Server native fixture compilation failed'}
foreach($item in $binding){if((Get-FileHash -LiteralPath $item.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw ('Source changed during compilation: '+$item.path)}}
$receipt=[ordered]@{bin=$bin;exeSha256=(Get-FileHash -LiteralPath (Join-Path $bin 'Test.exe') -Algorithm SHA256).Hash.ToLowerInvariant();scope='Actual native controller/preflight/stage/production process command/recovery with isolated files and actual snapshot creation; no game or foreground interaction';inputs=$binding}
[IO.File]::WriteAllText((Join-Path $output "$RunTag-source.json"),($receipt|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
& "$bin/Test.exe" (Join-Path $bin 'fixture') 2>&1|Tee-Object (Join-Path $output "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Server native assertions failed'}

if($NativeUi){
 if(-not $env:OZL_PREVIOUS_BRIDGE){throw 'Native upgrade UI test requires -PreviousBridge from an actual previous release.'}
 $assets=Join-Path $bin 'web'
 New-Item -ItemType Directory -Path $assets | Out-Null
 foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico')){Copy-Item -LiteralPath (Join-Path $repo "Orange3DTool/Web/$name") -Destination $assets}
 Copy-Item -LiteralPath (Join-Path $sdk 'Microsoft.Web.WebView2.Core.dll'),(Join-Path $sdk 'Microsoft.Web.WebView2.WinForms.dll') -Destination $bin
 Copy-Item -LiteralPath (Join-Path $repo 'Dependencies/WebView2/runtimes/win-x64/native/WebView2Loader.dll') -Destination $assets
 $env:OZL_RECOVERY_UI_ASSETS=$assets
 $env:OZL_RECOVERY_UI_EVIDENCE=Join-Path $output "$RunTag-ui"
 foreach($case in @('old','invalid')){
  $env:OZL_RECOVERY_UI_CASE=$case
  & "$bin/Test.exe" (Join-Path $bin ("native-ui-"+$case)) ui 2>&1|Tee-Object (Join-Path $output "$RunTag-ui-$case.log")
  if($LASTEXITCODE -ne 0){throw ('Native recovery UI failed: '+$case)}
 }
}
