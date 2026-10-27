param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/snapshot-long-path-'+[guid]::NewGuid().ToString('N'))}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$sources=@((Join-Path $repo 'Orange3DTool/LauncherRuntime.cs'),(Join-Path $repo 'Orange3DTool/SnapshotFileSystemTests.cs'))
$implementation=Join-Path $repo 'Orange3DTool/SnapshotFileSystem.cs';if(Test-Path -LiteralPath $implementation){$sources+=$implementation}
$binary=Join-Path $EvidenceDirectory 'SnapshotFileSystemTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SnapshotFileSystemTests "/win32manifest:$repo/Orange3DTool/app.manifest" "/out:$binary" $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Long-path adapter compilation failed'}
& $binary 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'results.log')
if($LASTEXITCODE -ne 0){throw 'Long-path adapter regression failed'}
Write-Host "PASS long-path adapter; evidence $EvidenceDirectory"
