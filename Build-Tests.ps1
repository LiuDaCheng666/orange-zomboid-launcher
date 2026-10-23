param([string]$Entry='PZ3D.DiagnosticProgram',[string]$Output='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build\ProductionSources.ps1')
& (Join-Path $PSScriptRoot 'Prepare-Dependencies.ps1')
if(-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool\Orange3D.Bundle.zip'))){& (Join-Path $PSScriptRoot 'Prepare-Assets.ps1')}
if(-not $Output){$Output=Join-Path $PSScriptRoot 'artifacts\regression.exe'}
New-Item -ItemType Directory -Path (Split-Path -Parent $Output) -Force | Out-Null
$project=Join-Path $PSScriptRoot 'Orange3DTool'
$sources=@(Get-OzlDiagnosticSources -ProjectDirectory $project)
if($Entry -eq 'PZ3D.CoreReleaseTool'){$sources+=Join-Path $project 'CoreReleaseTool.cs'}
$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2\lib\net462'
$versionAttributes = & (Join-Path $PSScriptRoot 'Build-Version.ps1')
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /define:OZL_DIAGNOSTICS /target:exe "/main:$Entry" "/out:$Output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" "/resource:$project\Assets\OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" "/resource:$project\Orange3D.Bundle.zip,Orange3D.Bundle.zip" "/resource:$project\Assets\OrangeShootPresentation.jar,OrangeShootPresentation.jar" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" $versionAttributes $sources
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
