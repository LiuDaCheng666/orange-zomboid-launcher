$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$out=Join-Path $repo 'artifacts/java-workflow-ui'
[IO.Directory]::CreateDirectory($out)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-java-ui-bin-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)+(Join-Path $project 'JavaComponentWorkflowTests.cs')+(Join-Path $PSScriptRoot 'JavaWorkflowNativeUi.cs')
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.JavaWorkflowNativeUi "/out:$bin/NativeUi.exe" $refs $sources 2>&1 | Tee-Object -FilePath "$out/compile.log"
if($LASTEXITCODE -ne 0){throw 'native UI compilation failed'}
Set-Content "$out/runtime-path.txt" $bin
node (Join-Path $PSScriptRoot 'JavaWorkflowNativeUi.cjs')
if($LASTEXITCODE -ne 0){throw 'native UI clicks failed'}
