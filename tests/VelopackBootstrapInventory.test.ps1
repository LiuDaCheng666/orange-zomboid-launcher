$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'build/Verify-VelopackArchives.ps1')
$taskFiles=@('橙子启动器.exe','VERSION','CHANGELOG.md','使用说明.txt','release-public-key.xml',
 'Web/index.html','Web/app.js','Web/app.css','Web/home.png','Web/orange-app.ico','Web/WebView2-LICENSE.txt',
 'Web/Microsoft.Web.WebView2.Core.dll','Web/Microsoft.Web.WebView2.WinForms.dll','Web/WebView2Loader.dll',
 'Assets/OrangeShootPresentation.jar','Assets/OrangeLaunchBridge.lua',
 'Velopack.dll','Newtonsoft.Json.dll','ThirdPartyNotices/Velopack.txt','ThirdPartyNotices/Newtonsoft.Json.txt')
function Assert-Rejected($taskManifest,[string]$taskVersion){
 $taskRejected=$false
 try{Assert-OzlVelopackBootstrapInventory -Manifest $taskManifest -Version $taskVersion}catch{
  if($_.Exception.Message -ne 'Packaged bootstrap identity failed'){throw}
  $taskRejected=$true
 }
 if(-not $taskRejected){throw 'Invalid package inventory was accepted'}
}
$taskOld=[pscustomobject]@{schema=1;version='1.2.20';files=@($taskFiles|ForEach-Object{[pscustomobject]@{path=$_}})}
Assert-OzlVelopackBootstrapInventory -Manifest $taskOld -Version '1.2.20'
'PASS historical 20-file release remains supported'
$taskNew=[pscustomobject]@{schema=1;version='1.2.21';files=@($taskOld.files)+@([pscustomobject]@{path='Assets/OrangeStartup.jar'})}
Assert-OzlVelopackBootstrapInventory -Manifest $taskNew -Version '1.2.21'
'PASS new release requires and accepts in-game startup payload'
$taskNew.files=@($taskOld.files)
Assert-Rejected $taskNew '1.2.21'
'PASS missing startup component is rejected'
$taskNew.files=@($taskOld.files)+@([pscustomobject]@{path='private.xml'})
Assert-Rejected $taskNew '1.2.21'
'PASS equal-count foreign replacement cannot stand in for startup component'
$taskNew.files=@($taskOld.files)+@([pscustomobject]@{path='Assets/orangestartup.jar'})
Assert-Rejected $taskNew '1.2.21'
'PASS case-changed startup path is rejected'
$taskNew.files=@($taskOld.files)+@([pscustomobject]@{path='Assets/OrangeStartup.jar'},[pscustomobject]@{path='Assets/OrangeStartup.jar'})
Assert-Rejected $taskNew '1.2.21'
'PASS duplicate startup path is rejected'
$taskNew.files=@($taskOld.files)+@([pscustomobject]@{path='Assets/OrangeStartup.jar'})
$taskNew.version='1.2.20'
Assert-Rejected $taskNew '1.2.21'
'PASS mismatched version is rejected'
