param([Parameter(Mandatory=$true)][string]$SigningKey,[Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$AuthenticodeConfig='',[switch]$AllowUnsignedRelease)
$ErrorActionPreference='Stop'
if(-not $AuthenticodeConfig){$AuthenticodeConfig=Join-Path $PSScriptRoot 'private/authenticode.json'}
. (Join-Path $PSScriptRoot 'build/VelopackDependencies.ps1')
. (Join-Path $PSScriptRoot 'build/Verify-VelopackArchives.ps1')
. (Join-Path $PSScriptRoot 'build/Verify-VelopackIcons.ps1')
. (Join-Path $PSScriptRoot 'build/Authenticode.ps1')
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $destination){throw 'Output already exists; choose a fresh release directory.'}
$windowsSigning=$null
if(-not $AllowUnsignedRelease){$windowsSigning=Get-OzlAuthenticodeContext $AuthenticodeConfig}
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new()
try {
 $rsa.PersistKeyInCsp=$false; $rsa.FromXmlString([IO.File]::ReadAllText($SigningKey))
 if($rsa.PublicOnly -or $rsa.ToXmlString($false) -cne [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'release-public-key.xml')).Trim()){throw 'Signing key must match the existing publication identity.'}
 $deps=Get-OzlVelopackDependencies
 New-Item -ItemType Directory $destination | Out-Null
 $launcherSigning=if($windowsSigning){$AuthenticodeConfig}else{''}
 $build=& (Join-Path $PSScriptRoot 'Build-VelopackLauncher.ps1') -SigningKey $SigningKey -OutputDirectory (Join-Path $destination 'app') -AuthenticodeConfig $launcherSigning
 $release=Join-Path $destination 'release'
 $exe=$deps.VpkExecutable; $prefix=@($deps.VpkArguments)
 $signingArguments=@()
 if($windowsSigning){
  $signParams=(@(Get-OzlSignToolArguments $windowsSigning.Config) | ForEach-Object {if($_ -match '\s'){'"'+$_+'"'}else{$_}}) -join ' '
  $frozenPeExclude=Get-OzlFrozenPeSignExclude
  $signingArguments=@('--signParams',$signParams,'--signExclude',$frozenPeExclude)
 }
 & $exe @prefix pack --packId $build.PackageId --packTitle '橙子启动器' --packVersion $build.Version --packDir $build.PackDirectory --outputDir $release --channel win --runtime win-x64 --mainExe $build.MainExecutable --icon (Join-Path $PSScriptRoot 'Orange3DTool/Assets/orange-app.ico') --aumid OrangeZomboidLauncher.Launcher --framework net48 --delta None --shortcuts Desktop,StartMenuRoot @signingArguments --skip-updates --legacyConsole --yes | Out-Host
 if($LASTEXITCODE){throw 'Pinned Velopack packaging failed'}
 $package="$($build.PackageId)-$($build.Version)-full.nupkg"
 $names=@($package,'OrangeZomboidLauncher-win-Setup.exe','OrangeZomboidLauncher-win-Portable.zip','releases.win.json','assets.win.json','RELEASES')
 $actual=@(Get-ChildItem -LiteralPath $release -File | ForEach-Object {$_.Name})
 if(@(Compare-Object $names $actual).Count -or @(Get-ChildItem -LiteralPath $release -Directory).Count){throw 'Unexpected framework release outputs'}
 Test-OzlVelopackArchives -ReleaseDirectory $release -PublicKey (Join-Path $PSScriptRoot 'release-public-key.xml') -Version $build.Version
 # Preserve the unmodified official payload as build evidence outside publication.
 $official=Join-Path $destination 'official-Setup.exe'
 Move-Item -LiteralPath (Join-Path $release 'OrangeZomboidLauncher-win-Setup.exe') -Destination $official
 & (Join-Path $PSScriptRoot 'build/Build-VelopackSetup.ps1') -OfficialSetup $official -OutputPath (Join-Path $release 'OrangeZomboidLauncher-win-Setup.exe') -VersionFile (Join-Path $PSScriptRoot 'VERSION') -PublicKey (Join-Path $PSScriptRoot 'release-public-key.xml')
 if($windowsSigning){Invoke-OzlAuthenticodeSigning -Context $windowsSigning -Path (Join-Path $release 'OrangeZomboidLauncher-win-Setup.exe')}
 Test-OzlVelopackIcons -ReleaseDirectory $release -Icon (Join-Path $PSScriptRoot 'Orange3DTool/Assets/orange-app.ico') -Version $build.Version | Out-Null
 if($windowsSigning){Assert-OzlSignedRelease -Context $windowsSigning -ReleaseDirectory $release -Version $build.Version -OfficialSetup $official -EvidencePath (Join-Path $destination 'authenticode-verification.json') | Out-Null}
 $payload=Get-Item -LiteralPath (Join-Path $release $package)
 $feed=[ordered]@{schema=1;appId=$build.PackageId;channel='win';version=$build.Version;fileName=$package;length=$payload.Length;sha256=(Get-FileHash -LiteralPath $payload.FullName -Algorithm SHA256).Hash;notes=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CHANGELOG.md'))}
 $writeSigned={param($name,$value) $raw=[Text.UTF8Encoding]::new($false).GetBytes(($value|ConvertTo-Json -Depth 6 -Compress)); [IO.File]::WriteAllBytes((Join-Path $release ($name+'.json')),$raw); [IO.File]::WriteAllBytes((Join-Path $release ($name+'.sig')),$rsa.SignData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))}
 & $writeSigned 'velopack-manifest' $feed
 $files=@(Get-ChildItem -LiteralPath $release -File | Sort-Object Name | ForEach-Object {[ordered]@{name=$_.Name;length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 & $writeSigned 'velopack-artifacts' ([ordered]@{schema=1;distribution='velopack';appId=$build.PackageId;channel='win';version=$build.Version;date=[DateTime]::UtcNow.ToString('yyyy-MM-dd');files=$files})
 if($AllowUnsignedRelease){[IO.File]::WriteAllText((Join-Path $destination 'windows-signing-status.json'),([ordered]@{status='NotSigned';version=$build.Version;explicitUnsignedPublication=$true;rsaUpdateAuthentication=$true}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))}
 [pscustomobject]@{PackDirectory=$build.PackDirectory;ReleaseDirectory=$release;Version=$build.Version;PackageId=$build.PackageId;Channel='win';ArtifactCount=10}
} finally {$rsa.Dispose()}
