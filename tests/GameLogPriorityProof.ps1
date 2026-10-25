param([Parameter(Mandatory=$true)][string]$AppDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$DownloadsDirectory='D:\User\MC233\Downloads',[string]$LocalUser='C:\Users\MC233\Zomboid')
$ErrorActionPreference='Stop'
[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($OutputDirectory))|Out-Null
$taskExecutable=Join-Path ([IO.Path]::GetFullPath($AppDirectory)) '橙子启动器.exe'
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskExecutable)
$taskFlags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$taskService=$taskAssembly.GetType('PZ3D.GameLogAnalysisService',$true)
$taskAnalyze=$taskService.GetMethod('AnalyzePaths',$taskFlags)
$taskDiscover=$taskService.GetMethod('DiscoverLocal',$taskFlags)
$taskCases=@([ordered]@{name='local-console';paths=[string[]]@((Join-Path $LocalUser 'console.txt'));origin='local'})
$taskDiscoveryArgs=New-Object object[] 3
$taskDiscoveryArgs[0]=$LocalUser;$taskDiscoveryArgs[1]='';$taskDiscoveryArgs[2]=[Threading.CancellationToken]::None
$taskDiscovered=[string[]]$taskDiscover.Invoke($null,$taskDiscoveryArgs)
$taskCases+=,[ordered]@{name='local-default';paths=$taskDiscovered;origin='local'}
foreach($taskNumber in @(30,34,35,56,65,70)){$taskCases+=,[ordered]@{name=('console-'+$taskNumber);paths=[string[]]@((Join-Path $DownloadsDirectory ('console ('+$taskNumber+').txt')));origin='imported'}}
$taskResults=@()
function Assert-PriorityProof([bool]$condition,[string]$message){if(-not $condition){throw ('Production priority regression: '+$message)};Write-Output ('PASS '+$message)}
foreach($taskCase in $taskCases){
 $taskArguments=New-Object object[] 4
 $taskArguments[0]=[string[]]$taskCase.paths;$taskArguments[1]=[string]$taskCase.origin;$taskArguments[2]=[Threading.CancellationToken]::None;$taskArguments[3]=$null
 $taskReport=$taskAnalyze.Invoke($null,$taskArguments)
 $taskReportDirectory=Join-Path $OutputDirectory 'reports';[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($taskReportDirectory))|Out-Null
 Add-Type -AssemblyName System.Web.Extensions
 $taskSerializer=New-Object Web.Script.Serialization.JavaScriptSerializer
 $taskSerializer.MaxJsonLength=2097152
 [IO.File]::WriteAllText([IO.Path]::GetFullPath((Join-Path $taskReportDirectory ($taskCase.name+'.json'))),$taskSerializer.Serialize($taskReport),[Text.UTF8Encoding]::new($false))
 $taskPrimary=@($taskReport.issues|Where-Object {if($_.PSObject.Properties['attention']){$_.attention -eq 'primary'}else{$_.severity -eq 'error'}})
 if($taskCase.name -eq 'local-default'){
  $taskConsole=$taskResults|Where-Object {$_.name -eq 'local-console'}
  Assert-PriorityProof ($taskReport.errorCount -eq $taskConsole.rawErrors -and $taskReport.warningCount -eq $taskConsole.rawWarnings) 'local console and discovery do not duplicate mirrored ERROR/WARN records'
  Assert-PriorityProof ($taskPrimary.Count -eq 0 -and $taskReport.state -eq 'unknown') 'actual local resource diagnostics do not fabricate a core fault or successful entry'
 }
 if($taskCase.name -in @('console-30','console-56')){Assert-PriorityProof ($taskReport.state -eq 'failed' -and @($taskPrimary|Where-Object {$_.impact -eq 'blocking'}).Count -gt 0) ($taskCase.name+' preserves actual blocking failure')}
 if($taskCase.name -in @('console-34','console-35','console-65','console-70')){Assert-PriorityProof (@($taskPrimary|Where-Object {$_.type -like 'lua-*' -and $_.frames.Length -gt 0}).Count -gt 0) ($taskCase.name+' preserves actionable Lua fault and script position')}
 $taskResults+=[ordered]@{
  name=$taskCase.name;files=@($taskCase.paths|ForEach-Object {[IO.Path]::GetFileName($_)});state=$taskReport.state;stage=$taskReport.stage;summary=$taskReport.summary;rawErrors=$taskReport.errorCount;rawWarnings=$taskReport.warningCount;incomplete=$taskReport.incomplete
  primaryGroups=$taskPrimary.Count;reviewGroups=@($taskReport.issues|Where-Object {$_.attention -eq 'review'}).Count;backgroundGroups=@($taskReport.issues|Where-Object {$_.attention -eq 'background'}).Count
  types=@($taskReport.issues|Group-Object type|ForEach-Object {[ordered]@{type=$_.Name;groups=$_.Count;occurrences=($_.Group|Measure-Object count -Sum).Sum}})
  primary=@($taskPrimary|Select-Object -First 8|ForEach-Object {[ordered]@{type=$_.type;title=$_.title;meaning=$_.meaning;count=$_.count;impact=$_.impact;mods=$_.mods;frames=@($_.frames|Select-Object -First 3 mod,file,function,line)}})
 }
 Write-Output ('PASS actual production analysis '+$taskCase.name+': '+$taskPrimary.Count+' primary groups; '+$taskReport.errorCount+' raw ERROR records')
}
$taskProof=[ordered]@{assemblyVersion=$taskAssembly.GetName().Version.ToString();assemblySha256=(Get-FileHash -LiteralPath $taskExecutable).Hash;actualProductionAssembly=$true;rawLogsUploaded=$false;cases=$taskResults}
$taskProof|ConvertTo-Json -Depth 10|Set-Content -LiteralPath (Join-Path $OutputDirectory 'priority-proof.json') -Encoding UTF8
