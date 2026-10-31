param([string]$Agent='',[string]$GameDirectory='E:/Steam/steamapps/common/ProjectZomboid')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo ('artifacts/forced-chinese-runtime-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$classes=Join-Path $output 'classes'
[IO.Directory]::CreateDirectory($classes)|Out-Null
if(!$Agent){$Agent=Join-Path $output 'OrangeLive.jar'; & (Join-Path $repo 'Build-LiveComponent.ps1') -OutputPath $Agent}
& javac -encoding UTF-8 --release 8 -Xlint:-options -cp $Agent -d $classes (Join-Path $PSScriptRoot 'java/live/ForcedChineseTest.java') (Join-Path $PSScriptRoot 'java/live/ServerManifestTest.java')
if($LASTEXITCODE -ne 0){throw 'Forced Chinese runtime fixture compilation failed'}
foreach($fixture in @('ForcedChineseTest','ServerManifestTest')){
 & java -cp ($classes+';'+$Agent) ('ozl.environment.'+$fixture) (Join-Path $output $fixture) $GameDirectory
 if($LASTEXITCODE -ne 0){throw ($fixture+' assertions failed')}
}
