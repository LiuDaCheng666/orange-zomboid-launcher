param([string]$OutputDirectory='artifacts/mod-workshop-repair/all-web')
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $repo
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$results=@()
foreach($file in Get-ChildItem Orange3DTool/Web -Filter '*tests.cjs' -File | Sort-Object Name){
 $log=Join-Path $OutputDirectory ($file.BaseName+'.log')
 & node $file.FullName *> $log
 $code=$LASTEXITCODE
 $last=Get-Content -LiteralPath $log -Tail 1
 $results+=@{name=$file.Name;exit=$code;summary=$last}
 Write-Output "$($file.Name) exit=$code $last"
}
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding UTF8
if(@($results | Where-Object {$_.exit -ne 0}).Count){exit 1}
