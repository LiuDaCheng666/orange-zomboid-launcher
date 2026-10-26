param([switch]$SetupOnly)
$ErrorActionPreference = 'Stop'
$servicePath = Join-Path $PSScriptRoot 'server.py'
$privatePath = Join-Path $env:LOCALAPPDATA 'OrangeLauncher\recommendations-local'
$serviceArguments = @($servicePath, '--private-dir', $privatePath, '--port', '18083')
if ($SetupOnly) { $serviceArguments += '--setup-only' }
& python @serviceArguments
if ($LASTEXITCODE -ne 0) { throw 'Local recommendation service failed to start.' }
