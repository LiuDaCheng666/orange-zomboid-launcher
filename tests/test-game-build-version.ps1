param([string]$OutputDirectory='', [string]$ActualGame='', [string]$ExpectedVersion='42.22.2')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory){$OutputDirectory=Join-Path $repo 'artifacts\game-build-version'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
. (Join-Path $repo 'build\ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$sources+=Join-Path $PSScriptRoot 'GameBuildVersionTests.cs'
foreach($name in @('GameVersionProbeTests.cs','LauncherGameVersionTests.cs','LauncherTestPaths.cs')){$sources+=Join-Path $project $name}
$sdk=Join-Path $repo 'Dependencies\WebView2\lib\net462'
$exe=Join-Path $OutputDirectory 'build-version-tests.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /nowarn:0649 /target:exe /main:PZ3D.GameBuildVersionTests "/out:$exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" $sources
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
$testArgs=@((Join-Path $OutputDirectory 'isolated'))
if($ActualGame){$testArgs+=@($ActualGame,$ExpectedVersion)}
& $exe @testArgs
if($LASTEXITCODE -ne 0){throw 'Game build version regression failed'}
