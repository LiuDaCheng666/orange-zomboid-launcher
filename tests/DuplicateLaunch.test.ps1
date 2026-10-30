param([string]$EvidenceDirectory='E:\Projects\OrangeLauncher\artifacts\duplicate-launch-20261031\latest')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
. (Join-Path $repo 'build/VelopackDependencies.ps1')
$deps=Get-OzlVelopackDependencies
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-duplicate-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
[IO.File]::WriteAllText((Join-Path $fixture '.owner'),'OrangeLauncher isolated duplicate-launch regression')
try {
 $project=Join-Path $repo 'Orange3DTool';$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
 $refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
 $refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll";$refs+="/reference:$($deps.SdkAssembly)";$refs+="/reference:$($deps.NewtonsoftAssembly)"
 $sources=@(Get-OzlVelopackSources -ProjectDirectory $project)+(Join-Path $PSScriptRoot 'DuplicateLaunchTests.cs')
 $output=Join-Path $fixture 'DuplicateLaunchTests.exe'
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_VELOPACK /target:exe /main:PZ3D.DuplicateLaunchTests "/out:$output" "/resource:$repo/VERSION,Launcher.Version.txt" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Duplicate-launch compilation failed'}
 foreach($file in @($deps.SdkAssembly,$deps.NewtonsoftAssembly)){[IO.File]::WriteAllBytes((Join-Path $fixture ([IO.Path]::GetFileName($file))),[IO.File]::ReadAllBytes($file))}
 & $output (Join-Path $fixture 'cases') 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'native.log')
 if($LASTEXITCODE -ne 0){throw 'Duplicate-launch regression failed'}
} finally {
 $absolute=[IO.Path]::GetFullPath($fixture);$tempPrefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(-not $absolute.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase)-or-not([IO.File]::Exists((Join-Path $absolute '.owner')))){throw 'Refused cleanup outside owned temp fixture'}
 Remove-Item -LiteralPath $absolute -Recurse -Force
}