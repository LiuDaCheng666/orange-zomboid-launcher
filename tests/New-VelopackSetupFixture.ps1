param()
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. "$repo/build/VelopackDependencies.ps1"
$deps=Get-OzlVelopackDependencies
$nonce=[Guid]::NewGuid().ToString('N')
$evidence=Join-Path $repo ('artifacts/setup-isolation-'+$nonce)
$fixture=Join-Path ([IO.Path]::GetTempPath()) ('ozl-setup-'+$nonce)
if([IO.Path]::GetPathRoot($fixture) -eq [IO.Path]::GetPathRoot($env:WINDIR)){throw 'Installation fixture must use a non-system drive'}
$appId='OzlSetupOnly'+$nonce.Substring(0,12);$title='橙子安装验收-'+$nonce.Substring(0,12);$version='7.8.9'
$app=Join-Path $evidence 'app';$release=Join-Path $evidence 'release'
New-Item -ItemType Directory $evidence,$fixture,$app | Out-Null
$owned=@('橙子启动器.exe','VERSION','CHANGELOG.md','使用说明.txt','release-public-key.xml',
 'Web/index.html','Web/app.js','Web/app.css','Web/home.png','Web/orange-app.ico','Web/WebView2-LICENSE.txt',
 'Web/Microsoft.Web.WebView2.Core.dll','Web/Microsoft.Web.WebView2.WinForms.dll','Web/WebView2Loader.dll',
 'Assets/OrangeShootPresentation.jar','Assets/OrangeLaunchBridge.lua','Assets/OrangeStartup.jar',
 'Velopack.dll','Newtonsoft.Json.dll','ThirdPartyNotices/Velopack.txt','ThirdPartyNotices/Newtonsoft.Json.txt')
foreach($relative in $owned){$path=Join-Path $app $relative;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null;[IO.File]::WriteAllText($path,'Isolated installer fixture: '+$relative)}
[IO.File]::WriteAllText("$app/VERSION",$version)
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(2048);$rsa.PersistKeyInCsp=$false
try{
 [IO.File]::WriteAllText("$app/release-public-key.xml",$rsa.ToXmlString($false))
 Copy-Item -LiteralPath $deps.SdkAssembly,$deps.NewtonsoftAssembly -Destination $app
 $source='using System;using System.IO;using Velopack;class SetupOnlyFixture {[STAThread]static int Main(string[] args){VelopackApp.Build().SetArgs(args).SetAutoApplyOnStartup(false).Run();File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-opened.txt"),"isolated fixture only");return 0;}}'
 [IO.File]::WriteAllText("$evidence/SetupOnlyFixture.cs",$source)
 & C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /target:winexe /platform:x64 "/out:$app/橙子启动器.exe" "/win32icon:$repo/Orange3DTool/Assets/orange-app.ico" "/reference:$($deps.SdkAssembly)" "/reference:$($deps.NewtonsoftAssembly)" ([IO.Path]::GetFullPath("$evidence/SetupOnlyFixture.cs"))
 if($LASTEXITCODE){throw 'Inert lifecycle fixture compilation failed'}
 $files=@($owned|ForEach-Object{$path=Join-Path $app $_;$file=Get-Item -LiteralPath $path;[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash;url='';length=$file.Length}})
 $raw=[Text.UTF8Encoding]::new($false).GetBytes(([ordered]@{schema=1;version=$version;files=$files}|ConvertTo-Json -Depth 5 -Compress))
 [IO.File]::WriteAllBytes("$app/velopack-bootstrap.json",$raw);[IO.File]::WriteAllBytes("$app/velopack-bootstrap.sig",$rsa.SignData($raw,'SHA256'))
}finally{$rsa.Dispose()}
& $deps.VpkExecutable @($deps.VpkArguments) pack --packId $appId --packTitle $title --packVersion $version --packDir $app --outputDir $release --channel win --runtime win-x64 --mainExe '橙子启动器.exe' --icon "$repo/Orange3DTool/Assets/orange-app.ico" --aumid ($appId+'.Launcher') --framework net48 --delta None --shortcuts Desktop,StartMenuRoot --skip-updates --legacyConsole --yes *> "$evidence/pack.log"
if($LASTEXITCODE){throw 'Official fixture pack failed'}
$wrapper=Join-Path $evidence 'chooser.exe'
& "$repo/build/Build-VelopackSetup.ps1" -OfficialSetup "$release/$appId-win-Setup.exe" -OutputPath $wrapper -VersionFile "$app/VERSION" -PublicKey "$app/release-public-key.xml" -PackageId $appId -Title $title
[ordered]@{schema=1;evidence=$evidence;fixture=$fixture;appId=$appId;title=$title;version=$version;wrapper=$wrapper;app=$app;officialSetup="$release/$appId-win-Setup.exe"}|ConvertTo-Json|Set-Content -LiteralPath "$evidence/fixture.json" -Encoding UTF8
"FIXTURE $evidence"
