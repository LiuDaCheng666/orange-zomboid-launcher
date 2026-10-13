param([string]$Entry='PZ3D.Program',[string]$Output='E:\AppCaches\OrangeLauncherProfiles\regression.exe')
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'Orange3DTool'
$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2\lib\net462'
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe "/main:$Entry" "/out:$Output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" "/resource:$project\Assets\OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" "/resource:$project\Orange3D.Bundle.zip,Orange3D.Bundle.zip" "/resource:$project\Assets\OrangeShootPresentation.jar,OrangeShootPresentation.jar" (Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
