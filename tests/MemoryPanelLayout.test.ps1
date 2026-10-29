param([string]$RunTag='current')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
& (Join-Path $repo 'Build-StartupComponent.ps1') -JdkPath 'C:/Program Files/Microsoft/jdk-21.0.12.101-hotspot' -GameDirectory 'E:/Steam/steamapps/common/ProjectZomboid'
$output=Join-Path $repo "artifacts/memory-panel-1240/$RunTag"
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
$asset=Join-Path $repo 'Orange3DTool/Assets/OrangeStartup.jar'
$inputs=@((Get-ChildItem -LiteralPath (Join-Path $repo 'Orange3DTool/StartupSource') -Filter '*.java' -Recurse -File).FullName)+@($asset,$PSCommandPath,(Join-Path $PSScriptRoot 'java/startup/MemoryPanelLayoutTest.java'),(Join-Path $PSScriptRoot 'java/startup/SceneProbe.java'),(Join-Path $PSScriptRoot 'java/startup/LoadingVisualRefinementTest.java'))
$binding=@($inputs|ForEach-Object{@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})
& javac -encoding UTF-8 --release 17 -cp $asset -d $classes (Join-Path $PSScriptRoot 'java/startup/SceneProbe.java') (Join-Path $PSScriptRoot 'java/startup/MemoryPanelLayoutTest.java') (Join-Path $PSScriptRoot 'java/startup/LoadingVisualRefinementTest.java')
if($LASTEXITCODE -ne 0){throw 'Memory panel test compilation failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.MemoryPanelLayoutTest $output (Join-Path $repo 'Orange3DTool/Assets/orange-app.png') 2>&1|Tee-Object (Join-Path $output 'results.log')
if($LASTEXITCODE -ne 0){throw 'Memory panel scene contract failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.LoadingVisualRefinementTest layout 2>&1|Tee-Object (Join-Path $output 'related-layout.log')
if($LASTEXITCODE -ne 0){throw 'Existing loading layout contract failed'}
& java '-Djava.awt.headless=true' -cp ($classes+';'+$asset) ozl.startup.tests.SceneProbe (Join-Path $output 'related-scenes') (Join-Path $repo 'Orange3DTool/Assets/orange-app.png') 2>&1|Tee-Object (Join-Path $output 'related-scenes.log')
if($LASTEXITCODE -ne 0){throw 'Existing 640-to-4K loading scene viewport/frame checks failed'}
foreach($inputFile in $binding){if((Get-FileHash -LiteralPath $inputFile.path -Algorithm SHA256).Hash -ne $inputFile.sha256){throw 'Memory scene input changed during acceptance'}}
@{scope='Production scene offscreen raster, no game or GL window';inputs=$binding;verifiedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'source.json') -Encoding utf8
