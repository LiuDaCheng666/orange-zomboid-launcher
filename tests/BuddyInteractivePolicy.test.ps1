param([string]$EvidenceDirectory='E:\Projects\OrangeLauncher\artifacts\connection-overlap-20261031\buddy-policy\latest')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-buddy-policy-'+[Guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($fixture)|Out-Null;[IO.File]::WriteAllText((Join-Path $fixture '.owner'),'OrangeLauncher isolated Buddy authorization arguments')
try {
 $project=Join-Path $repo 'Orange3DTool';$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
 $refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
 $refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
 $resources=@("/resource:$repo/VERSION,Launcher.Version.txt","/resource:$project/Assets/OrangeStartup.jar,OrangeStartup.jar","/resource:$project/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua")
 $sources=@(Get-OzlProductionSources -ProjectDirectory $project)+(Join-Path $PSScriptRoot 'BuddyInteractivePolicyTests.cs')
 $output=Join-Path $fixture 'BuddyInteractivePolicyTests.exe'
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.BuddyInteractivePolicyTests "/out:$output" $refs $resources $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
 if($LASTEXITCODE-ne0){throw 'Buddy interactive policy compilation failed'}
 & $output (Join-Path $fixture 'fixture') 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'native.log')
 if($LASTEXITCODE-ne0){throw 'Buddy interactive policy regression failed'}
} finally {
 $absolute=[IO.Path]::GetFullPath($fixture);$prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(-not $absolute.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)-or-not([IO.File]::Exists((Join-Path $absolute '.owner')))){throw 'Refused cleanup outside owned policy fixture'}
 Remove-Item -LiteralPath $absolute -Recurse -Force
}