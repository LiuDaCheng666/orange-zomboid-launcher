# Local read-only authentication. This helper never downloads or executes dependencies.
function Get-OzlWebView2Lock {
    param([string]$LockPath = (Join-Path $PSScriptRoot 'dependencies.lock.json'))
    $lock = Get-Content -LiteralPath $LockPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    if ($lock.schema -cne 1) { throw 'Unsupported WebView2 lock schema' }
    $package = $lock.package
    if ($package.id -cne 'Microsoft.Web.WebView2' -or $package.version -cnotmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid WebView2 package identity' }
    $expectedUrl = 'https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/{0}/microsoft.web.webview2.{0}.nupkg' -f $package.version
    if ($package.url -cne $expectedUrl -or $package.catalog -cnotmatch '^https://api\.nuget\.org/v3/catalog0/data/[0-9.]+/microsoft\.web\.webview2\.[0-9.]+\.json$') { throw 'Invalid WebView2 package source' }
    if ($package.packageSha512 -cnotmatch '^[A-Za-z0-9+/]{86}==$') { throw 'Invalid WebView2 package SHA512' }
    try { $digest = [Convert]::FromBase64String($package.packageSha512) } catch { throw 'Invalid WebView2 package SHA512' }
    if ($digest.Length -ne 64 -or [Convert]::ToBase64String($digest) -cne $package.packageSha512) { throw 'Invalid WebView2 package SHA512' }
    $expected = @('lib/net462/Microsoft.Web.WebView2.Core.dll', 'lib/net462/Microsoft.Web.WebView2.WinForms.dll', 'runtimes/win-x64/native/WebView2Loader.dll')
    if (@($lock.files).Count -ne 3) { throw 'WebView2 lock must contain exactly three dependencies' }
    $seen = @()
    foreach ($entry in $lock.files) {
        if ($expected -cnotcontains $entry.path -or $seen -contains $entry.path) { throw "Invalid or duplicate WebView2 dependency path: $($entry.path)" }
        if ($entry.sha256 -cnotmatch '^[A-Fa-f0-9]{64}$') { throw "Invalid WebView2 dependency SHA256: $($entry.path)" }
        $seen += $entry.path
    }
    return $lock
}

function Assert-OzlWebView2Package {
    param([Parameter(Mandatory=$true)][string]$PackagePath, [string]$LockPath = (Join-Path $PSScriptRoot 'dependencies.lock.json'))
    $lock = Get-OzlWebView2Lock -LockPath $LockPath
    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) { throw "Missing WebView2 package: $PackagePath" }
    $sha = [Security.Cryptography.SHA512]::Create()
    try {
        $stream = [IO.File]::OpenRead($PackagePath)
        try { $actual = [Convert]::ToBase64String($sha.ComputeHash($stream)) } finally { $stream.Dispose() }
    } finally { $sha.Dispose() }
    if (-not [string]::Equals($actual, $lock.package.packageSha512, [StringComparison]::Ordinal)) { throw 'WebView2 package SHA512 mismatch' }
}

function Assert-OzlWebView2Dependencies {
    param([Parameter(Mandatory=$true)][string]$SdkDirectory, [string]$LockPath = (Join-Path $PSScriptRoot 'dependencies.lock.json'))
    $lock = Get-OzlWebView2Lock -LockPath $LockPath
    foreach ($entry in $lock.files) {
        $file = Join-Path $SdkDirectory $entry.path
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing WebView2 dependency: $($entry.path)" }
        $actual = (Get-FileHash -LiteralPath $file -Algorithm SHA256 -ErrorAction Stop).Hash
        if (-not [string]::Equals($actual, $entry.sha256, [StringComparison]::OrdinalIgnoreCase)) { throw "WebView2 dependency hash mismatch: $($entry.path)" }
    }
}
