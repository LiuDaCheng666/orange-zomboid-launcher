param([string]$EvidenceDirectory='E:\Projects\OrangeLauncher\artifacts\update-announcement-20261031',[switch]$WithUi)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-ann-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
[IO.File]::WriteAllText((Join-Path $fixture '.owner'),'OrangeLauncher isolated update announcement tests')
try {
 $project=Join-Path $repo 'Orange3DTool';$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
 $refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
 $refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
 $sources=@(Get-OzlProductionSources -ProjectDirectory $project)+(Join-Path $PSScriptRoot 'UpdateAnnouncementTests.cs')
 $output=Join-Path $fixture 'UpdateAnnouncementTests.exe'
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.UpdateAnnouncementTests "/out:$output" "/resource:$repo/VERSION,Launcher.Version.txt" "/resource:$repo/CHANGELOG.md,Launcher.Changelog.md" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Update announcement test compilation failed'}
 & $output (Join-Path $fixture 'native') 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'native.log')
 if($LASTEXITCODE -ne 0){throw 'Update announcement native regression failed'}
 if($WithUi){
  $webFixture=Join-Path $fixture 'Web';[IO.Directory]::CreateDirectory($webFixture)|Out-Null
  $webInputs=foreach($name in @('index.html','app.js','app.css','home.png','orange-app.ico','WebView2-LICENSE.txt')){$from=Join-Path $project ('Web/'+$name);$to=Join-Path $webFixture $name;[IO.File]::WriteAllBytes($to,[IO.File]::ReadAllBytes($from));[pscustomobject]@{file=$name;sha256=(Get-FileHash -LiteralPath $to -Algorithm SHA256).Hash}}
  [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'web-input-hashes.json'),($webInputs|ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
  $priorWebRoot=$env:OZL_TEST_WEB_ROOT
  try{$env:OZL_TEST_WEB_ROOT=$webFixture;& node (Join-Path $PSScriptRoot 'UpdateAnnouncementUi.cjs') $output (Join-Path $fixture 'ui') $EvidenceDirectory 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'ui.log');if($LASTEXITCODE -ne 0){throw 'Update announcement headless UI failed'}}finally{$env:OZL_TEST_WEB_ROOT=$priorWebRoot}
 }
} finally {
 $absolute=[IO.Path]::GetFullPath($fixture);$tempPrefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(-not $absolute.StartsWith($tempPrefix,[StringComparison]::OrdinalIgnoreCase)-or-not([IO.File]::Exists((Join-Path $absolute '.owner')))){throw 'Refused cleanup outside owned temp fixture'}
 Remove-Item -LiteralPath $absolute -Recurse -Force
}