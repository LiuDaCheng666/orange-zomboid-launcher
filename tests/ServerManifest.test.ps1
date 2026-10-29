param([string]$Agent='',[string]$RunTag='current')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo 'artifacts/server-manifest'
[IO.Directory]::CreateDirectory($output)|Out-Null
$classes=Join-Path $output 'classes'
[IO.Directory]::CreateDirectory($classes)|Out-Null
if(!$Agent){$Agent=Join-Path $output 'OrangeLive.jar'; & (Join-Path $repo 'Build-LiveComponent.ps1') -OutputPath $Agent}
& javac -encoding UTF-8 --release 8 -Xlint:-options -cp $Agent -d $classes (Join-Path $PSScriptRoot 'java/live/ServerManifestTest.java') 2>&1|Tee-Object (Join-Path $output "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Server manifest probe compilation failed'}
& java -cp ($classes+';'+$Agent) ozl.environment.ServerManifestTest (Join-Path $output ('fixture-'+[guid]::NewGuid().ToString('N').Substring(0,8))) 2>&1|Tee-Object (Join-Path $output "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Server manifest assertions failed'}
