param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'artifacts\server-launch-feedback'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
. (Join-Path $repo 'build\ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$sources+=Join-Path $PSScriptRoot 'ComponentVersionDiagnosticsTests.cs'
$sdk=Join-Path $repo 'Dependencies\WebView2\lib\net462'
$exe=Join-Path $OutputDirectory 'component-version-diagnostics.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe /main:PZ3D.ComponentVersionDiagnosticsTests "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" $sources
if($LASTEXITCODE -ne 0){throw 'Diagnostic compilation failed'}
& $exe (Join-Path $OutputDirectory 'isolated-components')
if($LASTEXITCODE -ne 0){throw 'Native version diagnostics failed'}
$prior=$env:OZL_TEST_ARTIFACT_ROOT
try{$env:OZL_TEST_ARTIFACT_ROOT=$OutputDirectory;& node (Join-Path $project 'Web\server-launch-feedback-ui-tests.cjs');if($LASTEXITCODE -ne 0){throw 'Server launch feedback UI tests failed'}}finally{$env:OZL_TEST_ARTIFACT_ROOT=$prior}
