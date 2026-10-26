param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo ('artifacts\frozen-environment-'+[guid]::NewGuid().ToString('N'))
$classes=Join-Path $output 'test-classes';$fixtureClasses=Join-Path $output 'fixture-classes';$agent=Join-Path $output 'EnvironmentAgent.jar'
New-Item -ItemType Directory -Path $classes,$fixtureClasses -Force | Out-Null
$builder=Join-Path $repo 'Build-EnvironmentComponent.ps1'
& $builder -OutputPath $agent
$fixtures=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\environment\fixtures') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 8 -Xlint:-options -d $fixtureClasses $fixtures
if($LASTEXITCODE -ne 0){throw 'Frozen boundary fixture compilation failed'}
$tests=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\environment') -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 8 -Xlint:-options -cp $fixtureClasses -d $classes $tests
if($LASTEXITCODE -ne 0){throw 'Frozen environment test compilation failed'}
& java -cp ($classes+';'+$agent) ozl.environment.tests.EnvironmentManifestTest $output
if($LASTEXITCODE -ne 0){throw 'Frozen manifest regression failed'}
$fixtureJar=Join-Path $output 'fixture-game.jar'
& jar --create --file $fixtureJar -C $fixtureClasses .
if($LASTEXITCODE -ne 0){throw 'Frozen boundary game JAR failed'}
function Write-Text([string]$Path,[string]$Text){New-Item -ItemType Directory -Path (Split-Path -Parent $Path) -Force | Out-Null;[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
function Encode-Path([string]$Path){[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Path))}
function New-Case([string]$Name,[string]$Bridge='none'){
 $base=Join-Path $output $Name;$snapshot=Join-Path $base 'snapshot';$game=Join-Path $snapshot 'game';$user=Join-Path $base 'live-user';$runtimeHome=Join-Path $snapshot 'runtime\home';$mods=Join-Path $snapshot 'runtime\mods';$first=Join-Path $snapshot 'mods\Versioned';$second=Join-Path $snapshot 'mods\Flat';$workshop=Join-Path $base 'live-workshop'
 New-Item -ItemType Directory -Path $game,$user,$runtimeHome,$mods,$first,$second,$workshop -Force | Out-Null
 Copy-Item -LiteralPath $fixtureJar -Destination (Join-Path $game 'projectzomboid.jar')
 Write-Text (Join-Path $first 'common\mod.info') "id=FrozenVersioned`n";Write-Text (Join-Path $first 'common\media\frozen.txt') 'original-common-bytes';Write-Text (Join-Path $first '42\mod.info') "id=FrozenVersioned`n";Write-Text (Join-Path $first '42\media\frozen.txt') 'original-versioned-bytes'
 Write-Text (Join-Path $second 'mod.info') "id=FrozenFlat`n";Write-Text (Join-Path $second 'media\frozen.txt') 'original-flat-bytes'
 Write-Text (Join-Path $user 'mods\ConflictingMod\mod.info') "id=FrozenFlat`n";Write-Text (Join-Path $user 'mods\ConflictingMod\media\frozen.txt') 'live-updated-bytes';Write-Text (Join-Path $workshop 'mod.info') "id=FrozenVersioned`n"
 Write-Text (Join-Path $mods 'default.txt') 'frozen-default';Write-Text (Join-Path $mods 'loaded.txt') 'frozen-loaded';Write-Text (Join-Path $user 'mods\default.txt') 'live-default';Write-Text (Join-Path $user 'mods\loaded.txt') 'live-loaded'
 $bridgePath=Join-Path $user 'mods\OrangeLauncherSession'
 if($Bridge -eq 'flat'){Write-Text (Join-Path $bridgePath 'mod.info') "id=OrangeLauncherSession`n"}
 if($Bridge -eq 'versioned'){Write-Text (Join-Path $bridgePath 'common\mod.info') "id=OrangeLauncherSession`n";Write-Text (Join-Path $bridgePath '42\mod.info') "id=OrangeLauncherSession`n"}
 $manifest=Join-Path $snapshot 'environment.properties'
 $text="version=OZLENV1`nid=1234567890abcdef1234567890abcdef`ngame=$(Encode-Path $game)`nuser=$(Encode-Path $user)`nhome=$(Encode-Path $runtimeHome)`nmods=$(Encode-Path $mods)`nroots=$(Encode-Path $first),$(Encode-Path $second)`nbridge=$(Encode-Path $bridgePath)`n"
 [IO.File]::WriteAllText($manifest,$text,[Text.Encoding]::ASCII)
 [pscustomobject]@{Base=$base;Game=$game;User=$user;Home=$runtimeHome;Mods=$mods;First=$first;Second=$second;Workshop=$workshop;Manifest=$manifest;Hash=(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash;Bridge=($Bridge -ne 'none')}
}
function Run-Isolated($Case,[string]$Mode='discovery',[int]$Expected=0,[string]$Classpath='',[string]$Hash='',[switch]$ActualApi,[switch]$NoManifest){
 if(-not $Classpath){$Classpath=$classes+';'+(Join-Path $Case.Game 'projectzomboid.jar')}
 if(-not $Hash){$Hash=$Case.Hash}
 $arguments=@(('-Duser.home='+$Case.Home),('-Dozl.fixture.user='+$Case.User),('-Dozl.fixture.workshop='+$Case.Workshop),'-Dozl.startup.enabled=false',('-javaagent:'+$agent))
 if(-not $NoManifest){$arguments+=@(('-Dozl.environment='+$Case.Manifest),('-Dozl.environment.sha256='+$Hash))}
 if($ActualApi){$arguments+=@('-cp',$classes,'ozl.environment.tests.ActualEnvironmentApiProbe',(Join-Path $Case.Game 'projectzomboid.jar'));$runtime=Join-Path $GameDirectory 'jre64\bin\java.exe'}
 else{$arguments+=@('-cp',$Classpath,'ozl.environment.tests.EnvironmentAgentProbe',$Mode,$Case.User,$Case.First,$Case.Second,$Case.Mods,$Case.Bridge.ToString().ToLowerInvariant());$runtime='java'}
 Push-Location $Case.Game
 try{$lines=& $runtime @arguments 2>&1;$code=$LASTEXITCODE;$lines | Set-Content -LiteralPath (Join-Path $Case.Base ($Mode+'.log'));$lines | ForEach-Object {Write-Host $_};if($code -ne $Expected){throw "Frozen subprocess $Mode returned $code; expected $Expected"};if($Expected -eq 0 -and -not ($lines -match '^PASS ')){throw 'Frozen probe did not reach verification'};if($Expected -eq 78 -and -not ($lines -match '\[OZL-ENVIRONMENT\] isolation failure:')){throw 'Invalid environment did not fail through the required isolation guard'}}finally{Pop-Location}
}
function Run-ApiProbe([string[]]$GamePaths,[int]$Expected=0){
 $arguments=@('-cp',$agent,'ozl.environment.EnvironmentProbe')+$GamePaths
 $lines=@(& java @arguments 2>&1);$code=$LASTEXITCODE
 $wanted=if($Expected -eq 0){'PASS'}else{'UNSUPPORTED'}
 if($code -ne $Expected -or @($lines).Count -ne 1 -or [string]$lines[0] -ne $wanted){throw "Pure API probe returned $code / $($lines -join ' '); expected $Expected / $wanted"}
 Write-Host "PASS pure API probe $wanted / exit $Expected"
}
Run-ApiProbe @($GameDirectory)
Run-ApiProbe @() -Expected 2
Run-ApiProbe @('relative-game') -Expected 2
Run-ApiProbe @($GameDirectory,$GameDirectory) -Expected 2
Run-Isolated (New-Case 'without-presentation')
Run-Isolated (New-Case 'b41-flat-bridge' 'flat')
Run-Isolated (New-Case 'b42-versioned-bridge' 'versioned')
Run-Isolated (New-Case 'missing-manifest') -NoManifest -Expected 78
Run-Isolated (New-Case 'changed-manifest') -Hash ('0'*64) -Expected 78
Run-Isolated (New-Case 'deleted-root') -Mode 'delete-root' -Expected 78
Run-Isolated (New-Case 'foreign-bridge') -Mode 'foreign-bridge' -Expected 78
Run-Isolated (New-Case 'null-output') -Mode 'null-output' -Expected 78
Run-Isolated (New-Case 'deleted-settings') -Mode 'delete-settings' -Expected 78
$wrong=New-Case 'wrong-class-source';Run-Isolated $wrong -Classpath ($classes+';'+$fixtureJar) -Expected 78
$badClasses=Join-Path $output 'bad-classes';$badSource=Join-Path $output 'bad-source\zombie\ZomboidFileSystem.java'
$body=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'java\environment\fixtures\zombie\ZomboidFileSystem.java')).Replace('Core.getMyDocumentFolder()','System.getProperty("ozl.fixture.user")');Write-Text $badSource $body
New-Item -ItemType Directory -Path $badClasses -Force | Out-Null
& javac -encoding UTF-8 --release 8 -Xlint:-options -cp $fixtureClasses -d $badClasses $badSource
if($LASTEXITCODE -ne 0){throw 'Changed API fixture compilation failed'}
$bad=New-Case 'changed-path-bytecode';Remove-Item -LiteralPath (Join-Path $bad.Game 'projectzomboid.jar');& jar --create --file (Join-Path $bad.Game 'projectzomboid.jar') -C $badClasses .
if($LASTEXITCODE -ne 0){throw 'Changed API fixture JAR failed'}
Run-Isolated $bad -Expected 78
Run-ApiProbe @($bad.Game) -Expected 2
$actual=New-Case 'actual-installed-api';Copy-Item -LiteralPath (Join-Path $GameDirectory 'projectzomboid.jar') -Destination (Join-Path $actual.Game 'projectzomboid.jar') -Force;Run-Isolated $actual -ActualApi
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($agent)
try{
 if(@($archive.Entries | Where-Object FullName -match '^(zombie/|org/lwjgl|ozl/startup/)').Count){throw 'Game or optional presentation classes leaked into mandatory agent'}
 foreach($entry in $archive.Entries | Where-Object {$_.FullName.EndsWith('.class')}){$stream=$entry.Open();try{$head=New-Object byte[] 8;if($stream.Read($head,0,8) -ne 8 -or $head[6] -ne 0 -or $head[7] -gt 52 -or ($entry.FullName.StartsWith('ozl/environment/') -and $head[7] -ne 52)){throw 'Packaged environment agent/dependency bytecode exceeds Java 8'}}finally{$stream.Dispose()}}
 if(-not($archive.Entries | Where-Object FullName -eq 'META-INF/BYTE-BUDDY-LICENSE.txt')){throw 'Pinned Byte Buddy license missing'}
}finally{$archive.Dispose()}
'PASS mandatory frozen agent: isolated fixtures, fatal guards, actual installed API metadata, Java8 classfiles and package boundary; no game/Steam initialization'
