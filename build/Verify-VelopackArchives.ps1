function Test-OzlVelopackArchives {
 param([string]$ReleaseDirectory,[string]$PublicKey,[string]$Version)
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 $stubTitle=if([Version]$Version -ge [Version]'1.2.16'){'橙子启动器'}else{'OrangeZomboidLauncher'}
 $rsa=[Security.Cryptography.RSACryptoServiceProvider]::new()
 try {
  $rsa.PersistKeyInCsp=$false; $rsa.FromXmlString([IO.File]::ReadAllText($PublicKey))
  foreach($item in @(@("OrangeZomboidLauncher-$Version-full.nupkg",'lib/app/'),@('OrangeZomboidLauncher-win-Portable.zip','current/'))){
   $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $ReleaseDirectory $item[0]))
   try {
    $entries=@{}; foreach($entry in $archive.Entries){if($entries.ContainsKey($entry.FullName)){throw 'Duplicate archive entry'}; $entries[$entry.FullName]=$entry}
    $read={param($name) $entry=$entries[$name]; if(-not $entry){throw "Missing packaged file: $name"}; $stream=$entry.Open(); $memory=[IO.MemoryStream]::new(); try{$stream.CopyTo($memory); ,$memory.ToArray()}finally{$memory.Dispose();$stream.Dispose()}}
    $raw=& $read ($item[1]+'velopack-bootstrap.json'); $sig=& $read ($item[1]+'velopack-bootstrap.sig')
    if(-not $rsa.VerifyData($raw,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'),$sig)){throw 'Packaged bootstrap signature failed'}
    $manifest=[Text.Encoding]::UTF8.GetString($raw)|ConvertFrom-Json
    Assert-OzlVelopackBootstrapInventory -Manifest $manifest -Version $Version
    if([Version]$Version -ge [Version]'1.2.18'){
     $settings=[Xml.XmlReaderSettings]::new();$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
     $metadataStream=[IO.MemoryStream]::new([byte[]](& $read ($item[1]+'sq.version')))
     $reader=[Xml.XmlReader]::Create($metadataStream,$settings);$metadata=[Xml.XmlDocument]::new();$metadata.XmlResolver=$null
     try{$metadata.Load($reader)}finally{$reader.Dispose();$metadataStream.Dispose()}
     $identity=$metadata.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='shortcutAumid']")
     if($null -eq $identity -or $identity.InnerText -cne 'OrangeZomboidLauncher.Launcher'){throw 'Packaged taskbar identity still uses the legacy installation association'}
    }
    $allowed=@{}; foreach($file in $manifest.files){$allowed[$item[1]+$file.path]=$true}
    foreach($name in @('velopack-bootstrap.json','velopack-bootstrap.sig','sq.version')){$allowed[$item[1]+$name]=$true}
    if($item[1] -eq 'current/'){
     foreach($name in @('.portable','Update.exe',($stubTitle+'.exe'))){$allowed[$name]=$true}
    } else {
     foreach($name in @('[Content_Types].xml','OrangeZomboidLauncher.nuspec','_rels/.rels','lib/app/Squirrel.exe',('lib/app/'+$stubTitle+'_ExecutionStub.exe'))){$allowed[$name]=$true}
     if([Version]$Version -ge [Version]'1.2.18'){
      $allowed['setup.ico']=$true
      if([Convert]::ToBase64String((& $read 'setup.ico')) -cne [Convert]::ToBase64String((& $read 'lib/app/Web/orange-app.ico'))){throw 'Framework setup icon differs from the signed application icon'}
     }
    }
    foreach($name in $entries.Keys){if(-not $allowed.ContainsKey($name)){throw "Unexpected packaged file: $name"}}
    foreach($file in $manifest.files){
     $bytes=& $read ($item[1]+$file.path); $sha=[Security.Cryptography.SHA256]::Create()
     try{$hash=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()}
     if($bytes.Length -ne $file.length -or $hash -cne $file.sha256){throw "Packaged file differs from signed bootstrap: $($file.path)"}
    }
    foreach($name in $entries.Keys){if($name -match '(\u6a59\u5b50\u66f4\u65b0\u52a9\u624b|\u5378\u8f7d\u6a59\u5b50\u542f\u52a8\u5668)\.exe$'){throw 'Legacy auxiliary found in Velopack artifact'}}
    if($item[1] -eq 'current/'){
     foreach($name in @('.portable','Update.exe',($stubTitle+'.exe'))){if(-not $entries.ContainsKey($name)){throw "Missing framework portable file: $name"}}
    }
   } finally {$archive.Dispose()}
  }
 } finally {$rsa.Dispose()}
}
function Assert-OzlVelopackBootstrapInventory {
 param($Manifest,[string]$Version)
 # Historical releases have 20 paths. The in-game loading component is required
 # from 1.2.21 onward; validate identities as well as count, matching runtime.
 $expected=@('橙子启动器.exe','VERSION','CHANGELOG.md','使用说明.txt','release-public-key.xml',
  'Web/index.html','Web/app.js','Web/app.css','Web/home.png','Web/orange-app.ico','Web/WebView2-LICENSE.txt',
  'Web/Microsoft.Web.WebView2.Core.dll','Web/Microsoft.Web.WebView2.WinForms.dll','Web/WebView2Loader.dll',
  'Assets/OrangeShootPresentation.jar','Assets/OrangeLaunchBridge.lua',
  'Velopack.dll','Newtonsoft.Json.dll','ThirdPartyNotices/Velopack.txt','ThirdPartyNotices/Newtonsoft.Json.txt')
 if([Version]$Version -ge [Version]'1.2.21'){$expected+='Assets/OrangeStartup.jar'}
 if($Manifest.schema -ne 1 -or $Manifest.version -cne $Version -or @($Manifest.files).Count -ne $expected.Count){throw 'Packaged bootstrap identity failed'}
 $names=@($Manifest.files|ForEach-Object {$_.path})
 foreach($name in $expected){if(@($names|Where-Object {$_ -ceq $name}).Count -ne 1){throw 'Packaged bootstrap identity failed'}}
}
