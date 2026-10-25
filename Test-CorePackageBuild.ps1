param([string]$ExpectedVersion='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'build\ProductionSources.ps1')
& (Join-Path $PSScriptRoot 'Build-Tests.ps1') -Entry PZ3D.CoreReleaseTool -Output (Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe')
$tool=Join-Path $PSScriptRoot 'artifacts\core-release-tool.exe'
if(-not $ExpectedVersion){$ExpectedVersion=(Get-Content -Raw (Join-Path $PSScriptRoot 'VERSION')).Trim()}
$assembly=[Reflection.Assembly]::LoadFile($tool)
$stream=$assembly.GetManifestResourceStream('Launcher.Version.txt')
try{$reader=[IO.StreamReader]::new($stream);try{$embeddedVersion=$reader.ReadToEnd().Trim()}finally{$reader.Dispose()}}finally{$stream.Dispose()}
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($tool)
if($assembly.GetName().Version.ToString() -ne "$ExpectedVersion.0" -or $info.FileVersion -ne "$ExpectedVersion.0" -or $info.ProductVersion -ne $ExpectedVersion -or $embeddedVersion -ne $ExpectedVersion){throw "Release metadata expected $ExpectedVersion; assembly=$($assembly.GetName().Version), file=$($info.FileVersion), product=$($info.ProductVersion), embedded=$embeddedVersion"}
Write-Output "PASS: release metadata assembly/file/product/embedded version $ExpectedVersion"
$fixture=Join-Path $PSScriptRoot ('artifacts\TestFixtures\package-build-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$private=Join-Path $fixture 'fixture-private.xml';$public=Join-Path $fixture 'fixture-public.xml'
& $tool keygen $private $public
if($LASTEXITCODE -ne 0){throw 'Fixture key generation failed'}
& $tool keygen $private $public
if($LASTEXITCODE -eq 0){throw 'Existing key was overwritten'}
$core=Join-Path $fixture 'source';New-Item -ItemType Directory -Path $core,(Join-Path $core 'Web'),(Join-Path $core 'Assets') | Out-Null
$project=Join-Path $PSScriptRoot 'Orange3DTool';$sdk=Join-Path $PSScriptRoot 'Dependencies\WebView2'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@(Get-OzlDiagnosticSources -ProjectDirectory $project)
$versionAttributes=& (Join-Path $PSScriptRoot 'Build-Version.ps1')
$version=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()
& $compiler /nologo /define:OZL_DIAGNOSTICS /target:exe /platform:x64 /main:PZ3D.CorePackageFixtureTests "/out:$core\橙子启动器.exe" "/resource:$public,Launcher.UpdatePublicKey.xml" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll" $versionAttributes $sources
if($LASTEXITCODE -ne 0){throw 'Fixture binary compilation failed'}
foreach($name in @('橙子更新助手.exe','卸载橙子启动器.exe')){Copy-Item -LiteralPath (Join-Path $core '橙子启动器.exe') -Destination (Join-Path $core $name)}
foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){Copy-Item -LiteralPath (Join-Path $project "Web\$name") -Destination (Join-Path $core "Web\$name")}
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){Copy-Item -LiteralPath (Join-Path $sdk "lib\net462\$name") -Destination (Join-Path $core "Web\$name")}
Copy-Item -LiteralPath (Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination (Join-Path $core 'Web\WebView2Loader.dll')
foreach($name in @('OrangeShootPresentation.jar','OrangeLaunchBridge.lua','OrangeStartup.jar')){Copy-Item -LiteralPath (Join-Path $project "Assets\$name") -Destination (Join-Path $core "Assets\$name")}
foreach($name in @('VERSION','CHANGELOG.md')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $core $name)}
& (Join-Path $PSScriptRoot 'Build-Guide.ps1') -OutputPath (Join-Path $core '使用说明.txt')
Copy-Item -LiteralPath $public -Destination (Join-Path $core 'release-public-key.xml')
$release=Join-Path $fixture 'release';$again=Join-Path $fixture 'again'
& $tool pack $core $release $private $public
if($LASTEXITCODE -ne 0){throw 'Actual fixture pack failed'}
& $tool pack $core (Join-Path $fixture 'mismatched-key') $private (Join-Path $PSScriptRoot 'release-public-key.xml')
if($LASTEXITCODE -eq 0 -or (Test-Path -LiteralPath (Join-Path $fixture 'mismatched-key'))){throw 'Signing key mismatch did not fail before output'}
& $tool validate (Join-Path $release 'core') $release $public
if($LASTEXITCODE -ne 0){throw 'Actual fixture validation failed'}
$manifest=Get-Content -Raw (Join-Path $release 'update-manifest.json') | ConvertFrom-Json
if($manifest.version -ne $ExpectedVersion){throw 'Signed manifest version mismatch'}
foreach($name in @('橙子启动器.exe','橙子更新助手.exe','卸载橙子启动器.exe')){$info=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $release "core\$name"));if($info.FileVersion -ne "$ExpectedVersion.0" -or $info.ProductVersion -ne $ExpectedVersion){throw "Packaged executable version mismatch: $name"}}
& $tool pack $core $again $private $public
if($LASTEXITCODE -ne 0){throw 'Repeated fixture pack failed'}
foreach($name in @('update-manifest.json','update-manifest.sig',"orange-launcher-v$version-win-x64.zip")){if((Get-FileHash -LiteralPath (Join-Path $release $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $again $name)).Hash){throw "Unchanged fixture output is unstable: $name"}}
& (Join-Path $PSScriptRoot 'Build-Setup.ps1') -CoreReleaseDirectory $release -PublicKey $public
$setup=Join-Path $release "orange-launcher-setup-v$version.exe"
$process=Start-Process -FilePath $setup -ArgumentList '--verify-setup' -PassThru -Wait -WindowStyle Hidden
if($process.ExitCode -ne 0){throw 'Actual installer extraction/uninstall failed'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$extracted=Join-Path $fixture 'extracted';[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $release "orange-launcher-v$version-win-x64.zip"),$extracted)
$neverStarted=Join-Path $fixture 'never-started';[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $release "orange-launcher-v$version-win-x64.zip"),$neverStarted)
& (Join-Path $neverStarted '卸载橙子启动器.exe') --fixture-uninstall
if($LASTEXITCODE -ne 0){throw 'ZIP uninstaller must work before first launcher start'}
& (Join-Path $extracted '橙子启动器.exe')
if($LASTEXITCODE -ne 0){throw 'Direct ZIP fixture failed'}
if((Get-Content -Raw (Join-Path $extracted 'orange-installation.json') | ConvertFrom-Json).version -ne $ExpectedVersion){throw 'Direct ZIP install record version mismatch'}
$sha=[Security.Cryptography.SHA256]::Create()
try{$rootHash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($extracted.ToUpperInvariant()))).Replace('-','')}finally{$sha.Dispose()}
$mutex=[Threading.Mutex]::new($false,('Global\OrangeLauncher.Core.'+$rootHash))
try{if(-not $mutex.WaitOne(0)){throw 'Could not own health fixture mutex'};& (Join-Path $extracted '橙子启动器.exe');if($LASTEXITCODE -ne 0){throw 'Existing owned sidecars must initialize while updater holds transaction mutex'}}finally{$mutex.ReleaseMutex();$mutex.Dispose()}
& (Join-Path $extracted '卸载橙子启动器.exe') --fixture-uninstall
if($LASTEXITCODE -ne 0){throw 'Delayed uninstaller scheduling failed'}
$deadline=[DateTime]::UtcNow.AddSeconds(15)
while((Test-Path -LiteralPath (Join-Path $extracted 'orange-installation.json')) -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
if((Test-Path -LiteralPath (Join-Path $extracted '卸载橙子启动器.exe')) -or (Test-Path -LiteralPath (Join-Path $extracted 'orange-installation.json'))){throw 'Running uninstaller completion left owned executable or record'}
& (Join-Path $project 'Build-Modern.ps1')
$legacy=Join-Path $fixture 'legacy';New-Item -ItemType Directory -Path $legacy | Out-Null
& $compiler /nologo /define:OZL_DIAGNOSTICS /target:exe /platform:x64 /main:PZ3D.CorePackageFixtureTests "/out:$legacy\legacy-fixture.exe" "/resource:$project\Modern.Web.zip,Modern.Web.zip" "/resource:$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll,Modern.Microsoft.Web.WebView2.Core.dll" "/resource:$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll,Modern.Microsoft.Web.WebView2.WinForms.dll" "/resource:$sdk\runtimes\win-x64\native\WebView2Loader.dll,Modern.WebView2Loader.dll" "/resource:$project\Assets\OrangeLaunchBridge.lua,OrangeLaunchBridge.lua" "/resource:$project\Assets\OrangeShootPresentation.jar,OrangeShootPresentation.jar" "/resource:$project\Assets\OrangeStartup.jar,OrangeStartup.jar" "/resource:$PSScriptRoot\VERSION,Launcher.Version.txt" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Security.dll "/reference:$sdk\lib\net462\Microsoft.Web.WebView2.Core.dll" "/reference:$sdk\lib\net462\Microsoft.Web.WebView2.WinForms.dll" $versionAttributes $sources
if($LASTEXITCODE -ne 0){throw 'Legacy fixture compilation failed'}
& (Join-Path $legacy 'legacy-fixture.exe')
if($LASTEXITCODE -ne 0){throw 'Legacy embedded resource fallback failed'}
Write-Output "PASS: actual fixture executable resources/version, deterministic signed package, installer v2, direct ZIP sidecars, running uninstaller completion. Fixture: $fixture"
Get-ChildItem -LiteralPath $release -File | ForEach-Object {[pscustomobject]@{Name=$_.Name;Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash}}
