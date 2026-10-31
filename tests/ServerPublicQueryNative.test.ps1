param([string]$RunTag='current',[ValidateSet('query','race','external','saved','recommended','encoding')][string]$Group='query')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$output=Join-Path $repo 'artifacts/server-public-query-native'
[IO.Directory]::CreateDirectory($output)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-server-query-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$main=if($Group -eq 'encoding'){'PZ3D.ServerInfoEncodingTests'}elseif($Group -eq 'saved'){'PZ3D.ServerProfilesTests'}elseif($Group -eq 'recommended'){'PZ3D.RecommendedServersTests'}else{'PZ3D.ServerPublicQueryNative'}
$tests=if($Group -eq 'encoding'){@('tests/ServerInfoEncodingTests.cs')}elseif($Group -eq 'saved'){@('Orange3DTool/ServerProfilesTests.cs','Orange3DTool/LauncherTestPaths.cs')}elseif($Group -eq 'recommended'){@('Orange3DTool/RecommendedServersTests.cs')}else{@('tests/ServerPublicQueryNative.cs')}
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+@($tests|ForEach-Object{Join-Path $repo $_})
$raceOriginal=$null
if($Group -eq 'race'){
 $raceOriginal=Join-Path $repo 'Orange3DTool/ServerPublicQuery.cs'
 $raceSource=Join-Path $bin 'ServerPublicQuery.race.cs'
 $original=[IO.File]::ReadAllText($raceOriginal)
 $boundary='if(cancel)selected.Cancel.Cancel();'
 if(-not $original.Contains($boundary)){throw 'Last-reader cancellation boundary changed; review the test-only hook'}
 $hooked=$original.Replace($boundary,'if(cancel){ServerQueryRaceSync.BeforeCancel();selected.Cancel.Cancel();}')
 [IO.File]::WriteAllText($raceSource,$hooked,[Text.UTF8Encoding]::new($false))
 $sources=@($sources|ForEach-Object{if($_ -eq $raceOriginal){$raceSource}else{$_}})
}
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
$inputs=@($sources)+(Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar')+(Join-Path $repo 'Orange3DTool/Assets/OrangeLaunchBridge.lua')
if($Group -eq 'encoding'){$encodingFixture=Join-Path $PSScriptRoot 'fixtures/a2s-truncated-name.bin';$refs+="/resource:$encodingFixture,A2sTruncatedName.bin";$inputs+=@($encodingFixture)}
if($raceOriginal){$inputs+=@($raceOriginal)}
$binding=@($inputs|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()}})
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe "/main:$main" "/out:$bin/Test.exe" $refs $sources 2>&1|Tee-Object (Join-Path $output "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Server native fixture compilation failed'}
foreach($item in $binding){if((Get-FileHash -LiteralPath $item.path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256){throw ('Source changed during compilation: '+$item.path)}}
$receipt=[ordered]@{bin=$bin;exeSha256=(Get-FileHash -LiteralPath (Join-Path $bin 'Test.exe') -Algorithm SHA256).Hash.ToLowerInvariant();scope='Actual native read-only controller and loopback A2S UDP queries; no external network/game/foreground/user data';inputs=$binding}
[IO.File]::WriteAllText((Join-Path $output "$RunTag-source.json"),($receipt|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
& "$bin/Test.exe" (Join-Path $bin 'fixture') $Group 2>&1|Tee-Object (Join-Path $output "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Server native assertions failed'}
