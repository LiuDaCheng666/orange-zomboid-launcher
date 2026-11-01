param([string]$OutputAgent='',[string]$SignedBody='C:/Users/MC233/.zombie_buddy/authors.json')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo ('artifacts/buddy-authors-prefetch-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$classes=Join-Path $output classes;[IO.Directory]::CreateDirectory($classes)|Out-Null
if(!$OutputAgent){$OutputAgent=Join-Path $output OrangeLive.jar;& (Join-Path $repo Build-LiveComponent.ps1) -OutputPath $OutputAgent}
$buddy=Join-Path $repo Dependencies/ZombieBuddy-v2.3.4.jar
& javac -encoding UTF-8 --release 11 -Xlint:-options -cp ($OutputAgent+';'+$buddy) -d $classes (Join-Path $PSScriptRoot java/live/BuddyAuthorsPrefetchTest.java) (Join-Path $PSScriptRoot java/live/BuddyAuthorsFixedProbe.java) (Join-Path $PSScriptRoot java/environment/fixtures/zombie/gameStates/ChooseGameInfo.java)
if($LASTEXITCODE -ne 0){throw 'Author prefetch fixture compilation failed'}
$mf=Join-Path $output probe.mf;[IO.File]::WriteAllText($mf,"Manifest-Version: 1.0`nPremain-Class: ozl.environment.BuddyAuthorsFixedProbe`n`n",[Text.Encoding]::ASCII)
$probe=Join-Path $output Probe.jar
& jar --create --file $probe --manifest $mf -C $classes .
if($LASTEXITCODE -ne 0){throw 'Author probe packaging failed'}
$cp=$classes+';'+$OutputAgent+';'+$buddy
& java -cp $cp ozl.environment.BuddyAuthorsPrefetchTest
if($LASTEXITCODE -ne 0){throw 'Author prefetch unit assertions failed'}
foreach($case in @('success','io','http500','invalid','invalidboth','unchecked','interrupted','preinterrupted')){
 & java ('-javaagent:'+$probe+'='+$buddy) -cp $cp ozl.environment.BuddyAuthorsFixedProbe $case (Join-Path $output $case) $SignedBody
 if($LASTEXITCODE -ne 0){throw ('Fixed Buddy author probe failed: '+$case)}
}
$foreignClasses=Join-Path $output 'foreign-classes';[IO.Directory]::CreateDirectory($foreignClasses)|Out-Null
& javac -encoding UTF-8 --release 8 -Xlint:-options -d $foreignClasses (Join-Path $PSScriptRoot 'java/authors-foreign/me/zed_0xff/zombie_buddy/KnownAuthors.java')
if($LASTEXITCODE -ne 0){throw 'Foreign-origin author fixture compilation failed'}
$foreign=Join-Path $output 'ForeignAuthors.jar'; & jar --create --file $foreign -C $foreignClasses .
& java ('-javaagent:'+$probe+'='+$buddy) -cp $cp ozl.environment.BuddyAuthorsFixedProbe foreign (Join-Path $output 'foreign-origin') $SignedBody $buddy $foreign
if($LASTEXITCODE -ne 0){throw 'Foreign author source boundary failed'}
# Exercise the production LiveEnvironmentAgent premain wiring, not just helper.install.
$wireGame=Join-Path $output 'wire-game';$wireUser=Join-Path $output 'wire-user';$wireContext=Join-Path $output 'wire-context';$wireChoices=Join-Path $wireContext 'session';$wireClasses=Join-Path $output 'wire-fixture-classes'
foreach($path in @($wireGame,$wireUser,$wireClasses,(Join-Path $wireChoices 'mods'))){[IO.Directory]::CreateDirectory($path)|Out-Null}
$fixtureSources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java/environment/fixtures') -Recurse -Filter '*.java'|Where-Object{$_.Name -notin @('ZomboidFileSystem.java','ActiveMods.java','ActiveModsFile.java')}|ForEach-Object FullName)+@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java/live/fixtures') -Recurse -Filter '*.java'|ForEach-Object FullName)
& javac -encoding UTF-8 --release 8 -Xlint:-options -d $wireClasses $fixtureSources
if($LASTEXITCODE -ne 0){throw 'Production wiring engine fixture compilation failed'}
& jar --create --file (Join-Path $wireGame 'projectzomboid.jar') -C $wireClasses .
$wireBuddy=Join-Path $wireChoices 'ZombieBuddy.jar';Copy-Item -LiteralPath $buddy -Destination $wireBuddy
foreach($file in @('default.txt','loaded.txt')){[IO.File]::WriteAllText((Join-Path $wireChoices ('mods/'+$file)),"",[Text.UTF8Encoding]::new($false))}
function Record([string]$key,[string[]]$values){return $key+(($values|ForEach-Object{"`t"+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_))}) -join '')+"`n"}
$manifest=Join-Path $wireChoices 'live.manifest'
$body="OZLLIVE1`n"+(Record 'game' @($wireGame))+(Record 'user' @($wireUser))+(Record 'context' @($wireContext))+(Record 'choices' @($wireChoices))+(Record 'vanilla' @('false'))+(Record 'buddy' @($wireBuddy,(Get-FileHash -LiteralPath $wireBuddy -Algorithm SHA256).Hash.ToLowerInvariant()))
[IO.File]::WriteAllText($manifest,$body,[Text.UTF8Encoding]::new($false))
$wireCp=$OutputAgent+';'+$wireBuddy+';'+(Join-Path $wireGame 'projectzomboid.jar')+';'+$probe
Push-Location $wireGame
try{
 & java ('-javaagent:'+$probe+'=transport-only') ('-Dozl.live.manifest='+$manifest) ('-Dozl.live.sha256='+(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash) ('-javaagent:'+$OutputAgent) -cp $wireCp ozl.environment.BuddyAuthorsFixedProbe success (Join-Path $output 'production-wire-success') $SignedBody
 if($LASTEXITCODE -ne 0){throw 'Production live agent prefetch wiring failed'}
}finally{Pop-Location}
Write-Host ('Author prefetch evidence: '+$output)
