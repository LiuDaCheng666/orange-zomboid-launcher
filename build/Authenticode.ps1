# Windows publisher signing is independent of the RSA update-feed key.
function Read-OzlAuthenticodeConfig {
 param([Parameter(Mandatory=$true)][string]$ConfigPath)
 if(-not(Test-Path -LiteralPath $ConfigPath -PathType Leaf)){throw 'Windows code-signing configuration is missing. Configure a real publisher certificate; the RSA update key is not an Authenticode certificate.'}
 $config=[IO.File]::ReadAllText([IO.Path]::GetFullPath($ConfigPath),[Text.Encoding]::UTF8) | ConvertFrom-Json -ErrorAction Stop
 if($config.schema -ne 1 -or $config.certificateThumbprint -cnotmatch '^[a-fA-F0-9]{40}$' -or [string]::IsNullOrWhiteSpace($config.certificateSubject) -or $config.certificateStore -cnotin @('CurrentUser','LocalMachine')){throw 'Invalid Windows signing identity: schema, exact certificate thumbprint, subject and My store location are required.'}
 $uri=$null
 if(-not [Uri]::TryCreate($config.timestampUrl,[UriKind]::Absolute,[ref]$uri) -or $uri.Scheme -notin @('http','https') -or $uri.UserInfo -or $uri.Query -or $uri.Fragment -or $config.timestampUrl -match '[\s"`%&|<>^!]'){throw 'Timestamp URL must be an HTTP(S) RFC3161 endpoint without shell characters, credentials, query or fragment.'}
 $config.certificateThumbprint=$config.certificateThumbprint.ToUpperInvariant()
 return $config
}
function Get-OzlAuthenticodeContext {
 param([Parameter(Mandatory=$true)][string]$ConfigPath,[switch]$VerifyOnly)
 $config=Read-OzlAuthenticodeConfig $ConfigPath
 $tool=$config.signToolPath
 if(-not $tool){
  . (Join-Path $PSScriptRoot 'VelopackDependencies.ps1')
  $dependencies=Get-OzlVelopackDependencies
  $tool=Join-Path $dependencies.DependencyRoot 'vpk/vendor/signing/signtool.exe'
 }
 $tool=[IO.Path]::GetFullPath($tool)
 $toolSignature=Get-AuthenticodeSignature -LiteralPath $tool -ErrorAction Stop
 if($toolSignature.Status -ne 'Valid' -or $null -eq $toolSignature.SignerCertificate -or $toolSignature.SignerCertificate.Subject -notmatch '^CN=Microsoft Corporation,'){throw 'SignTool must have a valid Microsoft publisher signature.'}
 $certificate=$null
 if(-not $VerifyOnly){
  $certificate=Get-Item -LiteralPath ('Cert:\'+$config.certificateStore+'\My\'+$config.certificateThumbprint) -ErrorAction SilentlyContinue
  if($null -eq $certificate -or -not $certificate.HasPrivateKey){throw 'The selected Windows publisher certificate/private-key provider is unavailable. Activate the approved cloud/token certificate first; an unsigned formal release will not be built.'}
  if($certificate.Subject -cne $config.certificateSubject -or $certificate.NotBefore -gt [DateTime]::Now -or $certificate.NotAfter -lt [DateTime]::Now -or @($certificate.EnhancedKeyUsageList | Where-Object {$_.ObjectId -eq '1.3.6.1.5.5.7.3.3'}).Count -eq 0){throw 'Selected certificate identity, validity or code-signing usage does not match the release configuration.'}
 }
 return [pscustomobject]@{Config=$config;SignToolPath=$tool;Certificate=$certificate}
}
function Get-OzlSignToolArguments {
 param([Parameter(Mandatory=$true)]$Config)
 $parameters=@('/sha1',$Config.certificateThumbprint,'/s','My','/fd','SHA256','/tr',$Config.timestampUrl,'/td','SHA256','/d','橙子启动器','/du','https://orangezl.com/')
 if($Config.certificateStore -ceq 'LocalMachine'){$parameters+='/sm'}
 return $parameters
}
function Assert-OzlAuthenticodeFile {
 param([Parameter(Mandatory=$true)]$Context,[Parameter(Mandatory=$true)][string]$Path)
 $signature=Get-AuthenticodeSignature -LiteralPath $Path -ErrorAction Stop
 if($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate){throw "Windows signature is not trusted/valid: $Path ($($signature.Status))"}
 if($signature.SignerCertificate.Thumbprint -cne $Context.Config.certificateThumbprint -or $signature.SignerCertificate.Subject -cne $Context.Config.certificateSubject){throw "Unexpected publisher certificate: $Path"}
 if($null -eq $signature.TimeStamperCertificate){throw "Windows signature has no trusted timestamp: $Path"}
 & $Context.SignToolPath verify /pa /all /v /tw $Path | Out-Host
 if($LASTEXITCODE -ne 0){throw "SignTool verification or timestamp check failed: $Path"}
 return [pscustomobject]@{path=[IO.Path]::GetFullPath($Path);sha256=(Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash;length=(Get-Item -LiteralPath $Path).Length;status='Valid';certificateThumbprint=$signature.SignerCertificate.Thumbprint;certificateSubject=$signature.SignerCertificate.Subject;timestampSubject=$signature.TimeStamperCertificate.Subject}
}
function Invoke-OzlAuthenticodeSigning {
 param([Parameter(Mandatory=$true)]$Context,[Parameter(Mandatory=$true)][string[]]$Path)
 $parameters=@(Get-OzlSignToolArguments $Context.Config)
 foreach($file in $Path){
  & $Context.SignToolPath sign @parameters $file | Out-Host
  if($LASTEXITCODE -ne 0){throw "Windows signing failed: $file"}
  Assert-OzlAuthenticodeFile -Context $Context -Path $file | Out-Null
 }
}
function Get-OzlFrozenPeSignExclude {
 # vpk 1.2.161 matches one Regex against preprocessed absolute paths.
 # The generated execution stub is still named *_ExecutionStub.exe here.
 return '(?i)[\\/](?:橙子启动器\.exe|Velopack\.dll|Newtonsoft\.Json\.dll|Web[\\/](?:Microsoft\.Web\.WebView2\.(?:Core|WinForms)\.dll|WebView2Loader\.dll))\z'
}
function Assert-OzlSignedRelease {
 param([Parameter(Mandatory=$true)]$Context,[Parameter(Mandatory=$true)][string]$ReleaseDirectory,[Parameter(Mandatory=$true)][string]$Version,[string]$OfficialSetup='',[string]$EvidencePath='')
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 Add-Type -AssemblyName System.IO.Compression
 $release=[IO.Path]::GetFullPath($ReleaseDirectory)
 $leases=@()
 try {
 # Keep the archive/signature checks and recorded hashes on the same bytes.
 # Windows denies in-place writes, deletion and replacement while these are held.
 foreach($item in Get-ChildItem -LiteralPath $release -File){$leases+=[IO.File]::Open($item.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)}
 $stage=Join-Path (Split-Path $release -Parent) ('signature-audit-'+[Guid]::NewGuid().ToString('N'))
 New-Item -ItemType Directory $stage | Out-Null
 $results=@()
 foreach($package in @(
  @{name="OrangeZomboidLauncher-$Version-full.nupkg";entries=@('lib/app/橙子启动器.exe','lib/app/Squirrel.exe','lib/app/橙子启动器_ExecutionStub.exe')},
  @{name='OrangeZomboidLauncher-win-Portable.zip';entries=@('current/橙子启动器.exe','Update.exe','橙子启动器.exe')}
 )){
  $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $release $package.name))
  try{
   foreach($name in $package.entries){
    $matches=@($archive.Entries | Where-Object {$_.FullName -ceq $name})
    if($matches.Count -ne 1){throw "Missing/duplicate signed executable in $($package.name): $name"}
    $file=Join-Path $stage ([Guid]::NewGuid().ToString('N')+'.exe')
    [IO.Compression.ZipFileExtensions]::ExtractToFile($matches[0],$file,$false)
    $result=Assert-OzlAuthenticodeFile -Context $Context -Path $file
    $result | Add-Member -NotePropertyName role -NotePropertyValue ($package.name+':'+$name)
    $results+=$result
   }
  }finally{$archive.Dispose()}
 }
 $result=Assert-OzlAuthenticodeFile -Context $Context -Path (Join-Path $release 'OrangeZomboidLauncher-win-Setup.exe')
 $result | Add-Member -NotePropertyName role -NotePropertyValue 'published-path-chooser'
 $results+=$result
 if($OfficialSetup){
  $result=Assert-OzlAuthenticodeFile -Context $Context -Path $OfficialSetup
  $result | Add-Member -NotePropertyName role -NotePropertyValue 'embedded-official-installer-input'
  $results+=$result
 }
 $artifacts=@(Get-ChildItem -LiteralPath $release -File | Sort-Object Name | ForEach-Object {[ordered]@{name=$_.Name;length=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
 $report=[ordered]@{schema=1;version=$Version;verifiedUtc=[DateTime]::UtcNow.ToString('o');publisher=$Context.Config.certificateSubject;certificateThumbprint=$Context.Config.certificateThumbprint;executables=$results;artifacts=$artifacts}
 if($EvidencePath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($EvidencePath),($report|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))}
 return [pscustomobject]$report
 }finally{foreach($lease in $leases){$lease.Dispose()}}
}
