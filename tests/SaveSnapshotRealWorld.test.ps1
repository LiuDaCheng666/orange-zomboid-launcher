param(
 [Parameter(Mandatory=$true)][string]$Evidence,
 [ValidateSet('Probe','Click','Capture','SaveExit')][string]$Action='Probe',
 [ValidatePattern('^[a-z0-9-]+$')][string]$Run='final-1'
)
$ErrorActionPreference='Stop'
$Evidence=[IO.Path]::GetFullPath($Evidence)
$fixture=Get-Content -LiteralPath (Join-Path $Evidence 'fixture.json') -Raw|ConvertFrom-Json
$launch=Get-Content -LiteralPath (Join-Path $Evidence 'launch-start.json') -Raw|ConvertFrom-Json
$created=Get-Content -LiteralPath (Join-Path $Evidence 'create.json') -Raw|ConvertFrom-Json
$owned=[IO.Path]::GetFullPath($fixture.fixture).TrimEnd('\')+'\'
if(!(Split-Path -Leaf $fixture.fixture).StartsWith('ozl-save-snapshot-real-') -or !(Test-Path -LiteralPath (Join-Path $fixture.fixture 'owner.txt'))){throw 'Refuse probe without exact isolated owner marker'}
foreach($path in @($fixture.user,$fixture.data,$fixture.snapshots,$launch.engineGame,$launch.engineUser)){
 if(![IO.Path]::GetFullPath($path).StartsWith($owned,[StringComparison]::OrdinalIgnoreCase)){throw 'Refuse game probe outside isolated fixture'}
}
if($launch.testMode -ne $false -or $launch.switchDesktopCalled -ne $false){throw 'Probe requires actual background production-mode session'}
if([IO.File]::ReadAllText((Join-Path $created.snapshot.rootPath '.owner')) -cne $created.snapshot.id){throw 'Snapshot owner identity differs'}
$gameProcess=Get-Process -Id $launch.pid -ErrorAction Stop
$gamePrefix=[IO.Path]::GetFullPath($launch.engineGame).TrimEnd('\')+'\'
if(!$gameProcess.Path.StartsWith($gamePrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Refuse attach to a process outside this exact frozen game'}
if(!$launch.launchedUtc -or $gameProcess.StartTime.ToUniversalTime() -lt ([DateTime]::Parse($launch.launchedUtc).ToUniversalTime().AddSeconds(-2))){throw 'Refuse process without matching fresh launch birth'}
$jdk=Split-Path -Parent (Get-Command javac.exe -ErrorAction Stop).Source
foreach($name in @('java.exe','javac.exe','jar.exe')){if(!(Test-Path -LiteralPath (Join-Path $jdk $name))){throw 'Existing JDK diagnostic tooling is incomplete'}}
$source=Join-Path $PSScriptRoot 'SaveSnapshotRealWorldProbe.java'
$sourceHash=(Get-FileHash -LiteralPath $source).Hash
$loaded=Join-Path $Evidence ('world-probe-loaded-'+$gameProcess.Id+'-'+$gameProcess.StartTime.ToUniversalTime().Ticks+'.sha256')
if((Test-Path -LiteralPath $loaded) -and [IO.File]::ReadAllText($loaded).Trim() -cne $sourceHash){throw 'Probe code changed after attach; retain evidence and use a fresh owned game process'}
$classes=Join-Path $Evidence ('world-probe-'+$sourceHash.Substring(0,12))
$jar=Join-Path $Evidence ('SaveSnapshotRealWorldProbe-'+$sourceHash.Substring(0,12)+'.jar')
if(!(Test-Path -LiteralPath $jar)){
 New-Item -ItemType Directory -Path $classes -Force|Out-Null
 & (Join-Path $jdk 'javac.exe') -encoding UTF-8 --add-modules jdk.attach -d $classes $source
 if($LASTEXITCODE){throw 'Owned world probe compilation failed'}
 $manifest=Join-Path $classes 'MANIFEST.MF'
 [IO.File]::WriteAllText($manifest,"Manifest-Version: 1.0`nAgent-Class: SaveSnapshotRealWorldProbe`n`n",[Text.UTF8Encoding]::new($false))
 & (Join-Path $jdk 'jar.exe') cfm $jar $manifest -C $classes .
 if($LASTEXITCODE){throw 'Owned world probe packaging failed'}
}
$label=$Action.ToLowerInvariant()
$output=Join-Path $Evidence ($Run+'-world-'+$label+'.jsonl')
$config=Join-Path $Evidence ($Run+'-world-'+$label+'.properties')
$parts=$fixture.saveId.Split('/')
$settings=@{game=$launch.engineGame;user=$fixture.user;mode=$parts[0];save=$parts[1];action=$label;output=$output;capture=('SaveSnapshotReal-'+$Run+'.png')}
$lines=@($settings.GetEnumerator()|ForEach-Object{$_.Key+'='+$_.Value.Replace('\','/')})
[IO.File]::WriteAllText($config,($lines -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
& (Join-Path $jdk 'java.exe') --add-modules jdk.attach -cp $jar SaveSnapshotRealWorldProbe $gameProcess.Id $jar $config *> (Join-Path $Evidence ($Run+'-world-'+$label+'-attach.log'))
if($LASTEXITCODE){throw 'Owned world probe attach failed; retain original attach log'}
[IO.File]::WriteAllText($loaded,$sourceHash,[Text.UTF8Encoding]::new($false))
if(Test-Path -LiteralPath $output){Get-Content -LiteralPath $output -Tail 3}
"Queued real game $Action on exact owned PID $($gameProcess.Id); inspect actual result JSONL, native logs and files before asserting success."
