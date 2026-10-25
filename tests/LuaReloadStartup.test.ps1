param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
& (Join-Path $repo 'Build-StartupComponent.ps1') -GameDirectory $GameDirectory
$output=Join-Path $repo 'artifacts\lua-reload-fix-20261026'
$classes=Join-Path $output 'classes'
$api=Join-Path $output 'compile-api'
New-Item -ItemType Directory -Path $classes,$api -Force | Out-Null
$apiFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\kahlua-api') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -d $api $apiFiles
if($LASTEXITCODE -ne 0){throw 'Compile-only Kahlua signatures failed'}
$asset=Join-Path $repo 'Orange3DTool\Assets\OrangeStartup.jar'
$fixtureFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java\startup\fixtures') -Recurse -Filter '*.java' -File | ForEach-Object FullName)
& javac -encoding UTF-8 --release 17 -classpath ($api+';'+$asset+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $classes $fixtureFiles (Join-Path $PSScriptRoot 'java\startup\LuaReloadTest.java') (Join-Path $PSScriptRoot 'java\startup\IsolatedLuaProbe.java')
if($LASTEXITCODE -ne 0){throw 'Lua reload fixture compilation failed'}
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
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.IsolatedLuaProbe LuaReloadTest $GameDirectory $output;if($LASTEXITCODE -ne 0){throw 'Scoped Lua reload regression failed'}}finally{Pop-Location}
$actual=Join-Path $output 'actual-api'
New-Item -ItemType Directory -Path $actual -Force | Out-Null
& javac -encoding UTF-8 --release 17 -classpath $asset -d $actual (Join-Path $PSScriptRoot 'java\startup\LuaReloadApiProbe.java')
if($LASTEXITCODE -ne 0){throw 'Installed reload API probe compilation failed'}
Push-Location $fixture
try{& $runtime '-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar' -cp ($actual+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.LuaReloadApiProbe;if($LASTEXITCODE -ne 0){throw 'Installed Lua reload APIs changed'}}finally{Pop-Location}
'PASS focused Lua reload regression; no live game launch or Steam initialization'
