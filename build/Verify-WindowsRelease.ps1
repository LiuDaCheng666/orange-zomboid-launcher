param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[Parameter(Mandatory=$true)][string]$AuthenticodeConfig,[Parameter(Mandatory=$true)][string]$EvidencePath)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Authenticode.ps1')
. (Join-Path $PSScriptRoot 'Verify-VelopackArchives.ps1')
$release=[IO.Path]::GetFullPath($ReleaseDirectory)
$feed=Get-Content -LiteralPath (Join-Path $release 'velopack-manifest.json') -Raw | ConvertFrom-Json
if($feed.version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'Invalid release version'}
$context=Get-OzlAuthenticodeContext -ConfigPath $AuthenticodeConfig -VerifyOnly
Test-OzlVelopackArchives -ReleaseDirectory $release -PublicKey (Join-Path (Split-Path $PSScriptRoot -Parent) 'release-public-key.xml') -Version $feed.version
Assert-OzlSignedRelease -Context $context -ReleaseDirectory $release -Version $feed.version -EvidencePath $EvidencePath | Out-Null
'PASS final Windows publisher signatures, timestamps and archive bootstrap inventory'
