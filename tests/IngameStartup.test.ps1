param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
& (Join-Path $repo 'Build-StartupComponent.ps1') -GameDirectory $GameDirectory
$output=Join-Path $repo 'artifacts\ingame-startup\tests'
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
$asset=Join-Path $repo 'Orange3DTool\Assets\OrangeStartup.jar'
$files=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\startup') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -classpath ($asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $classes $files
if($LASTEXITCODE -ne 0){throw 'Startup Java test compilation failed'}
& java -cp ($classes+';'+$asset) ozl.startup.tests.StartupModelTest
if($LASTEXITCODE -ne 0){throw 'Startup state/session behavior failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.SceneProbe (Join-Path $output 'scenes') (Join-Path $repo 'Orange3DTool\Assets\orange-app.png')
if($LASTEXITCODE -ne 0){throw 'Loading scene viewport checks failed'}
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
try{& $runtime '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+$asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.GlProbe (Join-Path $output 'scenes');if($LASTEXITCODE -ne 0){throw 'Real GL render/state regression failed'}}finally{Pop-Location}
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
