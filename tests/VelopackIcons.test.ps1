param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[string]$Version,[string]$Evidence)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not $Version){$Version=[IO.File]::ReadAllText((Join-Path $taskRoot 'VERSION')).Trim()}
. (Join-Path $taskRoot 'build/Verify-VelopackIcons.ps1')
$taskResults=@(Test-OzlVelopackIcons -ReleaseDirectory $ReleaseDirectory -Icon (Join-Path $taskRoot 'Orange3DTool/Assets/orange-app.ico') -Version $Version)
if($Evidence){$taskResults|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $Evidence -Encoding UTF8}
Write-Output ('PASS '+$taskResults.Count+' actual setup, portable and update-package executable icons match the application brand')
