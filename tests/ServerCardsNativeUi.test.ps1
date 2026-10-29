param([string]$RunTag='current',[string]$WebRoot='',[string]$BinDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$output=Join-Path $repo ('artifacts/server-cards-native-ui/'+$RunTag)
[IO.Directory]::CreateDirectory($output)|Out-Null
$bin=if($BinDirectory){$BinDirectory}else{Join-Path ([IO.Path]::GetTempPath()) ('ozl-server-cards-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'ServerCardsNativeUi.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
$inputs=@($sources)+(Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar')+(Join-Path $repo 'Orange3DTool/Assets/OrangeLaunchBridge.lua')
$inputs+=@((Join-Path $PSScriptRoot 'ServerCardsNativeUi.cjs'),(Join-Path $PSScriptRoot 'ServerCardsNativeUi.test.ps1'))
$binding=@($inputs|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()}})
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.ServerCardsNativeUi "/out:$bin/NativeUi.exe" $refs $sources 2>&1|Tee-Object (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Server cards native fixture compilation failed'}
foreach($item in $binding){if((Get-FileHash -LiteralPath $item.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw ('Source changed during compilation: '+$item.path)}}
$receipt=[ordered]@{bin=$bin;exeSha256=(Get-FileHash -LiteralPath (Join-Path $bin 'NativeUi.exe') -Algorithm SHA256).Hash.ToLowerInvariant();scope='Production DOM to strict ModernController CaptureRequest and ServerPublicQuery/RecommendedServers, real isolated loopback A2S UDP; fixed recommendation feed, real isolated personal profiles and game-favorite SQLite; no game/Steam/public network/foreground/user data';inputs=$binding}
[IO.File]::WriteAllText((Join-Path $output 'native-source.json'),($receipt|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
$previousWebRoot=$env:OZL_TEST_WEB_ROOT
try{if($WebRoot){$env:OZL_TEST_WEB_ROOT=$WebRoot};node (Join-Path $PSScriptRoot 'ServerCardsNativeUi.cjs') $bin $output;if($LASTEXITCODE -ne 0){throw 'Server cards native UI assertions failed'}}finally{$env:OZL_TEST_WEB_ROOT=$previousWebRoot}
