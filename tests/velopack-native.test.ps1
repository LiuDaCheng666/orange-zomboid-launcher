$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not(Test-Path "$root/Orange3DTool/VelopackUpdateBackend.cs")){throw 'RED: authenticated Velopack backend is not implemented'}
. "$root/build/ProductionSources.ps1"
. "$root/build/VelopackDependencies.ps1"
$deps=Get-OzlVelopackDependencies
$out="$root/artifacts/velopack-native-tests"
New-Item -ItemType Directory -Force $out | Out-Null
Copy-Item $deps.SdkAssembly,$deps.NewtonsoftAssembly $out -Force
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider 2048
$rsa.PersistKeyInCsp=$false
[IO.File]::WriteAllText("$out/fixture-private.xml",$rsa.ToXmlString($true))
[IO.File]::WriteAllText("$out/fixture-public.xml",$rsa.ToXmlString($false))
$rsa.Dispose()
$sdk="$root/Dependencies/WebView2/lib/net462"
$sources=@(Get-OzlVelopackSources -ProjectDirectory "$root/Orange3DTool")
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_VELOPACK /target:exe /main:PZ3D.VelopackNativeTests "/out:$out/tests.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll /reference:System.Web.dll /reference:System.Net.Http.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/reference:$sdk/Microsoft.Web.WebView2.Core.dll" "/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll" "/reference:$($deps.SdkAssembly)" "/reference:$($deps.NewtonsoftAssembly)" "/resource:$root/VERSION,Launcher.Version.txt" "/resource:$out/fixture-public.xml,Launcher.UpdatePublicKey.xml" $sources ([IO.Path]::GetFullPath("$root/tests/VelopackNativeTests.cs")) ([IO.Path]::GetFullPath("$root/tests/VelopackLeaseTests.cs"))
if($LASTEXITCODE){throw 'Native test compilation failed'}
& "$out/tests.exe"
if($LASTEXITCODE){throw 'Native tests failed'}
