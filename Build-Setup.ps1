param([switch]$PrivatePresets,[string]$CoreReleaseDirectory='',[string]$PublicKey='')
$ErrorActionPreference='Stop'
$project=Join-Path $PSScriptRoot 'Orange3DTool'
if($CoreReleaseDirectory){
 . (Join-Path $PSScriptRoot 'build\DependencyIntegrity.ps1')
 Assert-OzlWebView2Dependencies -SdkDirectory (Join-Path $PSScriptRoot 'Dependencies\WebView2')
 if(-not $PublicKey){$PublicKey=Join-Path $PSScriptRoot 'release-public-key.xml'}
 $rsa=[Security.Cryptography.RSACryptoServiceProvider]::new()
 try{$rsa.PersistKeyInCsp=$false;$publicXml=[IO.File]::ReadAllText($PublicKey);$rsa.FromXmlString($publicXml);if(-not $rsa.PublicOnly -or $publicXml.Trim() -cne $rsa.ToXmlString($false)){throw 'Installer trust resource must contain canonical public parameters only.'}}finally{$rsa.Dispose()}
 $versionAttributes=& (Join-Path $PSScriptRoot 'Build-Version.ps1')
 $releaseVersion=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 $sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2\lib\net462'
 $sources=(Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
 $setup=Join-Path $CoreReleaseDirectory "orange-launcher-setup-v$releaseVersion.exe"
 if(Test-Path -LiteralPath $setup){throw 'Installer output exists.'}
 & $compiler /nologo /target:winexe /main:PZ3D.InstallerProgram /platform:x64 /optimize+ "/out:$setup" "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" "/resource:$PublicKey,Launcher.UpdatePublicKey.xml" "/resource:$CoreReleaseDirectory\orange-launcher-v$releaseVersion-win-x64.zip,Setup.Core.zip" "/resource:$CoreReleaseDirectory\update-manifest.json,Setup.Manifest.json" "/resource:$CoreReleaseDirectory\update-manifest.sig,Setup.Manifest.sig" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" $versionAttributes $sources
 if($LASTEXITCODE -ne 0){throw 'Core installer compilation failed'}
 return
}
& (Join-Path $project 'Build-Modern.ps1') -PrivatePresets:$PrivatePresets
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2\lib\net462'
$sources=(Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk\Microsoft.Web.WebView2.Core.dll","/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll")
$uninstall=Join-Path $PSScriptRoot '卸载橙子启动器.exe'
& $compiler /nologo /target:winexe /main:PZ3D.UninstallerProgram /platform:x64 /optimize+ "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" "/out:$uninstall" $refs $sources
if($LASTEXITCODE -ne 0){throw 'Uninstaller compilation failed'}
$launcher=Join-Path $project '橙子3D一键启用工具-现代界面.exe'
$setup=Join-Path $PSScriptRoot '橙子启动器-安装程序.exe'
$versionAttributes=& (Join-Path $PSScriptRoot 'Build-Version.ps1')
& $compiler /nologo /target:winexe "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" "/resource:$PSScriptRoot\使用说明.md,Setup.Guide.md" /main:PZ3D.InstallerProgram /platform:x64 /optimize+ "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" "/resource:$launcher,Setup.Launcher.exe" "/resource:$uninstall,Setup.Uninstall.exe" "/out:$setup" $refs $versionAttributes $sources
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
Copy-Item -LiteralPath $launcher -Destination (Join-Path $PSScriptRoot '橙子启动器-便携版.exe') -Force
Get-Item -LiteralPath $setup,$uninstall,$launcher | Select-Object Name,Length
