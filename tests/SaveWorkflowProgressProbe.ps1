param(
 [string]$AppDirectory,
 [string]$AssemblyPath,
 [string]$Game='E:\steam\steamapps\common\ProjectZomboid',
 [string]$UserDirectory='C:\Users\MC233\Zomboid',
 [string]$SaveId='Apocalypse/2026-08-02_18-36-09',
 [string]$EvidenceDirectory
)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
if(-not $AppDirectory){$AppDirectory=Join-Path $repo 'artifacts/publication-1.2.34/portable/current'}
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/save-progress-real-'+[guid]::NewGuid().ToString('N'))}
$EvidenceDirectory=[IO.Path]::GetFullPath($EvidenceDirectory)
[IO.Directory]::CreateDirectory($EvidenceDirectory)|Out-Null
if($AssemblyPath){$app=[IO.Path]::GetFullPath($AssemblyPath)}else{
 $appFiles=@(Get-ChildItem -LiteralPath ([IO.Path]::GetFullPath($AppDirectory)) -Filter '*.exe' -File)
 if($appFiles.Count -ne 1){throw 'Expected exactly one packaged launcher assembly'}
 $app=$appFiles[0].FullName
}
$assembly=[Reflection.Assembly]::LoadFrom($app)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$assembly.GetType('PZ3D.LauncherRuntime',$true).GetMethod('Initialize',$flags).Invoke($null,@())|Out-Null
$data=Join-Path $EvidenceDirectory 'isolated-data'
$sourceFiles=@((Join-Path $Game 'ProjectZomboid64.json'),(Join-Path $UserDirectory 'mods/default.txt'),(Join-Path $UserDirectory ('Saves/'+$SaveId+'/mods.txt')))
$sourceHashes=@{}
foreach($file in $sourceFiles){if(Test-Path -LiteralPath $file -PathType Leaf){$sourceHashes[$file]=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash}}
$events=[Collections.Generic.List[object]]::new()
$watch=[Diagnostics.Stopwatch]::StartNew()
$nativeEvent=[Action[string,object]]{param($name,$value)
 if($name -ne 'environmentProgress'){return}
 $event=[ordered]@{elapsedMs=$watch.ElapsedMilliseconds;jobId=$value.jobId;saveId=$value.saveId;stage=$value.stage;text=$value.text;value=$value.value;completedFiles=$value.completedFiles;totalFiles=$value.totalFiles;completedBytes=$value.completedBytes;totalBytes=$value.totalBytes;completedItems=$value.completedItems;totalItems=$value.totalItems;currentItem=$value.currentItem}
 $events.Add($event)
}
$type=$assembly.GetType('PZ3D.ModernController',$true)
$ctor=@($type.GetConstructors($flags)|Where-Object {$_.GetParameters().Count -eq 4})[0]
$controller=$null;$result=$null;$errorText=$null
try{
 $controller=$ctor.Invoke([object[]]@([string]$Game,[string]$UserDirectory,[string]$data,[bool]$false))
 $type.GetField('Event',$flags).SetValue($controller,$nativeEvent)
 $payload=[Collections.Generic.Dictionary[string,object]]::new();$payload.Add('saveId',$SaveId);$payload.Add('jobId',[guid]::NewGuid().ToString('N'))
 $result=$type.GetMethod('Execute',$flags).Invoke($controller,[object[]]@('environmentPreview',$payload))
}catch{$errorText=$_.Exception.ToString()}
finally{if($controller){([IDisposable]$controller).Dispose()}}
$unchanged=$true
foreach($file in $sourceHashes.Keys){if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $sourceHashes[$file]){$unchanged=$false}}
$report=[ordered]@{app=$app;appSha256=(Get-FileHash -LiteralPath $app -Algorithm SHA256).Hash;game=$Game;user=$UserDirectory;saveId=$SaveId;testMode=$false;elapsedMs=$watch.ElapsedMilliseconds;eventCount=$events.Count;sourceMetadataUnchanged=$unchanged;sourceHashes=$sourceHashes;events=@($events);result=$result;error=$errorText;boundary='Read-only public ModernController.Execute(environmentPreview); isolated data/cache only; no game start, download, delete, picker, or foreground UI'}
$report|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'report.json') -Encoding utf8
[ordered]@{evidenceDirectory=$EvidenceDirectory;elapsedMs=$report.elapsedMs;eventCount=$events.Count;sourceMetadataUnchanged=$unchanged;error=$errorText;ready=$result.ready;missing=@($result.missing).Count}|ConvertTo-Json -Compress
if(-not $unchanged){throw 'Source metadata changed during read-only probe'}
if($errorText){throw $errorText}
