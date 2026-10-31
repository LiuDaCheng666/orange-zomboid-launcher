param([string]$JdkPath='C:/Program Files/Microsoft/jdk-21.0.12.101-hotspot',[string]$EvidenceDirectory='',[string]$LiveAgentPath='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=if($EvidenceDirectory){[IO.Path]::GetFullPath($EvidenceDirectory)}else{Join-Path $repo ('artifacts/discovery-scope-'+[guid]::NewGuid().ToString('N').Substring(0,8))}
$probeClasses=Join-Path $output 'probe-classes';$game=Join-Path $output 'game';$classes=Join-Path $output 'classes';$context=Join-Path $output 'context';$choices=Join-Path $context 'runtime';$user=Join-Path $output 'user';$ordinary=Join-Path $output 'ordinary';$pin=Join-Path $output 'pin';$extra=Join-Path $output 'extra'
foreach($d in @($probeClasses,$game,$classes,$choices,$user,$ordinary,$pin,$extra,(Join-Path $choices 'mods'))){New-Item -ItemType Directory -Path $d -Force|Out-Null}
foreach($name in @('default.txt','loaded.txt')){[IO.File]::WriteAllText((Join-Path $choices ('mods/'+$name)),'')}
[IO.File]::WriteAllText((Join-Path $pin 'mod.info'),"id=Pinned`n");[IO.File]::WriteAllText((Join-Path $ordinary 'mod.info'),"id=Ordinary`n");[IO.File]::WriteAllText((Join-Path $extra 'mod.info'),"id=Extra`n")
$javac=Join-Path $JdkPath 'bin/javac.exe';$jar=Join-Path $JdkPath 'bin/jar.exe';$java=Join-Path $JdkPath 'bin/java.exe'
if(-not $LiveAgentPath){$LiveAgentPath=Join-Path $output 'OrangeLive.jar';& (Join-Path $repo 'Build-LiveComponent.ps1') -OutputPath $LiveAgentPath -JdkPath $JdkPath}
$sources=@('tests/java/live/fixtures/zombie/modding/ActiveMods.java','tests/java/live/fixtures/zombie/modding/ActiveModsFile.java','tests/java/live/fixtures/zombie/network/GameClient.java')|ForEach-Object{Join-Path $repo $_}
$sources+=@(Get-ChildItem -LiteralPath (Join-Path $repo 'tests/java/server-approval/fixtures') -Filter '*.java' -Recurse -File|ForEach-Object FullName)
& $javac -encoding UTF-8 --release 8 -Xlint:-options -d $classes $sources
if($LASTEXITCODE){throw 'Boundary compilation failed'}
& $jar --create --file (Join-Path $game 'projectzomboid.jar') -C $classes .
if($LASTEXITCODE){throw 'Boundary packaging failed'}
& $javac -encoding UTF-8 --release 8 -Xlint:-options -cp (Join-Path $game 'projectzomboid.jar') -d $probeClasses (Join-Path $repo 'tests/java/live/DiscoveryScopeProbe.java')
if($LASTEXITCODE){throw 'Probe compilation failed'}
function Record([string]$key,[string[]]$values){$key+([string]::Concat(@($values|ForEach-Object{"`t"+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_))})))+"`n"}
$body="OZLLIVE1`n"+(Record 'game' @($game))+(Record 'user' @($user))+(Record 'context' @($context))+(Record 'choices' @($choices))+(Record 'vanilla' @('false'))+(Record 'server-components' @('force-local'))+(Record 'managed' @('Pinned'))+(Record 'selected' @('Pinned',$pin))+(Record 'component' @('Pinned'))+(Record 'order' @('Pinned'))
$manifest=Join-Path $choices 'live.manifest';[IO.File]::WriteAllText($manifest,$body,[Text.UTF8Encoding]::new($false))
Push-Location $game
try{& $java "-Dozl.live.manifest=$manifest" "-Dozl.live.sha256=$((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash)" "-Dozl.fixture.user=$user" "-javaagent:$LiveAgentPath" -cp ($probeClasses+';'+(Join-Path $game 'projectzomboid.jar')) ozl.environment.tests.DiscoveryScopeProbe $ordinary $pin $extra
if($LASTEXITCODE){throw 'Discovery scope assertions failed'}}finally{Pop-Location}
$body=$body.Replace((Record 'server-components' @('force-local')),'');[IO.File]::WriteAllText($manifest,$body,[Text.UTF8Encoding]::new($false))
Push-Location $game
try{& $java "-Dozl.live.manifest=$manifest" "-Dozl.live.sha256=$((Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash)" "-Dozl.fixture.user=$user" "-javaagent:$LiveAgentPath" -cp ($probeClasses+';'+(Join-Path $game 'projectzomboid.jar')) ozl.environment.tests.DiscoveryScopeProbe $ordinary $pin $extra gate-only
if($LASTEXITCODE){throw 'Auxiliary Java permission assertions failed'}}finally{Pop-Location}
Write-Host ('Evidence: '+$output)
