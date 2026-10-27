param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop';$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/snapshot-manifest-capacity-'+[guid]::NewGuid().ToString('N'))};[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+@(Join-Path $repo 'Orange3DTool/SnapshotManifestCapacityTests.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462';$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"};$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$binary=Join-Path $EvidenceDirectory 'SnapshotManifestCapacityTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /platform:x64 /target:exe /main:PZ3D.SnapshotManifestCapacityTests "/out:$binary" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log');if($LASTEXITCODE -ne 0){throw 'Manifest capacity test compilation failed'}
& $binary 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'results.log');if($LASTEXITCODE -ne 0){throw 'Manifest capacity roundtrip failed'}
