param([string]$GameDirectory='E:/Steam/steamapps/common/ProjectZomboid',[string]$Output='artifacts/workshop-download/final')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$outputPath=Join-Path $repo $Output
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
& (Join-Path $repo 'Build-StartupComponent.ps1') -GameDirectory $GameDirectory
if($LASTEXITCODE -ne 0){throw 'Workshop component build failed'}
# Impact-selected only: changed transfer/model/layout and shared native primitive/lifetime hooks.
$probes=@('WorkshopLayoutContractTest','WorkshopDownloadTest','WorkshopDownloadGlProbe','NativeCompositionTest','NativeLifecycleTest','NativeStatusTest','QueueCompositionTest','RefinementTransformerTest','NativeLuaGlProbe')
foreach($probe in $probes){
 & (Join-Path $PSScriptRoot 'Run-NativeCompositionFixture.ps1') -GameDirectory $GameDirectory -Probe $probe -Output ($Output+'/'+$probe) *> (Join-Path $outputPath ($probe+'.txt'))
 if($LASTEXITCODE -ne 0){throw "Workshop impact check failed: $probe"}
 Get-Content (Join-Path $outputPath ($probe+'.txt')) | Where-Object {$_ -match '^PASS'}
}
$classes=Join-Path $outputPath 'WorkshopDownloadTest/classes'
$jar=Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar'
foreach($probe in @('StartupModelTest','SceneContractTest')){
 & java -cp ($classes+';'+$jar) ('ozl.startup.tests.'+$probe)
 if($LASTEXITCODE -ne 0){throw "Changed model/scene dependency failed: $probe"}
}
$actual=Join-Path $outputPath 'actual-classes'
New-Item -ItemType Directory -Path $actual -Force | Out-Null
& javac -encoding UTF-8 --release 17 -classpath $jar -d $actual (Join-Path $PSScriptRoot 'java/startup/GameClassProbe.java')
if($LASTEXITCODE -ne 0){throw 'Noninitializing API probe compilation failed'}
$fixture=Get-ChildItem (Join-Path $outputPath 'WorkshopDownloadTest') -Directory -Filter 'fixture-*' | Select-Object -First 1
Push-Location $fixture.FullName
try{
 & (Join-Path $GameDirectory 'jre64/bin/java.exe') '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -cp ($actual+';'+(Join-Path $GameDirectory 'projectzomboid.jar')+';'+(Join-Path $GameDirectory 'ZombieBuddy.jar')) ozl.startup.tests.GameClassProbe
 if($LASTEXITCODE -ne 0){throw 'Actual game APIs or agent transform failed'}
}finally{Pop-Location}
$archive=[IO.Compression.ZipFile]::OpenRead($jar)
try{
 if(@($archive.Entries | Where-Object {$_.FullName -match '^(zombie/|org/lwjgl|me/zed_0xff/|se/krka/)'}).Count){throw 'Fixture/game/framework class leaked into component'}
 if(-not($archive.Entries | Where-Object FullName -eq 'META-INF/BYTE-BUDDY-LICENSE.txt')){throw 'Byte Buddy license absent'}
}finally{$archive.Dispose()}
$hash=(Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash
$copies=Get-ChildItem $outputPath -Recurse -File -Filter OrangeStartup.jar
foreach($copy in $copies){if((Get-FileHash -LiteralPath $copy.FullName -Algorithm SHA256).Hash -ne $hash){throw 'Fixture payload hash mismatch'}}
"PASS workshop component package audit; $($copies.Count) accepted fixture Jars match SHA256 $hash"
