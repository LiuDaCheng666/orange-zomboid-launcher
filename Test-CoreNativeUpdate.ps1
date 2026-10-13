param([string]$FixtureRoot='')
$ErrorActionPreference='Stop'
if(-not $FixtureRoot){$FixtureRoot=Join-Path $PSScriptRoot ('artifacts\TestFixtures\native-update-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
New-Item -ItemType Directory -Path $FixtureRoot | Out-Null
& (Join-Path $PSScriptRoot 'Build-Tests.ps1') -Entry PZ3D.CoreReleaseTool -Output (Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe')
$tool=Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe'
$private=Join-Path $FixtureRoot 'fixture-private.xml';$public=Join-Path $FixtureRoot 'fixture-public.xml'
& $tool keygen $private $public
if($LASTEXITCODE -ne 0){throw 'Ephemeral fixture key generation failed'}
$source=Join-Path $FixtureRoot 'source';New-Item -ItemType Directory -Path $source,(Join-Path $source 'Web'),(Join-Path $source 'Assets') | Out-Null
$project=Join-Path $PSScriptRoot 'Orange3DTool';$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2';$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll","/reference:$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll")
$versionAttributes=& (Join-Path $PSScriptRoot 'Build-Version.ps1')
foreach($entry in @(@('PZ3D.CoreUpdateNativeFixtureTests','橙子启动器.exe'),@('PZ3D.CoreUpdaterProgram','橙子更新助手.exe'),@('PZ3D.CoreUpdateNativeFixtureTests','卸载橙子启动器.exe'))){
 & $compiler /nologo /target:winexe /platform:x64 "/main:$($entry[0])" "/out:$source\$($entry[1])" "/resource:$public,Launcher.UpdatePublicKey.xml" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" $refs $versionAttributes (Get-ChildItem -LiteralPath $project -Filter '*.cs').FullName
 if($LASTEXITCODE -ne 0){throw 'Native fixture compile failed'}
}
foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $project "Web\$name") -Destination (Join-Path $source "Web\$name")}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){Copy-Item -LiteralPath (Join-Path $sdk "lib\net462\$name") -Destination (Join-Path $source "Web\$name")}
Copy-Item -LiteralPath (Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination (Join-Path $source 'Web\WebView2Loader.dll')
foreach($name in @('OrangeShootPresentation.jar','OrangeLaunchBridge.lua')){Copy-Item -LiteralPath (Join-Path $project "Assets\$name") -Destination (Join-Path $source "Assets\$name")}
foreach($name in @('VERSION','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source $name)}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '使用说明.md') -Destination (Join-Path $source '使用说明.txt');Copy-Item -LiteralPath $public -Destination (Join-Path $source 'release-public-key.xml')
$release=Join-Path $FixtureRoot 'release'
& $tool pack $source $release $private $public
if($LASTEXITCODE -ne 0){throw 'Fixture base pack failed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach($case in @('healthy','failed')){
 $root=Join-Path $FixtureRoot $case;[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $release 'orange-launcher-v1.1.0-win-x64.zip'),$root)
 $feed=Join-Path $root 'fixture-feed';New-Item -ItemType Directory -Path $feed | Out-Null
 $manifest=Get-Content -Raw (Join-Path $release 'update-manifest.json') | ConvertFrom-Json
 $manifest.version='1.1.1';$manifest.notes='Native WebView fixture; not a published release'
 $text=[IO.File]::ReadAllText((Join-Path $root 'Web\app.js'))
 if($case -eq 'healthy'){$text+="`n// Native sparse update fixture`n"}else{$text="throw new Error('Deliberately failed native page fixture');"}
 $bytes=[Text.UTF8Encoding]::new($false).GetBytes($text);[IO.File]::WriteAllBytes((Join-Path $feed 'app.js'),$bytes)
 $sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()}
 foreach($file in $manifest.files){$file.url=$file.url.Replace('/v1.1.0/','/v1.1.1/');if($file.path -eq 'Web/app.js'){$file.sha256=$hash;$file.length=$bytes.Length;$file.url='https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v1.1.1/app.js'}}
 $raw=[Text.UTF8Encoding]::new($false).GetBytes(($manifest | ConvertTo-Json -Depth 8 -Compress));[IO.File]::WriteAllBytes((Join-Path $feed 'update-manifest.json'),$raw)
 $rsa=[Security.Cryptography.RSACryptoServiceProvider]::new();try{$rsa.PersistKeyInCsp=$false;$rsa.FromXmlString([IO.File]::ReadAllText($private));[IO.File]::WriteAllBytes((Join-Path $feed 'update-manifest.sig'),$rsa.SignData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))}finally{$rsa.Dispose()}
 $process=Start-Process -FilePath (Join-Path $root '橙子启动器.exe') -ArgumentList '--native-flow' -PassThru -WindowStyle Hidden
 if(-not $process.WaitForExit(60000)){throw "Parent did not hand off: $root"}
 if($process.ExitCode -ne 0){throw "Native parent failed: $root"}
 $task=Get-Content -Raw (Join-Path $root 'native-task.txt');$journal=Join-Path $root "OrangeLauncherRuntime\CoreUpdates\$task\journal.json"
 $deadline=[DateTime]::UtcNow.AddSeconds(65);$state=''
 do {Start-Sleep -Milliseconds 150;try{$stream=[IO.FileStream]::new($journal,[IO.FileMode]::Open,[IO.FileAccess]::Read,([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete));try{$reader=[IO.StreamReader]::new($stream);try{$state=($reader.ReadToEnd() | ConvertFrom-Json).state}finally{$reader.Dispose()}}finally{$stream.Dispose()}}catch [IO.IOException]{}}while($state -notin @('Succeeded','RolledBack','Conflicted') -and [DateTime]::UtcNow -lt $deadline)
 $expected=if($case -eq 'healthy'){'Succeeded'}else{'RolledBack'}
 if($state -ne $expected){throw "Native $case expected $expected, got $state; $root"}
 $receipt=if($case -eq 'healthy'){'native-health-succeeded'}else{'native-rollback-restarted'}
 $deadline=[DateTime]::UtcNow.AddSeconds(10);while(-not (Test-Path -LiteralPath (Join-Path $root $receipt)) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if(-not (Test-Path -LiteralPath (Join-Path $root $receipt))){throw "Real native page receipt missing: $root"}
 if((Get-Content (Join-Path $root 'downloaded-files.txt')).Count -ne 1 -or (Get-Content -Raw (Join-Path $root 'downloaded-files.txt')).Trim() -ne 'app.js'){throw 'Sparse fixture downloaded unexpected assets'}
 Start-Sleep -Milliseconds 750
 $uninstall=Start-Process -FilePath (Join-Path $root '卸载橙子启动器.exe') -ArgumentList '--fixture-uninstall' -PassThru -WindowStyle Hidden -Wait
 if($uninstall.ExitCode -ne 0){throw "Native fixture uninstall failed: $root"}
 $deadline=[DateTime]::UtcNow.AddSeconds(10);while((Test-Path -LiteralPath (Join-Path $root 'orange-installation.json')) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
 if((Test-Path -LiteralPath (Join-Path $root '橙子启动器.exe')) -or (Test-Path -LiteralPath (Join-Path $root 'orange-installation.json'))){throw 'Owned uninstall left release files'}
 if(-not (Test-Path -LiteralPath (Join-Path $root 'downloaded-files.txt'))){throw 'Uninstall removed unowned evidence'}
 Write-Output "PASS: real native WebView $case -> $state; only app.js fetched; scoped uninstall retained evidence: $root"
}
Write-Output "NATIVE UPDATE FIXTURES: $FixtureRoot"
