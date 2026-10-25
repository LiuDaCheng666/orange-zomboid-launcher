param([string]$JdkPath='',[switch]$ForceBridge)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build\DependencyIntegrity.ps1')
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression.FileSystem
$deps=Join-Path $PSScriptRoot 'Dependencies'
$sdk=Join-Path $deps 'WebView2'
$core=Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'
if(-not (Test-Path -LiteralPath $core)) {
  New-Item -ItemType Directory -Path $deps -Force | Out-Null
  $package=Join-Path $deps 'microsoft.web.webview2.1.0.4258.31.nupkg'
  $url=(Get-OzlWebView2Lock).package.url
  Invoke-WebRequest -Uri $url -OutFile $package
  Assert-OzlWebView2Package -PackagePath $package
  [IO.Compression.ZipFile]::ExtractToDirectory($package,$sdk)
}
Assert-OzlWebView2Dependencies -SdkDirectory $sdk
$bridge=Join-Path $PSScriptRoot 'Orange3DTool\Assets\OrangeShootPresentation.jar'
if((Test-Path -LiteralPath $bridge) -and -not $ForceBridge){
  & (Join-Path $PSScriptRoot 'Build-StartupComponent.ps1') -JdkPath $JdkPath
  return
}
$buddy=Join-Path $deps 'ZombieBuddy-v2.3.4.jar'
if(-not (Test-Path -LiteralPath $buddy)){
  Invoke-WebRequest -Uri 'https://github.com/zed-0xff/ZombieBuddy/releases/download/v2.3.4/ZombieBuddy.jar' -OutFile $buddy
}
if((Get-FileHash -LiteralPath $buddy -Algorithm SHA256).Hash -ne 'EB79B9876332010733A8E0D7CE4FE0377846059A9E28859029E2A0FB449F6CF2'){throw 'ZombieBuddy compile dependency hash mismatch'}
if($JdkPath){$javac=Join-Path $JdkPath 'bin\javac.exe';$jar=Join-Path $JdkPath 'bin\jar.exe'}else{$javac=(Get-Command javac.exe -ErrorAction Stop).Source;$jar=(Get-Command jar.exe -ErrorAction Stop).Source}
$buildDir=Join-Path $PSScriptRoot ('artifacts\bridge-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$source=Join-Path $PSScriptRoot 'Orange3DTool\BridgeSource'
$names=@('Bootstrap','Hook','Preflight','ReticleMode','RuntimeBridge','AddonUpdate','AddonRender','ParkourAbsentGuard','ParkourBusyPatch')
$files=$names | ForEach-Object {Join-Path $source ($_+'.java')}
& $javac --release 17 -classpath $buddy -d $buildDir $files
if($LASTEXITCODE -ne 0){throw 'Own Java bridge compilation failed'}
& $jar --create --file $bridge --manifest (Join-Path $source 'agent.mf') -C $buildDir orange
if($LASTEXITCODE -ne 0){throw 'Own Java bridge packaging failed'}
Write-Host 'Compiled original bridge only; author framework is not packed into our JAR.'

& (Join-Path $PSScriptRoot 'Build-StartupComponent.ps1') -JdkPath $JdkPath
