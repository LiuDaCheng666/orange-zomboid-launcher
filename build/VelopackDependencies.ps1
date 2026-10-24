# Read-only dependency verification; this file never downloads or executes packages.
function Assert-OzlVelopackRelativePath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or $Path -match '[\\:]' -or $Path.StartsWith('/')) { throw "Unsafe dependency path: $Path" }
    foreach ($part in $Path.Split('/')) {
        if (-not $part -or $part -eq '.' -or $part -eq '..' -or $part -match '[<>"|?*\x00-\x1f]' -or $part -match '[. ]$' -or $part -match '^(?i:CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') { throw "Unsafe dependency path: $Path" }
    }
}

function Get-OzlVelopackLock {
    param([string]$LockPath = (Join-Path $PSScriptRoot 'velopack.lock.json'))
    $lock = Get-Content -LiteralPath $LockPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    if ($lock.schema -ne 1) { throw 'Unsupported Velopack lock schema' }
    $names = @()
    foreach ($package in $lock.packages) {
        if ($package.name -cnotmatch '^[a-z][a-z0-9-]*$' -or $names -contains $package.name -or $package.id -notmatch '^[a-z0-9.-]+$' -or $package.version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid or duplicate Velopack package identity' }
        $names += $package.name
        $uri = [Uri]$package.url
        if (-not $uri.IsAbsoluteUri -or $uri.UserInfo -or ($uri.Scheme -ne 'https' -and -not ($uri.Scheme -eq 'http' -and $uri.IsLoopback))) { throw 'Dependency URL must use HTTPS (loopback HTTP is allowed for tests)' }
        if ($package.sha256 -notmatch '^[a-f0-9]{64}$' -or @($package.files).Count -eq 0) { throw 'Invalid package SHA256 or empty inventory' }
        $paths = @()
        foreach ($file in $package.files) {
            Assert-OzlVelopackRelativePath $file.path
            if ($paths -contains $file.path -or $file.sha256 -notmatch '^[a-f0-9]{64}$') { throw 'Invalid or duplicate dependency inventory entry' }
            $paths += $file.path
        }
    }
    if ($names.Count -ne 3 -or $names -notcontains 'sdk' -or $names -notcontains 'newtonsoft' -or $names -notcontains 'vpk') { throw 'Velopack lock requires sdk, newtonsoft and vpk packages' }
    foreach ($path in @($lock.sdkAssembly, $lock.newtonsoftAssembly, $lock.vpk.executable)) {
        Assert-OzlVelopackRelativePath $path
        $parts = $path.Split('/', 2)
        $package = @($lock.packages | Where-Object { $_.name -ceq $parts[0] })
        if ($parts.Count -ne 2 -or $package.Count -ne 1 -or @($package[0].files.path) -cnotcontains $parts[1]) { throw "Undeclared dependency entry point: $path" }
    }
    if ($lock.vpk.runtime -ne 'self-contained' -and ($lock.vpk.runtime -cne 'Microsoft.NETCore.App' -or $lock.vpk.runtimeVersion -notmatch '^\d+\.\d+\.\d+$')) { throw 'Unsupported vpk runtime requirement' }
    return $lock
}

function Assert-OzlVelopackNoReparsePoint {
    param([string]$Path)
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force -ErrorAction Stop
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point is not allowed in dependency path: $current" }
        }
        $parent = [IO.Directory]::GetParent($current)
        $current = if ($parent) { $parent.FullName } else { $null }
    }
}

function Assert-OzlVelopackInstalledPackage {
    param([string]$Directory, $Package)
    Assert-OzlVelopackNoReparsePoint $Directory
    if (-not (Test-Path -LiteralPath $Directory -PathType Container)) { throw "Missing Velopack dependencies: $Directory. Run Prepare-Velopack.ps1." }
    $actual = @()
    # Check each directory before descending; never traverse a junction/symlink.
    $pending = [Collections.Generic.Queue[string]]::new()
    $pending.Enqueue([IO.Path]::GetFullPath($Directory))
    while ($pending.Count) {
        $folder = $pending.Dequeue()
        foreach ($item in Get-ChildItem -LiteralPath $folder -Force -ErrorAction Stop) {
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Reparse point in dependency inventory: $($item.Name)" }
            if ($item.PSIsContainer) { $pending.Enqueue($item.FullName) }
            else { $actual += $item.FullName.Substring([IO.Path]::GetFullPath($Directory).TrimEnd('\','/').Length + 1).Replace('\','/') }
        }
    }
    if ($actual.Count -ne @($Package.files).Count) { throw "Dependency inventory mismatch: $($Package.name)" }
    foreach ($file in $Package.files) {
        if ($actual -cnotcontains $file.path) { throw "Missing dependency inventory file: $($file.path)" }
        $path = Join-Path $Directory $file.path
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256 -ErrorAction Stop).Hash -ine $file.sha256) { throw "Velopack dependency SHA256 mismatch: $($Package.name)/$($file.path)" }
    }
}

function Get-OzlVelopackDependencies {
    [CmdletBinding()]
    param(
        [string]$DependencyRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Dependencies/Velopack'),
        [string]$LockPath = (Join-Path $PSScriptRoot 'velopack.lock.json'),
        [string]$DotNetPath = ''
    )
    $lock = Get-OzlVelopackLock -LockPath $LockPath
    $root = [IO.Path]::GetFullPath($DependencyRoot)
    foreach ($package in $lock.packages) { Assert-OzlVelopackInstalledPackage -Directory (Join-Path $root $package.name) -Package $package }
    $entryPoint = Join-Path $root $lock.vpk.executable
    $executable = $entryPoint
    $arguments = @($lock.vpk.arguments)
    if ($lock.vpk.runtime -ne 'self-contained') {
        if (-not $DotNetPath) {
            $DotNetPath = (Get-Command dotnet.exe -CommandType Application -ErrorAction Stop).Source
        }
        if (-not (Test-Path -LiteralPath $DotNetPath -PathType Leaf)) { throw "Missing dotnet host: $DotNetPath" }
        $executable = [IO.Path]::GetFullPath($DotNetPath)
        $arguments = @($entryPoint) + $arguments
    }
    [pscustomobject]@{
        SdkAssembly = (Join-Path $root $lock.sdkAssembly)
        NewtonsoftAssembly = (Join-Path $root $lock.newtonsoftAssembly)
        VpkExecutable = $executable
        VpkArguments = [string[]]$arguments
        VpkEntryPoint = $entryPoint
        Runtime = $lock.vpk.runtime
        RuntimeVersion = $lock.vpk.runtimeVersion
        DependencyRoot = $root
    }
}
