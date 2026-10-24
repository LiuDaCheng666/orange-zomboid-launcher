param([Parameter(Mandatory=$true)][string]$Wrapper,[Parameter(Mandatory=$true)][string]$Root,[Parameter(Mandatory=$true)][string]$Evidence,[switch]$NoDesktop)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$a=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Wrapper))
$flags=[Reflection.BindingFlags]'Instance,Static,NonPublic,Public'
$t=$a.GetType('PZ3D.VelopackInstallerService',$true)
$service=[Activator]::CreateInstance($t,$true)
$platform=$a.GetType('PZ3D.InstallerPlatform',$true);$policy=$a.GetType('PZ3D.InstallerDestinationPolicy',$true)
$recommend=$policy.GetMethod('Recommend',$flags)
$probe=[Delegate]::CreateDelegate($recommend.GetParameters()[4].ParameterType,$platform.GetMethod('ProbeRecommendation',$flags))
$facts=$platform.GetMethod('ReadDriveFacts',$flags).Invoke($null,@())
$recommended=$recommend.Invoke($null,@($facts,[Environment]::GetFolderPath('Windows'),$Wrapper,[string](Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs'),$probe))
$recommendedRoot=$recommended.GetType().GetField('Root',$flags).GetValue($recommended)
if([IO.Path]::GetPathRoot($recommendedRoot) -eq [IO.Path]::GetPathRoot([Environment]::GetFolderPath('Windows'))){throw 'Available non-system fixed drive must be recommended'}
$destinationType=$a.GetType('PZ3D.InstallerDestination',$true)
$destination=[Activator]::CreateInstance($destinationType,$true)
$destinationType.GetField('Root',$flags).SetValue($destination,$Root)
$destinationType.GetField('Reason',$flags).SetValue($destination,'隔离测试：用户选择的非系统盘目录')
$formType=$a.GetType('PZ3D.InstallerForm',$true)
$ctor=@($formType.GetConstructors($flags)|Where-Object {$_.GetParameters().Count -eq 4 -and $_.GetParameters()[1].ParameterType.GetGenericArguments().Count -eq 4})[0]
$install=[Delegate]::CreateDelegate($ctor.GetParameters()[1].ParameterType,$service,$t.GetMethod('Install',$flags))
$open=[Delegate]::CreateDelegate($ctor.GetParameters()[2].ParameterType,$service,$t.GetMethod('Open',$flags))
$form=$ctor.Invoke(@($destination,$install,$open,$null))
try{
 $handle=$form.Handle
 $path=$form.Controls.Find('installer-path',$true)[0]
 $check=$form.Controls.Find('installer-desktop-shortcut',$true)[0]
 if($path.Text -cne $Root -or -not $check.Checked){throw 'Chooser must display exact custom path and default desktop checked'}
 $check.Checked=-not $NoDesktop
 $formType.GetMethod('StartInstall',$flags).Invoke($form,@())|Out-Null
 $deadline=[DateTime]::UtcNow.AddSeconds(60)
 while(-not $formType.GetField('terminal',$flags).GetValue($form)){
  [Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 50
  if([DateTime]::UtcNow -gt $deadline){throw 'Chooser installation timeout'}
 }
 if($formType.GetField('ExitCode',$flags).GetValue($form) -ne 0){throw $form.Controls.Find('installer-details',$true)[0].Text}
 $result=$formType.GetField('completed',$flags).GetValue($form)
 if(-not $result -or $result.GetType().GetField('Root',$flags).GetValue($result) -ne $Root){throw 'Installed result must preserve chosen non-C root'}
 $title=$t.GetField('Title',$flags).GetValue($service)
 $appId=$t.GetField('AppId',$flags).GetValue($service)
 $link=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($title+'.lnk')
 if($NoDesktop){if(Test-Path -LiteralPath $link){throw 'Unchecked desktop must omit official shortcut'}}
 else{
  if(-not(Test-Path -LiteralPath $link)){throw 'Checked desktop must create official Chinese shortcut'}
  $shell=New-Object -ComObject WScript.Shell;$lnk=$shell.CreateShortcut($link)
  if($lnk.TargetPath -ne (Join-Path $Root 'current/橙子启动器.exe')){throw 'Chinese shortcut must target actual selected installation'}
  [ordered]@{name=[IO.Path]::GetFileName($link);target=$lnk.TargetPath;arguments=$lnk.Arguments}|ConvertTo-Json|Set-Content (Join-Path $Evidence 'shortcut.json') -Encoding UTF8
  [Runtime.InteropServices.Marshal]::FinalReleaseComObject($lnk)|Out-Null;[Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)|Out-Null
 }
 $reg=Get-ItemProperty -LiteralPath ('HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\'+$appId)
 if([IO.Path]::GetFullPath($reg.InstallLocation).TrimEnd('\') -ne [IO.Path]::GetFullPath($Root).TrimEnd('\')){throw 'Official registration must use chosen path'}
 "PASS Chinese form → official setup; chosen root=$Root; desktop=$(-not $NoDesktop); no application auto-launch"
}finally{$form.Dispose()}
