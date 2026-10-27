$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo ('artifacts/live-environment-'+[guid]::NewGuid().ToString('N'))
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force|Out-Null
$agent=Join-Path $output 'OrangeLive.jar'
if(Test-Path (Join-Path $repo 'Build-LiveComponent.ps1')){& (Join-Path $repo 'Build-LiveComponent.ps1') -OutputPath $agent}
& javac -encoding UTF-8 --release 8 -Xlint:-options -d $classes (Join-Path $PSScriptRoot 'java/live/LiveManifestTest.java')
if($LASTEXITCODE -ne 0){throw 'Live test compilation failed'}
& java -cp ($classes+';'+$agent) ozl.environment.LiveManifestTest (Join-Path $output 'fixture')
if($LASTEXITCODE -ne 0){throw 'Live manifest assertions failed'}
