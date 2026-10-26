param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path ([IO.Path]::GetTempPath()) 'OrangeLauncher-SaveDeletionGuards'}
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$output=Join-Path $evidence 'SaveDeletionTests.exe'
function Assert-NoDetection {
    param([switch]$AllowMissing)
    $hits=@(Get-MpThreatDetection|Where-Object{ @($_.Resources|Where-Object{$_ -like "*$output*"}).Count -gt 0 })
    if($hits.Count){throw 'Defender detected this test artifact. Execution stopped; do not rename, restore or rebuild to evade detection.'}
    if(-not $AllowMissing -and -not (Test-Path -LiteralPath $output -PathType Leaf)){throw 'Save-deletion guard artifact missing; execution stopped.'}
}
Assert-NoDetection -AllowMissing
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
foreach($name in @('SaveDeletionTests.cs','QuietLaunch128Tests.cs','LauncherTestPaths.cs')){$sources+=Join-Path $project $name}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.SaveDeletionTests "/out:$output" "/resource:$project/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" $refs $sources 2>&1|Tee-Object (Join-Path $evidence 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Save-deletion guard test compilation failed'}
Assert-NoDetection
Start-MpScan -ScanType CustomScan -ScanPath $output -ErrorAction Stop -WarningAction SilentlyContinue -WarningVariable scanWarnings
if($scanWarnings){throw "Defender scan did not complete without warnings: $($scanWarnings -join '; ')"}
Assert-NoDetection
Get-FileHash -LiteralPath $output -Algorithm SHA256|Format-List|Out-File -LiteralPath (Join-Path $evidence 'artifact.txt')
$stdout=Join-Path $evidence 'guards.log'
$stderr=Join-Path $evidence 'guards.error.txt'
$child=Start-Process -FilePath $output -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
if(-not $child.WaitForExit(60000)){throw "Save-deletion guard test still running as PID $($child.Id); inspect logs before further execution."}
$child.WaitForExit()
Get-Content -LiteralPath $stdout
if((Get-Item -LiteralPath $stderr).Length){Get-Content -LiteralPath $stderr}
Assert-NoDetection
if($child.ExitCode -ne 0){throw "Save-deletion guard tests failed with exit $($child.ExitCode)"}
