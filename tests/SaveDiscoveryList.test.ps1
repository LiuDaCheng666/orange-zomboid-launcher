param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ozl-save-discovery-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$project=Join-Path $repo 'Orange3DTool'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)+(Join-Path $PSScriptRoot 'SaveDiscoveryListTests.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$references=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$references+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$references+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$resources=@("/resource:$repo/VERSION,Launcher.Version.txt","/resource:$project/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua","/resource:$project/Assets/OrangeShootPresentation.jar,OrangeShootPresentation.jar","/resource:$project/Assets/OrangeStartup.jar,OrangeStartup.jar")
$binary=Join-Path $EvidenceDirectory 'SaveDiscoveryListTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SaveDiscoveryListTests "/win32manifest:$project/app.manifest" "/out:$binary" $references $resources $sources 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Save discovery regression compilation failed'}
[IO.File]::WriteAllText(($binary+'.config'),'<configuration><runtime><AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false;Switch.System.IO.BlockLongPaths=false" /></runtime></configuration>')
& $binary (Join-Path $EvidenceDirectory 'fixtures') 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'results.log')
if($LASTEXITCODE -ne 0){throw 'Save discovery list regression failed'}
