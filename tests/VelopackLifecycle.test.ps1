param([Parameter(Mandatory=$true)][string]$TemplateDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. "$repo/build/ProductionSources.ps1"
. "$repo/build/VelopackDependencies.ps1"
$deps=Get-OzlVelopackDependencies
if(-not(Test-Path "$TemplateDirectory/velopack-bootstrap.json")){throw 'Supply an existing signed build as fixture file template'}
$nonce=[Guid]::NewGuid().ToString('N')
$appId='OzlLifecycle'+$nonce.Substring(0,12)
$evidence=Join-Path $repo ('artifacts/uniquelifecycle-'+$nonce)
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-life-'+$nonce.Substring(0,12))
New-Item -ItemType Directory $evidence,$fixture | Out-Null
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider 2048
$rsa.PersistKeyInCsp=$false
[IO.File]::WriteAllText("$evidence/fixture-public.xml",$rsa.ToXmlString($false))
$feedSource=[IO.File]::ReadAllText("$repo/Orange3DTool/VelopackFeed.cs").Replace('internal const string AppId="OrangeZomboidLauncher";',('internal const string AppId="'+$appId+'";'))
[IO.File]::WriteAllText("$evidence/VelopackFeed.cs",$feedSource)
$sources=@(Get-OzlVelopackSources -ProjectDirectory "$repo/Orange3DTool" | Where-Object { [IO.Path]::GetFileName($_) -ne 'VelopackFeed.cs' })+@([IO.Path]::GetFullPath("$evidence/VelopackFeed.cs"),[IO.Path]::GetFullPath("$PSScriptRoot/VelopackLifecycleFixture.cs"))
$sdk="$repo/Dependencies/WebView2/lib/net462"
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Web.dll','/reference:System.Net.Http.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk/Microsoft.Web.WebView2.Core.dll","/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll","/reference:$($deps.SdkAssembly)","/reference:$($deps.NewtonsoftAssembly)")
function Wait-Lifecycle([scriptblock]$Condition,[int]$Seconds=40){$deadline=[DateTime]::UtcNow.AddSeconds($Seconds);while(-not (& $Condition)){if([DateTime]::UtcNow -gt $deadline){throw 'Lifecycle condition timed out'};Start-Sleep -Milliseconds 100}}
function Start-Hidden([string]$File,[string[]]$Arguments){Start-Process -FilePath $File -ArgumentList $Arguments -WindowStyle Hidden -PassThru}
function Get-IconHash([string]$Path){
 Add-Type -AssemblyName System.Drawing
 $icon=[Drawing.Icon]::ExtractAssociatedIcon([IO.Path]::GetFullPath($Path));$bitmap=$icon.ToBitmap();$stream=[IO.MemoryStream]::new();$sha=[Security.Cryptography.SHA256]::Create()
 try{$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);[BitConverter]::ToString($sha.ComputeHash($stream.ToArray())).Replace('-','')}finally{$sha.Dispose();$stream.Dispose();$bitmap.Dispose();$icon.Dispose()}
}
function Read-Playtime([string]$Path){
 $sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes([IO.Path]::GetFullPath($Path).ToUpperInvariant()))).Replace('-','')}finally{$sha.Dispose()}
 $mutex=[Threading.Mutex]::new($false,('Local\OrangeState-'+$hash));$held=$false
 try{try{$held=$mutex.WaitOne(5000)}catch [Threading.AbandonedMutexException]{$held=$true};if(-not $held){throw 'Playtime state lock timeout'};[IO.File]::ReadAllText($Path)|ConvertFrom-Json}finally{if($held){$mutex.ReleaseMutex()};$mutex.Dispose()}
}
$owned=@()
$registryKeys=@()
try {
 foreach($version in @('1.0.0','1.0.1')){
  $app=Join-Path $evidence ('app-'+$version);$release=Join-Path $evidence ('release-'+$version)
  Copy-Item -LiteralPath $TemplateDirectory -Destination $app -Recurse
  [IO.File]::WriteAllText("$app/VERSION",$version)
  [IO.File]::WriteAllText("$app/release-public-key.xml",$rsa.ToXmlString($false))
  $resources=@("/resource:$app/VERSION,Launcher.Version.txt","/resource:$evidence/fixture-public.xml,Launcher.UpdatePublicKey.xml","/resource:$repo/build/velopack-distribution.txt,Launcher.Distribution.Velopack")
  & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION,OZL_VELOPACK /target:winexe /main:PZ3D.VelopackLifecycleFixture /platform:x64 /optimize+ "/out:$app/橙子启动器.exe" "/win32manifest:$repo/Orange3DTool/app.manifest" $refs $resources $sources | Out-File "$evidence/compile-$version.log"
  if($LASTEXITCODE){throw "Fixture compilation failed: $version"}
  $files=@(Get-ChildItem -LiteralPath $app -File -Recurse | Where-Object {$_.Name -notin @('velopack-bootstrap.json','velopack-bootstrap.sig')} | Sort-Object FullName | ForEach-Object {[ordered]@{path=$_.FullName.Substring($app.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;url='';length=$_.Length}})
  if($files.Count -ne 20){throw 'Fixture bootstrap inventory must remain exactly20'}
  $raw=[Text.Encoding]::UTF8.GetBytes(([ordered]@{schema=1;version=$version;files=$files}|ConvertTo-Json -Depth 5 -Compress))
  [IO.File]::WriteAllBytes("$app/velopack-bootstrap.json",$raw);[IO.File]::WriteAllBytes("$app/velopack-bootstrap.sig",$rsa.SignData($raw,'SHA256'))
  $displayTitle=if($version -eq '1.0.0'){$appId}else{'橙子启动器'}
  $iconArguments=if($version -eq '1.0.1'){@('--icon',"$repo/Orange3DTool/Assets/orange-app.ico")}else{@()}
  $identityArguments=if($version -eq '1.0.1'){@('--aumid',($appId+'.Launcher'))}else{@()}
  & $deps.VpkExecutable @($deps.VpkArguments) pack --packId $appId --packTitle $displayTitle --packVersion $version --packDir $app --outputDir $release --channel win --runtime win-x64 --mainExe '橙子启动器.exe' @iconArguments @identityArguments --framework net48 --delta None --shortcuts None --skip-updates --legacyConsole --yes *> "$evidence/pack-$version.log"
  if($LASTEXITCODE){throw "Actual vpk pack failed: $version"}
 }
 $portable=Join-Path $fixture 'portable';$installed=Join-Path $fixture 'installed';$game=Join-Path $fixture 'game'
 Expand-Archive -LiteralPath "$evidence/release-1.0.0/$appId-win-Portable.zip" -DestinationPath $portable
 $brandIconHash=Get-IconHash "$repo/Orange3DTool/Assets/orange-app.ico"
 if((Get-IconHash "$portable/Update.exe") -eq $brandIconHash){throw 'Old updater icon fixture must reproduce the default-icon omission'}
 $setup=Start-Hidden "$evidence/release-1.0.0/$appId-win-Setup.exe" @('--silent','--installto',('"'+$installed+'"'))
 if(-not $setup.WaitForExit(45000) -or $setup.ExitCode -ne 0){throw 'Actual silent Setup failed'}
 if(-not(Test-Path "$installed/current/橙子启动器.exe")){throw 'Setup did not install current'}
 $regRoot='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
 $registryKeys=@(Get-ChildItem -LiteralPath $regRoot | Where-Object {$_.PSChildName -eq $appId})
 if($registryKeys.Count -ne 1){throw 'Unique fixture uninstall registry entry missing'}
 $reg=Get-ItemProperty -LiteralPath $registryKeys[0].PSPath
 if([IO.Path]::GetFullPath($reg.InstallLocation).TrimEnd('\') -ne [IO.Path]::GetFullPath($installed).TrimEnd('\')){throw 'Installer registry location differs from isolated fixture'}
 'PASS actual upstream silent installer, unique appId, dedicated install directory, shortcuts None' | Tee-Object -FilePath "$evidence/results.log" -Append
 $state=Join-Path $portable 'OrangeLauncherRuntime'
 New-Item -ItemType Directory "$state/browser",$game | Out-Null
 [IO.File]::WriteAllText("$state/preferences.json",'{"fixture":"personal retained"}')
 [IO.File]::WriteAllText("$state/browser/persist.txt",'browser retained')
 Copy-Item -LiteralPath "$portable/current/橙子启动器.exe" -Destination "$game/ProjectZomboid64.exe"
 [IO.File]::WriteAllText("$game/ProjectZomboid64.json",'{}')
 $gameProcess=Start-Hidden "$game/ProjectZomboid64.exe" @('--fixture-hold',('"'+"$fixture/game-ready"+'"'),('"'+"$fixture/game-stop"+'"'));$owned+=@{Process=$gameProcess;Image="$game/ProjectZomboid64.exe"}
 Wait-Lifecycle {Test-Path "$fixture/game-ready"}
 $feed=Join-Path $evidence 'release-1.0.1';$package=Join-Path $feed "$appId-1.0.1-full.nupkg"
 $raw=[Text.Encoding]::UTF8.GetBytes(([ordered]@{schema=1;appId=$appId;channel='win';version='1.0.1';fileName=[IO.Path]::GetFileName($package);length=(Get-Item -LiteralPath $package).Length;sha256=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash;notes='isolated native lifecycle'}|ConvertTo-Json -Compress))
 [IO.File]::WriteAllBytes("$feed/velopack-manifest.json",$raw);[IO.File]::WriteAllBytes("$feed/velopack-manifest.sig",$rsa.SignData($raw,'SHA256'))
 $nested=Start-Hidden "$portable/current/橙子启动器.exe" @('--fixture-hold',('"'+"$fixture/nested-ready"+'"'),('"'+"$fixture/nested-stop"+'"'));$owned+=@{Process=$nested;Image="$portable/current/橙子启动器.exe"}
 Wait-Lifecycle {Test-Path "$fixture/nested-ready"}
 $negative=Start-Hidden "$portable/current/橙子启动器.exe" @('--fixture-update',('"'+$feed+'"'),('"'+$game+'"'),'reject')
 if(-not $negative.WaitForExit(30000) -or $negative.ExitCode -ne 0 -or -not(Test-Path "$state/rejected.txt") -or $nested.HasExited){throw 'Nested unrelated-process safety fixture failed'}
 [IO.File]::WriteAllText("$fixture/nested-stop",'stop');if(-not $nested.WaitForExit(5000)){throw 'Owned nested fixture did not exit'}
 'PASS actual backend refuses nested live process before handoff; unrelated fixture survives' | Tee-Object -FilePath "$evidence/results.log" -Append
 $parent=Start-Hidden "$portable/current/橙子启动器.exe" @('--fixture-update',('"'+$feed+'"'),('"'+$game+'"'))
 if(-not $parent.WaitForExit(30000) -or $parent.ExitCode -ne 0){throw 'Real backend parent failed; inspect runtime fixture-error.txt'}
 Wait-Lifecycle {Test-Path "$state/restarted.txt"} 45
 if([IO.File]::ReadAllText("$state/restarted.txt") -ne '1.0.1'){throw 'Actual restart did not reach target version'}
 if([IO.File]::ReadAllText("$state/restarted-aumid.txt") -cne ($appId+'.Launcher')){throw 'Actual restarted application did not adopt the new shortcut taskbar identity'}
 if(-not(Test-Path "$portable/橙子启动器.exe")){throw 'Chinese root execution stub missing after title-changing native update'}
 if((Get-IconHash "$portable/Update.exe") -cne $brandIconHash){throw 'Native apply did not replace the old default updater icon with the branded updater'}
 'PASS actual native upgrade adopts the new taskbar identity and replaces default-icon root Update.exe with the branded updater' | Tee-Object -FilePath "$evidence/results.log" -Append
 $monitorId=[int][IO.File]::ReadAllText("$state/monitor-pid.txt");$monitor=Get-Process -Id $monitorId -ErrorAction Stop;$monitorImage=[IO.File]::ReadAllText("$state/monitor-image.txt");$owned+=@{Process=$monitor;Image=$monitorImage}
 if($monitorImage.StartsWith($portable,[StringComparison]::OrdinalIgnoreCase) -or $gameProcess.HasExited){throw 'Monitor/game did not survive native root force-stop'}
 $before=Read-Playtime "$state/player/playtime/time.json";Start-Sleep -Milliseconds 2200;$after=Read-Playtime "$state/player/playtime/time.json"
 if(-not $after.running -or $after.totalSeconds -le $before.totalSeconds){throw 'Actual playtime monitor stopped accounting after native apply'}
 [IO.File]::WriteAllText("$fixture/game-stop",'stop');if(-not $gameProcess.WaitForExit(5000) -or -not $monitor.WaitForExit(5000)){throw 'Game/monitor terminal sample did not finish'}
 $terminal=Read-Playtime "$state/player/playtime/time.json"
 if($terminal.running -or $terminal.totalSeconds -lt $after.totalSeconds){throw 'Terminal playtime accounting lost'}
 if([IO.File]::ReadAllText("$state/preferences.json") -ne '{"fixture":"personal retained"}' -or [IO.File]::ReadAllText("$state/browser/persist.txt") -ne 'browser retained'){throw 'Personal/browser data changed'}
 'PASS actual SDK discovery/download + official native1.0.0→1.0.1 apply/restart; personal/browser data retained; real playtime worker survived and finalized accounting' | Tee-Object -FilePath "$evidence/results.log" -Append
 Copy-Item -LiteralPath $state -Destination "$evidence/runtime-evidence" -Recurse
 [ordered]@{appId=$appId;fixture=$fixture;framework='1.2.161';runtime='net48';oldVersion='1.0.0';newVersion='1.0.1';updaterBrandVerified=$true;playtimeSeconds=$terminal.totalSeconds;monitorImage=$monitorImage;installerExit=$setup.ExitCode;parentExit=$parent.ExitCode;status='PASS'}|ConvertTo-Json | Set-Content "$evidence/summary.json" -Encoding UTF8
 "EVIDENCE $evidence"
} finally {
 $rsa.Dispose()
 if($state -and (Test-Path "$state/monitor-pid.txt") -and (Test-Path "$state/monitor-image.txt")){
  $knownMonitor=Get-Process -Id ([int][IO.File]::ReadAllText("$state/monitor-pid.txt")) -ErrorAction SilentlyContinue
  if($knownMonitor){$owned+=@{Process=$knownMonitor;Image=[IO.File]::ReadAllText("$state/monitor-image.txt")}}
 }
 foreach($item in $owned){$p=$item.Process;try{if(-not $p.HasExited -and [IO.Path]::GetFullPath($p.MainModule.FileName) -eq [IO.Path]::GetFullPath($item.Image)){$p.Kill();$p.WaitForExit(5000)|Out-Null}}catch{}}
 # Exact unique registration only; never invoke a production uninstaller.
 foreach($key in $registryKeys){$record=Get-ItemProperty -LiteralPath $key.PSPath;if($key.PSChildName -eq $appId -and [IO.Path]::GetFullPath($record.InstallLocation).StartsWith([IO.Path]::GetFullPath($fixture)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $key.PSPath -Recurse -Force}}
 # Retain owned filesystem fixtures for evidence; registry and live processes are cleaned.
}
