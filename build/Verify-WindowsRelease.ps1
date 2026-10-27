param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[string]$AuthenticodeConfig='',[Parameter(Mandatory=$true)][string]$EvidencePath,[switch]$AllowUnsignedRelease)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Authenticode.ps1')
. (Join-Path $PSScriptRoot 'Verify-VelopackArchives.ps1')
$release=[IO.Path]::GetFullPath($ReleaseDirectory)
$feed=Get-Content -LiteralPath (Join-Path $release 'velopack-manifest.json') -Raw | ConvertFrom-Json
if($feed.version -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'){throw 'Invalid release version'}
$context=$null
if(-not $AllowUnsignedRelease){$context=Get-OzlAuthenticodeContext -ConfigPath $AuthenticodeConfig -VerifyOnly}
$leases=@()
try{
 foreach($item in Get-ChildItem -LiteralPath $release -File){$leases+=[IO.File]::Open($item.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)}
 Test-OzlVelopackArchives -ReleaseDirectory $release -PublicKey (Join-Path (Split-Path $PSScriptRoot -Parent) 'release-public-key.xml') -Version $feed.version
 if($AllowUnsignedRelease){
  $files=@(Get-ChildItem -LiteralPath $release -File | Sort-Object Name | ForEach-Object {[ordered]@{name=$_.Name;length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
  $report=[ordered]@{schema=1;status='NotSigned';explicitUnsignedPublication=$true;archiveBootstrapVerified=$true;version=$feed.version;artifacts=$files}
  [IO.File]::WriteAllText([IO.Path]::GetFullPath($EvidencePath),($report|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
  'PASS authenticated archive inventory; Windows publisher signature is NotSigned (explicit publication choice)'
 }else{
  Assert-OzlSignedRelease -Context $context -ReleaseDirectory $release -Version $feed.version -EvidencePath $EvidencePath | Out-Null
  'PASS final Windows publisher signatures, timestamps and archive bootstrap inventory'
 }
}finally{foreach($lease in $leases){$lease.Dispose()}}
