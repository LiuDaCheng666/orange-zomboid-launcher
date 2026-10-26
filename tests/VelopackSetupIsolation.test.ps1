param([Parameter(Mandatory=$true)][string]$Evidence,[Parameter(Mandatory=$true)][ValidateSet('checked','unchecked')][string]$Mode,[switch]$ExpectOpened,[switch]$Uninstall)
$ErrorActionPreference='Stop'
$state=Get-Content -LiteralPath (Join-Path $Evidence 'fixture.json') -Raw|ConvertFrom-Json
$fixture=[IO.Path]::GetFullPath($state.fixture)
if($state.appId -cnotmatch '^OzlSetupOnly[a-f0-9]{12}$' -or [IO.Path]::GetFileName($fixture) -cnotmatch '^ozl-setup-[a-f0-9]{32}$'){throw 'Not an isolated installation identity'}
$root=Join-Path $fixture $Mode
$reg='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\'+$state.appId
$registration=Get-ItemProperty -LiteralPath $reg
if([IO.Path]::GetFullPath($registration.InstallLocation).TrimEnd('\') -cne $root){throw 'Unique registration does not point at exact fixture root'}
$manifest=Get-Content -LiteralPath "$($state.app)/velopack-bootstrap.json" -Raw|ConvertFrom-Json
if(@($manifest.files).Count -ne 21){throw 'Fixture must verify all 21 application files'}
foreach($file in $manifest.files){$path=Join-Path "$root/current" $file.path;if(-not(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -ne $file.length -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $file.sha256){throw "Installed bytes differ: $($file.path)"}}
$opened=Test-Path -LiteralPath "$root/current/fixture-opened.txt"
if($opened -ne [bool]$ExpectOpened){throw 'Installed fixture launch does not match the explicit Open action'}
if($ExpectOpened -and [IO.File]::ReadAllText("$root/current/fixture-opened.txt") -cne 'isolated fixture only'){throw 'Explicit Open did not reach the inert installed fixture'}
$desktop=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($state.title+'.lnk')
$programs=Join-Path ([Environment]::GetFolderPath('Programs')) ($state.title+'.lnk')
if((Test-Path -LiteralPath $desktop) -ne ($Mode -eq 'checked')){throw 'Desktop option does not match actual shortcut result'}
if(-not(Test-Path -LiteralPath $programs)){throw 'Official Start Menu shortcut missing'}
if($Mode -eq 'checked'){
 $shell=New-Object -ComObject WScript.Shell;$link=$shell.CreateShortcut($desktop)
 try{if($link.TargetPath -cne "$root\current\橙子启动器.exe"){throw 'Desktop shortcut targets another installation'}}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($link)|Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)|Out-Null}
}
"PASS actual $Mode installation: exact non-system root, 21 original application files, unique registry/shortcuts, explicitly opened=$([bool]$ExpectOpened)"|Tee-Object -FilePath "$Evidence/results.log" -Append
if($Uninstall){
 # The validated unique registration and direct fixture child are the only target.
 if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($root)) -cne $fixture){throw 'Uninstall target is outside fixture'}
 $process=Start-Process -FilePath "$root/Update.exe" -ArgumentList '--silent','uninstall' -WindowStyle Hidden -PassThru
 if(-not $process.WaitForExit(30000) -or $process.ExitCode -ne 0){throw 'Official isolated uninstall failed'}
 if((Test-Path -LiteralPath $reg) -or (Test-Path -LiteralPath $desktop) -or (Test-Path -LiteralPath $programs) -or (Test-Path -LiteralPath "$root/current/橙子启动器.exe")){throw 'Official uninstall left registered application or shortcuts'}
 "PASS actual $Mode official uninstall: unique registry/shortcuts/main removed"|Tee-Object -FilePath "$Evidence/results.log" -Append
}
