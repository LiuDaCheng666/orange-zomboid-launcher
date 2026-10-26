param([string]$GameDirectory='E:\Steam\steamapps\common\ProjectZomboid')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo 'artifacts\loading-humor-20261026'
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
& (Join-Path $repo 'Build-StartupComponent.ps1') -GameDirectory $GameDirectory
$asset=Join-Path $repo 'Orange3DTool\Assets\OrangeStartup.jar'
$gameApi=Join-Path $GameDirectory 'projectzomboid.jar'
$javaSources=@('SceneContractTest.java','SceneProbe.java','HumorSceneTest.java','HumorGlProbe.java','GlProbe.java','fixtures\org\lwjglx\opengl\Display.java') | ForEach-Object {Join-Path $PSScriptRoot ('java\startup\'+$_)}
& javac -encoding UTF-8 --release 17 -cp ($asset+';'+$gameApi) -d $classes $javaSources
if($LASTEXITCODE -ne 0){throw 'Loading humor test compilation failed'}
function Run-Checked([string]$Program,[string[]]$Arguments,[string]$Log){
 & $Program @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $output $Log)
 if($LASTEXITCODE -ne 0){throw "Focused loading verification failed: $Log"}
}
Run-Checked 'java' @('-Djava.awt.headless=true','-cp',($classes+';'+$asset),'ozl.startup.tests.HumorSceneTest',(Join-Path $output 'humor'),(Join-Path $repo 'Orange3DTool\Assets\orange-app.png')) 'java-green.log'
Run-Checked 'java' @('-cp',($classes+';'+$asset),'ozl.startup.tests.SceneContractTest') 'scene-regression.log'
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
try{
 Run-Checked $runtime @('--enable-native-access=ALL-UNNAMED','-cp',($classes+';'+$asset+';'+$gameApi),'ozl.startup.tests.HumorGlProbe',(Join-Path $output 'gl')) 'gl-green.log'
 Run-Checked $runtime @('--enable-native-access=ALL-UNNAMED','-cp',($classes+';'+$asset+';'+$gameApi),'ozl.startup.tests.GlProbe',(Join-Path $output 'gl')) 'gl-regression.log'
}finally{Pop-Location}
$native=Join-Path $output 'startup-controls.exe'
& (Join-Path $repo 'Build-Tests.ps1') -Entry 'PZ3D.GameStartupControlsTests' -Output $native
Run-Checked $native @() 'controls-green.log'
Run-Checked $native @('--component-regression') 'component-regression.log'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($asset)
try{if(@($archive.Entries | Where-Object FullName -match '^(zombie/|org/lwjgl|me/zed_0xff/|se/krka/)').Count){throw 'Engine or framework classes leaked into startup component'}}finally{$archive.Dispose()}
Get-FileHash -LiteralPath $asset -Algorithm SHA256 | Select-Object Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'jar-sha256.json')
'PASS focused humor scenes, deterministic GL, preference/uninstall, prior owned-component transactions and payload boundary'
