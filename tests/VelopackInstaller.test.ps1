param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$release=[IO.File]::ReadAllText("$repo/Build-VelopackRelease.ps1")
if($release -notmatch '--packTitle\s+[''"'']?橙子启动器'){throw 'RED: release must set the official Chinese shortcut title'}
if(-not(Test-Path "$repo/build/Build-VelopackSetup.ps1")){throw 'RED: Chinese chooser bootstrap build missing'}
if($release -notmatch 'Build-VelopackSetup'){throw 'RED: published Setup must use the chooser bootstrap'}
'PASS installer release contract'
