param([Parameter(Mandatory=$true)][string]$OfficialSetup,[Parameter(Mandatory=$true)][string]$OutputPath,[Parameter(Mandatory=$true)][string]$VersionFile,[Parameter(Mandatory=$true)][string]$PublicKey,[string]$PackageId='OrangeZomboidLauncher',[string]$Title='橙子启动器')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/ProductionSources.ps1"
. "$PSScriptRoot/VelopackDependencies.ps1"
$deps=Get-OzlVelopackDependencies
if(Test-Path -LiteralPath $OutputPath){throw 'Chooser output already exists'}
$out=[IO.Path]::GetFullPath($OutputPath);$identity=$out+'.identity'
[IO.File]::WriteAllText($identity,($PackageId+"`n"+$Title+"`n"+(Get-FileHash -LiteralPath $OfficialSetup -Algorithm SHA256).Hash),[Text.UTF8Encoding]::new($false))
try{
 $sdk="$repo/Dependencies/WebView2/lib/net462"
 $refs=@('/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:System.Web.dll','/reference:System.Xml.dll','/reference:System.Net.Http.dll','/reference:System.Management.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Security.dll',"/reference:$sdk/Microsoft.Web.WebView2.Core.dll","/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll","/reference:$($deps.SdkAssembly)","/reference:$($deps.NewtonsoftAssembly)")
 $sources=@(Get-OzlVelopackSources -ProjectDirectory "$repo/Orange3DTool")+@([IO.Path]::GetFullPath("$repo/Orange3DTool/VelopackInstallerProgram.cs"))
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /define:OZL_PRODUCTION,OZL_VELOPACK /target:winexe /main:PZ3D.VelopackInstallerProgram /platform:x64 /optimize+ "/out:$out" "/win32manifest:$repo/Orange3DTool/app.manifest" "/win32icon:$repo/Orange3DTool/Assets/orange-app.ico" "/resource:$VersionFile,Launcher.Version.txt" "/resource:$PublicKey,Launcher.UpdatePublicKey.xml" "/resource:$OfficialSetup,Setup.Official.exe" "/resource:$identity,Setup.Identity" $refs $sources | Out-Host
 if($LASTEXITCODE){throw 'Chinese Velopack setup bootstrap compilation failed'}
}finally{Remove-Item -LiteralPath $identity -Force}
