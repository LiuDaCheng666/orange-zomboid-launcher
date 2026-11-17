param([Parameter(Mandatory=$true)][string]$EvidenceDirectory,[string]$ProductionCandidate='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$evidence=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+@(Join-Path $PSScriptRoot 'GameGraphicsPreferenceTests.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
$binary=Join-Path $evidence 'GameGraphicsPreferenceTests.exe'
$fixture=Join-Path $evidence 'GpuPreferenceProcessFixture.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /platform:x64 /target:exe /main:PZ3D.GameGraphicsPreferenceTests "/out:$binary" $refs $sources 2>&1|Tee-Object (Join-Path $evidence 'compile.log')
if($LASTEXITCODE -ne 0){throw 'GPU preference regression compilation failed'}
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /platform:x64 /target:exe "/out:$fixture" (Join-Path $PSScriptRoot 'GpuPreferenceProcessFixture.cs')
if($LASTEXITCODE -ne 0){throw 'GPU preference child compilation failed'}
$prior=$env:OZL_GPU_PROCESS_FIXTURE
try{
 $env:OZL_GPU_PROCESS_FIXTURE=$fixture
 $arguments=@($evidence)
 if($ProductionCandidate){$arguments+=[IO.Path]::GetFullPath($ProductionCandidate)}
 & $binary @arguments 2>&1|Tee-Object (Join-Path $evidence 'results.log')
 if($LASTEXITCODE -ne 0){throw 'GPU preference regression assertions failed'}
}finally{$env:OZL_GPU_PROCESS_FIXTURE=$prior}
