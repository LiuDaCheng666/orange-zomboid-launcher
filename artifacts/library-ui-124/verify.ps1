param([ValidateSet('Native','Browser','BuildFlows','Candidate','Visual','Proof')][string]$Gate='Native',[string]$Candidate='')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Set-Location -LiteralPath $repo
$engine=(Get-Process -Id $PID).Path
$run=Join-Path $PSScriptRoot ('logs-'+$Gate+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $run | Out-Null
function Step([string]$Name,[string]$Exe,[string[]]$Arguments){
 $command=$Exe+' '+(($Arguments | ForEach-Object {'"'+$_+'"'}) -join ' ')
 Add-Content -LiteralPath (Join-Path $run 'commands.txt') -Value $command
 & $Exe @Arguments *> (Join-Path $run ($Name+'.log'))
 $code=$LASTEXITCODE
 Add-Content -LiteralPath (Join-Path $run 'commands.txt') -Value ('EXIT '+$code)
 Write-Output ($Name+': exit '+$code)
 if($code -ne 0){Get-Content -LiteralPath (Join-Path $run ($Name+'.log'));throw ($Name+' failed')}
}
if($Gate -eq 'Native'){
 Copy-Item Dependencies/WebView2/lib/net462/*.dll -Destination $PSScriptRoot
 foreach($name in @('BrandingTests','SaveDeletionTests','SaveProfilesTests','ModernControllerTests','ModernHostTests','LauncherStatusRestoreTests','LaunchProfilesTests','ServerProfilesTests','LauncherMaintenanceTests','InstallerShortcutTests','InstallerExperienceTests','CoreReleaseProtocolTests','CorePackageTests','CoreUpdateServiceTests','CoreUpdateTransactionTests','CoreUpdaterIntegrationTests','CoreRestartPolicyTests','CoreFinalRegressionTests')){
  $exe=Join-Path $PSScriptRoot ($name+'.exe')
  Step ('build-'+$name) $engine @('-NoProfile','-File',(Join-Path $repo 'Build-Tests.ps1'),'-Entry',('PZ3D.'+$name),'-Output',$exe)
  Step $name $exe @()
 }
}
if($Gate -eq 'Browser'){
 foreach($file in Get-ChildItem -LiteralPath Orange3DTool/Web -Filter '*ui-tests.cjs'){
  Step $file.BaseName 'node' @($file.FullName)
 }
}
if($Gate -eq 'BuildFlows'){
 foreach($script in @('Test-CorePackageBuild.ps1','Test-LegacySetupBuild.ps1')){
  $beforeTemp=$env:TEMP;$beforeTmp=$env:TMP
  try{$temp=Join-Path $repo ('artifacts/TestFixtures/t124-'+[Guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $temp | Out-Null;$env:TEMP=$temp;$env:TMP=$temp;Step $script $engine @('-NoProfile','-File',(Join-Path $repo $script),'-ExpectedVersion','1.2.4')}finally{$env:TEMP=$beforeTemp;$env:TMP=$beforeTmp}
 }
}
if($Gate -eq 'Candidate'){
 if(-not $Candidate){throw 'A fresh candidate directory is required'}
 Step 'signed-local-build' $engine @('-NoProfile','-File',(Join-Path $repo 'Build-CoreRelease.ps1'),'-SigningKey','E:/Projects/OrangeLauncher-Signing/release-private.xml','-OutputDirectory',$Candidate)
}
if($Gate -eq 'Visual'){Step 'visual-check' 'node' @((Join-Path $PSScriptRoot 'visual-check.cjs'))}
if($Gate -eq 'Proof'){Step 'candidate-proof' $engine @('-NoProfile','-File',(Join-Path $PSScriptRoot 'verify-candidate.ps1'),'-Candidate',$Candidate)}
Write-Output ('EVIDENCE: '+$run)
