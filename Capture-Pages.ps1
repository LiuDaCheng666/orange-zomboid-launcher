$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot 'Orange3DTool\橙子3D一键启用工具-现代界面.exe'
$shots=Join-Path $PSScriptRoot 'native-shots'
New-Item -ItemType Directory -Path $shots -Force | Out-Null
foreach($theme in @('dark','light')) {
 foreach($page in @('home','saves','servers','mods','workshop','components','shaders','settings','memory','maintenance','help')) {
  $output=Join-Path $shots ($theme+'-'+$page+'.png')
  $info=[Diagnostics.ProcessStartInfo]@{FileName=$exe;Arguments=('--modern-preview "'+$output+'" --page '+$page+' --theme '+$theme+' --size 1100x760');UseShellExecute=$false;CreateNoWindow=$true}
  $own=[Diagnostics.Process]::Start($info)
  if(!$own.WaitForExit(30000)){$own.Kill();throw ('Own preview timeout '+$page)}
  if($own.ExitCode -ne 0 -or !(Test-Path -LiteralPath $output)){throw ('Capture failed '+$page)}
  'CAPTURE_OK '+$theme+' '+$page
 }
}
