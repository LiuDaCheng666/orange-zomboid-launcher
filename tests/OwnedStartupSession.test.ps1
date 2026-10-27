$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$out=Join-Path ([IO.Path]::GetTempPath()) ('ozl-own-jvm-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($out)|Out-Null
& javac -encoding UTF-8 -d $out "$repo/Orange3DTool/StartupSource/ozl/startup/Session.java" "$repo/Orange3DTool/StartupSource/ozl/startup/LoadingModel.java" "$repo/tests/java/startup/OwnedStartupSessionTest.java"
if($LASTEXITCODE -ne 0){throw 'JVM receipt compilation failed'}
& java -cp $out OwnedStartupSessionTest $out
if($LASTEXITCODE -ne 0){throw 'JVM receipt assertion failed'}
