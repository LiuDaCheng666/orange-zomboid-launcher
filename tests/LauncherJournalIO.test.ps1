param([string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path ([IO.Path]::GetTempPath()) ('ozl-journal-io-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
$project=Join-Path $repo 'Orange3DTool'
$sources=@(Get-OzlProductionSources -ProjectDirectory $project)+(Join-Path $PSScriptRoot 'LauncherJournalIOTests.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462'
$references=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"}
$references+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$references+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$binary=Join-Path $EvidenceDirectory 'LauncherJournalIOTests.exe'
$fixtureParent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$fixtures=Join-Path $fixtureParent ('oj-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$fixtures | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'fixture-root.txt') -Encoding utf8
$sourceBefore=@($sources | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})
try {
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION /target:exe /platform:x64 /main:PZ3D.LauncherJournalIOTests "/win32manifest:$project/app.manifest" "/resource:$repo/VERSION,Launcher.Version.txt" "/out:$binary" $references $sources 2>&1 | Tee-Object -FilePath (Join-Path $EvidenceDirectory 'compile.log')
 if($LASTEXITCODE -ne 0){throw 'Launcher journal IO regression compilation failed'}
 [IO.File]::WriteAllText(($binary+'.config'),'<configuration><runtime><AppContextSwitchOverrides value="Switch.System.IO.UseLegacyPathHandling=false;Switch.System.IO.BlockLongPaths=false" /></runtime></configuration>')
 $sourceAfter=@($sources | ForEach-Object {[pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash}})
 if(($sourceBefore | ConvertTo-Json -Depth 3) -cne ($sourceAfter | ConvertTo-Json -Depth 3)){throw 'Production sources changed during journal IO regression compilation'}
 $sourceAfter | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'source-hashes.json') -Encoding utf8
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName=$binary;$start.Arguments='"'+$fixtures+'"';$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.StandardOutputEncoding=New-Object Text.UTF8Encoding($false);$start.StandardErrorEncoding=New-Object Text.UTF8Encoding($false)
 $process=New-Object Diagnostics.Process
 try {$process.StartInfo=$start;[void]$process.Start();$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(-not $process.WaitForExit(45000)){$process.Kill();throw 'Launcher journal IO regression timed out'};$stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'results.log') -Encoding utf8;$stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'stderr.log') -Encoding utf8;$code=$process.ExitCode}finally{$process.Dispose()}
 $code | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'exit-code.txt') -Encoding ascii
 if($code -ne 0){throw 'Launcher journal IO regression failed; see results.log'}
} finally {
 $fullFixture=[IO.Path]::GetFullPath($fixtures)
 if([IO.Path]::GetDirectoryName($fullFixture) -ne $fixtureParent -or -not [IO.Path]::GetFileName($fullFixture).StartsWith('oj-')){throw 'Cleanup escaped owned journal fixture root'}
 if(Test-Path -LiteralPath $fullFixture){Remove-Item -LiteralPath $fullFixture -Recurse -Force}
 foreach($target in @($binary,($binary+'.config'))) {$full=[IO.Path]::GetFullPath($target);if(-not $full.StartsWith($EvidenceDirectory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Cleanup escaped journal test evidence directory'};if(Test-Path -LiteralPath $full){Remove-Item -LiteralPath $full -Recurse -Force}}
 [pscustomobject]@{fixturePath=$fullFixture;fixtureExists=(Test-Path -LiteralPath $fullFixture);binaryExists=(Test-Path -LiteralPath $binary);configurationExists=(Test-Path -LiteralPath ($binary+'.config'))} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'cleanup.json') -Encoding utf8
}
