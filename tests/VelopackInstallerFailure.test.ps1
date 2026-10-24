param([string]$Fixture='artifacts/uniquelifecycle-eb4506d29b7a4f3bb33c310611901bc1/app-1.0.0')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$dir=Join-Path $repo ('artifacts/installer-failure-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory $dir|Out-Null
$source='using System;using System.IO;class FailureSetup {static int Main(string[] args){for(int i=0;i+1<args.Length;i++)if(args[i]=="--log")File.WriteAllText(args[i+1],"fixture-native-error");return 7;}}'
[IO.File]::WriteAllText("$dir/FailureSetup.cs",$source)
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe "/out:$dir/failure-setup.exe" ([IO.Path]::GetFullPath("$dir/FailureSetup.cs"))
if($LASTEXITCODE){throw 'Failure fixture compilation failed'}
& "$repo/build/Build-VelopackSetup.ps1" -OfficialSetup "$dir/failure-setup.exe" -OutputPath "$dir/failure-wrapper.exe" -VersionFile "$Fixture/VERSION" -PublicKey "$Fixture/release-public-key.xml" -PackageId OzlFailureFixture -Title fixture
& "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -File "$PSScriptRoot/VelopackInstallerFailureProbe.ps1" -Wrapper "$dir/failure-wrapper.exe" -Root (Join-Path ([IO.Path]::GetTempPath()) ('ozl-fail-'+[Guid]::NewGuid().ToString('N'))) | Tee-Object "$dir/results.log"
if($LASTEXITCODE){throw 'Failure evidence probe failed'}
