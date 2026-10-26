param([Parameter(Mandatory=$true)][string]$Evidence)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Core'){
 & "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -File $PSCommandPath -Evidence ([IO.Path]::GetFullPath($Evidence))
 if($LASTEXITCODE){throw 'NET Framework installer validation failed'}
 return
}
$state=Get-Content -LiteralPath (Join-Path $Evidence 'fixture.json') -Raw|ConvertFrom-Json
$assembly=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($state.wrapper))
$flags=[Reflection.BindingFlags]'Static,NonPublic,Public'
$policy=$assembly.GetType('PZ3D.InstallerDestinationPolicy',$true).GetMethod('ValidateInstallRoot',$flags)
$validation=Join-Path ([IO.Path]::GetFullPath($Evidence)) ('validation-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $validation | Out-Null
function Require-Rejection([scriptblock]$Action,[string]$Name,[string]$ExpectedReason){
 $rejected=$false
 try{& $Action}catch{if($_.Exception.ToString() -notmatch [regex]::Escape($ExpectedReason)){throw};$rejected=$true}
 if(-not $rejected){throw "Validation unexpectedly accepted: $Name"}
}
$empty=Join-Path $validation 'empty';New-Item -ItemType Directory $empty | Out-Null
if($policy.Invoke($null,@([string]$empty)) -cne $empty){throw 'Independent empty destination did not retain exact path'}
$occupied=Join-Path $validation 'occupied';New-Item -ItemType Directory $occupied | Out-Null
[IO.File]::WriteAllText("$occupied/personal.txt",'keep')
Require-Rejection {$policy.Invoke($null,@([string]$occupied))} 'non-empty destination' '目标目录不为空'
if([IO.File]::ReadAllText("$occupied/personal.txt") -cne 'keep'){throw 'Rejected path modified its existing file'}
Require-Rejection {$policy.Invoke($null,@([IO.Path]::GetPathRoot($validation)))} 'drive root' '请选择独立的工具文件夹'
$game=Join-Path $validation 'fixture-game';New-Item -ItemType Directory $game | Out-Null
[IO.File]::WriteAllText("$game/ProjectZomboid64.exe",'inert game marker')
Require-Rejection {$policy.Invoke($null,@([string]$game))} 'game destination' '请选择独立的工具文件夹'
$junction=Join-Path $validation 'junction'
New-Item -ItemType Junction -Path $junction -Target $empty | Out-Null
try{Require-Rejection {$policy.Invoke($null,@([string]$junction))} 'junction destination' 'Reparse points are not permitted'}finally{Remove-Item -LiteralPath $junction -Force}
$read=$assembly.GetType('PZ3D.CoreDeployment',$true).GetMethod('ReadBytes',$flags)
Require-Rejection {$read.Invoke($null,@("$occupied/personal.txt",2))} 'oversized stored document' 'Invalid stored document size'
$copy=Join-Path $validation 'bootstrap';Copy-Item -LiteralPath $state.app -Destination $copy -Recurse
$bootstrap=$assembly.GetType('PZ3D.DistributionBootstrap',$true).GetMethod('Read',$flags)
$key=[IO.File]::ReadAllText((Join-Path $state.app 'release-public-key.xml'))
$embedded=$assembly.GetType('PZ3D.CoreUpdateTrust',$true).GetProperty('PublicKeyXml',$flags).GetValue($null,$null)
if($embedded -cne $key){throw 'Wrapper trust differs from supplied embedded public key'}
if($bootstrap.Invoke($null,@([string]$copy,[string]$key)).Count -ne 21){throw 'Valid RSA bootstrap did not verify all 21 files'}
[IO.File]::AppendAllText("$copy/Web/app.js",'altered')
Require-Rejection {$bootstrap.Invoke($null,@([string]$copy,[string]$key))} 'altered application bytes' 'Distribution file verification failed: Web/app.js'
[IO.File]::WriteAllBytes("$copy/Web/app.js",[IO.File]::ReadAllBytes((Join-Path $state.app 'Web/app.js')))
$signature=[IO.File]::ReadAllBytes("$copy/velopack-bootstrap.sig");$signature[0]=$signature[0] -bxor 1;[IO.File]::WriteAllBytes("$copy/velopack-bootstrap.sig",$signature)
Require-Rejection {$bootstrap.Invoke($null,@([string]$copy,[string]$key))} 'altered RSA signature' 'Bootstrap signature is invalid'
if(@(Get-ChildItem -LiteralPath $Evidence -File | Where-Object {$_.Name -match '\.[a-f0-9]{32}\.(identity|version\.g\.cs)$'}).Count){throw 'Wrapper temporary build files remain'}
"PASS original empty/non-empty/drive/game/junction path policy, bounded reads, embedded key and 21-file RSA/tamper checks: $validation"|Tee-Object -FilePath "$Evidence/validation-results.log"
