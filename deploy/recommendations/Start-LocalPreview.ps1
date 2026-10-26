$ErrorActionPreference = 'Stop'
$bundleRoot = $PSScriptRoot
$appDirectory = Join-Path $bundleRoot 'app'
$serviceScript = Join-Path $bundleRoot 'recommendations/server.py'
$pythonCommand = Get-Command python -ErrorAction Stop
$privateDirectory = Join-Path $env:LOCALAPPDATA 'OrangeLauncher/recommendations-local'
$logDirectory = Join-Path $privateDirectory 'logs'
[IO.Directory]::CreateDirectory($logDirectory) | Out-Null
function Test-LocalFeed {
    try {
        $feed = Invoke-RestMethod -Uri 'http://127.0.0.1:18083/api/recommended-servers' -TimeoutSec 2
        return $feed.schema -eq 1 -and $null -ne $feed.servers -and $null -ne $feed.revision
    } catch { return $false }
}
if (-not (Test-LocalFeed)) {
    $arguments = @(('"' + $serviceScript + '"'), '--private-dir', ('"' + $privateDirectory + '"'), '--port', '18083')
    $service = Start-Process -FilePath $pythonCommand.Source -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDirectory 'local-preview.stdout.log') -RedirectStandardError (Join-Path $logDirectory 'local-preview.stderr.log')
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if (Test-LocalFeed) { $ready = $true; break }
        $service.Refresh()
        if ($service.HasExited) { break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw 'The local recommendation service did not start. Check its log files in LocalAppData/OrangeLauncher/recommendations-local/logs.' }
}
$launchers = @(Get-ChildItem -LiteralPath $appDirectory -File -Filter '*.exe')
if ($launchers.Count -ne 1) { throw 'The local preview package must contain exactly one launcher executable.' }
Start-Process -FilePath $launchers[0].FullName -WorkingDirectory $appDirectory
