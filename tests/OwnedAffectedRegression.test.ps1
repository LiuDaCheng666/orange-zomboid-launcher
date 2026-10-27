param([string[]]$Groups=@('ModernHostTests','LaunchProfilesTests','GameProcessGuardTests','ModConfigurationsTests','LaunchTransportTests','ConfigurationRuntimeTests','GameStartupControlsTests'))
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$out=Join-Path ([IO.Path]::GetTempPath()) ('o4-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($out)|Out-Null
$sources=@(Get-OzlDiagnosticSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'OwnedAffectedRegressionTests.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_DIAGNOSTICS /target:exe /main:PZ3D.OwnedAffectedRegressionTests $refs "/out:$out/Test.exe" $sources
if($LASTEXITCODE -ne 0){throw 'Owned entry fixture compilation failed'}
& "$out/Test.exe" @Groups | Tee-Object -FilePath "$out/results.log"
if($LASTEXITCODE -ne 0){throw 'Affected owned launch regression failed'}

