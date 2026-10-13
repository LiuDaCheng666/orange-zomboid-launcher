$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Orange3DTool/Build-Modern.ps1')
$exe=Join-Path $PSScriptRoot 'Orange3DTool/橙子3D一键启用工具-现代界面.exe'
$output=Join-Path $PSScriptRoot ('artifacts/legacy-start-'+[Guid]::NewGuid().ToString('N')+'.png')
$process=Start-Process -FilePath $exe -ArgumentList @('--modern-preview',('"'+$output+'"'),'--live-read','--page','help') -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(45000)){throw 'Legacy UI startup timed out'}
if($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $output)){throw ('Actual default legacy UI failed: '+$output+'.error.txt')}
$version=(Get-Content -Raw (Join-Path $PSScriptRoot 'VERSION')).Trim()
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
if($info.FileVersion -ne "$version.0" -or $info.ProductVersion -ne $version){throw 'New legacy executable metadata differs from VERSION'}
Write-Output "PASS: actual default legacy native UI/service and version metadata; $output"
