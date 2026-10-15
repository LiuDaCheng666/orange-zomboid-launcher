$ErrorActionPreference='Stop'
$releaseVersion = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VERSION')).Trim()
if($releaseVersion -cnotmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'VERSION must use canonical major.minor.patch' }
foreach($component in $releaseVersion.Split('.')) {
    $numeric = 0
    if(-not [int]::TryParse($component, [ref]$numeric) -or $numeric -gt 65534) { throw 'VERSION components must fit assembly version fields (0..65534)' }
}
$generatedDirectory = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $generatedDirectory -Force | Out-Null
$generatedPath = Join-Path $generatedDirectory 'Launcher.Version.g.cs'
$attributes = "// Generated from VERSION by Build-Version.ps1; do not edit.`r`nusing System.Reflection;`r`n[assembly: AssemblyTitle(`"OZL`")]`r`n[assembly: AssemblyProduct(`"Orange zombiod launcher`")]`r`n[assembly: AssemblyVersion(`"$releaseVersion.0`")]`r`n[assembly: AssemblyFileVersion(`"$releaseVersion.0`")]`r`n[assembly: AssemblyInformationalVersion(`"$releaseVersion`")]`r`n"
[IO.File]::WriteAllText($generatedPath, $attributes, [Text.UTF8Encoding]::new($false))
return $generatedPath
