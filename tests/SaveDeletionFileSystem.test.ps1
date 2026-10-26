param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$evidence=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/save-deletion-filesystem'))
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$output=Join-Path $evidence 'SaveDeletionFileSystemTests.exe'
function Assert-NoDetection {
    param([switch]$AllowMissing)
    $hits=@(Get-MpThreatDetection|Where-Object{ @($_.Resources|Where-Object{$_ -like "*$output*"}).Count -gt 0 })
    if($hits.Count){throw 'Defender detected this test artifact. Execution stopped; do not rename, restore or rebuild to evade detection.'}
    if(-not $AllowMissing -and -not (Test-Path -LiteralPath $output -PathType Leaf)){throw 'Filesystem test artifact missing; execution stopped.'}
}
Assert-NoDetection -AllowMissing
if(-not $SkipBuild){
    $sources=@(Get-OzlProductionSources -ProjectDirectory $project)
    $sources+=Join-Path $PSScriptRoot 'SaveDeletionFileSystemTests.cs'
    $sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
    $refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
    $refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
    $refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
    & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.SaveDeletionFileSystemTests "/out:$output" $refs $sources 2>&1|Tee-Object (Join-Path $evidence 'compile.log')
    if($LASTEXITCODE -ne 0){throw 'Filesystem save-deletion test compilation failed'}
}
Assert-NoDetection
Start-MpScan -ScanType CustomScan -ScanPath $output -ErrorAction Stop -WarningAction SilentlyContinue -WarningVariable scanWarnings
if($scanWarnings){throw "Defender scan did not complete without warnings: $($scanWarnings -join '; ')"}
Assert-NoDetection
Get-FileHash -LiteralPath $output -Algorithm SHA256|Format-List|Out-File -LiteralPath (Join-Path $evidence 'artifact.txt')
$stdout=Join-Path $evidence 'filesystem.log'
$stderr=Join-Path $evidence 'filesystem.error.txt'
$child=Start-Process -FilePath $output -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if(-not $child.WaitForExit(60000)){throw "Filesystem test still running as PID $($child.Id); inspect its logs before any further execution."}
$child.WaitForExit()
Get-Content -LiteralPath $stdout
if((Get-Item -LiteralPath $stderr).Length){Get-Content -LiteralPath $stderr}
Assert-NoDetection
if($child.ExitCode -ne 0){throw "Filesystem save-deletion tests failed with exit $($child.ExitCode)"}
