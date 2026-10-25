param([Parameter(Mandatory=$true)][string]$AppDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory,[Parameter(Mandatory=$true)][string]$DownloadsDirectory)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$taskExe=Join-Path $AppDirectory '橙子启动器.exe'
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskExe)
$taskFlags=[Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
$taskMethod=$taskAssembly.GetType('PZ3D.GameLogAnalysisService',$true).GetMethod('AnalyzePaths',$taskFlags)
$taskCases=@()
foreach($taskNumber in @(35,69,30,56)){
 $taskInput=Join-Path $DownloadsDirectory ('console ('+$taskNumber+').txt')
 $taskArguments=New-Object object[] 4
 $taskArguments[0]=[string[]]@($taskInput);$taskArguments[1]='imported';$taskArguments[2]=[Threading.CancellationToken]::None;$taskArguments[3]=$null
 $taskClock=[Diagnostics.Stopwatch]::StartNew();$taskReport=$taskMethod.Invoke($null,$taskArguments)
 $taskExpected=if($taskNumber -in @(35,69)){'entered-with-issues'}else{'failed'}
 if($taskReport.state -ne $taskExpected){throw ('Production case '+$taskNumber+' expected '+$taskExpected+' got '+$taskReport.state)}
 if($taskNumber -eq 30 -and -not @($taskReport.issues|Where-Object {$_.type -eq 'world-load'}).Count){throw 'World failure evidence missing'}
 if($taskNumber -eq 56 -and -not @($taskReport.issues|Where-Object {$_.type -eq 'missing-resource' -and $_.impact -eq 'blocking'}).Count){throw 'Required resource evidence missing'}
 $taskCases+=[ordered]@{source=[IO.Path]::GetFileName($taskInput);state=$taskReport.state;origin=$taskReport.origin;issueTypes=@($taskReport.issues|ForEach-Object {$_.type}|Select-Object -Unique);incomplete=$taskReport.incomplete;milliseconds=$taskClock.ElapsedMilliseconds}
}
$taskParser=$taskAssembly.GetType('PZ3D.GameLogAnalyzer',$true).GetMethod('AnalyzeText',$taskFlags)
$taskArguments=New-Object object[] 2
$taskArguments[0]="ERROR: unfamiliar callback failed`nLua((MOD:Never Seen Mod XYZ)).tick(any.lua:42)`n"
$taskArguments[1]='imported'
$taskArbitrary=$taskParser.Invoke($null,$taskArguments)
if(-not @($taskArbitrary.issues|Where-Object {$_.mods -contains 'Never Seen Mod XYZ' -and $_.frames[0].line -eq 42}).Count){throw 'Production arbitrary MOD/frame proof failed'}
$taskResult=[ordered]@{assemblyVersion=$taskAssembly.GetName().Version.ToString();executableSha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash;actualProductionAssembly=$true;rawLogsUploaded=$false;cases=$taskCases;arbitraryModPassed=$true}
$taskResult|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $OutputDirectory 'production-analysis-proof.json') -Encoding UTF8
'PASS actual signed production assembly: four real cases and unknown MOD stack/frame'
