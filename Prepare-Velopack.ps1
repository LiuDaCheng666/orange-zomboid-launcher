[CmdletBinding()]
param(
    [string]$DependencyRoot = '',
    [string]$LockPath = '',
    [string]$DotNetPath = ''
)
$ErrorActionPreference = 'Stop'
if (-not $DependencyRoot) { $DependencyRoot = Join-Path $PSScriptRoot 'Dependencies/Velopack' }
if (-not $LockPath) { $LockPath = Join-Path $PSScriptRoot 'build/velopack.lock.json' }
. (Join-Path $PSScriptRoot 'build/VelopackDependencies.ps1')
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$lock = Get-OzlVelopackLock -LockPath $LockPath
$DependencyRoot = [IO.Path]::GetFullPath($DependencyRoot)
Assert-OzlVelopackNoReparsePoint $DependencyRoot
$cache = Join-Path $DependencyRoot 'packages'
Assert-OzlVelopackNoReparsePoint $cache
New-Item -ItemType Directory -Path $cache -Force | Out-Null
foreach ($package in $lock.packages) {
    $destination = Join-Path $DependencyRoot $package.name
    if (Test-Path -LiteralPath $destination) {
        Assert-OzlVelopackInstalledPackage -Directory $destination -Package $package
        continue
    }
    $packagePath = Join-Path $cache ($package.id + '.' + $package.version + '.nupkg')
    Assert-OzlVelopackNoReparsePoint $packagePath
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        $download = $packagePath + '.' + [Guid]::NewGuid().ToString('N') + '.download'
        try {
            # Keep the verified package bytes; never invoke a package manager or its hooks.
            Invoke-WebRequest -UseBasicParsing -Uri $package.url -OutFile $download -TimeoutSec 120
            if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ine $package.sha256) { throw "Velopack package SHA256 mismatch: $($package.name)" }
            [IO.File]::Move($download, $packagePath)
        } finally { if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force } }
    }
    # Authenticate the entire ZIP before even opening the archive.
    if ((Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash -ine $package.sha256) { throw "Velopack package SHA256 mismatch: $($package.name)" }
    $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)
    $stage = Join-Path $DependencyRoot ('.staging-' + [Guid]::NewGuid().ToString('N'))
    try {
        $seen = @()
        foreach ($entry in $archive.Entries) {
            $isDirectory = $entry.FullName.EndsWith('/')
            $name = if ($isDirectory) { $entry.FullName.Substring(0, $entry.FullName.Length - 1) } else { $entry.FullName }
            Assert-OzlVelopackRelativePath $name
            if ($seen -contains $name) { throw "Unsafe duplicate ZIP entry: $name" }
            $seen += $name
            $unixType = ($entry.ExternalAttributes -shr 16) -band 0xF000
            if ($unixType -ne 0 -and $unixType -ne 0x8000 -and $unixType -ne 0x4000) { throw "Unsafe ZIP file type: $name" }
            if ($isDirectory) { continue }
            $declared = @($package.files | Where-Object { $_.path -ceq $name })
            if ($declared.Count -ne 1) { throw "Unsafe or undeclared ZIP entry: $name" }
            $stream = $entry.Open(); $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $sha.Dispose(); $stream.Dispose() }
            if ($hash -ine $declared[0].sha256) { throw "Velopack dependency SHA256 mismatch in package: $name" }
        }
        if (@($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') }).Count -ne @($package.files).Count) { throw "Dependency package inventory mismatch: $($package.name)" }
        # All entries have passed validation before any extraction takes place.
        New-Item -ItemType Directory -Path $stage | Out-Null
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $file = Join-Path $stage $entry.FullName
            New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $file, $false)
        }
        Assert-OzlVelopackInstalledPackage -Directory $stage -Package $package
        [IO.Directory]::Move($stage, $destination)
    } finally {
        $archive.Dispose()
        if (Test-Path -LiteralPath $stage) {
            # Only our freshly generated, workspace-local staging directory is removed.
            if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($stage)) -ne $DependencyRoot -or [IO.Path]::GetFileName($stage) -notmatch '^\.staging-[a-f0-9]{32}$') { throw 'Invalid staging cleanup path' }
            Remove-Item -LiteralPath $stage -Recurse -Force
        }
    }
}
Get-OzlVelopackDependencies -DependencyRoot $DependencyRoot -LockPath $LockPath -DotNetPath $DotNetPath
