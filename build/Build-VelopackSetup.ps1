param([Parameter(Mandatory=$true)][string]$OfficialSetup,[Parameter(Mandatory=$true)][string]$OutputPath,[Parameter(Mandatory=$true)][string]$VersionFile,[Parameter(Mandatory=$true)][string]$PublicKey,[string]$PackageId='OrangeZomboidLauncher',[string]$Title='橙子启动器')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
if(Test-Path -LiteralPath $OutputPath){throw 'Chooser output already exists'}
$out=[IO.Path]::GetFullPath($OutputPath);$nonce=[Guid]::NewGuid().ToString('N')
$identity=$out+'.'+$nonce+'.identity';$attributes=$out+'.'+$nonce+'.version.g.cs'
[IO.File]::WriteAllText($identity,($PackageId+"`n"+$Title+"`n"+(Get-FileHash -LiteralPath $OfficialSetup -Algorithm SHA256).Hash),[Text.UTF8Encoding]::new($false))
try{
 $versionAttributes=& "$repo/Build-Version.ps1" -VersionFile $VersionFile -OutputPath $attributes
 $refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Xml.dll','/reference:System.Security.dll')
 # The chooser has no game, Steam, WebView2, SDK or legacy installer entry points.
 # InstallerOnly sources retain the existing validation bodies without pulling in
 # unrelated executable code. Never replace this list with the launcher allowlist.
 $sources=@('VelopackInstallerProgram.cs','InstallerForm.cs','LauncherTheme.cs','LauncherRuntime.cs','CoreReleaseProtocol.cs',
  'InstallerOnly/InstallerContracts.cs','InstallerOnly/InstallerTrustAndPaths.cs','InstallerOnly/InstallerBootstrap.cs','InstallerOnly/InstallerShortcutReader.cs') |
  ForEach-Object { $path=[IO.Path]::GetFullPath((Join-Path "$repo/Orange3DTool" $_));if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing installer source: $_"};$path }
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION,OZL_VELOPACK /target:winexe /main:PZ3D.VelopackInstallerProgram /platform:x64 /optimize+ "/out:$out" "/win32manifest:$repo/Orange3DTool/app.manifest" "/win32icon:$repo/Orange3DTool/Assets/orange-app.ico" "/resource:$VersionFile,Launcher.Version.txt" "/resource:$PublicKey,Launcher.UpdatePublicKey.xml" "/resource:$OfficialSetup,Setup.Official.exe" "/resource:$identity,Setup.Identity" $refs $versionAttributes $sources | Out-Host
 if($LASTEXITCODE){throw 'Chinese Velopack setup bootstrap compilation failed'}
}finally{foreach($path in @($identity,$attributes)){if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}}}
