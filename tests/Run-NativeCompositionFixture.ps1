param([string]$GameDirectory='E:/Steam/steamapps/common/ProjectZomboid',[string]$Probe='NativeCompositionTest',[string]$Output='artifacts/native-composition',[string]$AgentJar='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$outputPath=Join-Path $repo $Output
$classes=Join-Path $outputPath 'classes';$api=Join-Path $outputPath 'compile-api'
New-Item -ItemType Directory -Path $classes,$api -Force | Out-Null
$apiFiles=@(Get-ChildItem (Join-Path $PSScriptRoot 'java/kahlua-api') -Recurse -Filter '*.java' | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -d $api $apiFiles
if($LASTEXITCODE -ne 0){throw 'Compile-only Kahlua declarations failed'}
$jar=Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar'
$files=@(Get-ChildItem (Join-Path $PSScriptRoot 'java/startup') -Recurse -Filter '*.java' | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -cp ($api+';'+$jar+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $classes $files
if($LASTEXITCODE -ne 0){throw 'Fixture compilation failed'}
$fixture=Join-Path $outputPath ('fixture-'+[guid]::NewGuid().ToString('N'));$owned=Join-Path $fixture 'OrangeLauncherRuntime/startup'
New-Item -ItemType Directory -Path $owned -Force | Out-Null
Copy-Item $(if($AgentJar){$AgentJar}else{$jar}) (Join-Path $owned 'OrangeStartup.jar')
$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
function Encode([string]$p){[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($p))}
$receipt="version=OZL1`nsession=1234567890abcdef1234567890abcdef`ncreated=$now`nexpires=$($now+600)`ngame=$(Encode $fixture)`nuser=$(Encode (Join-Path $fixture 'user'))`nissues=$(Encode (Join-Path $owned 'issues.txt'))`ntheme=dark`nreducedMotion=false`n"
[IO.File]::WriteAllText((Join-Path $owned 'session.properties'),$receipt,[Text.UTF8Encoding]::new($false))
Push-Location $fixture
try{
 & (Join-Path $GameDirectory 'jre64/bin/java.exe') '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.IsolatedLuaProbe $Probe $GameDirectory $outputPath
 if($LASTEXITCODE -ne 0){throw "Native fixture $Probe failed"}
}finally{Pop-Location}
