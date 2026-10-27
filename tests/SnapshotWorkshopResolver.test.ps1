param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/snapshot-resolver-'+[guid]::NewGuid().ToString('N'))}
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$sources=@((Join-Path $repo 'Orange3DTool/SnapshotWorkshopResolverTests.cs'))
$implementation=Join-Path $repo 'Orange3DTool/SnapshotWorkshopResolver.cs'
if(Test-Path -LiteralPath $implementation){$sources+=$implementation}
$output=Join-Path $EvidenceDirectory 'ResolverTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe "/out:$output" /reference:System.Web.Extensions.dll $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Resolver compilation failed'}
& $output 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'tests.log')
if($LASTEXITCODE -ne 0){throw 'Resolver tests failed'}
