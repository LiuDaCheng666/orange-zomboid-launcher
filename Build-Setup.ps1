$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'Orange3DTool'
& (Join-Path $project 'Build-Modern.ps1')
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2\lib\net462'
$sources=(Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk\Microsoft.Web.WebView2.Core.dll","/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll")
$uninstall=Join-Path $PSScriptRoot '卸载橙子启动器.exe'
& $compiler /nologo /target:winexe /main:PZ3D.UninstallerProgram /platform:x64 /optimize+ "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" "/out:$uninstall" $refs $sources
if($LASTEXITCODE -ne 0){throw 'Uninstaller compilation failed'}
$launcher=Join-Path $project '橙子3D一键启用工具-现代界面.exe'
$setup=Join-Path $PSScriptRoot '橙子启动器-安装程序.exe'
& $compiler /nologo /target:winexe "/resource:$PSScriptRoot\使用说明.md,Setup.Guide.md" /main:PZ3D.InstallerProgram /platform:x64 /optimize+ "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" "/resource:$launcher,Setup.Launcher.exe" "/resource:$uninstall,Setup.Uninstall.exe" "/out:$setup" $refs $sources
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
Copy-Item -LiteralPath $launcher -Destination (Join-Path $PSScriptRoot '橙子启动器-便携版.exe') -Force
Get-Item -LiteralPath $setup,$uninstall,$launcher | Select-Object Name,Length
