param([switch]$BuildOnly)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$outputDirectory=Join-Path $repo 'artifacts/save-workflow-integration'
[IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$output=Join-Path $outputDirectory 'SaveWorkflowIntegrationBridge.exe'
$agent=Join-Path $outputDirectory 'OrangeEnvironment.jar'
& (Join-Path $repo 'Build-EnvironmentComponent.ps1') -OutputPath $agent
if($LASTEXITCODE -ne 0){throw 'Integration environment agent build failed'}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$sources+=Join-Path $PSScriptRoot 'SaveWorkflowIntegrationBridge.cs'
$compiledSources=@($sources | ForEach-Object { $fileHash=Get-FileHash -LiteralPath $_ -Algorithm SHA256; [ordered]@{path=$fileHash.Path;sha256=$fileHash.Hash} })
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /main:PZ3D.SaveWorkflowIntegrationBridge "/out:$output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" "/resource:$project\Assets\OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" "/resource:$project\Assets\OrangeShootPresentation.jar,OrangeShootPresentation.jar" "/resource:$project\Assets\OrangeStartup.jar,OrangeStartup.jar" "/resource:$agent,OrangeEnvironment.jar" "/resource:$repo\VERSION,Launcher.Version.txt" $sources
if($LASTEXITCODE -ne 0){throw 'Integration bridge compilation failed'}
$hash=(Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
$scanStarted=Get-Date
Start-MpScan -ScanType CustomScan -ScanPath $output -ErrorAction Stop
$detections=@(Get-MpThreatDetection -ErrorAction Stop | Where-Object {
    $match=$false
    foreach($resource in $_.Resources){if($resource.IndexOf($output,[StringComparison]::OrdinalIgnoreCase) -ge 0){$match=$true}}
    $match
})
if($detections.Count -ne 0){throw 'Defender detected the integration executable. Execution stopped; do not rename or rebuild to evade detection.'}
if(-not (Test-Path -LiteralPath $output -PathType Leaf)){throw 'Scanned integration executable is no longer present; execution stopped.'}
if((Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash -ne $hash){throw 'Integration executable changed after scan; execution stopped.'}
[ordered]@{path=$output;sha256=$hash;scanStartedUtc=$scanStarted.ToUniversalTime().ToString('o');scanCompletedUtc=(Get-Date).ToUniversalTime().ToString('o');matchingDetections=$detections.Count;compiledSources=$compiledSources} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputDirectory 'defender-scan.json') -Encoding utf8
Write-Output "Defender custom scan completed: $hash"
if(-not $BuildOnly){& node (Join-Path $PSScriptRoot 'SaveWorkflowIntegration.cjs');if($LASTEXITCODE -ne 0){throw 'Native save workflow integration failed'}}
