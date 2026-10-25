param([string]$OutputDirectory='', [switch]$Real, [switch]$Corpus)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $taskRoot 'artifacts/log-analysis/corpus-verification'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiled=Join-Path $OutputDirectory ('log-corpus-tests-'+[Guid]::NewGuid().ToString('N').Substring(0,8)+'.exe')
& (Join-Path $taskRoot 'Build-Tests.ps1') -Entry 'PZ3D.GameLogCorpusTests' -Output $compiled
foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){
 Copy-Item -LiteralPath (Join-Path $taskRoot ('Dependencies/WebView2/lib/net462/'+$name)) -Destination $OutputDirectory -Force
}
$sourceHashes=@('GameLogModels.cs','GameLogAnalyzer.cs','GameLogAnalysisService.cs','GameLogCorpusTests.cs') | ForEach-Object {
 $path=Join-Path $taskRoot ('Orange3DTool/'+$_)
 [pscustomobject]@{name=$_;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$sourceHashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'corpus-source-hashes.json') -Encoding UTF8
$env:OZL_CORPUS_OUTPUT=$OutputDirectory
$arguments=@()
if($Corpus){$arguments+='--corpus'}elseif($Real){$arguments+='--real'}
& $compiled @arguments | Tee-Object -FilePath (Join-Path $OutputDirectory 'corpus-run.log')
if($LASTEXITCODE -ne 0){throw 'Game log analysis validation failed; inspect sanitized evidence'}
"PASS game log analysis validation. Evidence: $OutputDirectory"
