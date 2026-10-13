param([switch]$PrivatePresets)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '..\Prepare-Dependencies.ps1')
& (Join-Path $PSScriptRoot '..\Prepare-Assets.ps1') -PrivatePresets:$PrivatePresets
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$dependencyRoot=Join-Path $PSScriptRoot '..\Dependencies\WebView2'
$core=Join-Path $dependencyRoot 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$forms=Join-Path $dependencyRoot 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
$loader=Join-Path $dependencyRoot 'runtimes\win-x64\native\WebView2Loader.dll'
$sources=(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs').FullName
$webBundle=Join-Path $PSScriptRoot 'Modern.Web.zip'
$webFiles=@('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt') | ForEach-Object {Join-Path $PSScriptRoot ('Web\'+$_)}
if($PrivatePresets -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Web\author-covers'))){$webFiles+=Join-Path $PSScriptRoot 'Web\author-covers'}
Compress-Archive -LiteralPath $webFiles -DestinationPath $webBundle -Force
$bundleName=if($PrivatePresets){'Orange3D.PrivateBundle.zip'}else{'Orange3D.Bundle.zip'}
$bundle=Join-Path $PSScriptRoot $bundleName
& $compiler /nologo /target:winexe "/resource:$PSScriptRoot\..\使用说明.md,Profiles.Guide.md" "/resource:$PSScriptRoot\Assets\OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" /main:PZ3D.Program /platform:x64 /optimize+ "/resource:$bundle,Orange3D.Bundle.zip" "/resource:$webBundle,Modern.Web.zip" "/resource:$core,Modern.Microsoft.Web.WebView2.Core.dll" "/resource:$forms,Modern.Microsoft.Web.WebView2.WinForms.dll" "/resource:$loader,Modern.WebView2Loader.dll" "/resource:$PSScriptRoot\Assets\OrangeShootPresentation.jar,OrangeShootPresentation.jar" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\Assets\orange-app.ico" "/out:$PSScriptRoot\橙子3D一键启用工具-现代界面.exe" "/reference:$core" "/reference:$forms" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.Security.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $sources
if($LASTEXITCODE -ne 0){throw '编译失败'}
