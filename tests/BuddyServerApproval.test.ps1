param([string]$JdkPath='C:/Program Files/Microsoft/jdk-21.0.12.101-hotspot', [string]$LiveAgentPath='', [switch]$WithoutServerMode, [switch]$ReproduceLegacyGate)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$output=Join-Path $repo ('artifacts/buddy-server-approval-'+[guid]::NewGuid().ToString('N').Substring(0,8))
$game=Join-Path $output 'game';$classes=Join-Path $output 'probe-classes';$gameClasses=Join-Path $output 'game-classes';$payloadClasses=Join-Path $output 'payload-classes'
foreach($directory in @($game,$classes,$gameClasses,$payloadClasses)){New-Item -ItemType Directory -Path $directory -Force|Out-Null}
$javac=Join-Path $JdkPath 'bin/javac.exe';$java=Join-Path $JdkPath 'bin/java.exe';$jar=Join-Path $JdkPath 'bin/jar.exe'
$buddySource=Join-Path $repo 'Dependencies/ZombieBuddy-v2.3.4.jar'
if((Get-FileHash -LiteralPath $buddySource -Algorithm SHA256).Hash -ne 'EB79B9876332010733A8E0D7CE4FE0377846059A9E28859029E2A0FB449F6CF2'){throw 'Exact trusted Buddy 2.3.4 required'}
if(-not $LiveAgentPath){$LiveAgentPath=Join-Path $output 'OrangeLive.jar';& (Join-Path $repo 'Build-LiveComponent.ps1') -OutputPath $LiveAgentPath -JdkPath $JdkPath}
$LiveAgentPath=[IO.Path]::GetFullPath($LiveAgentPath)
$sources=@('tests/java/live/fixtures/zombie/modding/ActiveMods.java','tests/java/live/fixtures/zombie/modding/ActiveModsFile.java','tests/java/live/fixtures/zombie/network/GameClient.java')|ForEach-Object{Join-Path $repo $_}
$sources+=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java/server-approval/fixtures') -Filter '*.java' -Recurse -File|ForEach-Object FullName)
& $javac -encoding UTF-8 --release 8 -d $gameClasses $sources
if($LASTEXITCODE -ne 0){throw 'Game boundary fixture compilation failed'}
& $jar --create --file (Join-Path $game 'projectzomboid.jar') -C $gameClasses .
if($LASTEXITCODE -ne 0){throw 'Game boundary fixture packaging failed'}
$probeSource=Join-Path $PSScriptRoot 'java/server-approval/ServerApprovalProbe.java'
& $javac -encoding UTF-8 --release 21 -cp ($buddySource+';'+(Join-Path $game 'projectzomboid.jar')) -d $classes $probeSource
if($LASTEXITCODE -ne 0){throw 'Approval probe compilation failed'}
$payloadSources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'java/server-approval/payload') -Filter '*.java' -Recurse -File|ForEach-Object FullName)
& $javac -encoding UTF-8 --release 8 -d $payloadClasses $payloadSources
if($LASTEXITCODE -ne 0){throw 'Sentinel payload compilation failed'}
$payloads=@()
foreach($version in @('v1','v2','LOCAL')){[IO.File]::WriteAllText((Join-Path $payloadClasses 'payload-origin.txt'),$(if($version -eq 'LOCAL'){'LOCAL'}else{'SERVER-'+$version}));$payload=Join-Path $output ($version+'.jar');& $jar --create --file $payload -C $payloadClasses .;if($LASTEXITCODE -ne 0){throw 'Sentinel payload packaging failed'};$payloads+=$payload}
$extraClasses=Join-Path $output 'extra-classes';New-Item -ItemType Directory -Path $extraClasses -Force|Out-Null
& $javac -encoding UTF-8 --release 8 -Xlint:-options -d $extraClasses (Join-Path $PSScriptRoot 'java/server-approval/payload-extra/fixture/extra/Main.java')
if($LASTEXITCODE -ne 0){throw 'Extra local payload compilation failed'}
$extraPayload=Join-Path $output 'extra.jar';& $jar --create --file $extraPayload -C $extraClasses .
if($LASTEXITCODE -ne 0){throw 'Extra local payload packaging failed'}
function Record([string]$name,[string[]]$values){$name+([string]::Concat(@($values|ForEach-Object{"`t"+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($_))})))+"`n"}
function Quote([string]$value){'"'+($value -replace '(\\*)"','$1$1\"' -replace '(\\+)$','$1$1')+'"'}
$results=New-Object System.Collections.Generic.List[object]
function RunCase([string]$name,[string]$answers,[bool]$expected,[int]$payloadIndex,[string]$approvalGroup,[bool]$serverMode,[bool]$expectPrompt,[string]$callPath='direct'){
    $caseRoot=Join-Path $output $name;$context=Join-Path $caseRoot 'context';$choices=Join-Path $context 'runtime/session';$user=Join-Path $caseRoot 'user';$config=Join-Path $output ('approvals-'+$approvalGroup)
    foreach($directory in @($choices,(Join-Path $choices 'mods'),$user,$config)){New-Item -ItemType Directory -Path $directory -Force|Out-Null}
    [IO.File]::WriteAllText((Join-Path $choices 'mods/default.txt'),'VERSION = 1, mods { }')
    [IO.File]::WriteAllText((Join-Path $choices 'mods/loaded.txt'),'')
    if(-not(Test-Path -LiteralPath (Join-Path $config 'config.json'))){[IO.File]::WriteAllText((Join-Path $config 'config.json'),'{"trusted_authors":[],"preload_mods":{},"auto_fix_mod_order":false,"fix_approval_dialog_cursor":true}')}
    $buddy=Join-Path $context 'ZombieBuddy.jar';Copy-Item -LiteralPath $buddySource -Destination $buddy
    $body="OZLLIVE1`n"+(Record 'game' @($game))+(Record 'user' @($user))+(Record 'context' @($context))+(Record 'choices' @($choices))+(Record 'vanilla' @('false'))+(Record 'buddy' @($buddy,'eb79b9876332010733a8e0d7ce4fe0377846059a9e28859029e2a0fb449f6cf2'))
    if($callPath.StartsWith('same-id')){$local=Join-Path $context 'local-pin/ServerJava';New-Item -ItemType Directory -Path $local -Force|Out-Null;Copy-Item -LiteralPath $payloads[2] -Destination (Join-Path $local 'payload.jar');[IO.File]::WriteAllText((Join-Path $local 'mod.info'),"id=ServerJava`njavaJarFile=payload.jar`njavaPkgName=fixture.server`n");$body+=(Record 'managed' @('ServerJava'))+(Record 'selected' @('ServerJava',$local))+(Record 'component' @('ServerJava'))+(Record 'order' @('ServerJava'))}
    if($callPath -eq 'same-id-force'){$extra=Join-Path $context 'local-pin/ClientExtra';New-Item -ItemType Directory -Path $extra -Force|Out-Null;Copy-Item -LiteralPath $extraPayload -Destination (Join-Path $extra 'payload.jar');[IO.File]::WriteAllText((Join-Path $extra 'mod.info'),"id=ClientExtra`njavaJarFile=payload.jar`njavaPkgName=fixture.extra`n");$body+=(Record 'managed' @('ClientExtra'))+(Record 'selected' @('ClientExtra',$extra))+(Record 'component' @('ClientExtra'))+(Record 'order' @('ClientExtra'))}
    if($serverMode -and -not $ReproduceLegacyGate){$body+=(Record 'server-components' @($(if($callPath -eq 'same-id-force'){'force-local'}else{'follow-server'})))}
    $manifest=Join-Path $choices 'live.manifest';[IO.File]::WriteAllText($manifest,$body,(New-Object Text.UTF8Encoding($false)))
    $executed=Join-Path $caseRoot 'executed.txt'
    $arguments=@(('-Dozl.live.manifest='+$manifest),('-Dozl.live.sha256='+(Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash.ToLowerInvariant()),('-Dozl.fixture.user='+$user),('-Dozl.fixture.executed='+$executed),('-Dozl.fixture.payload.version='+($payloadIndex+1)),('-Duser.home='+$caseRoot),('-Dzb.config_dir='+$config),('-javaagent:'+$LiveAgentPath),('-javaagent:'+$buddy+'=prop_prefix=zb,policy=prompt,frontend=console'),'-cp',($classes+';'+(Join-Path $game 'projectzomboid.jar')+';'+$buddy),'ozl.environment.tests.ServerApprovalProbe',$caseRoot,$payloads[$payloadIndex],$expected.ToString().ToLowerInvariant(),$callPath)
    $start=New-Object Diagnostics.ProcessStartInfo;$start.FileName=$java;$start.WorkingDirectory=$game;$start.Arguments=($arguments|ForEach-Object{Quote $_}) -join ' ';$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process;$process.StartInfo=$start;$null=$process.Start();$out=$process.StandardOutput.ReadToEndAsync();$err=$process.StandardError.ReadToEndAsync();if($answers){$process.StandardInput.Write($answers)};$process.StandardInput.Close()
    if(-not $process.WaitForExit(45000)){$process.Kill();throw ('Timed out: '+$name)}
    $stdout=$out.Result;$stderr=$err.Result;[IO.File]::WriteAllText((Join-Path $caseRoot 'stdout.txt'),$stdout);[IO.File]::WriteAllText((Join-Path $caseRoot 'stderr.txt'),$stderr)
    $prompted=$stdout.Contains('Allow this Java mod to load?');$passed=$process.ExitCode -eq 0 -and $prompted -eq $expectPrompt -and $stdout.Contains('PASS real Buddy late-server approval')
    $entry=[pscustomobject]@{name=$name;passed=$passed;exit=$process.ExitCode;prompted=$prompted;expectedExecution=$expected;executed=(Test-Path -LiteralPath $executed);stdout=(Join-Path $caseRoot 'stdout.txt');stderr=(Join-Path $caseRoot 'stderr.txt')};$results.Add($entry);$process.Dispose();Write-Host ($name+': '+$(if($passed){'PASS'}else{'FAIL'}));if(-not $passed){Write-Host (($stdout -split "`n"|Select-Object -Last 20) -join "`n");Write-Host $stderr}
}
$server=-not $WithoutServerMode
RunCase 'allow-late-server' "y`ny`n" $server 0 'persist-allow' $server $true
RunCase 'deny-late-server' "n`ny`n" $false 0 'persist-deny' $server $true
RunCase 'closed-input-denies' '' $false 0 'closed' $server $true
RunCase 'saved-allow-same-hash' '' $server 0 'persist-allow' $server $false
RunCase 'saved-allow-new-hash-prompts' "n`nn`n" $false 1 'persist-allow' $server $true
RunCase 'saved-deny-same-hash' '' $false 0 'persist-deny' $server $false
RunCase 'singleplayer-gate-unchanged' "y`nn`n" $false 0 'singleplayer' $false $true
RunCase 'client-load-lifecycle-allow' "y`nn`n" $server 0 'client-allow' $server $true 'client'
RunCase 'client-load-lifecycle-deny' "n`nn`n" $false 0 'client-deny' $server $true 'client'
if($server){RunCase 'same-id-server-source-wins' "y`nn`n" $true 0 'same-id' $true $true 'same-id'}
if($server){RunCase 'force-custom-java-and-server-source' "y`nn`ny`nn`n" $true 0 'same-id-force' $true $true 'same-id-force'}
$record=[pscustomobject]@{scope='Real pinned Buddy 2.3.4 agent + production OrangeLive agent + original ConsoleModApprovalFrontend stdin; game metadata boundary fixture, not a game launch or ImGui acceptance';buddySha256=(Get-FileHash -LiteralPath $buddySource -Algorithm SHA256).Hash;agentSha256=(Get-FileHash -LiteralPath $LiveAgentPath -Algorithm SHA256).Hash;cases=$results}
$record|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'results.json') -Encoding UTF8
Write-Host ('Evidence: '+$output)
if(@($results|Where-Object{-not $_.passed}).Count){throw 'Real Buddy approval integration failed'}
