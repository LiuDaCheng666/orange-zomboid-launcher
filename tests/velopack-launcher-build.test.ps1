param([Parameter(Mandatory=$true)][string]$SigningKey)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not(Test-Path "$taskRoot/Build-VelopackLauncher.ps1")){throw 'RED: single-launcher authenticated build entry is missing'}
$output=Join-Path $taskRoot ('artifacts/velopack-launcher-'+[Guid]::NewGuid().ToString('N'))
$build=@(& "$taskRoot/Build-VelopackLauncher.ps1" -SigningKey $SigningKey -OutputDirectory $output)
if($build.Count -ne 1 -or $build[0].PackDirectory -ne $output -or $build[0].MainExecutable -ne '橙子启动器.exe'){throw 'Build entry must return exactly one packaging contract object'}
if(@(Get-ChildItem $output -Filter '*.exe').Count -ne 1){throw 'Build must contain only launcher EXE'}
$testExe=Join-Path $taskRoot 'artifacts/velopack-native-tests/tests.exe'
& $testExe --bootstrap $output (Join-Path $taskRoot 'release-public-key.xml')
if($LASTEXITCODE){throw 'Native bootstrap verification failed'}
$cache=Join-Path $taskRoot ('artifacts/velopack-worker-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $cache | Out-Null
$worker=Join-Path $cache '橙子启动器.exe'
Copy-Item (Join-Path $output '橙子启动器.exe') $worker
$process=Start-Process -FilePath $worker -ArgumentList @('--session-watch','invalid','invalid') -WindowStyle Hidden -PassThru -Wait
if($process.ExitCode -ne 1){throw "Copied monitor entry did not dispatch without SDK: $($process.ExitCode)"}
$lease=Start-Process -FilePath $worker -ArgumentList @('--velopack-lease','invalid','-1') -WindowStyle Hidden -PassThru -Wait
if($lease.ExitCode -ne 1){throw "Copied lease entry did not dispatch without SDK: $($lease.ExitCode)"}
"PASS signed launcher-only build and SDK-free worker dispatch: $output"
