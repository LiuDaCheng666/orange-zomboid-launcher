param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
& (Join-Path $repo 'Build-StartupComponent.ps1') -GameDirectory $GameDirectory
$output=Join-Path $repo 'artifacts\ingame-startup\tests'
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
$api=Join-Path $output 'compile-api'
New-Item -ItemType Directory -Path $api -Force | Out-Null
$apiFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\kahlua-api') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
# Signatures only: javac21 can target Java17 while the installed Kahlua VM uses Java25.
# This directory is deliberately absent from every runtime classpath and the shipped JAR.
& javac -encoding UTF-8 --release 17 -d $api $apiFiles
if($LASTEXITCODE -ne 0){throw 'Compile-only Kahlua API declarations failed'}
$asset=Join-Path $repo 'Orange3DTool\Assets\OrangeStartup.jar'
$files=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\startup') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -classpath ($api+';'+$asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $classes $files
if($LASTEXITCODE -ne 0){throw 'Startup Java test compilation failed'}
& java -cp ($classes+';'+$asset) ozl.startup.tests.StartupModelTest
if($LASTEXITCODE -ne 0){throw 'Startup state/session behavior failed'}
& java -cp ($classes+';'+$asset) ozl.startup.tests.RefinementModelTest
if($LASTEXITCODE -ne 0){throw 'Loading refinement model behavior failed'}
& java -cp ($classes+';'+$asset) ozl.startup.tests.ServerInformationTest
if($LASTEXITCODE -ne 0){throw 'Server information receipt behavior failed'}
& java -cp ($classes+';'+$asset) ozl.startup.tests.ServerQueryTest
if($LASTEXITCODE -ne 0){throw 'Owned server public query behavior failed'}
& java -cp ($classes+';'+$asset) ozl.startup.tests.SceneContractTest
if($LASTEXITCODE -ne 0){throw 'Loading scene animation/context behavior failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.SceneProbe (Join-Path $output 'scenes') (Join-Path $repo 'Orange3DTool\Assets\orange-app.png')
if($LASTEXITCODE -ne 0){throw 'Loading scene viewport checks failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.GrowthRasterTest (Join-Path $output 'growth') (Join-Path $repo 'Orange3DTool\Assets\orange-app.png')
if($LASTEXITCODE -ne 0){throw 'Sustained growth raster contracts failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.GrowthMotionProbe (Join-Path $output 'growth-motion') (Join-Path $repo 'Orange3DTool\Assets\orange-app.png')
if($LASTEXITCODE -ne 0){throw 'Visible seed/growth/leaf/loop contracts failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.HumorSceneTest (Join-Path $output 'humor') (Join-Path $repo 'Orange3DTool\Assets\orange-app.png')
if($LASTEXITCODE -ne 0){throw 'Occasional humor scene/timing/reduced-motion contracts failed'}
$fixture=Join-Path $output ('game-'+[guid]::NewGuid().ToString('N'))
$owned=Join-Path $fixture 'OrangeLauncherRuntime\startup'
New-Item -ItemType Directory -Path $owned -Force | Out-Null
Copy-Item -LiteralPath $asset -Destination (Join-Path $owned 'OrangeStartup.jar')
$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
function Encode-Path([string]$Value){[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value))}
$receipt="version=OZL1`nsession=1234567890abcdef1234567890abcdef`ncreated=$now`nexpires=$($now+600)`ngame=$(Encode-Path $fixture)`nuser=$(Encode-Path (Join-Path $fixture 'user'))`nissues=$(Encode-Path (Join-Path $owned 'issues.txt'))`ntheme=dark`nreducedMotion=false`n"
[IO.File]::WriteAllText((Join-Path $owned 'session.properties'),$receipt,[Text.UTF8Encoding]::new($false))
$runtime=Join-Path $GameDirectory 'jre64\bin\java.exe'
Push-Location $fixture
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.TransformerTest;if($LASTEXITCODE -ne 0){throw 'Real agent transformer behavior failed'}}finally{Pop-Location}
Push-Location $fixture
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.RefinementTransformerTest;if($LASTEXITCODE -ne 0){throw 'Refinement agent behavior failed'}}finally{Pop-Location}
Push-Location $fixture
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.ServerInformationTransformerTest;if($LASTEXITCODE -ne 0){throw 'Server information agent behavior failed'}}finally{Pop-Location}
foreach($probe in @('NativeCompositionTest','NativeLifecycleTest','NativeStatusTest','QueueCompositionTest','NativeLuaGlProbe','WorkshopDownloadTest','WorkshopScaleTest','WorkshopScrollbarTest','WorkshopDownloadGlProbe')){
 Push-Location $fixture
 try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.IsolatedLuaProbe $probe $GameDirectory (Join-Path $output 'native-composition');if($LASTEXITCODE -ne 0){throw "Native Lua composition fixture failed: $probe"}}finally{Pop-Location}
}
Push-Location $fixture
try{& $runtime '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+$asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.GlProbe (Join-Path $output 'scenes');if($LASTEXITCODE -ne 0){throw 'Real GL render/state regression failed'}}finally{Pop-Location}
Push-Location $fixture
try{& $runtime '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+$asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.HumorGlProbe (Join-Path $output 'humor');if($LASTEXITCODE -ne 0){throw 'Deterministic actual-GL humor regression failed'}}finally{Pop-Location}
Push-Location $fixture
try{& $runtime '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+$asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.GrowthGlProbe (Join-Path $output 'growth-motion');if($LASTEXITCODE -ne 0){throw 'Actual GL visible growth/reduced-motion regression failed'}}finally{Pop-Location}
Push-Location $fixture
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.QueuedGlProbe (Join-Path $output 'scenes');if($LASTEXITCODE -ne 0){throw 'Queued GL compositing regression failed'}}finally{Pop-Location}
$actualClasses=Join-Path $output 'actual-classes'
New-Item -ItemType Directory -Path $actualClasses -Force | Out-Null
& javac -encoding UTF-8 --release 17 -classpath $asset -d $actualClasses (Join-Path $PSScriptRoot 'java\startup\GameClassProbe.java')
if($LASTEXITCODE -ne 0){throw 'Actual class probe compilation failed'}
Push-Location $fixture
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -cp ($actualClasses+';'+(Join-Path $GameDirectory 'projectzomboid.jar')+';'+(Join-Path $GameDirectory 'ZombieBuddy.jar')) ozl.startup.tests.GameClassProbe;if($LASTEXITCODE -ne 0){throw 'Actual game-class transformation verification failed'}}finally{Pop-Location}
$archive=[IO.Compression.ZipFile]::OpenRead($asset)
try{
 $forbidden=@($archive.Entries | Where-Object {$_.FullName -match '^(zombie/|org/lwjgl|me/zed_0xff/|se/krka/)'});if($forbidden.Count){throw 'Game/framework classes leaked into startup component'}
 if(-not($archive.Entries | Where-Object FullName -eq 'META-INF/BYTE-BUDDY-LICENSE.txt')){throw 'Byte Buddy license absent'}
}finally{$archive.Dispose()}
'PASS startup Java behavior, scene checks, real-agent JVM and package audit'
