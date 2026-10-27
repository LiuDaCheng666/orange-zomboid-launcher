param([string]$JdkPath='',[string]$GameDirectory='',[switch]$Force)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$source=Join-Path $PSScriptRoot 'Orange3DTool\StartupSource'
$asset=Join-Path $PSScriptRoot 'Orange3DTool\Assets\OrangeStartup.jar'
$stamp=Join-Path $PSScriptRoot 'Orange3DTool\Assets\OrangeStartup.build.json'
$logo=Join-Path $PSScriptRoot 'Orange3DTool\Assets\orange-app.png'
$buddy=Join-Path $PSScriptRoot 'Dependencies\ZombieBuddy-v2.3.4.jar'
$buddyHash='EB79B9876332010733A8E0D7CE4FE0377846059A9E28859029E2A0FB449F6CF2'
if(-not(Test-Path -LiteralPath $buddy)){
 New-Item -ItemType Directory -Path (Split-Path -Parent $buddy) -Force | Out-Null
 Invoke-WebRequest -Uri 'https://github.com/zed-0xff/ZombieBuddy/releases/download/v2.3.4/ZombieBuddy.jar' -OutFile $buddy
}
if((Get-FileHash -LiteralPath $buddy -Algorithm SHA256).Hash -ne $buddyHash){throw 'Pinned Byte Buddy development container hash mismatch'}
$files=@(Get-ChildItem -LiteralPath $source -Recurse -Filter '*.java' -File | Sort-Object FullName)
if($files.Count -eq 0){throw 'Startup Java sources missing'}
$environmentFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool\EnvironmentSource') -Recurse -Filter '*.java' -File | Sort-Object FullName)
$liveFiles=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool\LiveSource') -Recurse -Filter '*.java' -File | Sort-Object FullName)
$inputs=@($PSCommandPath,$logo,(Join-Path $PSScriptRoot 'Build-EnvironmentComponent.ps1'),(Join-Path $PSScriptRoot 'Build-LiveComponent.ps1'))+@($files.FullName)+@($environmentFiles.FullName)+@($liveFiles.FullName)
$fingerprint=(@($inputs | ForEach-Object {(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash})+$buddyHash)-join ':'
if(-not $Force -and (Test-Path -LiteralPath $asset) -and (Test-Path -LiteralPath $stamp)){
 try{$cached=Get-Content -LiteralPath $stamp -Raw | ConvertFrom-Json;if($cached.inputs -eq $fingerprint -and $cached.jar -eq (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash){return}}catch{}
}
if(-not $GameDirectory){
 $steamRoot=(Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
 $libraries=@($steamRoot)
 if($steamRoot -and (Test-Path -LiteralPath (Join-Path $steamRoot 'steamapps\libraryfolders.vdf'))){
  $vdf=Get-Content -LiteralPath (Join-Path $steamRoot 'steamapps\libraryfolders.vdf') -Raw
  $libraries+=@([regex]::Matches($vdf,'"path"\s+"([^"]+)"') | ForEach-Object {$_.Groups[1].Value.Replace('\\','\')})
 }
 foreach($library in $libraries | Select-Object -Unique){if($library){$candidate=Join-Path $library 'steamapps\common\ProjectZomboid';if(Test-Path -LiteralPath (Join-Path $candidate 'projectzomboid.jar')){$GameDirectory=$candidate;break}}}
}
$gameApi=if($GameDirectory){Join-Path $GameDirectory 'projectzomboid.jar'}else{''}
if(-not $gameApi -or -not(Test-Path -LiteralPath $gameApi)){throw 'Pass -GameDirectory pointing to an installed Project Zomboid; its API is compile-only and is never packaged.'}
$javac=if($JdkPath){Join-Path $JdkPath 'bin\javac.exe'}else{(Get-Command javac.exe -ErrorAction Stop).Source}
$jar=if($JdkPath){Join-Path $JdkPath 'bin\jar.exe'}else{(Get-Command jar.exe -ErrorAction Stop).Source}
$output=Join-Path ([IO.Path]::GetTempPath()) ('ozl-startup-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$classes=Join-Path $output 'classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
& $javac -encoding UTF-8 --release 17 -classpath ($buddy+';'+$gameApi) -d $classes $files.FullName
if($LASTEXITCODE -ne 0){throw 'Startup component compilation failed'}
$logoTarget=Join-Path $classes 'ozl\startup\orange.png'
Copy-Item -LiteralPath $logo -Destination $logoTarget
$environmentAgent=Join-Path $classes 'ozl\environment\EnvironmentAgent.jar'
& (Join-Path $PSScriptRoot 'Build-EnvironmentComponent.ps1') -OutputPath $environmentAgent -JdkPath $JdkPath
if($LASTEXITCODE -ne 0){throw 'Mandatory environment asset compilation failed'}
$liveAgent=Join-Path $classes 'ozl\live\LiveEnvironmentAgent.jar'
& (Join-Path $PSScriptRoot 'Build-LiveComponent.ps1') -OutputPath $liveAgent -JdkPath $JdkPath
if($LASTEXITCODE -ne 0){throw 'Mandatory live discovery asset compilation failed'}
$container=[IO.Compression.ZipFile]::OpenRead($buddy)
try{
 foreach($entry in $container.Entries){
  if($entry.FullName.StartsWith('net/bytebuddy/') -and -not $entry.FullName.EndsWith('/')){
   $target=Join-Path $classes $entry.FullName
   New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
   [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)
  }
 }
 $license=$container.Entries | Where-Object {$_.FullName -match '(?i)byte.?buddy.*(license|notice)|(license|notice).*byte.?buddy|META-INF/LICENSE$'} | Select-Object -First 1
 if(-not $license){throw 'Pinned dependency does not contain a distributable Byte Buddy license'}
 $licenseTarget=Join-Path $classes 'META-INF\BYTE-BUDDY-LICENSE.txt'
 New-Item -ItemType Directory -Path (Split-Path -Parent $licenseTarget) -Force | Out-Null
 [IO.Compression.ZipFileExtensions]::ExtractToFile($license,$licenseTarget,$false)
 foreach($entry in $container.Entries | Where-Object {$_.Length -gt 0 -and ($_.FullName -eq 'META-INF/NOTICE' -or $_.FullName.StartsWith('META-INF/licenses/'))}){
  $target=Join-Path $classes $entry.FullName
  New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
  [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)
 }
}finally{$container.Dispose()}
$manifest=Join-Path $output 'agent.mf'
[IO.File]::WriteAllText($manifest,"Manifest-Version: 1.0`nPremain-Class: ozl.startup.Agent`nCan-Retransform-Classes: true`nCan-Redefine-Classes: true`n`n",[Text.Encoding]::ASCII)
& $jar --create --file $asset --manifest $manifest -C $classes .
if($LASTEXITCODE -ne 0){throw 'Startup Java packaging failed'}
@{inputs=$fingerprint;jar=(Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash} | ConvertTo-Json | Set-Content -LiteralPath $stamp -Encoding UTF8
Write-Host 'Built OZL game-window Java component (game API compile-only).'
