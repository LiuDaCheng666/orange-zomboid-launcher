param([Parameter(Mandatory=$true)][string]$ApplicationDirectory,[Parameter(Mandatory=$true)][string]$GameFixtureParent,[Parameter(Mandatory=$true)][string]$DataFixtureParent,[Parameter(Mandatory=$true)][string]$ConfigurationSource)
$ErrorActionPreference='Stop'
$app=[IO.Path]::GetFullPath($ApplicationDirectory)
foreach($name in @('Newtonsoft.Json.dll','Velopack.dll')){[void][Reflection.Assembly]::LoadFrom((Join-Path $app $name))}
$image=@(Get-ChildItem -LiteralPath $app -Filter '*.exe' -File)
if($image.Count -ne 1){throw 'Supply the flat signed app directory containing one launcher'}
$assembly=[Reflection.Assembly]::LoadFrom($image[0].FullName)
$type=$assembly.GetType('PZ3D.ModernController',$true)
$flags=[Reflection.BindingFlags]'Instance,Public,NonPublic'
$id='ozl-effects-'+[Guid]::NewGuid().ToString('N').Substring(0,12)
$game=Join-Path $GameFixtureParent ($id+'/game');$user=Join-Path $DataFixtureParent ($id+'/user');$data=Join-Path $DataFixtureParent ($id+'/data')
[void][IO.Directory]::CreateDirectory($game);[void][IO.Directory]::CreateDirectory($user)
$config=Join-Path $game 'ProjectZomboid64.json'
$original=[IO.File]::ReadAllBytes($ConfigurationSource);[IO.File]::WriteAllBytes($config,$original)
[IO.File]::WriteAllText((Join-Path $game 'ProjectZomboid64.exe'),'inert fixture marker')
$constructor=$type.GetConstructor($flags,$null,[type[]]@([string],[string],[string],[bool]),$null)
$controller=$constructor.Invoke([object[]]@([string]$game,[string]$user,[string]$data,[bool]$true))
$execute=$type.GetMethod('Execute',$flags)
Add-Type -AssemblyName System.Web.Extensions
$json=New-Object Web.Script.Serialization.JavaScriptSerializer
function Invoke-Action([string]$Action,[hashtable]$Payload=@{}){
 $dictionary=New-Object 'Collections.Generic.Dictionary[string,object]'
 foreach($key in $Payload.Keys){$dictionary.Add([string]$key,$Payload[$key])}
 try{$answer=$execute.Invoke($controller,[object[]]@([string]$Action,[Collections.Generic.Dictionary[string,object]]$dictionary));$json.Serialize($answer)|ConvertFrom-Json}catch{throw $_.Exception.GetBaseException()}
}
function Assert-Effect([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message};'PASS '+$Message}
try{
 $memory=Invoke-Action 'memoryGet'
 [void](Invoke-Action 'memorySave' @{value=[int]8;stamp=[string]$memory.stamp})
 Assert-Effect ((Invoke-Action 'memoryGet').currentGiB -eq 8) 'signed production controller saves and reads back 8 GiB across volumes'
 $backups=@(Get-ChildItem -LiteralPath (Join-Path $data 'backups') -Filter ProjectZomboid64.json -Recurse -File)
 Assert-Effect ($backups.Count -eq 1 -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($backups[0].FullName)) -ceq [Convert]::ToBase64String($original)) 'production backup retains exact copied real-game configuration bytes'
 $memory=Invoke-Action 'memoryGet'
 [void](Invoke-Action 'memorySave' @{value=[int]12;stamp=[string]$memory.stamp})
 Assert-Effect ((Invoke-Action 'memoryGet').currentGiB -eq 12) 'same production controller can save another memory value'
 $mp=Join-Path $user 'Saves/Multiplayer/server';$single=Join-Path $user 'Saves/Sandbox/local';$mods=Join-Path $user 'mods/author'
 foreach($path in @((Join-Path $mp 'map/12'),$single,$mods)){[void][IO.Directory]::CreateDirectory($path)}
 $files=@('map/12/34.bin','map_5_6.bin','players.db','vehicles.db','map_symbols.bin','map_t.bin')
 foreach($name in $files){[IO.File]::WriteAllText((Join-Path $mp $name),'fixture bytes')}
 foreach($path in @((Join-Path $single 'map_1_2.bin'),(Join-Path $mods 'mod.info'))){[IO.File]::WriteAllText($path,'retained bytes')}
 [void](Invoke-Action 'maintenance' @{kind='cache';confirmed=$true})
 Assert-Effect (-not [IO.File]::Exists((Join-Path $mp 'map/12/34.bin')) -and -not [IO.File]::Exists((Join-Path $mp 'map_5_6.bin')) -and [IO.File]::Exists((Join-Path $mp 'players.db'))) 'production normal cleanup deletes both chunk formats and retains character data'
 [void](Invoke-Action 'maintenance' @{kind='cacheAll';confirmed=$true})
 $mpRoot=Join-Path $user 'Saves/Multiplayer'
 Assert-Effect (@(Get-ChildItem -LiteralPath $mpRoot -Force).Count -eq 0) 'production full cleanup deletes every multiplayer file and child directory'
 Assert-Effect ([IO.File]::ReadAllText((Join-Path $single 'map_1_2.bin')) -ceq 'retained bytes' -and [IO.File]::ReadAllText((Join-Path $mods 'mod.info')) -ceq 'retained bytes') 'production full cleanup preserves single-player and mod bytes'
 $history=Invoke-Action 'taskGet'
 Assert-Effect (($history|ConvertTo-Json -Depth 20).Contains('completed')) 'actual writes appear as completed tasks'
 'PASS production file effects; fixture retained: '+$id
}finally{$controller.Dispose()}
