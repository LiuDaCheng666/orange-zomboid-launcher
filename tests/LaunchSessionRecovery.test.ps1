param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/session-recovery'}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$fixtureDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ozl-session-test-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureDirectory)|Out-Null
$output=Join-Path $fixtureDirectory 'SessionRecoveryTests.exe'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$sources+=Join-Path $project 'LaunchSessionRecoveryTests.cs'
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
# A signed-asset marker with no installation bundle reproduces a cached EXE's
# missing-assets boundary. Recovery must validate owned names without asset reads.
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.LaunchSessionRecoveryTests "/out:$output" "/resource:$repo/release-public-key.xml,Launcher.UpdatePublicKey.xml" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Session recovery compile failed'}
& $output 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'recovery.log')
if($LASTEXITCODE -ne 0){throw 'Session recovery fixtures failed'}
