param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/version-preserve-20261029'}
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-version-progress-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'VersionPreserveProgress.cs')
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.VersionPreserveProgress "/out:$bin/Progress.exe" $refs $sources 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'native-compile.log')
if($LASTEXITCODE -ne 0){throw 'Native progress regression compilation failed'}
& "$bin/Progress.exe" (Join-Path $bin '独立验收') 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'native-results.log')
if($LASTEXITCODE -ne 0){throw 'Native progress regression failed'}
# The official Steam workflow also uses PreserveChecked and must retain its validation.
$steamSources=$sources+@((Join-Path $repo 'Orange3DTool/SteamVersionWorkflowTests.cs'),(Join-Path $repo 'Orange3DTool/GameVersionProbeTests.cs'),(Join-Path $repo 'Orange3DTool/LauncherTestPaths.cs'))
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SteamVersionWorkflowTests "/out:$bin/Steam.exe" $refs $steamSources 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'steam-preserve-compile.log')
if($LASTEXITCODE -ne 0){throw 'Steam preservation regression compilation failed'}
& "$bin/Steam.exe" 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'steam-preserve-results.log')
if($LASTEXITCODE -ne 0){throw 'Steam preservation regression failed'}
