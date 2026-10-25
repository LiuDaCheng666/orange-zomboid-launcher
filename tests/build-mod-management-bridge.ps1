$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$sources+=Join-Path $PSScriptRoot 'ModManagementNativeBridge.cs'
$output=Join-Path $repo 'artifacts/mod-workshop-repair/ModManagementNativeBridge.exe'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /main:PZ3D.ModSaveNativeFixture "/out:$output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" "/resource:$project\Assets\OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" "/resource:$project\Assets\OrangeShootPresentation.jar,OrangeShootPresentation.jar" "/resource:$project\Assets\OrangeStartup.jar,OrangeStartup.jar" "/resource:$repo\VERSION,Launcher.Version.txt" $sources
if($LASTEXITCODE -ne 0){throw 'Isolated native fixture compilation failed'}
