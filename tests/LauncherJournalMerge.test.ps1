param([string]$EvidenceDirectory='', [string]$JournalSource='')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo 'artifacts/journal-merge'}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory);[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$fixture=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('ozl-journal-merge-'+[Guid]::NewGuid().ToString('N'))));[IO.Directory]::CreateDirectory($fixture)|Out-Null;[IO.File]::WriteAllText((Join-Path $fixture '.owner'),'OrangeLauncher isolated journal merge regression')
try {
 $sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
 $refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"};$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
 $sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))
 if($JournalSource){$JournalSource=[IO.Path]::GetFullPath($JournalSource);if(-not [IO.File]::Exists($JournalSource)){throw 'Explicit red-baseline journal source is missing'};$sources=@($sources|Where-Object{[IO.Path]::GetFileName($_) -ne 'LauncherJournal.cs'})+$JournalSource}
 $sources+=Join-Path $PSScriptRoot 'LauncherJournalMergeTests.cs';$exe=Join-Path $fixture 'LauncherJournalMergeTests.exe'
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:exe /main:PZ3D.LauncherJournalMergeTests "/out:$exe" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log');if($LASTEXITCODE -ne 0){throw 'Journal merge test compilation failed'}
 & $exe (Join-Path $fixture 'data') 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'results.log');$code=$LASTEXITCODE
 [IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'exit.txt'),[string]$code)
 if($code -ne 0){throw "Journal merge regression failed, actual exit$code; see real native two-process evidence"}
} finally {
 $prefix=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\';if(-not $fixture.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)-or-not [IO.File]::Exists((Join-Path $fixture '.owner'))){throw 'Refusing unexpected cleanup path'}
 if(@(Get-ChildItem -LiteralPath $fixture -Force -Recurse|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Refusing unexpected reparse cleanup'}
 Remove-Item -LiteralPath $fixture -Recurse -Force;[IO.File]::WriteAllText((Join-Path $EvidenceDirectory 'cleanup.txt'),'Removed own fixture: '+$fixture+'; exists='+[IO.Directory]::Exists($fixture))
}
