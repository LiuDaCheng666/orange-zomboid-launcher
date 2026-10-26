param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/guide'}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('og-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
foreach($name in @('GuideController.cs','GuideControllerTests.cs','LauncherTestPaths.cs')){$file=Join-Path $project $name;if(Test-Path -LiteralPath $file){$sources+=$file}}
$sources=@($sources|Select-Object -Unique)
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$output=Join-Path $fixture 'GuideControllerTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.GuideControllerTests "/out:$output" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Guide compilation failed'}
& $output 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'native.log')
if($LASTEXITCODE -ne 0){throw 'Guide native tests failed'}
