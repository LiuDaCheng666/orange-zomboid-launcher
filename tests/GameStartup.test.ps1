param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $taskRoot 'artifacts/game-startup-verification'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiled=Join-Path $OutputDirectory 'startup-tests.exe'
& (Join-Path $taskRoot 'Build-Tests.ps1') -Entry 'PZ3D.GameStartupSuiteTests' -Output $compiled
$fixtureRoot=Join-Path ([IO.Path]::GetTempPath()) ('ozl-startup-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$runner=Join-Path $fixtureRoot 'startup-tests.exe'
Copy-Item -LiteralPath $compiled -Destination $runner
foreach($assembly in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll')){
 Copy-Item -LiteralPath (Join-Path $taskRoot ('Dependencies/WebView2/lib/net462/'+$assembly)) -Destination $fixtureRoot
}
& $runner | Tee-Object -FilePath (Join-Path $OutputDirectory 'native-tests.log')
if($LASTEXITCODE -ne 0){throw 'Native startup regression failed; fixture retained at '+$fixtureRoot}
& python (Join-Path $taskRoot 'Orange3DTool/game-startup-bridge-lua-tests.py') | Tee-Object -FilePath (Join-Path $OutputDirectory 'startup-lua-tests.log')
if($LASTEXITCODE -ne 0){throw 'Startup Lua bridge regression failed'}
& python (Join-Path $taskRoot 'Orange3DTool/launch-bridge-lua-tests.py') | Tee-Object -FilePath (Join-Path $OutputDirectory 'original-lua-tests.log')
if($LASTEXITCODE -ne 0){throw 'Existing Lua bridge regression failed'}
"PASS native startup, original launch/controller/memory regressions and real Lua harness. Logs: $OutputDirectory"
