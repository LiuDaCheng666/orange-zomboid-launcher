param([string]$ExpectedVersion='')
$ErrorActionPreference='Stop'
if(-not $ExpectedVersion){$ExpectedVersion=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()}
# Copy only build inputs into a fresh short fixture. The real no-argument build
# runs here, so it cannot replace a developer's existing generated legacy EXEs.
$fixture=Join-Path $PSScriptRoot ('artifacts\lsb-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$temp=Join-Path $fixture 'tmp'
New-Item -ItemType Directory -Path $fixture,$temp | Out-Null
Write-Output "Legacy setup build fixture: $fixture"
function Copy-Input([string]$Relative){
 $source=Join-Path $PSScriptRoot $Relative
 if(-not (Test-Path -LiteralPath $source -PathType Leaf)){throw "Prepared build input missing: $Relative"}
 $destination=Join-Path $fixture $Relative
 New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
 Copy-Item -LiteralPath $source -Destination $destination
}
foreach($name in @('Build-StartupComponent.ps1','Build-Setup.ps1','Build-Version.ps1','Build-CoverGallery.ps1','Prepare-Assets.ps1','Prepare-Dependencies.ps1','build\ProductionSources.ps1','build\DependencyIntegrity.ps1','build\dependencies.lock.json','VERSION','使用说明.md','docs\THIRD-PARTY.md','Orange3DTool\Build-Modern.ps1','Orange3DTool\app.manifest')){Copy-Input $name}
foreach($name in @('crossroads','riverside','safehouse')){Copy-Input ('docs\assets\cover-candidates\'+$name+'.png')}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool') -Filter '*.cs'){Copy-Input ('Orange3DTool\'+$file.Name)}
foreach($name in @('OrangeLaunchBridge.lua','OrangeShootPresentation.jar','OrangeStartup.jar','orange-app.png','orange-app.ico')){Copy-Input ('Orange3DTool\Assets\'+$name)}
foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool\StartupSource') -File -Recurse){Copy-Input $file.FullName.Substring($PSScriptRoot.Length+1)}
Copy-Input 'Dependencies\ZombieBuddy-v2.3.4.jar'
if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool\Assets\OrangeStartup.build.json')){Copy-Input 'Orange3DTool\Assets\OrangeStartup.build.json'}
foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){Copy-Input ('Orange3DTool\Web\'+$name)}
foreach($name in @('lib\net462\Microsoft.Web.WebView2.Core.dll','lib\net462\Microsoft.Web.WebView2.WinForms.dll','runtimes\win-x64\native\WebView2Loader.dll')){Copy-Input ('Dependencies\WebView2\'+$name)}
$previousTemp=$env:TEMP;$previousTmp=$env:TMP
try{
 $env:TEMP=$temp;$env:TMP=$temp
 & (Join-Path $fixture 'Build-Setup.ps1')
 $setup=Join-Path $fixture '橙子启动器-安装程序.exe'
 if(-not (Test-Path -LiteralPath $setup -PathType Leaf)){throw 'Actual legacy setup build produced no installer'}
 $probe=Join-Path $fixture 'legacy-setup-probe.exe'
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 & $compiler /nologo /target:exe /platform:x64 "/out:$probe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tests\LegacySetupBuildProbe.cs')
 if($LASTEXITCODE -ne 0){throw 'Legacy setup probe compilation failed'}
 & $probe $setup $ExpectedVersion (Join-Path $fixture 'ready')
 $probeExit=$LASTEXITCODE
 $process=Start-Process -FilePath $setup -ArgumentList '--verify-setup' -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput (Join-Path $fixture 'verify.stdout.txt') -RedirectStandardError (Join-Path $fixture 'verify.stderr.txt')
 Get-Content -LiteralPath (Join-Path $fixture 'verify.stdout.txt'),(Join-Path $fixture 'verify.stderr.txt')
 if($process.ExitCode -ne 0){Write-Output "FAIL: actual legacy --verify-setup exit=$($process.ExitCode)"}
 else{Write-Output 'PASS: actual legacy --verify-setup extraction and scoped uninstall'}
 if($probeExit -ne 0 -or $process.ExitCode -ne 0){throw "Legacy setup behavioral regression failed: probe=$probeExit verifier=$($process.ExitCode); evidence=$fixture"}
 Write-Output "PASS: actual no-argument legacy setup build, version $ExpectedVersion, service/form initialization and verifier; evidence=$fixture"
}finally{$env:TEMP=$previousTemp;$env:TMP=$previousTmp}
