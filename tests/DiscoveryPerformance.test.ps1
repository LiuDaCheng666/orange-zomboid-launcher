param([Parameter(Mandatory=$true)][string]$BaselineAgent,[Parameter(Mandatory=$true)][string]$CandidateAgent,[Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($EvidenceDirectory);[IO.Directory]::CreateDirectory($root)|Out-Null
$classes=Join-Path $root 'classes';[IO.Directory]::CreateDirectory($classes)|Out-Null
$baseline=[IO.Path]::GetFullPath($BaselineAgent);$candidate=[IO.Path]::GetFullPath($CandidateAgent)
& javac --release 8 -Xlint:-options -encoding UTF-8 -cp $candidate -d $classes (Join-Path $PSScriptRoot 'java/live/DiscoveryTimingProbe.java')
if($LASTEXITCODE){throw 'Discovery performance fixture compilation failed'}
$results=@{}
foreach($item in @(@{name='baseline';jar=$baseline},@{name='candidate';jar=$candidate})){
 $lines=@(& java -cp ($classes+';'+$item.jar) ozl.environment.DiscoveryTimingProbe (Join-Path $root 'owned-fixture'))
 if($LASTEXITCODE){throw ($item.name+' discovery benchmark failed')}
 $lines|Set-Content -LiteralPath (Join-Path $root ($item.name+'.log')) -Encoding UTF8
 $lines|ForEach-Object{Write-Host ($item.name+': '+$_)}
 $samples=@($lines|ForEach-Object{if($_ -match 'millis=(\d+)'){[long]$Matches[1]}}|Sort-Object)
 if($samples.Count -ne 3){throw 'Missing benchmark observations'}
 $results[$item.name]=$samples[1]
}
$ratio=$results.candidate/[double]$results.baseline
@{baselineMedianMs=$results.baseline;candidateMedianMs=$results.candidate;ratio=$ratio;scope='isolated real filesystem filter; no game launched'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding UTF8
if($ratio -ge 0.75){throw ('Filtering must improve by at least 25%; measured ratio '+$ratio)}
Write-Host 'PASS unchanged discovery roots/order and at least 25% lower isolated discovery time'
