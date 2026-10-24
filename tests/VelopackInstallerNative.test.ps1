param([string]$Template='artifacts/uniquelifecycle-eb4506d29b7a4f3bb33c310611901bc1/app-1.0.0')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. "$repo/build/VelopackDependencies.ps1"
$deps=Get-OzlVelopackDependencies
$id=[Guid]::NewGuid().ToString('N').Substring(0,12)
$evidence=Join-Path $repo ('artifacts/installer-native-'+$id)
New-Item -ItemType Directory $evidence|Out-Null
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-inst-'+$id)
if([IO.Path]::GetPathRoot($fixture) -eq [IO.Path]::GetPathRoot($env:WINDIR)){throw 'Actual fixture requires non-system temp drive'}
New-Item -ItemType Directory $fixture|Out-Null
$appId='OzlInstaller'+$id;$title='橙子启动器安装测试-'+$id
$app=Join-Path $evidence 'app';Copy-Item -LiteralPath $Template -Destination $app -Recurse
$release=Join-Path $evidence 'release'
& $deps.VpkExecutable @($deps.VpkArguments) pack --packId $appId --packTitle $title --packVersion 1.0.0 --packDir $app --outputDir $release --channel win --runtime win-x64 --mainExe '橙子启动器.exe' --framework net48 --delta None --shortcuts Desktop,StartMenuRoot --skip-updates --legacyConsole --yes *> "$evidence/pack.log"
if($LASTEXITCODE){throw 'Fixture vpk failed'}
& "$repo/build/Build-VelopackSetup.ps1" -OfficialSetup "$release/$appId-win-Setup.exe" -OutputPath "$evidence/chooser.exe" -VersionFile "$app/VERSION" -PublicKey "$app/release-public-key.xml" -PackageId $appId -Title $title
$reg='HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\'+$appId
try{
 foreach($mode in @('checked','unchecked')){
  $root=Join-Path $fixture $mode
  $args=@('-NoProfile','-File',"$PSScriptRoot/VelopackInstallerProbe.ps1",'-Wrapper',"$evidence/chooser.exe",'-Root',$root,'-Evidence',$evidence)
  if($mode -eq 'unchecked'){$args+='-NoDesktop'}
  & "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" @args | Tee-Object -FilePath "$evidence/results.log" -Append
  if($LASTEXITCODE){throw "Actual chooser fixture failed: $mode"}
  # The exact unique fixture is the only uninstall target. Never use production ID.
  $record=Get-ItemProperty -LiteralPath $reg
  if([IO.Path]::GetFullPath($record.InstallLocation).TrimEnd('\') -ne [IO.Path]::GetFullPath($root).TrimEnd('\')){throw 'Refuse uninstall of unexpected location'}
  $p=Start-Process -FilePath "$root/Update.exe" -ArgumentList '--silent','uninstall' -WindowStyle Hidden -PassThru
  if(-not $p.WaitForExit(20000) -or $p.ExitCode -ne 0){throw 'Fixture official uninstall failed'}
 }
 'PASS exact chosen non-C path, checked/unchecked Chinese official desktop shortcut, signed installation, official uninstall'|Tee-Object -FilePath "$evidence/results.log" -Append
 "EVIDENCE $evidence"
}finally{
 if(Test-Path $reg){$r=Get-ItemProperty -LiteralPath $reg;if([IO.Path]::GetFullPath($r.InstallLocation).StartsWith([IO.Path]::GetFullPath($fixture)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $reg -Recurse -Force}}
 foreach($folder in @([Environment]::GetFolderPath('DesktopDirectory'),[Environment]::GetFolderPath('Programs'))){$link=Join-Path $folder ($title+'.lnk');if(Test-Path -LiteralPath $link){$shell=New-Object -ComObject WScript.Shell;$lnk=$shell.CreateShortcut($link);try{if($lnk.TargetPath.StartsWith($fixture,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $link -Force}}finally{[Runtime.InteropServices.Marshal]::FinalReleaseComObject($lnk)|Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)|Out-Null}}}
}
