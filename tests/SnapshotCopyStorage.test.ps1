param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/snapshot-copy-storage'}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-copy-storage-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
[IO.File]::WriteAllText((Join-Path $fixture '.owner'),'OrangeLauncher isolated copy-storage regression; no real game or save data.')
$output=Join-Path $fixture 'SnapshotCopyStorageTests.exe'
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'SnapshotCopyStorageTests.cs')
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SnapshotCopyStorageTests "/out:$output" $refs $sources 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Snapshot copy-storage regression compilation failed'}
$data=Join-Path $fixture '中文隔离验收'
& $output $data 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'results.log')
$code=$LASTEXITCODE
if(Test-Path -LiteralPath (Join-Path $data 'regression-results.json')){Copy-Item -LiteralPath (Join-Path $data 'regression-results.json') -Destination (Join-Path $EvidenceDirectory 'regression-results.json')}
[IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'fixture-path.txt'),$fixture)
if($code -ne 0){throw "Snapshot copy-storage regression failed (exit $code); owned files retained: $fixture"}
Write-Output "PASS isolated native files retained: $fixture"
