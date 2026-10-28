param([string]$RunTag='current',[string]$Group='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
$evidence=Join-Path $repo 'artifacts/ordinary-java-membership-1239'
[IO.Directory]::CreateDirectory($evidence)|Out-Null
$bin=Join-Path ([IO.Path]::GetTempPath()) ('ozl-ordinary-java-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($bin)|Out-Null
$probeClasses=Join-Path $bin 'probe-classes'
[IO.Directory]::CreateDirectory($probeClasses)|Out-Null
& javac -encoding UTF-8 --release 8 -Xlint:-options -d $probeClasses (Join-Path $PSScriptRoot 'java/ordinary/OrdinaryJavaManifestProbe.java')
if($LASTEXITCODE -ne 0){throw 'Ordinary Java manifest probe compilation failed'}
$env:OZL_ORDINARY_JAVA_PROBE_CLASSES=$probeClasses
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'OrdinaryJavaMembership.cs')
$refs=@('System.Web.Extensions.dll','System.Windows.Forms.dll','System.Drawing.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll"
$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeStartup.jar,OrangeStartup.jar"
$refs+="/resource:$repo/Orange3DTool/Assets/OrangeLaunchBridge.lua,OrangeLaunchBridge.lua"
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /utf8output /target:exe /main:PZ3D.OrdinaryJavaMembership "/out:$bin/Test.exe" $refs $sources 2>&1|Tee-Object (Join-Path $evidence "$RunTag-compile.log")
if($LASTEXITCODE -ne 0){throw 'Ordinary Java fixture compilation failed'}
$receipt=[ordered]@{bin=$bin;group=$Group;scope='Isolated native controller/files and production manifest JVM probes; no game launch';sources=@($sources|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()}})}
[IO.File]::WriteAllText((Join-Path $evidence "$RunTag-source.json"),($receipt|ConvertTo-Json -Depth 6),[Text.UTF8Encoding]::new($false))
& "$bin/Test.exe" (Join-Path $bin 'isolated-fixture') $Group 2>&1|Tee-Object (Join-Path $evidence "$RunTag-results.log")
if($LASTEXITCODE -ne 0){throw 'Ordinary Java membership regression failed'}
