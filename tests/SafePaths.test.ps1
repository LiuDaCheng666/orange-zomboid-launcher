param([Parameter(Mandatory=$true)][string]$AgentPath,[string]$EvidenceDirectory='')
$ErrorActionPreference='Stop'
if(!$EvidenceDirectory){$EvidenceDirectory=Join-Path (Split-Path $PSScriptRoot -Parent) ('artifacts/safe-paths-'+[guid]::NewGuid().ToString('N'))}
$root=[IO.Path]::GetFullPath($EvidenceDirectory);$classes=Join-Path $root 'classes';$fixture=Join-Path $root 'fixture';[IO.Directory]::CreateDirectory($classes)|Out-Null;[IO.Directory]::CreateDirectory((Join-Path $fixture 'directory'))|Out-Null
$junction=Join-Path $fixture 'junction'
if(!(Test-Path -LiteralPath $junction)){New-Item -ItemType Junction -Path $junction -Target (Join-Path $fixture 'directory') -ErrorAction Stop|Out-Null}
$agent=[IO.Path]::GetFullPath($AgentPath)
& javac --release 8 -Xlint:-options -encoding UTF-8 -cp $agent -d $classes (Join-Path $PSScriptRoot 'java/filesystem/SafePathsTest.java')
if($LASTEXITCODE){throw 'Path checks fixture compilation failed'}
& java -cp ($classes+';'+$agent) ozl.environment.SafePathsTest $fixture $junction
if($LASTEXITCODE){throw 'Path checks fixture failed'}
