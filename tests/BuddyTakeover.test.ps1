param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/buddy-lifecycle'}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
# Keep runtime paths short: private backup transaction names exceed MAX_PATH in a deep worktree.
$fixtureDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ozl-buddy-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($fixtureDirectory)|Out-Null
$output=Join-Path $fixtureDirectory 'BuddyTakeoverTests.exe'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
$sources+=Join-Path $project 'BuddyTakeoverTests.cs'
$sources+=Join-Path $project 'LauncherTestPaths.cs'
$references=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$references+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$references+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$resources=@("/resource:$repo/VERSION,Launcher.Version.txt","/resource:$project/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua","/resource:$project/Assets/OrangeShootPresentation.jar,OrangeShootPresentation.jar","/resource:$project/Assets/OrangeStartup.jar,OrangeStartup.jar")
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.BuddyTakeoverTests "/out:$output" $references $resources $sources 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Buddy takeover fixture compilation failed'}
& $output 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'buddy-suite.log')
if($LASTEXITCODE -ne 0){throw 'Buddy takeover fixtures failed'}
Write-Output "Fixture executable: $output"
Write-Output "Evidence: $EvidenceDirectory"
