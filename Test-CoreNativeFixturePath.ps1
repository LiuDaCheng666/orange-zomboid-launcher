$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'CoreNativeFixturePath.TestHelper.ps1')
$root=Join-Path $PSScriptRoot 'artifacts\TestFixtures\path-predicate-fixture'
$image=Join-Path $root 'child\helper.exe'
$sibling=Join-Path ($root+'-sibling') 'helper.exe'
$passed=0
foreach($spelling in @($root,($root+'\'))) {
 $prefix=Get-CoreNativeFixtureDescendantPrefix $spelling
 foreach($case in @(@($image,$true,'descendant'),@($sibling,$false,'sibling-prefix'),@($root,$false,'root itself'))) {
  $actual=$case[0].StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)
  if($actual -ne $case[1]){throw "FAIL: $($case[2]) with root '$spelling': expected $($case[1]), got $actual"}
  $passed++;Write-Output "PASS: $($case[2]) with root '$spelling'"
 }
}
Write-Output "CORE NATIVE FIXTURE PATH: $passed PASS (no processes or files created)"
