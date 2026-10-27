param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop';$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/save-profiles-noop-'+[guid]::NewGuid().ToString('N'))};[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$project=Join-Path $repo 'Orange3DTool';$sources=@(Get-OzlProductionSources -ProjectDirectory $project)+@('SaveProfilesNoopTests.cs','SaveProfilesTests.cs','LauncherTestPaths.cs'|ForEach-Object{Join-Path $project $_})
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462';$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"};$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$binary=Join-Path $EvidenceDirectory 'SaveProfilesNoopTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /platform:x64 /target:exe /main:PZ3D.SaveProfilesNoopTests "/out:$binary" "/resource:$project/Assets/OrangeStartup.jar,OrangeStartup.jar" "/resource:$project/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log');if($LASTEXITCODE -ne 0){throw 'Save no-op regression compilation failed'}
& $binary 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'results.log');if($LASTEXITCODE -ne 0){throw 'Save no-op regression failed'}
