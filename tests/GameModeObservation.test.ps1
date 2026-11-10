param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid',[string]$LegacyGameDirectory='',[string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts\game-mode-observation-'+[Guid]::NewGuid().ToString('N'))}
[void][IO.Directory]::CreateDirectory($EvidenceDirectory)
& (Join-Path $repo 'Build-StartupComponent.ps1') -GameDirectory $GameDirectory
$agent=Join-Path $repo 'Orange3DTool\Assets\OrangeStartup.jar'
$classes=Join-Path $EvidenceDirectory 'mode-classes'
$actualClasses=Join-Path $EvidenceDirectory 'actual-classes'
[void][IO.Directory]::CreateDirectory($classes)
[void][IO.Directory]::CreateDirectory($actualClasses)
$sources=@('tests\java\startup\GameModeObservationTest.java','tests\java\startup\SceneProbe.java','tests\java\startup\zombie\core\GameMode.java')|ForEach-Object {Join-Path $repo $_}
& javac.exe -encoding UTF-8 --release 17 -cp $agent -d $classes $sources
if($LASTEXITCODE -ne 0){throw 'Game mode observation test compilation failed'}
& javac.exe -encoding UTF-8 --release 17 -cp $agent -d $actualClasses (Join-Path $repo 'tests\java\startup\GameClassProbe.java')
if($LASTEXITCODE -ne 0){throw 'Actual game class probe compilation failed'}
$runtime=Join-Path $GameDirectory 'jre64\bin\java.exe'
& $runtime '-Djava.awt.headless=true' -Xmx256m -cp ($classes+';'+$agent+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.GameModeObservationTest (Join-Path $EvidenceDirectory 'scenes') (Join-Path $repo 'Orange3DTool\Assets\orange-app.png') 2>&1|Tee-Object -FilePath (Join-Path $EvidenceDirectory 'mode-green.log')
if($LASTEXITCODE -ne 0){throw 'Production mode observations failed'}
$fixture=Join-Path $EvidenceDirectory 'fixture'
$owned=Join-Path $fixture 'OrangeLauncherRuntime\startup'
[void][IO.Directory]::CreateDirectory($owned)
$ownedAgent=Join-Path $owned 'OrangeStartup.jar'
Copy-Item -LiteralPath $agent -Destination $ownedAgent
$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
function Encode-ModePath([string]$value){[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value))}
$receipt="version=OZL1`nsession=1234567890abcdef1234567890abcdef`ncreated=$now`nexpires=$($now+600)`ngame=$(Encode-ModePath $fixture)`nuser=$(Encode-ModePath (Join-Path $fixture 'user'))`nissues=$(Encode-ModePath (Join-Path $owned 'issues.txt'))`ntheme=dark`nreducedMotion=false`n"
[IO.File]::WriteAllText((Join-Path $owned 'session.properties'),$receipt,[Text.UTF8Encoding]::new($false))
$gameRoots=@($GameDirectory)
if($LegacyGameDirectory){$gameRoots+=@($LegacyGameDirectory)}
$rows=@()
foreach($gameRoot in $gameRoots){
 $source=Join-Path $gameRoot 'projectzomboid.jar';$before=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
 $label=if($gameRoot -eq $GameDirectory){'current'}else{'legacy'}
 Push-Location $fixture
 try{
  & $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -Xmx256m -cp ($actualClasses+';'+$source+';'+(Join-Path $gameRoot 'ZombieBuddy.jar')) ozl.startup.tests.GameClassProbe 2>&1|Tee-Object -FilePath (Join-Path $EvidenceDirectory ('actual-'+$label+'.log'))
  $code=$LASTEXITCODE
 }finally{Pop-Location}
 if($code -ne 0){throw ('Actual '+$label+' game API verification failed')}
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $before){throw 'Game source changed during observation'}
 $rows+=[pscustomobject]@{kind=$label;game=$gameRoot;gameSha256=$before;classes=18;gameInitialized=$false}
}
$record=[ordered]@{utc=[DateTime]::UtcNow.ToString('o');startupSha256=(Get-FileHash -LiteralPath $agent -Algorithm SHA256).Hash;actual=$rows;foregroundUsed=$false;gameEntryPointInvoked=$false;images='Existing production scene, fixture identities supplied through the production observation helper; AWT raster does not prove live game rendering'}
[IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'verification.json'),(ConvertTo-Json -InputObject $record -Depth 5),[Text.UTF8Encoding]::new($false))
Write-Output 'PASS old/new production mode observation, actual game class contracts and background scene rasters'
