param([string]$GameDirectory='E:/steam/steamapps/common/ProjectZomboid',[string]$EvidenceDirectory='')
$ErrorActionPreference='Stop';$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/snapshot-long-java-'+[guid]::NewGuid().ToString('N'))};[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$agent=Join-Path $EvidenceDirectory 'OrangeEnvironment.jar';& (Join-Path $repo 'Build-EnvironmentComponent.ps1') -OutputPath $agent
$classes=Join-Path $EvidenceDirectory 'probe-classes';[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($classes))|Out-Null
& javac -encoding UTF-8 --release 8 -d $classes (Join-Path $PSScriptRoot 'java/environment/ActualLongPathIoProbe.java');if($LASTEXITCODE -ne 0){throw 'Actual JVM child-path probe compilation failed'}
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+@(Join-Path $PSScriptRoot 'SnapshotLongPathJavaProbe.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462';$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"};$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$binary=Join-Path $EvidenceDirectory 'SnapshotLongPathJavaProbe.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.SnapshotLongPathJavaProbe "/win32manifest:$repo/Orange3DTool/app.manifest" "/out:$binary" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log');if($LASTEXITCODE -ne 0){throw 'Long Java probe compilation failed'}
& $binary $GameDirectory $agent $classes 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'results.log');if($LASTEXITCODE -ne 0){throw 'Actual copied JRE long-path probe failed'}
