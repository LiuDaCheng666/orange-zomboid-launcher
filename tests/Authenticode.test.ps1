$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$helper=Join-Path $repo 'build/Authenticode.ps1'
if(-not(Test-Path -LiteralPath $helper)){throw 'Release signing helper is missing: main, generated updater and final Setup must be signed and verified'}
. $helper
$testRoot=Join-Path $repo ('artifacts/authenticode-contract-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $testRoot | Out-Null
$configPath=Join-Path $testRoot 'signing.json'
$tool=Join-Path $repo 'Dependencies/Velopack/vpk/vendor/signing/signtool.exe'
$base=[ordered]@{schema=1;certificateThumbprint=('A'*40);certificateSubject='CN=Fixture publisher';certificateStore='CurrentUser';timestampUrl='http://time.certum.pl';signToolPath=$tool}
function Write-Config($value){[IO.File]::WriteAllText($configPath,($value|ConvertTo-Json),[Text.UTF8Encoding]::new($false))}
function Rejected([scriptblock]$action,[string]$reason,[string]$expected=''){$failed=$false;try{& $action|Out-Null}catch{if($expected -and $_.Exception.Message -notmatch $expected){throw};$failed=$true};if(-not $failed){throw $reason}}
Write-Config $base
$config=Read-OzlAuthenticodeConfig $configPath
$base.certificateSubject='CN=中文姓名, O=Open Source Developer';Write-Config $base
if((Read-OzlAuthenticodeConfig $configPath).certificateSubject -cne $base.certificateSubject){throw 'UTF-8 certificate subject must retain the exact publisher identity on Windows PowerShell'}
$base.certificateSubject='CN=Fixture publisher';Write-Config $base
$argsList=@(Get-OzlSignToolArguments $config)
foreach($item in @('/sha1',('A'*40),'/fd','SHA256','/tr','http://time.certum.pl','/td')){if($argsList -cnotcontains $item){throw "Missing deterministic signing parameter $item"}}
if($argsList -contains '/a' -or $argsList -contains '/p' -or $argsList -contains '/f'){throw 'No automatic identity or exported key/password selection'}
$base.certificateStore='LocalMachine';Write-Config $base
if(@(Get-OzlSignToolArguments (Read-OzlAuthenticodeConfig $configPath)) -notcontains '/sm'){throw 'Machine certificate store is not passed to SignTool'}
$base.certificateStore='CurrentUser'
foreach($case in @(@{key='certificateThumbprint';value='invalid'},@{key='certificateStore';value='Root'},@{key='timestampUrl';value='http://time.certum.pl/?x=%22&del=1'},@{key='timestampUrl';value='file:///C:/local'},@{key='certificateSubject';value=''})){
 $old=$base[$case.key];$base[$case.key]=$case.value;Write-Config $base
 Rejected {Read-OzlAuthenticodeConfig $configPath} ('Invalid signing setting was accepted: '+$case.key)
 $base[$case.key]=$old
}
Write-Config $base
Rejected {Get-OzlAuthenticodeContext $configPath} 'A missing certificate must not silently build a formal unsigned release' 'certificate/private-key provider is unavailable'
$base.signToolPath='';Write-Config $base
$defaultContext=Get-OzlAuthenticodeContext -ConfigPath $configPath -VerifyOnly
if($defaultContext.SignToolPath -cne [IO.Path]::GetFullPath($tool)){throw 'Default SignTool must resolve to the existing authenticated/pinned framework tool'}
$base.signToolPath=$tool;Write-Config $base
$blockedOutput=Join-Path $testRoot 'formal-release-must-not-exist'
Rejected {& "$repo/Build-VelopackRelease.ps1" -SigningKey (Join-Path $testRoot 'must-not-read-private-key.xml') -OutputDirectory $blockedOutput -AuthenticodeConfig (Join-Path $testRoot 'missing-config.json')} 'Formal build must require publisher signing before output/key access' 'Windows code-signing configuration is missing'
if(Test-Path -LiteralPath $blockedOutput){throw 'Missing publisher identity must not leave a partial formal release'}
$context=[pscustomobject]@{Config=Read-OzlAuthenticodeConfig $configPath;SignToolPath=$tool}
Rejected {Assert-OzlAuthenticodeFile -Context $context -Path "$repo/Dependencies/Velopack/sdk/lib/net472/Velopack.dll"} 'Unsigned dependency cannot pass as a signed application' 'not trusted/valid'
Rejected {Assert-OzlAuthenticodeFile -Context $context -Path $tool} 'A valid Microsoft signature cannot pass as our expected publisher' 'Unexpected publisher'
# Exercise real trusted signature verification using an unchanged Microsoft tool.
# This does not sign OZL and these fixture archives must never be published.
$microsoft=Get-AuthenticodeSignature -LiteralPath $tool
$context.Config.certificateSubject=$microsoft.SignerCertificate.Subject
$context.Config.certificateThumbprint=$microsoft.SignerCertificate.Thumbprint
$verified=Assert-OzlAuthenticodeFile -Context $context -Path $tool
if($verified.status -ne 'Valid' -or -not $verified.timestampSubject){throw 'Trusted Microsoft fixture signature/timestamp verification did not run'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
function Write-Archive([string]$name,[string[]]$entries){
 $archive=[IO.Compression.ZipFile]::Open((Join-Path $testRoot $name),[IO.Compression.ZipArchiveMode]::Create)
 try{foreach($entry in $entries){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$tool,$entry)|Out-Null}}finally{$archive.Dispose()}
}
Write-Archive 'OrangeZomboidLauncher-1.0.0-full.nupkg' @('lib/app/橙子启动器.exe','lib/app/Squirrel.exe','lib/app/橙子启动器_ExecutionStub.exe')
Write-Archive 'OrangeZomboidLauncher-win-Portable.zip' @('current/橙子启动器.exe','Update.exe','橙子启动器.exe')
Copy-Item -LiteralPath $tool -Destination (Join-Path $testRoot 'OrangeZomboidLauncher-win-Setup.exe')
$report=Assert-OzlSignedRelease -Context $context -ReleaseDirectory $testRoot -Version '1.0.0' -OfficialSetup $tool -EvidencePath (Join-Path $testRoot 'verification.json')
if($report.executables.Count -ne 8 -or @($report.executables | Where-Object {$_.role -ceq 'OrangeZomboidLauncher-win-Portable.zip:橙子启动器.exe'}).Count -ne 1){throw 'Every final archive location, including root execution stub, must be verified'}
Remove-Item -LiteralPath (Join-Path $testRoot 'OrangeZomboidLauncher-win-Portable.zip')
Write-Archive 'OrangeZomboidLauncher-win-Portable.zip' @('current/橙子启动器.exe','Update.exe')
Rejected {Assert-OzlSignedRelease -Context $context -ReleaseDirectory $testRoot -Version '1.0.0'} 'Missing root stub must not pass' 'Missing/duplicate signed executable'
$exclude=Get-OzlFrozenPeSignExclude
foreach($name in @('橙子启动器.exe','Velopack.dll','Newtonsoft.Json.dll','Web/Microsoft.Web.WebView2.Core.dll','Web/Microsoft.Web.WebView2.WinForms.dll','Web/WebView2Loader.dll')){
 if(('C:/temp/vpk/preprocess/'+$name) -notmatch $exclude){throw "Frozen bootstrap PE would be rewritten: $name"}
}
foreach($name in @('Squirrel.exe','Update.exe','橙子启动器_ExecutionStub.exe','evil橙子启动器.exe')){
 if(('C:/temp/vpk/preprocess/'+$name) -match $exclude){throw "Generated PE must be signed: $name"}
}
$launcher=[IO.File]::ReadAllText("$repo/Build-VelopackLauncher.ps1")
if($launcher.IndexOf('Invoke-OzlAuthenticodeSigning') -lt 0 -or $launcher.IndexOf('Invoke-OzlAuthenticodeSigning') -gt $launcher.IndexOf('$files=@')){throw 'Main EXE must be signed before freezing the RSA bootstrap hashes'}
$release=[IO.File]::ReadAllText("$repo/Build-VelopackRelease.ps1")
foreach($required in @('Get-OzlAuthenticodeContext','Get-OzlFrozenPeSignExclude','--signParams','--signExclude','Assert-OzlSignedRelease')){if(-not $release.Contains($required)){throw "Release pipeline omits $required"}}
if($release.IndexOf('Assert-OzlSignedRelease') -gt $release.IndexOf('$writeSigned=')){throw 'Archive Authenticode verification must precede publication metadata'}
'PASS signing identity/parameters, invalid config, missing certificate, real unsigned/wrong-publisher rejection, exact six-file exclusion and release ordering'
