param([Parameter(Mandatory=$true)][string]$OutputPath,[string]$JdkPath='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$destination=[IO.Path]::GetFullPath($OutputPath)
$assetDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Orange3DTool\Assets'))+[IO.Path]::DirectorySeparatorChar
if($destination.StartsWith($assetDirectory,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($destination) -ne '.jar'){throw 'Pass a caller-owned artifact JAR path outside packaged Assets.'}
$buddy=Join-Path $PSScriptRoot 'Dependencies\ZombieBuddy-v2.3.4.jar'
if(-not(Test-Path -LiteralPath $buddy) -or (Get-FileHash -LiteralPath $buddy -Algorithm SHA256).Hash -ne 'EB79B9876332010733A8E0D7CE4FE0377846059A9E28859029E2A0FB449F6CF2'){throw 'Pinned Byte Buddy development container missing or changed; prepare the existing dependency first.'}
$javac=if($JdkPath){Join-Path $JdkPath 'bin\javac.exe'}else{(Get-Command javac.exe -ErrorAction Stop).Source}
$jar=if($JdkPath){Join-Path $JdkPath 'bin\jar.exe'}else{(Get-Command jar.exe -ErrorAction Stop).Source}
$output=Join-Path ([IO.Path]::GetTempPath()) ('ozl-live-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$classes=Join-Path $output 'classes';New-Item -ItemType Directory -Path $classes -Force | Out-Null
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Orange3DTool\LiveSource') -Recurse -Filter '*.java' -File | Sort-Object FullName | ForEach-Object FullName)
$sources+=@('ActiveSelectionSession.java','ActiveTargetContract.java','ActiveSelectionHooks.java')|ForEach-Object{Join-Path $PSScriptRoot ('Orange3DTool/EnvironmentSource/ozl/environment/'+$_)}
$sources+=@(Join-Path $PSScriptRoot 'Orange3DTool/EnvironmentSource/ozl/environment/SafePaths.java');$sources+=@(Join-Path $PSScriptRoot 'Orange3DTool/EnvironmentSource/ozl/environment/TargetContract.java')
if($sources.Count -eq 0){throw 'Environment agent sources missing'}
& $javac -encoding UTF-8 --release 8 -Xlint:-options -classpath $buddy -d $classes $sources
if($LASTEXITCODE -ne 0){throw 'Environment agent Java 8 compilation failed'}
$container=[IO.Compression.ZipFile]::OpenRead($buddy)
try{
 foreach($entry in $container.Entries){if($entry.FullName.StartsWith('net/bytebuddy/') -and -not $entry.FullName.EndsWith('/')){$target=Join-Path $classes $entry.FullName;New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)}}
 $license=$container.Entries | Where-Object {$_.FullName -match '(?i)byte.?buddy.*(license|notice)|(license|notice).*byte.?buddy|META-INF/LICENSE$'} | Select-Object -First 1
 if(-not $license){throw 'Pinned Byte Buddy license missing'}
 $licenseTarget=Join-Path $classes 'META-INF\BYTE-BUDDY-LICENSE.txt';New-Item -ItemType Directory -Path (Split-Path -Parent $licenseTarget) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($license,$licenseTarget,$false)
 foreach($entry in $container.Entries | Where-Object {$_.Length -gt 0 -and ($_.FullName -eq 'META-INF/NOTICE' -or $_.FullName.StartsWith('META-INF/licenses/'))}){$target=Join-Path $classes $entry.FullName;New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null;[IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)}
}finally{$container.Dispose()}
$manifest=Join-Path $output 'agent.mf';[IO.File]::WriteAllText($manifest,"Manifest-Version: 1.0`nPremain-Class: ozl.environment.LiveEnvironmentAgent`n`n",[Text.Encoding]::ASCII)
$built=Join-Path $output 'LiveEnvironmentAgent.jar'
& $jar --create --file $built --manifest $manifest -C $classes .
if($LASTEXITCODE -ne 0){throw 'Environment agent packaging failed'}
New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
Copy-Item -LiteralPath $built -Destination $destination -Force
Write-Host 'Built mandatory live-environment agent (Java 8 bytecode; no game or loading component packaged).'
