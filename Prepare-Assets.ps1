param([switch]$PrivatePresets)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$project=Join-Path $PSScriptRoot 'Orange3DTool'
$bundleName=if($PrivatePresets){'Orange3D.PrivateBundle.zip'}else{'Orange3D.Bundle.zip'}
$destination=Join-Path $project $bundleName
$file=$destination+'.new-'+[Guid]::NewGuid().ToString('N')+'.zip'
$stream=[IO.File]::Open($file,[IO.FileMode]::CreateNew)
$archive=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create)
try{
  $archive.CreateEntry('PresetShaders/') | Out-Null
  $entry=$archive.CreateEntry('ThirdPartyNotices/NOTICE.txt')
  $writer=New-Object IO.StreamWriter($entry.Open(),(New-Object Text.UTF8Encoding($false)))
  try{$writer.Write([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docs\THIRD-PARTY.md')))}finally{$writer.Dispose()}
  if($PrivatePresets){
    $packs=Join-Path $project 'PresetShaders'
    if(-not (Test-Path -LiteralPath $packs)){throw 'Private preset resources were not provided locally'}
    foreach($item in Get-ChildItem -LiteralPath $packs -Filter '*.zip'){
      [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$item.FullName,('PresetShaders/'+$item.Name)) | Out-Null
    }
    $notice=Join-Path $project 'ThirdPartyNotices\NOTICE.txt'
    if(Test-Path -LiteralPath $notice){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$notice,'ThirdPartyNotices/PRIVATE-RESOURCES.txt') | Out-Null}
  }
}finally{$archive.Dispose();$stream.Dispose()}
if(Test-Path -LiteralPath $destination){[IO.File]::Replace($file,$destination,[System.Management.Automation.Language.NullString]::Value)}else{[IO.File]::Move($file,$destination)}
