param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/snapshot-original-order/native'}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-environment-controller-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$agent=Join-Path $fixture 'OrangeEnvironment.jar'
& (Join-Path $repo 'Build-EnvironmentComponent.ps1') -OutputPath $agent
if($LASTEXITCODE -ne 0){throw 'Isolated environment agent build failed'}
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
foreach($name in @('SaveEnvironments.cs','SaveEnvironmentRuntime.cs','SnapshotPreparation.cs','SaveEnvironmentController.cs','SaveEnvironmentControllerTests.cs','LauncherTestPaths.cs')){$path=Join-Path $project $name;if(Test-Path -LiteralPath $path){$sources+=$path}}
$sources+=(Join-Path $PSScriptRoot 'SnapshotOriginalOrderNative.cs')
$sources=@($sources|Select-Object -Unique)
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$output=Join-Path $EvidenceDirectory 'SnapshotOriginalOrderNative.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SnapshotOriginalOrderNative "/out:$output" "/resource:$agent,OrangeEnvironment.jar" "/resource:$project/Assets/OrangeStartup.jar,OrangeStartup.jar" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Environment controller compilation failed'}
node (Join-Path $PSScriptRoot 'SnapshotOriginalOrderNative.cjs') $EvidenceDirectory
if($LASTEXITCODE -ne 0){throw 'Original order native UI failed'}
