param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $repo ('artifacts/setup-boundary-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $evidence | Out-Null
# The payload is deliberately inert: this test inspects the compiled wrapper only.
$payload=Join-Path $evidence 'official-payload.bin'
$version=Join-Path $evidence 'VERSION'
[IO.File]::WriteAllBytes($payload,[byte[]](79,90,76,45,102,105,120,116,117,114,101))
[IO.File]::WriteAllText($version,'7.8.9')
$wrapper=Join-Path $evidence 'chooser.exe'
& "$repo/build/Build-VelopackSetup.ps1" -OfficialSetup $payload -OutputPath $wrapper -VersionFile $version -PublicKey "$repo/release-public-key.xml" -PackageId OzlBoundaryFixture -Title '隔离安装边界测试'
$assembly=[Reflection.Assembly]::LoadFile($wrapper)
$failures=@()
foreach($name in @('PZ3D.Program','PZ3D.Engine','PZ3D.SteamWorkshop','PZ3D.WorkshopWorker','PZ3D.CoreUpdaterProgram','PZ3D.CoreUpdateTransaction','PZ3D.InstallerProgram','PZ3D.UninstallerProgram','PZ3D.InstallerInstallService')){
 if($null -ne $assembly.GetType($name,$false)){$failures+='Unrelated executable implementation in wrapper: '+$name}
}
foreach($reference in $assembly.GetReferencedAssemblies()){
 if($reference.Name -match '^(Velopack|Newtonsoft\.Json|Microsoft\.Web\.WebView2\..*|System\.Management|System\.Net\.Http)$'){$failures+='Unrelated runtime dependency in wrapper: '+$reference.Name}
}
$legacy=$assembly.GetType('PZ3D.PortableDeployment',$false)
if($legacy -and $legacy.GetMethod('DeleteSelfAfterExit')){$failures+='Legacy PowerShell self-delete implementation in wrapper'}
$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($wrapper)
if($assembly.GetName().Version.ToString() -cne '7.8.9.0' -or $info.FileVersion -cne '7.8.9.0' -or $info.ProductVersion -cne '7.8.9'){$failures+='Wrapper assembly/file/product version does not derive from supplied VERSION'}
if([string]::IsNullOrWhiteSpace($info.ProductName)){$failures+='Wrapper product metadata is missing'}
$stream=$assembly.GetManifestResourceStream('Setup.Official.exe');$memory=[IO.MemoryStream]::new()
try{$stream.CopyTo($memory);if([Convert]::ToBase64String($memory.ToArray()) -cne [Convert]::ToBase64String([IO.File]::ReadAllBytes($payload))){$failures+='Official payload bytes changed'}}finally{$memory.Dispose();$stream.Dispose()}
$failures | Set-Content -LiteralPath (Join-Path $evidence 'failures.txt') -Encoding UTF8
if($failures.Count){throw ($failures -join "`n")}
"PASS standalone installer dependency boundary, supplied version metadata and exact official payload: $evidence"
