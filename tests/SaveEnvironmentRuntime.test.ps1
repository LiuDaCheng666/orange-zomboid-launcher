param([string]$EvidenceDirectory,[string[]]$TestArguments=@())
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/save-environment-runtime'}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-environment-runtime-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
$agent=Join-Path $fixture 'OrangeEnvironment.jar'
& (Join-Path $repo 'Build-EnvironmentComponent.ps1') -OutputPath $agent
if($LASTEXITCODE -ne 0){throw 'Isolated mandatory agent build failed'}
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
foreach($name in @('SaveEnvironments.cs','SaveEnvironmentRuntime.cs','SnapshotFileSystem.cs','SaveEnvironmentRuntimeTests.cs','LauncherTestPaths.cs')){$path=Join-Path $project $name;if(Test-Path -LiteralPath $path){$sources+=$path}}
$sources=@($sources|Select-Object -Unique)
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$output=Join-Path $fixture 'SaveEnvironmentRuntimeTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SaveEnvironmentRuntimeTests "/out:$output" "/resource:$agent,OrangeEnvironment.jar" "/resource:$project/Assets/OrangeStartup.jar,OrangeStartup.jar" "/resource:$project/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Frozen runtime test compilation failed'}
& $output @TestArguments 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'runtime.log')
if($LASTEXITCODE -ne 0){throw 'Frozen runtime fixtures failed'}
