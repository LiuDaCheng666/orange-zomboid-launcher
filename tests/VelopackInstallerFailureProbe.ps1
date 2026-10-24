param([Parameter(Mandatory=$true)][string]$Wrapper,[Parameter(Mandatory=$true)][string]$Root)
$ErrorActionPreference='Stop'
$a=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($Wrapper));$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
$t=$a.GetType('PZ3D.VelopackInstallerService',$true);$service=[Activator]::CreateInstance($t,$true)
try{$t.GetMethod('Install',$flags).Invoke($service,@($Root,$true,$null));throw 'Expected failing setup'}catch{
 $errorText=$_.Exception.ToString()
 if($errorText -notmatch '7' -or $errorText -notmatch '(?<log>[A-Z]:\\[^\r\n]+\\setup.log)'){throw 'Failure must report native exit and retained log location'}
 $log=$Matches.log
 if(-not(Test-Path -LiteralPath $log) -or [IO.File]::ReadAllText($log) -ne 'fixture-native-error'){throw 'Native failure diagnostic log disappeared'}
 if(Test-Path -LiteralPath (Join-Path (Split-Path $log) 'Setup.exe')){throw 'Temporary extracted setup should be cleaned'}
 "PASS native failure exit7 retains exact diagnostic log: $log"
}
