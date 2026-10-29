param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid',[string[]]$Probe=@('StartupTransitionTest','TransitionAnimationTest'))
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo ('artifacts\startup-transition-'+[guid]::NewGuid().ToString('N'))
$classes=Join-Path $output 'classes';$production=Join-Path $output 'production';$api=Join-Path $output 'compile-api'
New-Item -ItemType Directory -Path $classes,$production,$api -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Build an isolated test agent; never rewrite the packaged launcher asset or installed game.
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $repo 'Orange3DTool\Assets\OrangeStartup.jar'),$production)
$source=@(Get-ChildItem -LiteralPath (Join-Path $repo 'Orange3DTool\StartupSource') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -classpath ($production+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $production $source
if($LASTEXITCODE -ne 0){throw 'Transition production compilation failed'}
$agent=Join-Path $output 'OrangeStartup.jar'
& jar --create --file $agent --manifest (Join-Path $production 'META-INF\MANIFEST.MF') -C $production .
if($LASTEXITCODE -ne 0){throw 'Isolated transition agent packaging failed'}
$apiFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\kahlua-api') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -d $api $apiFiles
if($LASTEXITCODE -ne 0){throw 'Compile-only Kahlua signatures failed'}
$fixtures=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\startup\fixtures') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
$tests=@('StartupTransitionTest','TransitionAnimationTest','LuaReloadTest','NativeLuaFixture','IsolatedLuaProbe','NativeLifecycleTest','NativeStatusTest','QueueCompositionTest','WorkshopDownloadTest','StartupModelTest','RefinementTransformerTest') | ForEach-Object {Join-Path $PSScriptRoot ('java\startup\'+$_+'.java')}
& javac -encoding UTF-8 --release 17 -classpath ($api+';'+$agent+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $classes $fixtures $tests
if($LASTEXITCODE -ne 0){throw 'Transition fixtures compilation failed'}
$fixture=Join-Path $output 'game';$owned=Join-Path $fixture 'OrangeLauncherRuntime\startup'
New-Item -ItemType Directory -Path $owned -Force | Out-Null
Copy-Item -LiteralPath $agent -Destination (Join-Path $owned 'OrangeStartup.jar')
$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
function Encode-Path([string]$Value){[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value))}
$receipt="version=OZL1`nsession=1234567890abcdef1234567890abcdef`ncreated=$now`nexpires=$($now+600)`ngame=$(Encode-Path $fixture)`nuser=$(Encode-Path (Join-Path $fixture 'user'))`nissues=$(Encode-Path (Join-Path $owned 'issues.txt'))`ntheme=dark`nreducedMotion=false`n"
[IO.File]::WriteAllText((Join-Path $owned 'session.properties'),$receipt,[Text.UTF8Encoding]::new($false))
Push-Location $fixture
try{
 foreach($test in $Probe){
  & (Join-Path $GameDirectory 'jre64\bin\java.exe') '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.IsolatedLuaProbe $test $GameDirectory $output
  if($LASTEXITCODE -ne 0){throw "Focused transition probe failed: $test"}
 }
}finally{Pop-Location}
"PASS focused $($Probe -join ', '); isolated artifact agent, no live game or Steam initialization"
