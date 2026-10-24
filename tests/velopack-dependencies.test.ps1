$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$repo = Split-Path -Parent $PSScriptRoot
$prepare = Join-Path $repo 'Prepare-Velopack.ps1'
$helper = Join-Path $repo 'build/VelopackDependencies.ps1'
$fixture = Join-Path $repo ('artifacts/velopack-dependencies-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$passed = 0
function Assert-True($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Test-Case([string]$Name, [scriptblock]$Body) {
    & $Body
    $script:passed++
    Write-Output "PASS: $Name"
}
function Assert-Rejected([scriptblock]$Body, [string]$Pattern) {
    $failure = $null
    try { & $Body | Out-Null } catch { $failure = $_.Exception.Message }
    Assert-True ($failure -and $failure -match $Pattern) "Expected rejection matching '$Pattern', got '$failure'"
}
function New-Package([string]$Name, [string[]]$ExtraEntries = @()) {
    $path = Join-Path $fixture ($Name + '.zip')
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entryName in @('lib/Fixture.dll') + $ExtraEntries) {
            $entry = $archive.CreateEntry($entryName)
            $writer = [IO.StreamWriter]::new($entry.Open())
            try { $writer.Write('ordinary fixture bytes') } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
    return $path
}
function New-Lock([string]$Name, [string[]]$ExtraEntries = @()) {
    $packagePath = New-Package $Name $ExtraEntries
    $bytes = [Text.Encoding]::UTF8.GetBytes('ordinary fixture bytes')
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $fileHash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-', '') } finally { $sha.Dispose() }
    $packages = @()
    foreach ($id in @('sdk', 'newtonsoft', 'vpk')) {
        $packages += [ordered]@{ name=$id; id=$id; version='1.0.0'; url=($baseUrl + $Name + '.zip'); sha256=(Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash; files=@(@{path='lib/Fixture.dll'; sha256=$fileHash}) }
    }
    return [ordered]@{ schema=1; packages=$packages; sdkAssembly='sdk/lib/Fixture.dll'; newtonsoftAssembly='newtonsoft/lib/Fixture.dll'; vpk=@{ executable='vpk/lib/Fixture.dll'; arguments=@(); runtime='self-contained' } }
}
function Save-Lock($Lock, [string]$Name) {
    $path = Join-Path $fixture ($Name + '.json')
    $Lock | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}
# A real loopback HTTP server keeps download/hash/extraction behavior under test.
$tcp = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$tcp.Start(); $port = $tcp.LocalEndpoint.Port; $tcp.Stop()
$baseUrl = "http://127.0.0.1:$port/"
$listener = [Net.HttpListener]::new(); $listener.Prefixes.Add($baseUrl); $listener.Start()
$server = [PowerShell]::Create()
$null = $server.AddScript({ param($Listener, $Root)
    while ($Listener.IsListening) {
        try {
            $context = $Listener.GetContext()
            $file = Join-Path $Root ([IO.Path]::GetFileName($context.Request.Url.AbsolutePath))
            if ([IO.File]::Exists($file)) { $bytes=[IO.File]::ReadAllBytes($file); $context.Response.ContentLength64=$bytes.Length; $context.Response.OutputStream.Write($bytes,0,$bytes.Length) }
            else { $context.Response.StatusCode=404 }
            $context.Response.Close()
        } catch { break }
    }
}).AddArgument($listener).AddArgument($fixture)
$serverRun = $server.BeginInvoke()
try {
    Test-Case 'preparation entry point exists' { Assert-True (Test-Path -LiteralPath $prepare -PathType Leaf) 'Missing Prepare-Velopack.ps1 implementation' }
    . $helper
    Test-Case 'tampered HTTP package fails before extraction or execution' {
        $lock = New-Lock 'tampered'
        $lock.packages[0].sha256 = ('0' * 64)
        $path = Save-Lock $lock 'tampered-lock'
        $target = Join-Path $fixture 'tampered-output'
        Assert-Rejected { & $prepare -DependencyRoot $target -LockPath $path } 'package SHA256 mismatch'
        Assert-True (-not (Test-Path (Join-Path $target 'sdk'))) 'Tampered package was extracted'
    }
    foreach ($entry in @('../escape.dll', 'nested/../../escape.dll', 'C:/escape.dll', '/escape.dll', '..\escape.dll', 'lib/Fixture.dll:stream', 'lib/fixture.dll')) {
        Test-Case "unsafe or duplicate ZIP entry rejected: $entry" {
            $name = 'unsafe-' + [Guid]::NewGuid().ToString('N')
            $lock = New-Lock $name @($entry)
            $path = Save-Lock $lock ($name + '-lock')
            $target = Join-Path $fixture ($name + '-output')
            Assert-Rejected { & $prepare -DependencyRoot $target -LockPath $path } 'Unsafe|duplicate|undeclared'
            Assert-True (-not (Test-Path (Join-Path $target 'sdk'))) 'Unsafe archive was partially extracted'
        }
    }
    $valid = New-Lock 'valid'
    $validLock = Save-Lock $valid 'valid-lock'
    $validRoot = Join-Path $fixture 'valid-output'
    Test-Case 'valid HTTP packages expose declared SDK, Newtonsoft and CLI paths' {
        $result = & $prepare -DependencyRoot $validRoot -LockPath $validLock
        Assert-True ([IO.File]::ReadAllText($result.SdkAssembly) -ceq 'ordinary fixture bytes') 'Wrong SDK bytes'
        Assert-True ([IO.File]::ReadAllText($result.NewtonsoftAssembly) -ceq 'ordinary fixture bytes') 'Wrong Newtonsoft bytes'
        Assert-True ([IO.File]::ReadAllText($result.VpkExecutable) -ceq 'ordinary fixture bytes') 'Wrong CLI path'
        Assert-True (@($result.VpkArguments).Count -eq 0) 'Unexpected CLI argument prefix'
    }
    Test-Case 'read-only dependency lookup rechecks installed bytes' {
        [IO.File]::AppendAllText((Join-Path $validRoot 'sdk/lib/Fixture.dll'), '!')
        Assert-Rejected { Get-OzlVelopackDependencies -DependencyRoot $validRoot -LockPath $validLock } 'dependency SHA256 mismatch'
        Assert-Rejected { & $prepare -DependencyRoot $validRoot -LockPath $validLock } 'dependency SHA256 mismatch'
    }
    Test-Case 'missing and unexpected installed files fail closed' {
        $root = Join-Path $fixture 'inventory-output'
        $null = & $prepare -DependencyRoot $root -LockPath $validLock
        [IO.File]::WriteAllText((Join-Path $root 'vpk/extra.dll'), 'unexpected')
        Assert-Rejected { Get-OzlVelopackDependencies -DependencyRoot $root -LockPath $validLock } 'inventory'
        Remove-Item -LiteralPath (Join-Path $root 'vpk/extra.dll')
        Remove-Item -LiteralPath (Join-Path $root 'newtonsoft/lib/Fixture.dll')
        Assert-Rejected { Get-OzlVelopackDependencies -DependencyRoot $root -LockPath $validLock } 'Missing|inventory'
    }
    Test-Case 'unsafe declared paths are rejected without a download' {
        $lock = New-Lock 'unsafe-lock'
        $lock.packages[0].files[0].path = '../escape.dll'
        $path = Save-Lock $lock 'unsafe-path-lock'
        Assert-Rejected { & $prepare -DependencyRoot (Join-Path $fixture 'unsafe-lock-output') -LockPath $path } 'Unsafe'
    }
    Test-Case 'framework-dependent CLI returns the selected host and DLL argument prefix' {
        $lock = New-Lock 'runtime'
        $lock.vpk.runtime = 'Microsoft.NETCore.App'
        $lock.vpk.runtimeVersion = '8.0.0'
        $path = Save-Lock $lock 'runtime-lock'
        $root = Join-Path $fixture 'runtime-output'
        $hostPath = (Get-Command dotnet.exe -CommandType Application).Source
        $null = & $prepare -DependencyRoot $root -LockPath $path -DotNetPath $hostPath
        # An undeclared local executable must not become the implicit host.
        New-Item -ItemType Directory -Path (Join-Path $root 'runtime') | Out-Null
        [IO.File]::WriteAllText((Join-Path $root 'runtime/dotnet.exe'), 'not an authenticated runtime')
        $result = Get-OzlVelopackDependencies -DependencyRoot $root -LockPath $path
        Assert-True ($result.VpkExecutable -ceq $hostPath) 'Selected an undeclared local runtime host'
        Assert-True (@($result.VpkArguments).Count -eq 1 -and $result.VpkArguments[0] -ceq (Join-Path $root 'vpk/lib/Fixture.dll')) 'Wrong dotnet DLL argument prefix'
    }
    Write-Output "PASS: $passed Velopack dependency tests"
} finally {
    $listener.Stop(); $listener.Close()
    $server.Stop(); $server.Dispose()
}
