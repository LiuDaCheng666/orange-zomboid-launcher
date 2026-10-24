param([string]$ReleaseDirectory)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not(Test-Path "$taskRoot/Build-VelopackRelease.ps1")){throw 'RED: Velopack dual-artifact release entry is missing'}
if(-not $ReleaseDirectory){throw 'Supply the fresh final release directory for artifact validation'}
. "$taskRoot/build/Verify-VelopackArchives.ps1"
Test-OzlVelopackArchives -ReleaseDirectory $ReleaseDirectory -PublicKey "$taskRoot/release-public-key.xml" -Version ([IO.File]::ReadAllText("$taskRoot/VERSION").Trim())
$fixture=Join-Path $taskRoot ('artifacts/velopack-archive-negative-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $fixture | Out-Null
$version=[IO.File]::ReadAllText("$taskRoot/VERSION").Trim()
$names=@("OrangeZomboidLauncher-$version-full.nupkg",'OrangeZomboidLauncher-win-Portable.zip')
foreach($name in $names){Copy-Item -LiteralPath (Join-Path $ReleaseDirectory $name) -Destination (Join-Path $fixture $name)}
try {
 $archive=[IO.Compression.ZipFile]::Open((Join-Path $fixture $names[1]),[IO.Compression.ZipArchiveMode]::Update)
 try{$entry=$archive.CreateEntry('current/unlisted-private.xml'); $stream=$entry.Open(); try{$stream.WriteByte(1)}finally{$stream.Dispose()}}finally{$archive.Dispose()}
 $rejected=$false
 try{Test-OzlVelopackArchives -ReleaseDirectory $fixture -PublicKey "$taskRoot/release-public-key.xml" -Version $version}catch{if($_.Exception.Message -notlike 'Unexpected packaged file*'){throw};$rejected=$true}
 if(-not $rejected){throw 'RED: unknown packaged file was accepted'}
} finally {
 foreach($name in $names){Remove-Item -LiteralPath (Join-Path $fixture $name)}
 Remove-Item -LiteralPath $fixture
}
'PASS actual full/portable bootstrap signatures, exact shipped inventory and absence of legacy auxiliaries'
