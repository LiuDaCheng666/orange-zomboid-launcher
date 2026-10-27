param([switch]$Packaged)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$out=Join-Path ([IO.Path]::GetTempPath()) ('ozl-components-bin-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($out)|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'LauncherComponentsTests.cs')
$flags=@()
if(Test-Path (Join-Path $repo 'Orange3DTool/LauncherComponents.cs')){$flags+='/define:COMPONENTS_IMPLEMENTED';if(-not($sources -match 'LauncherComponents.cs')){$sources+=Join-Path $repo 'Orange3DTool/LauncherComponents.cs'}}
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
if($Packaged){$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar";$flags=@('/define:COMPONENTS_IMPLEMENTED;PACKAGED_LIVE')}
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.LauncherComponentsTests $refs "/out:$out/Test.exe" $flags $sources
if($LASTEXITCODE -ne 0){throw 'Components fixture compilation failed'}
& "$out/Test.exe"
if($LASTEXITCODE -ne 0){throw 'Components fixture assertions failed'}
