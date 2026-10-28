param([string]$RunTag='current')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$evidence=Join-Path $repo 'artifacts/support-configuration-20261030'
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('o6-support-bin-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'UnifiedConfigurationNative.cs')
$inputs=@($sources)+@('Orange3DTool/Web/app.js','Orange3DTool/Web/app.css','Orange3DTool/Web/index.html','Orange3DTool/Web/launch-configurations-module.inc.js','Orange3DTool/Assets/OrangeStartup.jar','Orange3DTool/Assets/OrangeLaunchBridge.lua'|ForEach-Object{Join-Path $repo $_})
$receipt=@($inputs|ForEach-Object{@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.UnifiedConfigurationNative "/out:$bin/NativeUi.exe" $refs $sources 2>&1|Tee-Object (Join-Path $evidence "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Native support fixture compilation failed'}
Write-Output "Native fixture: $bin"
& node (Join-Path $PSScriptRoot 'SupportConfigurationNative.cjs') $bin 2>&1|Tee-Object (Join-Path $evidence "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Native support UI/file acceptance failed'}
foreach($inputFile in $receipt){if((Get-FileHash -LiteralPath $inputFile.path -Algorithm SHA256).Hash -ne $inputFile.sha256){throw "Test input changed during acceptance: $($inputFile.path)"}}
@{nativeBin=$bin;inputs=$receipt;verifiedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $evidence "$RunTag-inputs.json") -Encoding utf8
