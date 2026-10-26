$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo 'artifacts\loading-humor-variety-20261027'
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
$source=Join-Path $repo 'Orange3DTool\StartupSource\ozl\startup'
$sources=@('LoadingModel.java','LoadingHumor.java','LoadingScene.java','LogoGrowth.java','WorkshopScene.java') | ForEach-Object {Join-Path $source $_}
$sources+=@('LoadingHumorRandomTest.java','LoadingVisualRefinementTest.java','SceneContractTest.java','SceneProbe.java','HumorSceneTest.java') | ForEach-Object {Join-Path $PSScriptRoot ('java\startup\'+$_)}
& javac -encoding UTF-8 --release 17 -d $classes $sources
if($LASTEXITCODE -ne 0){throw 'Focused humor source compilation failed'}
function Run-Checked([string[]]$Arguments,[string]$Log){
    & java '-Djava.awt.headless=true' '-cp' $classes @Arguments 2>&1 | Tee-Object -FilePath (Join-Path $output $Log)
    if($LASTEXITCODE -ne 0){throw "Focused humor verification failed: $Log"}
}
Run-Checked @('ozl.startup.LoadingHumorRandomTest') 'shuffle.log'
Run-Checked @('ozl.startup.LoadingVisualRefinementTest','humor') 'timing.log'
Run-Checked @('ozl.startup.tests.HumorSceneTest',(Join-Path $output 'raster'),(Join-Path $repo 'Orange3DTool\Assets\orange-app.png')) 'scene.log'
'PASS focused headless humor variety, timing, layout and reduced-motion checks'
