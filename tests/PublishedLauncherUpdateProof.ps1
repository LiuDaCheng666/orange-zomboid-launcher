param([Parameter(Mandatory=$true)][string]$AppDirectory,[Parameter(Mandatory=$true)][string]$CurrentVersion,[Parameter(Mandatory=$true)][string]$ExpectedVersion,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$taskRuntime=Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) ('client-check-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskRuntime -ErrorAction Stop|Out-Null
[void][Reflection.Assembly]::LoadFrom((Join-Path $AppDirectory 'Newtonsoft.Json.dll'))
$taskSdk=[Reflection.Assembly]::LoadFrom((Join-Path $AppDirectory 'Velopack.dll'))
$taskMain=@(Get-ChildItem -LiteralPath $AppDirectory -File -Filter '*.exe')[0].FullName
$taskAssembly=[Reflection.Assembly]::LoadFrom($taskMain)
$taskFlags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$taskReader=[IO.StreamReader]::new($taskAssembly.GetManifestResourceStream('Launcher.UpdatePublicKey.xml'))
try{$taskKey=$taskReader.ReadToEnd()}finally{$taskReader.Dispose()}
$taskNetwork=$taskAssembly.GetType('PZ3D.VelopackNetwork').GetMethod('Open',$taskFlags)
$taskOpen=[Delegate]::CreateDelegate([Func[string,Threading.CancellationToken,IO.Stream]],$taskNetwork)
$taskSourceType=$taskAssembly.GetType('PZ3D.AuthenticatedVelopackSource')
$taskSourceCtor=@($taskSourceType.GetConstructors($taskFlags)|Where-Object {$_.GetParameters().Count -eq 4})[0]
$taskSource=$taskSourceCtor.Invoke([object[]]@([string]$taskKey,[string]$CurrentVersion,$taskOpen,[string](Join-Path $taskRuntime 'accepted-release.bin')))
$taskLocatorCtor=$taskSdk.GetType('Velopack.Locators.TestVelopackLocator').GetConstructors()[0]
$taskLocatorArguments=[object[]]@($taskLocatorCtor.GetParameters()|ForEach-Object {$_.DefaultValue})
$taskLocatorArguments[0]=[string]'OrangeZomboidLauncher';$taskLocatorArguments[1]=[string]$CurrentVersion;$taskLocatorArguments[2]=[string]$taskRuntime
$taskLocator=$taskLocatorCtor.Invoke($taskLocatorArguments)
$taskBackendType=$taskAssembly.GetType('PZ3D.VelopackUpdateBackend')
$taskBackendCtor=@($taskBackendType.GetConstructors($taskFlags)|Where-Object {$_.GetParameters().Count -eq 3})[0]
$taskBackend=$taskBackendCtor.Invoke([object[]]@($taskSource,$taskLocator,$true))
try{
 [void]$taskBackendType.GetMethod('Check',$taskFlags).Invoke($taskBackend,@())
 $taskDeadline=[DateTime]::UtcNow.AddSeconds(45)
 do{[Threading.Thread]::Sleep(100);$taskStatus=$taskBackendType.GetMethod('Status',$taskFlags).Invoke($taskBackend,@())}while($taskStatus.state -eq 'Checking' -and [DateTime]::UtcNow -lt $taskDeadline)
 if($taskStatus.state -ne 'Available' -or $taskStatus.latestVersion -ne $ExpectedVersion){throw ('Production check failed: '+$taskStatus.state+' '+$taskStatus.message)}
 [void]$taskBackendType.GetMethod('Download',$taskFlags).Invoke($taskBackend,@())
 $taskDeadline=[DateTime]::UtcNow.AddSeconds(90)
 do{[Threading.Thread]::Sleep(100);$taskStatus=$taskBackendType.GetMethod('Status',$taskFlags).Invoke($taskBackend,@())}while($taskStatus.state -eq 'Downloading' -and [DateTime]::UtcNow -lt $taskDeadline)
 if($taskStatus.state -ne 'Ready' -or $taskStatus.latestVersion -ne $ExpectedVersion){throw ('Production download failed: '+$taskStatus.state+' '+$taskStatus.message)}
 $taskResult=[ordered]@{state=$taskStatus.state;currentVersion=$taskStatus.currentVersion;latestVersion=$taskStatus.latestVersion;message=$taskStatus.message;checkedUtc=$taskStatus.checkedUtc;productionAssemblySHA256=(Get-FileHash -LiteralPath $taskMain -Algorithm SHA256).Hash;isolatedLocator=$true;usedRealProductionTransport=$true;updateApplied=$false}
 $taskResult|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $OutputDirectory 'production-client-proof.json') -Encoding UTF8
 $taskResult|ConvertTo-Json -Compress
}finally{([IDisposable]$taskBackend).Dispose()}
