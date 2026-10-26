param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$project=Join-Path $repo 'Orange3DTool'
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/save-environments'}
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$fixtureDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ozl-save-environments-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($fixtureDirectory)|Out-Null
$output=Join-Path $fixtureDirectory 'SaveEnvironmentsTests.exe'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)
foreach($name in @('SaveEnvironments.cs','SaveEnvironmentRuntime.cs')){
    $newSource=Join-Path $project $name
    if(Test-Path -LiteralPath $newSource){$sources+=$newSource}
}
$sources+=Join-Path $project 'SaveEnvironmentsTests.cs'
$sources+=Join-Path $project 'LauncherTestPaths.cs'
$sources=@($sources|Select-Object -Unique)
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SaveEnvironmentsTests "/out:$output" "/resource:$repo/release-public-key.xml,Launcher.UpdatePublicKey.xml" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Save environment test compilation failed'}
& $output 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'snapshots.log')
if($LASTEXITCODE -ne 0){throw 'Save environment fixtures failed'}
