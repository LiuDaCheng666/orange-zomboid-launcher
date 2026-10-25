param([switch]$VersionOnly,[string]$SigningKey='', [string]$CoreDirectory='', [string]$OutputDirectory='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build\ProductionSources.ps1')
$versionAttributes = & (Join-Path $PSScriptRoot 'Build-Version.ps1')
$releaseVersion=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()
if($VersionOnly){[pscustomobject]@{Version=$releaseVersion;Tag="v$releaseVersion"};return}
if(-not $SigningKey -or -not (Test-Path -LiteralPath $SigningKey)){throw 'Specify an existing private signing key with -SigningKey; never use fixture keys for publication.'}
$public=Join-Path $PSScriptRoot 'release-public-key.xml'
if(-not (Test-Path -LiteralPath $public)){throw 'Tracked publication public key is missing.'}
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new()
try{$rsa.PersistKeyInCsp=$false;$publicXml=[IO.File]::ReadAllText($public);$rsa.FromXmlString($publicXml);if(-not $rsa.PublicOnly -or $publicXml.Trim() -cne $rsa.ToXmlString($false)){throw 'Publication public key contains non-public or noncanonical data.'}}finally{$rsa.Dispose()}
& (Join-Path $PSScriptRoot 'Prepare-Dependencies.ps1')
$project=Join-Path $PSScriptRoot 'Orange3DTool'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2'
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll","/reference:$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll")
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$toolSources=@($sources)+@(Join-Path $project 'CoreReleaseTool.cs')
$resources=@("/resource:$PSScriptRoot\VERSION,Launcher.Version.txt","/resource:$public,Launcher.UpdatePublicKey.xml","/resource:$project\Assets\orange-app.png,OrangeAppLogo.png")
$tool=Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe'
& $compiler /nologo /define:OZL_PRODUCTION /target:exe /main:PZ3D.CoreReleaseTool "/out:$tool" $refs $resources $versionAttributes $toolSources
if($LASTEXITCODE -ne 0){throw 'Release tool compilation failed'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $PSScriptRoot "dist\$releaseVersion"}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Release output already exists; choose a fresh output directory.'}
if(-not $CoreDirectory){
 if(-not (Test-Path -LiteralPath (Join-Path $project 'CoreUpdaterProgram.cs'))){throw 'Task 4 CoreUpdaterProgram is required before building a real release. No updater stub is generated.'}
 $CoreDirectory=Join-Path $PSScriptRoot ('artifacts\core-build-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory -Path $CoreDirectory | Out-Null
 foreach($target in @(@('PZ3D.Program','橙子启动器.exe'),@('PZ3D.CoreUpdaterProgram','橙子更新助手.exe'),@('PZ3D.UninstallerProgram','卸载橙子启动器.exe'))){
  & $compiler /nologo /define:OZL_PRODUCTION /target:winexe "/main:$($target[0])" /platform:x64 /optimize+ "/out:$CoreDirectory\$($target[1])" "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" $refs $resources $versionAttributes $sources
  if($LASTEXITCODE -ne 0){throw "Compilation failed: $($target[0])"}
 }
 New-Item -ItemType Directory -Path (Join-Path $CoreDirectory 'Web'),(Join-Path $CoreDirectory 'Assets') | Out-Null
 foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $project "Web\$name") -Destination (Join-Path $CoreDirectory "Web\$name")}
 & (Join-Path $PSScriptRoot 'Build-CoverGallery.ps1') -OutputHtml (Join-Path $CoreDirectory 'Web\index.html')
 foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){Copy-Item -LiteralPath (Join-Path $sdk "lib\net462\$name") -Destination (Join-Path $CoreDirectory "Web\$name")}
 Copy-Item -LiteralPath (Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination (Join-Path $CoreDirectory 'Web\WebView2Loader.dll')
 foreach($name in @('OrangeShootPresentation.jar','OrangeLaunchBridge.lua','OrangeStartup.jar')){Copy-Item -LiteralPath (Join-Path $project "Assets\$name") -Destination (Join-Path $CoreDirectory "Assets\$name")}
 foreach($name in @('VERSION','CHANGELOG.md','release-public-key.xml')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $CoreDirectory $name)}
 & (Join-Path $PSScriptRoot 'Build-Guide.ps1') -OutputPath (Join-Path $CoreDirectory '使用说明.txt')
}
& $tool pack $CoreDirectory $OutputDirectory $SigningKey $public
if($LASTEXITCODE -ne 0){throw 'Core pack/sign validation failed'}
& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -CoreReleaseDirectory $OutputDirectory
Get-ChildItem -LiteralPath $OutputDirectory -File | Select-Object Name,Length
