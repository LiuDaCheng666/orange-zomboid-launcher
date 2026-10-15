param([string]$FixtureRoot='')
$ErrorActionPreference='Stop'
if(-not $FixtureRoot){$FixtureRoot=Join-Path $PSScriptRoot ('artifacts\TestFixtures\running-game-core-'+[Guid]::NewGuid().ToString('N').Substring(0,8))}
$FixtureRoot=[IO.Path]::GetFullPath($FixtureRoot)
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts\TestFixtures')).TrimEnd('\')+'\'
if(-not $FixtureRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'FixtureRoot must be a new child of repository artifacts/TestFixtures'}
for($ancestor=$FixtureRoot;$ancestor;$ancestor=Split-Path -Parent $ancestor){if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Fixture path has a reparse ancestor: $ancestor"}}
if(Test-Path -LiteralPath $FixtureRoot){throw 'FixtureRoot must not exist; retained evidence is never overwritten'}
New-Item -ItemType Directory -Path $FixtureRoot | Out-Null
$results=New-Object 'System.Collections.Generic.List[object]'
Push-Location $PSScriptRoot
try{
 & (Join-Path $PSScriptRoot 'Test-CoreNativeFixturePath.ps1') 2>&1 | Tee-Object -FilePath (Join-Path $FixtureRoot 'fixture-path.log')
 $results.Add([pscustomobject]@{Suite='Test-CoreNativeFixturePath.ps1';ExitCode=0})
 foreach($suite in @(@('PZ3D.CoreRestartPolicyTests','restart-policy'),@('PZ3D.ManagedBackgroundWorkerTests','background-workers'),@('PZ3D.InstallerShortcutTests','installer-shortcuts'))){
  # These established tests locate the repository relative to artifacts/*.exe.
  # The configurable evidence/real-lifecycle roots always remain in TestFixtures.
  $image=Join-Path $PSScriptRoot ("artifacts\running-game-"+$suite[1]+'.exe')
  & (Join-Path $PSScriptRoot 'Build-Tests.ps1') -Entry $suite[0] -Output $image 2>&1 | Tee-Object -FilePath (Join-Path $FixtureRoot ($suite[1]+'-build.log'))
  if($LASTEXITCODE -ne 0){throw "$($suite[0]) build failed: $LASTEXITCODE"}
  & $image 2>&1 | Tee-Object -FilePath (Join-Path $FixtureRoot ($suite[1]+'.log'))
  if($LASTEXITCODE -ne 0){throw "$($suite[0]) failed: $LASTEXITCODE"}
  $results.Add([pscustomobject]@{Suite=$suite[0];ExitCode=$LASTEXITCODE})
 }
 & node (Join-Path $PSScriptRoot 'Orange3DTool\Web\core-update-ready-ui-tests.cjs') 2>&1 | Tee-Object -FilePath (Join-Path $FixtureRoot 'ready-ui.log')
 if($LASTEXITCODE -ne 0){throw "Browser Ready suite failed: $LASTEXITCODE"}
 $results.Add([pscustomobject]@{Suite='core-update-ready-ui-tests.cjs';ExitCode=$LASTEXITCODE})
 & (Join-Path $PSScriptRoot 'Test-CoreNativeUpdate.ps1') -FixtureRoot (Join-Path $FixtureRoot 'native') -IncludeRootMonitorComparison 2>&1 | Tee-Object -FilePath (Join-Path $FixtureRoot 'native-lifecycle.log')
 if($LASTEXITCODE -ne 0){throw "Native lifecycle suite failed: $LASTEXITCODE"}
 $results.Add([pscustomobject]@{Suite='Test-CoreNativeUpdate.ps1 (old monitor characterization + sparse/full healthy/failed + interrupted recovery)';ExitCode=$LASTEXITCODE})
 $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $FixtureRoot 'results.json') -Encoding UTF8
 Write-Output "RUNNING GAME CORE UPDATE: all suites passed; evidence: $FixtureRoot"
}finally{Pop-Location}
