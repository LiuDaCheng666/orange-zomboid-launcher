param()
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$evidence=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/snapshot-preview-cancellation'))
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$output=Join-Path $evidence 'SnapshotPreviewCancellationTests.exe'
function Assert-NoDetection {
    param([switch]$AllowMissing)
    $hits=@(Get-MpThreatDetection|Where-Object{ @($_.Resources|Where-Object{$_ -like "*$output*"}).Count -gt 0 })
    if($hits.Count){throw 'Defender detected this test artifact; stop without renaming, restoring or rebuilding it.'}
    if(-not $AllowMissing -and -not (Test-Path -LiteralPath $output -PathType Leaf)){throw 'Preview cancellation test artifact missing.'}
}
Assert-NoDetection -AllowMissing
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)+(Join-Path $PSScriptRoot 'SnapshotPreviewCancellationTests.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.SnapshotPreviewCancellationTests "/out:$output" $refs $sources 2>&1|Tee-Object (Join-Path $evidence 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Preview cancellation test compilation failed'}
Assert-NoDetection
Start-MpScan -ScanType CustomScan -ScanPath $output -ErrorAction Stop -WarningAction SilentlyContinue -WarningVariable scanWarnings
if($scanWarnings){throw "Defender scan did not complete without warnings: $($scanWarnings -join '; ')"}
Assert-NoDetection
$stdout=Join-Path $evidence 'cancellation.log'
$stderr=Join-Path $evidence 'cancellation.error.txt'
$child=Start-Process -FilePath $output -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if(-not $child.WaitForExit(60000)){throw "Preview cancellation tests remain active as PID $($child.Id); inspect logs before rerunning."}
$child.WaitForExit()
Get-Content -LiteralPath $stdout
if((Get-Item -LiteralPath $stderr).Length){Get-Content -LiteralPath $stderr}
Assert-NoDetection
if($child.ExitCode -ne 0){throw "Preview cancellation tests failed with exit $($child.ExitCode)"}
