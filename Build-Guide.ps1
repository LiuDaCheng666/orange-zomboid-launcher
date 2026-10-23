param(
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$SourceRoot=$PSScriptRoot
)
$ErrorActionPreference='Stop'

$parts = foreach ($relativePath in @('使用说明.md', 'docs/THIRD-PARTY.md', 'docs/LICENSING.md', 'LICENSE')) {
    $inputPath = Join-Path $SourceRoot $relativePath
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
        throw "Required guide input is missing: $relativePath"
    }
    [IO.File]::ReadAllText($inputPath)
}

# Keep the existing core filename and updater allowlist; ship all legal texts in it.
$guide = [string]::Join("`r`n`r`n", [string[]]$parts)
[IO.File]::WriteAllText($OutputPath, $guide, [Text.UTF8Encoding]::new($false))
