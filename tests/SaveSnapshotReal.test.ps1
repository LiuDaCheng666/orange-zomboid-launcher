param(
 [ValidateSet('Prepare','Create','Preview','Check','Launch','LaunchJava','LaunchNativeProbe','Validate','Running','Resave','ResaveController','PathProbe','NativePathProbe','CatalogSources','ManifestCapacity','VerifySource')][string]$Action='Prepare',
 [string]$Evidence,
 [string]$Candidate
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(!$Evidence){$Evidence=Join-Path $repo ('artifacts\SaveSnapshotReal-'+[Guid]::NewGuid().ToString('N'))}
$Evidence=[IO.Path]::GetFullPath($Evidence)
New-Item -ItemType Directory -Path $Evidence -Force|Out-Null
$configFile=Join-Path $Evidence 'fixture.json'
function Save-Json($Value,[string]$Path){[IO.File]::WriteAllText($Path,($Value|ConvertTo-Json -Depth 30),[Text.UTF8Encoding]::new($false))}
function Source-Hashes($Config){
 $rows=@();$paths=@('ProjectZomboid64.exe','ProjectZomboid64.json','projectzomboid.jar')|ForEach-Object{Join-Path $Config.sourceGame $_}
 $paths+=Join-Path $Config.sourceUser 'mods\default.txt'
 $save=Join-Path $Config.sourceUser ('Saves\'+$Config.saveId.Replace('/','\'))
 $paths+=@(Get-ChildItem -LiteralPath $save -File|ForEach-Object FullName)
 foreach($path in $paths){if(Test-Path -LiteralPath $path -PathType Leaf){$file=Get-Item -LiteralPath $path;$rows+=[pscustomobject]@{path=$file.FullName;bytes=$file.Length;modifiedUtc=$file.LastWriteTimeUtc.ToString('o');sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash}}}
 return $rows
}
if($Action -eq 'Prepare'){
 if([string]::IsNullOrWhiteSpace($Candidate)){throw 'Prepare requires the exact verified candidate directory'}
 if(Test-Path -LiteralPath $configFile){throw 'Existing fixture: use its next action, never overwrite its clone'}
 $fixture=Join-Path 'E:\AppCaches\Temp' ('ozl-save-snapshot-real-'+[Guid]::NewGuid().ToString('N'))
 $config=[ordered]@{evidence=$Evidence;fixture=$fixture;assembly=(Join-Path $Candidate '橙子启动器.exe');sourceGame='E:\steam\steamapps\common\ProjectZomboid';sourceUser='C:\Users\MC233\Zomboid';game='E:\steam\steamapps\common\ProjectZomboid';user=(Join-Path $fixture 'clone-user');data=(Join-Path $fixture 'launcher-data');snapshots=(Join-Path $fixture 'snapshots');saveId='Apocalypse/2026-08-02_18-36-09';modChoices='{"AirdropMod.20":"AirdropMod","Ladders42131":"Ladders4220"}';launchSeconds=240}
 New-Item -ItemType Directory -Path $fixture,$config.user,$config.data,$config.snapshots -Force|Out-Null
 [IO.File]::WriteAllText((Join-Path $fixture 'owner.txt'),"OrangeLauncher isolated real save acceptance; original game/user remain read-only.`n$Evidence")
 Save-Json $config $configFile
 Save-Json @(Source-Hashes $config) (Join-Path $Evidence 'source-before.json')
 $sourceSave=Join-Path $config.sourceUser ('Saves\'+$config.saveId.Replace('/','\'));$cloneSave=Join-Path $config.user ('Saves\'+$config.saveId.Replace('/','\'))
 & robocopy.exe $sourceSave $cloneSave /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /NP /NDL /NFL "/LOG:$(Join-Path $Evidence 'clone-save.log')"|Out-Null
 if($LASTEXITCODE -ge 8){throw "Real save clone failed: robocopy $LASTEXITCODE"}
 & robocopy.exe (Join-Path $config.sourceUser 'mods') (Join-Path $config.user 'mods') /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /NP /NDL /NFL "/LOG:$(Join-Path $Evidence 'clone-local-mods.log')"|Out-Null
 if($LASTEXITCODE -ge 8){throw "Local mod clone failed: robocopy $LASTEXITCODE"}
 if(Test-Path -LiteralPath (Join-Path $config.sourceUser 'options.ini')){Copy-Item -LiteralPath (Join-Path $config.sourceUser 'options.ini') -Destination (Join-Path $config.user 'options.ini')}
 $files=@(Get-ChildItem -LiteralPath $cloneSave -File -Recurse);Save-Json @{saveFiles=$files.Count;saveBytes=($files|Measure-Object Length -Sum).Sum;cloneSave=$cloneSave;localMods=@(Get-ChildItem -LiteralPath (Join-Path $config.user 'mods') -Directory|Select-Object -ExpandProperty Name)} (Join-Path $Evidence 'clone.json')
 $originalRoot=[IO.Path]::GetFullPath($sourceSave);$copyRoot=[IO.Path]::GetFullPath($cloneSave)
 $clonedHashes=@(Get-ChildItem -LiteralPath $originalRoot -File -Recurse|ForEach-Object{$relative=$_.FullName.Substring($originalRoot.Length+1);$originalHash=(Get-FileHash -LiteralPath $_.FullName).Hash;$copiedHash=(Get-FileHash -LiteralPath (Join-Path $copyRoot $relative)).Hash;if($originalHash -cne $copiedHash){throw "Real save clone differs: $relative"};[pscustomobject]@{path=$relative;bytes=$_.Length;sha256=$originalHash}})
 Save-Json $clonedHashes (Join-Path $Evidence 'clone-save-all-hashes.json')
 $csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';$bridge=Join-Path $Evidence 'SaveSnapshotRealBridge.exe';$source=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'SaveSnapshotRealBridge.cs'))
 # The CLR performs managed assembly-path IO before MarshalByRef Run can call LauncherRuntime.Initialize.
 # Apply exactly the same two switches at child-domain startup to avoid caching legacy paths in the test host.
 [IO.File]::WriteAllText(($bridge+'.config'),'<configuration><runtime><AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false;Switch.System.IO.BlockLongPaths=false" /></runtime></configuration>')
 & $csc /nologo /target:exe /platform:x64 "/out:$bridge" "/win32manifest:$repo\Orange3DTool\app.manifest" /r:System.Core.dll /r:System.Web.Extensions.dll $source
 if($LASTEXITCODE){throw 'Real bridge compilation failed'}
 "Prepared isolated real fixture: $Evidence"
 return
}
if(!(Test-Path -LiteralPath $configFile)){throw 'Prepare a fixture first'}
$config=Get-Content -LiteralPath $configFile -Raw|ConvertFrom-Json
$fixtureRoot=[IO.Path]::GetFullPath($config.fixture).TrimEnd('\');$ownedPrefix=$fixtureRoot+'\'
if(!(Split-Path -Leaf $fixtureRoot).StartsWith('ozl-save-snapshot-real-') -or !(Test-Path -LiteralPath (Join-Path $fixtureRoot 'owner.txt'))){throw 'Refuse fixture without isolated owner marker'}
foreach($path in @($config.user,$config.data,$config.snapshots)){if(![IO.Path]::GetFullPath($path).StartsWith($ownedPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Refuse action outside isolated user/data/snapshots'}}
if($Action -eq 'VerifySource'){
 $after=@(Source-Hashes $config);Save-Json $after (Join-Path $Evidence 'source-after.json');$before=Get-Content -LiteralPath (Join-Path $Evidence 'source-before.json') -Raw|ConvertFrom-Json
 $diff=@(Compare-Object @($before|ForEach-Object{"$($_.path)|$($_.bytes)|$($_.sha256)"}) @($after|ForEach-Object{"$($_.path)|$($_.bytes)|$($_.sha256)"}))
 Save-Json $diff (Join-Path $Evidence 'source-diff.json');if($diff.Count){throw 'Source metadata changed; inspect source-diff.json'}
 $allBefore=Get-Content -LiteralPath (Join-Path $Evidence 'clone-save-all-hashes.json') -Raw|ConvertFrom-Json
 $saveRoot=[IO.Path]::GetFullPath((Join-Path $config.sourceUser ('Saves\'+$config.saveId.Replace('/','\'))))
 $allAfter=@(Get-ChildItem -LiteralPath $saveRoot -File -Recurse|ForEach-Object{[pscustomobject]@{path=$_.FullName.Substring($saveRoot.Length+1);bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
 $saveDiff=@(Compare-Object @($allBefore|ForEach-Object{"$($_.path)|$($_.bytes)|$($_.sha256)"}) @($allAfter|ForEach-Object{"$($_.path)|$($_.bytes)|$($_.sha256)"}))
 Save-Json $saveDiff (Join-Path $Evidence 'source-save-all-diff.json');if($saveDiff.Count){throw 'Source save bytes changed; inspect source-save-all-diff.json'}
 'PASS original game/save/default metadata hashes unchanged';return
}
if($Action -in @('Launch','LaunchJava','LaunchNativeProbe')){
 # Only the disposable cloned user's window/volume settings change for the background run.
 $options=Join-Path $config.user 'options.ini'
 if(Test-Path -LiteralPath $options){$text=[IO.File]::ReadAllText($options);$text=[regex]::Replace($text,'(?m)^(ambientVolume|jumpScareVolume|musicVolume|soundVolume|vehicleEngineVolume|voiceVolumeMic|voiceVolumePlayers)=\d+','$1=0');$text=[regex]::Replace($text,'(?m)^(fullScreen|borderless|voiceEnable)=(true|false)','$1=false');[IO.File]::WriteAllText($options,$text,[Text.UTF8Encoding]::new($false))}
}
& (Join-Path $Evidence 'SaveSnapshotRealBridge.exe') $configFile $Action.ToLowerInvariant() *> (Join-Path $Evidence ($Action.ToLowerInvariant()+'-runner.log'))
$exit=$LASTEXITCODE
Get-Content -LiteralPath (Join-Path $Evidence ($Action.ToLowerInvariant()+'-runner.log')) -Tail 5
if($exit){throw "Real candidate $Action failed (exit $exit); retained original failure and all fixture files"}
