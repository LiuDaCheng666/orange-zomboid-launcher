param([Parameter(Mandatory=$true)][string]$SigningKey,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$buildRoot=$PSScriptRoot
. (Join-Path $buildRoot 'build/ProductionSources.ps1')
. (Join-Path $buildRoot 'build/VelopackDependencies.ps1')
$deps=Get-OzlVelopackDependencies
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $destination){throw 'Output already exists; choose a fresh directory.'}
$public=Join-Path $buildRoot 'release-public-key.xml'
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new()
try {
 $rsa.PersistKeyInCsp=$false
 $rsa.FromXmlString([IO.File]::ReadAllText($SigningKey))
 if($rsa.PublicOnly -or $rsa.KeySize -lt 2048 -or $rsa.ToXmlString($false) -cne [IO.File]::ReadAllText($public).Trim()){throw 'Signing key must match the existing tracked publication identity.'}
 & (Join-Path $buildRoot 'Prepare-Dependencies.ps1') | Out-Null
 $project=Join-Path $buildRoot 'Orange3DTool'
 $sdk=Join-Path $buildRoot 'Dependencies/WebView2'
 $core=Join-Path $sdk 'lib/net462/Microsoft.Web.WebView2.Core.dll'
 $forms=Join-Path $sdk 'lib/net462/Microsoft.Web.WebView2.WinForms.dll'
 $sources=@(Get-OzlVelopackSources -ProjectDirectory $project)
 $versionAttributes=& (Join-Path $buildRoot 'Build-Version.ps1')
 $version=[IO.File]::ReadAllText((Join-Path $buildRoot 'VERSION')).Trim()
 New-Item -ItemType Directory $destination,(Join-Path $destination 'Web'),(Join-Path $destination 'Assets'),(Join-Path $destination 'ThirdPartyNotices') | Out-Null
 $launcher=Join-Path $destination '橙子启动器.exe'
 # The embedded marker is immutable launcher content, never a disk-controlled switch.
 $resources=@("/resource:$buildRoot\VERSION,Launcher.Version.txt","/resource:$public,Launcher.UpdatePublicKey.xml","/resource:$buildRoot\build\velopack-distribution.txt,Launcher.Distribution.Velopack","/resource:$project\Assets\orange-app.png,OrangeAppLogo.png")
 $refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Web.dll','/reference:System.Net.Http.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$core","/reference:$forms","/reference:$($deps.SdkAssembly)","/reference:$($deps.NewtonsoftAssembly)")
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION,OZL_VELOPACK /target:winexe /main:PZ3D.Program /platform:x64 /optimize+ "/out:$launcher" "/win32manifest:$project\app.manifest" "/win32icon:$project\Assets\orange-app.ico" $refs $resources $versionAttributes $sources | Out-Host
 if($LASTEXITCODE){throw 'Velopack launcher compilation failed'}
 Copy-Item -LiteralPath $deps.SdkAssembly,$deps.NewtonsoftAssembly -Destination $destination
 foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $project "Web/$name") -Destination (Join-Path $destination "Web/$name")}
 & (Join-Path $buildRoot 'Build-CoverGallery.ps1') -OutputHtml (Join-Path $destination 'Web/index.html') | Out-Null
 Copy-Item -LiteralPath $core,$forms -Destination (Join-Path $destination 'Web')
 Copy-Item -LiteralPath (Join-Path $sdk 'runtimes/win-x64/native/WebView2Loader.dll') -Destination (Join-Path $destination 'Web')
 foreach($name in @('OrangeShootPresentation.jar','OrangeLaunchBridge.lua','OrangeStartup.jar')){Copy-Item -LiteralPath (Join-Path $project "Assets/$name") -Destination (Join-Path $destination "Assets/$name")}
 foreach($name in @('VERSION','CHANGELOG.md','release-public-key.xml')){Copy-Item -LiteralPath (Join-Path $buildRoot $name) -Destination (Join-Path $destination $name)}
 Copy-Item -LiteralPath (Join-Path $buildRoot 'docs/licenses/Velopack.txt') -Destination (Join-Path $destination 'ThirdPartyNotices/Velopack.txt')
 Copy-Item -LiteralPath (Join-Path $deps.DependencyRoot 'newtonsoft/LICENSE.md') -Destination (Join-Path $destination 'ThirdPartyNotices/Newtonsoft.Json.txt')
 & (Join-Path $buildRoot 'Build-Guide.ps1') -OutputPath (Join-Path $destination '使用说明.txt') | Out-Null
 $files=@(Get-ChildItem -LiteralPath $destination -File -Recurse | Sort-Object FullName | ForEach-Object {[ordered]@{path=$_.FullName.Substring($destination.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;url='';length=$_.Length}})
 $raw=[Text.UTF8Encoding]::new($false).GetBytes(([ordered]@{schema=1;version=$version;files=$files} | ConvertTo-Json -Depth 5 -Compress))
 [IO.File]::WriteAllBytes((Join-Path $destination 'velopack-bootstrap.json'),$raw)
 [IO.File]::WriteAllBytes((Join-Path $destination 'velopack-bootstrap.sig'),$rsa.SignData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))
 [pscustomobject]@{PackDirectory=$destination;MainExecutable='橙子启动器.exe';PackageId='OrangeZomboidLauncher';Version=$version;Channel='win';BootstrapManifest='velopack-bootstrap.json';BootstrapSignature='velopack-bootstrap.sig'}
} finally {$rsa.Dispose()}
