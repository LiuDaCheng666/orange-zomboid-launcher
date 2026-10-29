param([string]$GameDirectory='E:/Steam/steamapps/common/ProjectZomboid',[string]$JdkPath='C:/Program Files/Microsoft/jdk-21.0.12.101-hotspot',[string]$AgentJar='',[string]$Label='candidate',[switch]$Baseline,[switch]$SkipRender)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $AgentJar){$AgentJar=Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar'}
$output=Join-Path $repo ('artifacts/loading-overhead-20261030/'+$Label)
$classes=Join-Path $output 'classes';$owned=Join-Path $output 'fixture/OrangeLauncherRuntime/startup'
New-Item -ItemType Directory -Path $classes,$owned -Force|Out-Null
$files=@((Join-Path $PSScriptRoot 'java/startup/LoadingTextCostTest.java'),(Join-Path $PSScriptRoot 'java/startup/LoadingRenderCostProbe.java'),(Join-Path $PSScriptRoot 'java/startup/RenderIssueIoProbe.java'),(Join-Path $PSScriptRoot 'java/startup/IssueSamplerTest.java'),(Join-Path $PSScriptRoot 'java/startup/fixtures/org/lwjglx/opengl/Display.java'))
& (Join-Path $JdkPath 'bin/javac.exe') -encoding UTF-8 --release 17 -cp ($AgentJar+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) -d $classes $files
if($LASTEXITCODE){throw 'Loading cost tests compilation failed'}
$fixture=Join-Path $output 'fixture';$runtime=Join-Path $GameDirectory 'jre64/bin/java.exe'
Copy-Item -LiteralPath $AgentJar -Destination (Join-Path $owned 'OrangeStartup.jar') -Force
$now=[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
function Encode([string]$p){[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($p))}
$receipt="version=OZL1`nsession=1234567890abcdef1234567890abcdef`ncreated=$now`nexpires=$($now+600)`ngame=$(Encode $fixture)`nuser=$(Encode (Join-Path $fixture 'user'))`nissues=$(Encode (Join-Path $owned 'issues.txt'))`ntheme=dark`nreducedMotion=false`n"
[IO.File]::WriteAllText((Join-Path $owned 'session.properties'),$receipt,[Text.UTF8Encoding]::new($false))
$textArgs=@('-Djava.awt.headless=true','-cp',($classes+';'+$AgentJar),'ozl.startup.LoadingTextCostTest')
if($Baseline){$textArgs+='baseline'}
& $runtime @textArgs | Tee-Object -FilePath (Join-Path $output 'text.log')
$textExit=$LASTEXITCODE
if(-not $SkipRender){Push-Location $fixture;try{& $runtime '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+$AgentJar+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.LoadingRenderCostProbe $output | Tee-Object -FilePath (Join-Path $output 'render.log');if($LASTEXITCODE){throw 'Loading GL benchmark failed'}}finally{Pop-Location}}
if($textExit){throw 'Loading text behavior/cost regression failed'}

if(-not $Baseline){
 & $runtime -cp ($classes+';'+$AgentJar) ozl.startup.IssueSamplerTest
 if($LASTEXITCODE){throw 'Issue sampling lifecycle regression failed'}
 Push-Location $fixture
 try{& $runtime '--enable-native-access=ALL-UNNAMED' -cp ($classes+';'+$AgentJar+';'+(Join-Path $GameDirectory 'projectzomboid.jar')) ozl.startup.tests.RenderIssueIoProbe;if($LASTEXITCODE){throw 'Actual render I/O isolation failed'}}finally{Pop-Location}
}
