param([string]$RunTag='current',[string]$WebRoot='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$evidence=Join-Path $repo 'artifacts/support-long-paths-ui-1240'
if($WebRoot){$evidence=Join-Path $evidence $RunTag}
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('o6-support-bin-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
# Only the test synchronization point changes: hold after both deep files physically exist.
$fixtureInput=Join-Path $PSScriptRoot 'UnifiedConfigurationNative.cs'
$fixtureGenerated=Join-Path $bin 'UnifiedConfigurationNative.SupportLongPaths.cs'
$fixtureText=[IO.File]::ReadAllText($fixtureInput)
$holdBefore='if(holdSupportProgress&&t.StartsWith("正在复制 ZombieBuddy",StringComparison.Ordinal))'
$holdAfter='if(holdSupportProgress&&t.StartsWith("正在复制 ZombieBuddy",StringComparison.Ordinal)&&t.Contains(" · 3 / "))'
if(-not $fixtureText.Contains($holdBefore)){throw 'Support progress test synchronization point changed'}
[IO.File]::WriteAllText($fixtureGenerated,$fixtureText.Replace($holdBefore,$holdAfter),(New-Object Text.UTF8Encoding($false)))
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+$fixtureGenerated
$inputs=@($sources)+@($fixtureInput,$PSCommandPath,(Join-Path $PSScriptRoot 'SupportConfigurationNative.cjs'))+@('Orange3DTool/Web/app.js','Orange3DTool/Web/app.css','Orange3DTool/Web/index.html','Orange3DTool/Web/launch-configurations-module.inc.js','Orange3DTool/Assets/OrangeStartup.jar','Orange3DTool/Assets/OrangeLaunchBridge.lua'|ForEach-Object{Join-Path $repo $_})
if($WebRoot){$inputs+=@('app.js','app.css','index.html'|ForEach-Object{Join-Path $WebRoot $_})}
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
$previousWebRoot=$env:OZL_TEST_WEB_ROOT
try{if($WebRoot){$env:OZL_TEST_WEB_ROOT=$WebRoot};& node (Join-Path $PSScriptRoot 'SupportConfigurationNative.cjs') $bin $evidence 2>&1|Tee-Object (Join-Path $evidence "$RunTag-results.log");if($LASTEXITCODE -ne 0){throw 'Native support UI/file acceptance failed'}}finally{$env:OZL_TEST_WEB_ROOT=$previousWebRoot}
foreach($inputFile in $receipt){if((Get-FileHash -LiteralPath $inputFile.path -Algorithm SHA256).Hash -ne $inputFile.sha256){throw "Test input changed during acceptance: $($inputFile.path)"}}
@{nativeBin=$bin;generatedFixture=$fixtureGenerated;fixtureChange='Test-only hold after 3 copied files; production and original fixture unchanged';inputs=$receipt;verifiedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $evidence "$RunTag-inputs.json") -Encoding utf8
