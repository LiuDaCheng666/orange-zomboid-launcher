param([string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/native-state-atomic'}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$fixture=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('ozl-native-state-'+[Guid]::NewGuid().ToString('N'))))
[IO.Directory]::CreateDirectory($fixture)|Out-Null
[IO.File]::WriteAllText((Join-Path $fixture '.owner'),'OrangeLauncher isolated native-state regression')
try {
 $output=Join-Path $fixture 'NativeStateAtomicTests.exe'
 $sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
 $refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
 $refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
 $sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+(Join-Path $PSScriptRoot 'NativeStateAtomicTests.cs')
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION /target:exe /platform:x64 /main:PZ3D.NativeStateAtomicTests "/out:$output" $refs $sources 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Native state regression compilation failed'}
 & $output (Join-Path $fixture 'data') 2>&1 | Tee-Object (Join-Path $EvidenceDirectory 'results.log')
 $code=$LASTEXITCODE
 if(Test-Path -LiteralPath (Join-Path $fixture 'data/results.json')){Copy-Item -LiteralPath (Join-Path $fixture 'data/results.json') -Destination (Join-Path $EvidenceDirectory 'results.json')}
 if($code -ne 0){throw "Native state regression failed (exit $code)"}
} finally {
 $resolved=[IO.Path]::GetFullPath($fixture)
 $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
 if(-not $resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path -Leaf $resolved) -notlike 'ozl-native-state-*'){throw 'Unexpected test cleanup path'}
 if((Get-ChildItem -LiteralPath $resolved -Force -Recurse | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -ne 0){throw 'Unexpected test reparse point; cleanup stopped'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
