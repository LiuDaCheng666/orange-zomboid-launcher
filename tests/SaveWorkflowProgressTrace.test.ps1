param([Parameter(Mandatory=$true)][string]$Report)
$ErrorActionPreference='Stop'
$trace=Get-Content -LiteralPath $Report -Raw | ConvertFrom-Json
if($trace.testMode -ne $false -or -not $trace.sourceMetadataUnchanged){throw 'Trace must come from read-only public native API with TestMode=false'}
$mods=@($trace.events | Where-Object stage -eq 'mod-inventory')
if(-not ($mods | Where-Object {$_.totalItems -gt 0 -and $_.completedItems -gt 0})){throw 'Native mod inventory must report real completed/total source folders'}
$previousFiles=0L;$previousBytes=0L
foreach($row in $mods){if($row.completedFiles -lt $previousFiles -or $row.completedBytes -lt $previousBytes){throw 'Native mod inventory counts must accumulate instead of restarting for every source'};$previousFiles=$row.completedFiles;$previousBytes=$row.completedBytes}
if($mods[-1].completedItems -ne $mods[-1].totalItems){throw 'Native source-folder denominator must complete exactly'}
$game=@($trace.events | Where-Object stage -eq 'game-inventory')
if(-not ($game | Where-Object {$_.currentItem -and $_.completedFiles -gt 0 -and $_.completedBytes -gt 0})){throw 'Unknown-total game scan must report actual current file and discovered counters'}
if($game | Where-Object {$_.totalFiles -eq 0 -and $_.value -ge 0}){throw 'Unknown game inventory total cannot emit an invented percentage'}
if($trace.events[-1].stage -ne 'preview-complete'){throw 'Public preview must report its completed state before returning its plan'}
Write-Output "PASS real native progress trace: $($trace.eventCount) events, $($mods[-1].totalItems) completed mod source folders"
