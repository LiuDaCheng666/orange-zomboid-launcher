param([Parameter(Mandatory=$true)][string]$MainEvidence,[Parameter(Mandatory=$true)][string]$Candidate)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$main=Get-Content -LiteralPath (Join-Path $MainEvidence 'fixture.json') -Raw|ConvertFrom-Json
$evidence=Join-Path $repo ('artifacts\SaveSnapshotReal-isolation-'+[Guid]::NewGuid().ToString('N'))
$fixture=Join-Path 'E:\AppCaches\Temp' ('ozl-save-snapshot-real-isolation-'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $fixture 'independent-source-game';$user=Join-Path $fixture 'own-user';$save=Join-Path $user 'Saves\Sandbox\OwnedFixture';$mod=Join-Path $user 'mods\OwnStable\42'
New-Item -ItemType Directory -Path $evidence,$source,$save,$mod,(Join-Path $fixture 'data'),(Join-Path $fixture 'snapshots') -Force|Out-Null
[IO.File]::WriteAllText((Join-Path $fixture 'owner.txt'),"Independent source mutation fixture. Never a playable full source game. $evidence")
foreach($name in @('ProjectZomboid64.exe','projectzomboid.jar')){Copy-Item -LiteralPath (Join-Path $main.sourceGame $name) -Destination (Join-Path $source $name)}
& robocopy.exe (Join-Path $main.sourceGame 'jre64') (Join-Path $source 'jre64') /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /XJ /NP /NDL /NFL "/LOG:$(Join-Path $evidence 'copy-real-jre.log')"|Out-Null
if($LASTEXITCODE -ge 8){throw 'Independent real JRE copy failed'}
# An isolated source config, not a mutation of the actual Steam JSON. No game is launched from this incomplete source fixture.
[IO.File]::WriteAllText((Join-Path $source 'ProjectZomboid64.json'),'{"mainClass":"zombie/gameStates/MainScreenState","classpath":[".","projectzomboid.jar"],"vmArgs":["-Xmx512m"]}')
[IO.File]::WriteAllText((Join-Path $source 'source-owned-marker.txt'),'source-before')
[IO.File]::WriteAllText((Join-Path $mod 'mod.info'),"name=Own stable fixture`nid=OwnStable`n")
$mods="VERSION = 1,`nmods`n{`n    mod = OwnStable,`n    mod = OwnUnavailable,`n}`nmaps`n{`n}`n"
[IO.File]::WriteAllText((Join-Path $save 'mods.txt'),$mods)
[IO.File]::WriteAllText((Join-Path $user 'mods\default.txt'),"VERSION = 1,`nmods`n{`n}`nmaps`n{`n}`n")
Copy-Item -LiteralPath (Join-Path $main.sourceUser ('Saves\'+$main.saveId.Replace('/','\')+'\map_ver.bin')) -Destination (Join-Path $save 'map_ver.bin')
$config=[ordered]@{evidence=$evidence;fixture=$fixture;assembly=(Join-Path $Candidate '橙子启动器.exe');sourceGame=$source;sourceUser=$user;game=$source;user=$user;data=(Join-Path $fixture 'data');snapshots=(Join-Path $fixture 'snapshots');saveId='Sandbox/OwnedFixture';modChoices='{"OwnUnavailable":""}';launchSeconds=1}
[IO.File]::WriteAllText((Join-Path $evidence 'fixture.json'),($config|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $MainEvidence 'SaveSnapshotRealBridge.exe') -Destination $evidence
Copy-Item -LiteralPath (Join-Path $MainEvidence 'SaveSnapshotRealBridge.exe.config') -Destination $evidence
& (Join-Path $PSScriptRoot 'SaveSnapshotReal.test.ps1') -Action Create -Evidence $evidence
$created=Get-Content -LiteralPath (Join-Path $evidence 'create.json') -Raw|ConvertFrom-Json
$root=$created.snapshot.rootPath
$before=@{};foreach($path in @((Join-Path $root 'game\source-owned-marker.txt'))+@(Get-ChildItem -LiteralPath (Join-Path $root 'mods') -Filter 'mod.info' -File -Recurse|ForEach-Object FullName)){$before[$path]=(Get-FileHash -LiteralPath $path).Hash}
[IO.File]::AppendAllText((Join-Path $source 'source-owned-marker.txt'),'-changed')
[IO.File]::AppendAllText((Join-Path $mod 'mod.info'),"description=changed only in independent source`n")
& (Join-Path $PSScriptRoot 'SaveSnapshotReal.test.ps1') -Action Check -Evidence $evidence
& (Join-Path $PSScriptRoot 'SaveSnapshotReal.test.ps1') -Action Validate -Evidence $evidence
foreach($path in $before.Keys){if((Get-FileHash -LiteralPath $path).Hash -cne $before[$path]){throw "Frozen bytes changed after independent source edit: $path"}}
$check=Get-Content -LiteralPath (Join-Path $evidence 'check.json') -Raw|ConvertFrom-Json
if(!$check.available -or !$check.gameChanged -or !$check.modsChanged){throw 'Actual CheckUpdates failed to detect independent source edits'}
[IO.File]::WriteAllText((Join-Path $evidence 'isolation-result.json'),(@{ok=$true;gameChanged=$check.gameChanged;modsChanged=$check.modsChanged;unchangedFrozenHashes=$before;scope='Real native TestMode=false create/check/validate with actual JRE+jar, explicit skip; no world launch for incomplete source fixture.'}|ConvertTo-Json -Depth 15))
# Only this explicitly owned source fixture is made unavailable; the snapshot must still validate.
$unavailable=[IO.Path]::GetFullPath((Join-Path $fixture 'independent-source-game-unavailable'))
$ownedPrefix=[IO.Path]::GetFullPath($fixture).TrimEnd('\')+'\'
if(!([IO.Path]::GetFullPath($source).StartsWith($ownedPrefix,[StringComparison]::OrdinalIgnoreCase)) -or !$unavailable.StartsWith($ownedPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Independent source move escaped fixture'}
Copy-Item -LiteralPath (Join-Path $evidence 'check.json') -Destination (Join-Path $evidence 'check-source-changed.json')
Move-Item -LiteralPath $source -Destination $unavailable
& (Join-Path $PSScriptRoot 'SaveSnapshotReal.test.ps1') -Action Check -Evidence $evidence
& (Join-Path $PSScriptRoot 'SaveSnapshotReal.test.ps1') -Action Validate -Evidence $evidence
$gone=Get-Content -LiteralPath (Join-Path $evidence 'check.json') -Raw|ConvertFrom-Json
if($gone.available){throw 'Unavailable independent source incorrectly reported available'}
[IO.File]::WriteAllText((Join-Path $evidence 'source-unavailable-result.json'),(@{ok=$true;available=$false;frozenValidation='passed actual JRE probe after independent source move';source=$source;movedTo=$unavailable;message=$gone.message}|ConvertTo-Json -Depth 10))
"PASS isolated source changes retained frozen bytes, real JRE probe and explicit skip: $evidence"
