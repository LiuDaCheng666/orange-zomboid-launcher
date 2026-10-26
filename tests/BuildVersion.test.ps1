$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$root=Join-Path $repo ('artifacts/version-metadata-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $root | Out-Null
$inputFile=Join-Path $root 'VERSION'
$outputFile=Join-Path $root 'Setup.Version.g.cs'
[IO.File]::WriteAllText($inputFile,'7.8.9',[Text.UTF8Encoding]::new($false))
$result=& "$repo/Build-Version.ps1" -VersionFile $inputFile -OutputPath $outputFile
if($result -cne $outputFile -or -not(Test-Path -LiteralPath $outputFile)){throw 'Supplied VERSION must produce attributes at the requested independent output'}
$text=[IO.File]::ReadAllText($outputFile)
foreach($attribute in @('AssemblyVersion("7.8.9.0")','AssemblyFileVersion("7.8.9.0")','AssemblyInformationalVersion("7.8.9")')){
 if(-not $text.Contains($attribute)){throw "Version metadata missing: $attribute"}
}
$before=Get-FileHash -LiteralPath $outputFile
foreach($invalid in @('01.2.3','1.2','1.2.65535','1.2.3.4')){
 [IO.File]::WriteAllText($inputFile,$invalid,[Text.UTF8Encoding]::new($false))
 $rejected=$false
 try{& "$repo/Build-Version.ps1" -VersionFile $inputFile -OutputPath $outputFile | Out-Null}catch{$rejected=$true}
 if(-not $rejected -or (Get-FileHash -LiteralPath $outputFile).Hash -ne $before.Hash){throw 'Invalid metadata must not replace the generated source'}
}
'PASS supplied version attributes, independent output and four invalid-version preservation cases'
