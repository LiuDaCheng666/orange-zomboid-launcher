param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/mod-sorting'}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('om-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
foreach($name in @('SaveEnvironments.cs','SaveEnvironmentRuntime.cs','SaveEnvironmentController.cs','ModWorkshopController.cs','ModSortingTests.cs','ModConfigurationsTests.cs','SaveProfilesTests.cs','LauncherTestPaths.cs')){$path=Join-Path $project $name;if(Test-Path -LiteralPath $path){$sources+=$path}}
$sources=@($sources|Select-Object -Unique)
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$output=Join-Path $fixture 'ModSortingTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.ModSortingTests "/out:$output" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Mod sorting compilation failed'}
& $output 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'sorting.log')
if($LASTEXITCODE -ne 0){throw 'Mod sorting fixtures failed'}
