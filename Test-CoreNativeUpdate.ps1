param([string]$FixtureRoot='',[switch]$RecoveryOnly,[switch]$IncludeRootMonitorComparison)
$ErrorActionPreference='Stop'
if(-not $FixtureRoot){$FixtureRoot=Join-Path $PSScriptRoot ('artifacts\TestFixtures\native-update-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
$FixtureRoot=[IO.Path]::GetFullPath($FixtureRoot)
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\TestFixtures')).TrimEnd('\')+'\'
if(-not $FixtureRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'FixtureRoot must be a new child of repository artifacts/TestFixtures'}
for($ancestor=$FixtureRoot;$ancestor;$ancestor=Split-Path -Parent $ancestor){if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Fixture path has a reparse ancestor: $ancestor"}}
if(Test-Path -LiteralPath $FixtureRoot){throw 'FixtureRoot must not exist; retained evidence is never overwritten'}
New-Item -ItemType Directory -Path $FixtureRoot | Out-Null
$owned=New-Object 'System.Collections.Generic.List[object]'
function Register-OwnedProcess($process,[string]$image=''){
 $null=$process.Handle
 if(-not $image){$image=$process.MainModule.FileName}
 $owned.Add([pscustomobject]@{Pid=$process.Id;Birth=$process.StartTime.ToUniversalTime().Ticks;Image=[IO.Path]::GetFullPath($image)});return $process
}
function Start-OwnedProcess([string]$image,[string[]]$arguments=@()){
 $options=@{FilePath=$image;WorkingDirectory=(Split-Path -Parent $image);PassThru=$true;WindowStyle='Hidden'}
 if($arguments.Count){$options.ArgumentList=$arguments}
 # MainModule can be temporarily null immediately after CreateProcess. Retain
 # the exact requested image; live-process assertions independently read it later.
 Register-OwnedProcess (Start-Process @options) $image
}
function Assert-Alive($identity){
 $p=Get-Process -Id $identity.Pid -ErrorAction Stop
 if($p.HasExited -or $p.StartTime.ToUniversalTime().Ticks -ne $identity.Birth -or $p.MainModule.FileName -ne $identity.Image){throw "Owned process identity changed: $($identity.Pid); expected $($identity.Birth) $($identity.Image); actual $($p.StartTime.ToUniversalTime().Ticks) $($p.MainModule.FileName)"}
 $null=$p.Handle # retain a process handle so ExitCode remains available after exit
 return $p
}
function Stop-OwnedProcess($identity){
 $p=Get-Process -Id $identity.Pid -ErrorAction SilentlyContinue
 if($p -and -not $p.HasExited -and $p.StartTime.ToUniversalTime().Ticks -eq $identity.Birth -and $p.MainModule.FileName -eq $identity.Image){$p.Kill();if(-not $p.WaitForExit(5000)){throw "Owned PID did not stop: $($identity.Pid)"}}
}
function Invoke-Fixture([string]$root,[string]$argument){$p=Start-OwnedProcess (Join-Path $root '橙子启动器.exe') @($argument);if(-not $p.WaitForExit(15000) -or $p.ExitCode -ne 0){throw "Fixture command failed: $argument; $root"}}
function Read-SharedJson([string]$path){
 $stream=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
 try{$reader=[IO.StreamReader]::new($stream);try{return ($reader.ReadToEnd() | ConvertFrom-Json)}finally{$reader.Dispose()}}finally{$stream.Dispose()}
}
function Start-Tracking([string]$root){
 $synthetic=Join-Path $root 'OrangeLauncherRuntime\Synthetic';$game=Join-Path $synthetic 'game';$data=Join-Path $synthetic 'data'
 New-Item -ItemType Directory -Path $game -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $root '橙子启动器.exe') -Destination (Join-Path $game 'ProjectZomboid64.exe')
 [IO.File]::WriteAllText((Join-Path $game 'ProjectZomboid64.json'),'{"vmArgs":[]}')
 $gameProcess=Start-OwnedProcess (Join-Path $game 'ProjectZomboid64.exe') @('--fixture-game');$gameIdentity=$owned[$owned.Count-1]
 Invoke-Fixture $root '--fixture-start-tracker'
 $receipt=Join-Path $data 'monitor-process.json';$deadline=[DateTime]::UtcNow.AddSeconds(15)
 while(-not (Test-Path -LiteralPath $receipt) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 $monitor=Read-SharedJson $receipt;$owned.Add($monitor)
 $store=Join-Path $data 'playtime\time.json';$deadline=[DateTime]::UtcNow.AddSeconds(15)
 do{Start-Sleep -Milliseconds 150;$sample=Read-SharedJson $store}while(($sample.totalSeconds -le 1235.5 -or -not $sample.running) -and [DateTime]::UtcNow -lt $deadline)
 if($sample.totalSeconds -le 1235.5 -or -not $sample.running -or $sample.reminderMinutes -ne 45){throw "Real tracker failed to sample owned game: $root"}
 $null=Assert-Alive $monitor;$null=Assert-Alive $gameIdentity
 return [pscustomobject]@{Game=$gameIdentity;Monitor=$monitor;Store=$store;Before=$sample.totalSeconds;WorkerHash=(Get-FileHash -LiteralPath $monitor.image).Hash}
}
try{
& (Join-Path $PSScriptRoot 'Build-Tests.ps1') -Entry PZ3D.CoreReleaseTool -Output (Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe')
if($LASTEXITCODE -ne 0){throw 'Core release tool build failed'}
$tool=Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe'
$private=Join-Path $FixtureRoot 'fixture-private.xml';$public=Join-Path $FixtureRoot 'fixture-public.xml'
& $tool keygen $private $public
if($LASTEXITCODE -ne 0){throw 'Ephemeral fixture key generation failed'}
$source=Join-Path $FixtureRoot 'source';New-Item -ItemType Directory -Path $source,(Join-Path $source 'Web'),(Join-Path $source 'Assets') | Out-Null
$project=Join-Path $PSScriptRoot 'Orange3DTool';$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2';$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll","/reference:$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll")
$versionAttributes=& (Join-Path $PSScriptRoot 'Build-Version.ps1')
if($LASTEXITCODE -ne 0){throw 'Fixture version generation failed'}
$currentVersion=(Get-Content -Raw (Join-Path $PSScriptRoot 'VERSION')).Trim()
$parts=$currentVersion.Split('.')
$nextVersion="$($parts[0]).$($parts[1]).$([int]$parts[2]+1)"
if([int]$parts[2] -ge 65534){throw 'Next fixture patch version exceeds assembly limits'}
foreach($entry in @(@('PZ3D.CoreUpdateNativeFixtureTests','橙子启动器.exe'),@('PZ3D.CoreUpdaterProgram','橙子更新助手.exe'),@('PZ3D.CoreUpdateNativeFixtureTests','卸载橙子启动器.exe'))){
 & $compiler /nologo /target:winexe /platform:x64 "/main:$($entry[0])" "/out:$source\$($entry[1])" "/resource:$public,Launcher.UpdatePublicKey.xml" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" $refs $versionAttributes (Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
 if($LASTEXITCODE -ne 0){throw 'Native fixture compile failed'}
}
foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $project "Web\$name") -Destination (Join-Path $source "Web\$name")}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){Copy-Item -LiteralPath (Join-Path $sdk "lib\net462\$name") -Destination (Join-Path $source "Web\$name")}
Copy-Item -LiteralPath (Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination (Join-Path $source 'Web\WebView2Loader.dll')
foreach($name in @('OrangeShootPresentation.jar','OrangeLaunchBridge.lua')){Copy-Item -LiteralPath (Join-Path $project "Assets\$name") -Destination (Join-Path $source "Assets\$name")}
foreach($name in @('VERSION','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source $name)}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '使用说明.md') -Destination (Join-Path $source '使用说明.txt');Copy-Item -LiteralPath $public -Destination (Join-Path $source 'release-public-key.xml')
$release=Join-Path $FixtureRoot 'release'
& $tool pack $source $release $private $public
if($LASTEXITCODE -ne 0){throw 'Fixture base pack failed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
if($IncludeRootMonitorComparison){
 # Rebuild only the old production tracker in an isolated signed source copy.
 # Current production files/HEAD are never rewritten; all other fixture paths match GREEN.
 $redSource=Join-Path $FixtureRoot 'root-bound-source';New-Item -ItemType Directory -Path $redSource | Out-Null
 Get-ChildItem -LiteralPath $source | Copy-Item -Destination $redSource -Recurse
 $revision=& git -C $PSScriptRoot rev-parse '0305a0ae47d52002203ba20769fd5340a14f9782^'
 if($LASTEXITCODE -ne 0){throw 'Cannot resolve pre-isolation tracker revision'}
 $oldTracker=& git -C $PSScriptRoot show "${revision}:Orange3DTool/PlaytimeTracker.cs"
 if($LASTEXITCODE -ne 0){throw 'Cannot read pre-isolation tracker source'}
 $oldFile=Join-Path $FixtureRoot 'RootBound.PlaytimeTracker.cs';[IO.File]::WriteAllText($oldFile,($oldTracker -join "`r`n"),[Text.UTF8Encoding]::new($false))
 $currentFiles=(Get-ChildItem -LiteralPath $project -Filter '*.cs' | Where-Object Name -ne 'PlaytimeTracker.cs').FullName
 & $compiler /nologo /target:winexe /platform:x64 /main:PZ3D.CoreUpdateNativeFixtureTests "/out:$redSource\橙子启动器.exe" "/resource:$public,Launcher.UpdatePublicKey.xml" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" $refs $versionAttributes $currentFiles $oldFile
 if($LASTEXITCODE -ne 0){throw 'Root-bound comparison fixture build failed'}
 $redRelease=Join-Path $FixtureRoot 'root-bound-release'; & $tool pack $redSource $redRelease $private $public
 if($LASTEXITCODE -ne 0){throw 'Root-bound comparison fixture signing failed'}
 $redRoot=Join-Path $FixtureRoot 'root-bound-comparison';[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $redRelease "orange-launcher-v$currentVersion-win-x64.zip"),$redRoot)
 $redTracking=Start-Tracking $redRoot
 if($redTracking.Monitor.image -ne (Join-Path $redRoot '橙子启动器.exe')){throw 'Old tracker did not execute the root image'}
 $replacement=Join-Path $redRoot 'replacement.exe';Copy-Item -LiteralPath (Join-Path $source '橙子启动器.exe') -Destination $replacement
 $failure=$null;try{[IO.File]::Replace($replacement,(Join-Path $redRoot '橙子启动器.exe'),(Join-Path $redRoot 'red-backup.exe'))}catch [IO.IOException]{$failure=$_.Exception.InnerException;if(-not $failure){$failure=$_.Exception}}
 # Windows atomic replacement can succeed for a running image. Characterize
 # the observed result honestly; do not invent an exclusive handle to force RED.
 $null=Assert-Alive $redTracking.Monitor;$null=Assert-Alive $redTracking.Game
 $deadline=[DateTime]::UtcNow.AddSeconds(10);do{Start-Sleep -Milliseconds 150;$comparisonSample=Read-SharedJson $redTracking.Store}while($comparisonSample.totalSeconds -le $redTracking.Before -and [DateTime]::UtcNow -lt $deadline)
 if($comparisonSample.totalSeconds -le $redTracking.Before){throw 'Old real tracker stopped sampling during replacement comparison'}
 [pscustomobject]@{SourceRevision=$revision;OldTrackerHash=(Get-FileHash -LiteralPath $oldFile).Hash;Monitor=$redTracking.Monitor;Game=$redTracking.Game;ReplacementSucceeded=($null -eq $failure);Error=$(if($failure){$failure.Message}else{''});HResult=$(if($failure){$failure.HResult}else{0});BeforeRootHash=(Get-FileHash -LiteralPath (Join-Path $redSource '橙子启动器.exe')).Hash;AfterRootHash=(Get-FileHash -LiteralPath (Join-Path $redRoot '橙子启动器.exe')).Hash;CandidateHash=(Get-FileHash -LiteralPath (Join-Path $source '橙子启动器.exe')).Hash;TimeBefore=$redTracking.Before;TimeAfter=$comparisonSample.totalSeconds} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $FixtureRoot 'root-monitor-comparison.json') -Encoding UTF8
 $monitor=Assert-Alive $redTracking.Monitor;Stop-OwnedProcess $redTracking.Game;if(-not $monitor.WaitForExit(5000) -or $monitor.ExitCode -ne 0){throw "Old real tracker did not finish after own game stopped; exited=$($monitor.HasExited); code=$($monitor.ExitCode)"}
 Write-Output "PASS: old root monitor characterization; atomic replacement succeeded=$($null -eq $failure); real tracker PID $($redTracking.Monitor.pid) remained alive and time advanced"
}
foreach($layout in $(if($RecoveryOnly){@('full')}else{@('sparse','full')})){foreach($case in $(if($RecoveryOnly){@('interrupted')}elseif($layout -eq 'full'){@('healthy','failed','interrupted')}else{@('healthy','failed')})){
 $root=Join-Path $FixtureRoot "$layout-$case";[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $release "orange-launcher-v$currentVersion-win-x64.zip"),$root)
 $feed=Join-Path $root 'fixture-feed';New-Item -ItemType Directory -Path $feed | Out-Null
 $manifest=Get-Content -Raw (Join-Path $release 'update-manifest.json') | ConvertFrom-Json
 $manifest.version=$nextVersion;$manifest.notes='Native WebView fixture; not a published release'
 $text=[IO.File]::ReadAllText((Join-Path $root 'Web\app.js'))
 if($case -ne 'failed'){$text+="`n// Native sparse update fixture`n"}else{$text="throw new Error('Deliberately failed native page fixture');"}
 $bytes=[Text.UTF8Encoding]::new($false).GetBytes($text);[IO.File]::WriteAllBytes((Join-Path $feed 'app.js'),$bytes)
 $sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()}
 foreach($file in $manifest.files){$file.url=$file.url.Replace("/v$currentVersion/","/v$nextVersion/");if($file.path -eq 'Web/app.js'){$file.sha256=$hash;$file.length=$bytes.Length;$file.url="https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v$nextVersion/app.js"}}
 $raw=[Text.UTF8Encoding]::new($false).GetBytes(($manifest | ConvertTo-Json -Depth 8 -Compress));[IO.File]::WriteAllBytes((Join-Path $feed 'update-manifest.json'),$raw)
 $rsa=[Security.Cryptography.RSACryptoServiceProvider]::new();try{$rsa.PersistKeyInCsp=$false;$rsa.FromXmlString([IO.File]::ReadAllText($private));[IO.File]::WriteAllBytes((Join-Path $feed 'update-manifest.sig'),$rsa.SignData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))}finally{$rsa.Dispose()}
 if($layout -eq 'full'){
  $nextSource=Join-Path $FixtureRoot "$layout-$case-source";New-Item -ItemType Directory -Path $nextSource | Out-Null
  Get-ChildItem -LiteralPath $source | Copy-Item -Destination $nextSource -Recurse
  [IO.File]::WriteAllText((Join-Path $nextSource 'VERSION'),$nextVersion,[Text.UTF8Encoding]::new($false))
  $nextAttributes=Join-Path $FixtureRoot 'Fixture.Version.g.cs'
  [IO.File]::WriteAllText($nextAttributes,"using System.Reflection; [assembly: AssemblyVersion(`"$nextVersion.0`")] [assembly: AssemblyFileVersion(`"$nextVersion.0`")] [assembly: AssemblyInformationalVersion(`"$nextVersion`")]",[Text.UTF8Encoding]::new($false))
  foreach($entry in @(@('PZ3D.CoreUpdateNativeFixtureTests','橙子启动器.exe'),@('PZ3D.CoreUpdaterProgram','橙子更新助手.exe'),@('PZ3D.CoreUpdateNativeFixtureTests','卸载橙子启动器.exe'))){
   & $compiler /nologo /target:winexe /platform:x64 "/main:$($entry[0])" "/out:$nextSource\$($entry[1])" "/resource:$public,Launcher.UpdatePublicKey.xml" "/resource:$nextSource\VERSION,Launcher.Version.txt" $refs $nextAttributes (Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
   if($LASTEXITCODE -ne 0){throw 'Version-bumped native fixture compile failed'}
  }
  [IO.File]::WriteAllBytes((Join-Path $nextSource 'Web\app.js'),$bytes)
  $nextRelease=Join-Path $FixtureRoot "$layout-$case-release";& $tool pack $nextSource $nextRelease $private $public
  if($LASTEXITCODE -ne 0){throw 'Version-bumped fixture validation/pack failed'}
  Get-ChildItem -LiteralPath $nextRelease -File | Copy-Item -Destination $feed
 }
 $tracking=$null;if($layout -eq 'full'){
  $tracking=Start-Tracking $root
  $launcherHash=(Get-FileHash -LiteralPath (Join-Path $source '橙子启动器.exe')).Hash
  $expectedWorker=Join-Path $root ("OrangeLauncherRuntime\BackgroundWorkers\"+$launcherHash.ToLowerInvariant()+'\橙子启动器.exe')
  if($tracking.Monitor.image -ne $expectedWorker -or $tracking.WorkerHash -ne $launcherHash){throw 'Real tracker did not run from the authenticated derived image'}
 }
 if($case -eq 'interrupted'){
  $crasher=Join-Path $FixtureRoot 'interrupt-fixture.exe';Copy-Item -LiteralPath (Join-Path $root '卸载橙子启动器.exe') -Destination $crasher
  # The interruption driver is external; it must not hold any replaced image.
  Copy-Item -LiteralPath $crasher -Destination (Join-Path $root 'interrupt-fixture.exe')
  $crash=Start-OwnedProcess (Join-Path $root 'interrupt-fixture.exe') @('--fixture-interrupt')
  if(-not $crash.WaitForExit(15000) -or $crash.ExitCode -ne 77){throw "Interruption fixture failed: $root"}
  $process=Start-OwnedProcess (Join-Path $root '橙子启动器.exe')
 }else{$process=Start-OwnedProcess (Join-Path $root '橙子启动器.exe') @('--native-flow')}
 if(-not $process.WaitForExit(60000)){throw "Parent did not hand off: $root"}
 if($process.ExitCode -ne 0){throw "Native parent failed: $root"}
 $task=Get-Content -Raw (Join-Path $root 'native-task.txt');$journal=Join-Path $root "OrangeLauncherRuntime\CoreUpdates\$task\journal.json"
 $deadline=[DateTime]::UtcNow.AddSeconds(65);$state=''
 do {Start-Sleep -Milliseconds 150;try{$stream=[IO.FileStream]::new($journal,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete));try{$reader=[IO.StreamReader]::new($stream);try{$state=($reader.ReadToEnd() | ConvertFrom-Json).state}finally{$reader.Dispose()}}finally{$stream.Dispose()}}catch [IO.IOException]{}}while($state -notin @('Succeeded','RolledBack','Conflicted') -and [DateTime]::UtcNow -lt $deadline)
 $expected=if($case -eq 'healthy'){'Succeeded'}else{'RolledBack'}
 if($state -ne $expected){throw "Native $case expected $expected, got $state; $root"}
 $receipt=if($case -eq 'healthy'){'native-health-succeeded'}else{'native-rollback-restarted'}
 $deadline=[DateTime]::UtcNow.AddSeconds(10);while(-not (Test-Path -LiteralPath (Join-Path $root $receipt)) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(-not (Test-Path -LiteralPath (Join-Path $root $receipt))){throw "Real native page receipt missing: $root"}
 if($case -eq 'healthy'){Start-Sleep -Milliseconds 750;$restart=Start-OwnedProcess (Join-Path $root '橙子启动器.exe');if(-not $restart.WaitForExit(15000) -or $restart.ExitCode -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $root 'native-success-restarted'))){throw "Healthy ordinary restart UI lost outcome: $root"}}
 if(-not (Test-Path -LiteralPath (Join-Path $root 'native-outcome-ui.txt'))){throw 'Native outcome and history were not rendered'}
 if($case -ne 'interrupted' -and -not (Test-Path -LiteralPath (Join-Path $root 'native-ready-confirmation.txt'))){throw 'Download click did not reach the real Ready confirmation'}
 if($layout -eq 'sparse'){
  if((Get-Content (Join-Path $root 'downloaded-files.txt')).Count -ne 1 -or (Get-Content -Raw (Join-Path $root 'downloaded-files.txt')).Trim() -ne 'app.js'){throw 'Sparse fixture downloaded unexpected assets'}
 }else{
  $expectedVersion=if($case -eq 'healthy'){$nextVersion}else{$currentVersion}
  if((Get-Content -Raw (Join-Path $root 'native-running-version')).Trim() -ne $expectedVersion){throw 'Actual running native AppVersion mismatch'}
  if((Get-Content -Raw (Join-Path $root 'VERSION')).Trim() -ne $expectedVersion -or (Get-Content -Raw (Join-Path $root 'orange-installation.json') | ConvertFrom-Json).version -ne $expectedVersion){throw 'VERSION/install record mismatch'}
  $hashEvidence=@();foreach($name in @('橙子启动器.exe','橙子更新助手.exe','卸载橙子启动器.exe')){
   $info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root $name));if($info.FileVersion -ne "$expectedVersion.0" -or $info.ProductVersion -ne $expectedVersion){throw "EXE version mismatch: $name"}
   $expectedSource=if($case -eq 'healthy'){$nextSource}else{$source}
   $expectedHash=(Get-FileHash -LiteralPath (Join-Path $expectedSource $name)).Hash
   if((Get-FileHash -LiteralPath (Join-Path $root $name)).Hash -ne $expectedHash){throw "Actual executable replacement/restore mismatch: $name"}
   $oldHash=(Get-FileHash -LiteralPath (Join-Path $source $name)).Hash;$candidateHash=(Get-FileHash -LiteralPath (Join-Path $nextSource $name)).Hash
   if($oldHash -eq $candidateHash){throw "Full fixture did not change executable bytes: $name"}
   $hashEvidence+=[pscustomobject]@{Name=$name;Before=$oldHash;Candidate=$candidateHash;Actual=$expectedHash}
  }
  if((Get-Content (Join-Path $root 'downloaded-files.txt')).Count -ne 5){throw 'Full fixture must fetch three executables, VERSION, and app.js'}
  $null=Assert-Alive $tracking.Monitor;$null=Assert-Alive $tracking.Game
  $deadline=[DateTime]::UtcNow.AddSeconds(10);do{Start-Sleep -Milliseconds 150;$after=Read-SharedJson $tracking.Store}while($after.totalSeconds -le $tracking.Before -and [DateTime]::UtcNow -lt $deadline)
  if($after.totalSeconds -le $tracking.Before -or $after.reminderMinutes -ne 45 -or -not $after.running -or (Get-FileHash -LiteralPath $tracking.Monitor.image).Hash -ne $tracking.WorkerHash){throw 'Playtime failed to advance/persist across launcher replacement/reopen'}
  Invoke-Fixture $root '--fixture-session-protection'
  if(-not (Test-Path -LiteralPath (Join-Path $root 'native-session-blocked'))){throw 'Controlled active session preflight proof missing'}
  $monitorProcess=Assert-Alive $tracking.Monitor;Stop-OwnedProcess $tracking.Game
  if(-not $monitorProcess.WaitForExit(5000) -or $monitorProcess.ExitCode -ne 0){throw 'Real tracker failed to exit naturally after owned game stopped'}
  $stopped=Read-SharedJson $tracking.Store;if($stopped.running -or $stopped.totalSeconds -lt $after.totalSeconds -or $stopped.reminderMinutes -ne 45){throw 'Real tracker final sample lost persisted totals/settings'}
  Invoke-Fixture $root '--fixture-session-recovery'
  if(-not (Test-Path -LiteralPath (Join-Path $root 'native-session-recovered'))){throw 'Controlled session recovery proof missing'}
  [pscustomobject]@{Case="$layout/$case";State=$state;Game=$tracking.Game;Monitor=$tracking.Monitor;WorkerHash=$tracking.WorkerHash;Before=$tracking.Before;AfterReopen=$after.totalSeconds;AfterExit=$stopped.totalSeconds;Executables=$hashEvidence} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $root 'native-lifecycle-evidence.json') -Encoding UTF8
 }
 if($case -ne 'healthy' -and (Get-Content (Join-Path $root 'native-rollback-restarted')).Count -ne 1){throw 'Old launcher restarted more than once'}
 Start-Sleep -Milliseconds 750
 $uninstall=Start-OwnedProcess (Join-Path $root '卸载橙子启动器.exe') @('--fixture-uninstall')
 if(-not $uninstall.WaitForExit(15000) -or $uninstall.ExitCode -ne 0){throw "Native fixture uninstall failed: $root"}
 $deadline=[DateTime]::UtcNow.AddSeconds(10);while((Test-Path -LiteralPath (Join-Path $root 'orange-installation.json')) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if((Test-Path -LiteralPath (Join-Path $root '橙子启动器.exe')) -or (Test-Path -LiteralPath (Join-Path $root 'orange-installation.json'))){throw 'Owned uninstall left release files'}
 if(-not (Test-Path -LiteralPath (Join-Path $root 'downloaded-files.txt'))){throw 'Uninstall removed unowned evidence'}
 Write-Output "PASS: real native WebView $layout/$case -> $state; download/versions/EXE bytes verified; scoped uninstall retained evidence: $root"
}}
Write-Output "NATIVE UPDATE FIXTURES: $FixtureRoot"
}finally{
 # Child helper/reopened launchers are discovered only inside this newly-created
 # fixture tree, then retained and stopped by exact PID + birth + image identity.
 foreach($p in Get-Process){try{$image=$p.MainModule.FileName;if($image.StartsWith($FixtureRoot+'\',[StringComparison]::OrdinalIgnoreCase) -and ([IO.Path]::GetFileName($image) -in @('橙子启动器.exe','卸载橙子启动器.exe','helper.exe','recovery-helper.exe','interrupt-fixture.exe','ProjectZomboid64.exe'))){$null=Register-OwnedProcess $p}}catch{}}
 foreach($identity in $owned){Stop-OwnedProcess $identity}
}
