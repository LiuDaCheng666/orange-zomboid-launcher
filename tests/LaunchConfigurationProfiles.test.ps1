$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$out=Join-Path ([IO.Path]::GetTempPath()) ('ozl-profiles-bin-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($out)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'LaunchConfigurationProfilesTests.cs')
$flags=@()
if(Test-Path (Join-Path $repo 'Orange3DTool/LaunchConfigurationProfiles.cs')){$flags+='/define:PROFILES_IMPLEMENTED';if(-not($sources -match 'LaunchConfigurationProfiles.cs')){$sources+=Join-Path $repo 'Orange3DTool/LaunchConfigurationProfiles.cs'}}
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.LaunchConfigurationProfilesTests $refs "/out:$out/Test.exe" $flags $sources
if($LASTEXITCODE -ne 0){throw 'Profiles fixture compilation failed'}
& "$out/Test.exe"
if($LASTEXITCODE -ne 0){throw 'Profiles fixture assertions failed'}
