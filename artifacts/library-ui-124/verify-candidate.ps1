param([Parameter(Mandatory=$true)][string]$Candidate)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
# Recreate the ignored test-only harness so this runner also works from a fresh checkout.
$probeSource=@'
using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
class NativeCandidateProbe {
 [STAThread] static int Main(string[] args){
  string root=Path.GetFullPath(args[0]),shots=Path.GetFullPath(args[1]);Directory.CreateDirectory(shots);
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string dll=Path.Combine(root,"Web",new AssemblyName(e.Name).Name+".dll");return File.Exists(dll)?Assembly.LoadFile(dll):null;};
  return Run(root,shots);
 }
 static int Run(string root,string shots){try{
  Application.EnableVisualStyles();var assembly=Assembly.LoadFile(Path.Combine(root,"橙子启动器.exe"));
  var type=assembly.GetType("PZ3D.ModernForm",true);string auto=Path.Combine(shots,"native-auto.png");
  using(var form=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public,null,new object[]{Path.Combine(root,"Web"),new[]{"--modern-preview",auto,"--page","home","--size","1366x768"}},null)){
   if(form.Text!="OZL")throw new Exception("native window title is not OZL");
   var web=(WebView2)type.GetField("web",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   bool busy=false,clicked=false,captured=false;Exception failure=null;var timer=new Timer{Interval=60};
   timer.Tick+=async(s,e)=>{if(busy||captured||web.CoreWebView2==null)return;busy=true;try{
    if(!clicked){string ready=await web.CoreWebView2.ExecuteScriptAsync("Boolean(document.querySelector('[data-scene-next]'))");if(ready=="true"){await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-scene-next]').click()");clicked=true;}}
    if(clicked){string ready=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const c=document.querySelector('[data-home-cover]'),src=JSON.parse(document.querySelector('#home-scene-data').textContent).crossroads;if(!window.nativeProbeImage){window.nativeProbeImage=new Image();nativeProbeImage.src=src;}const i=nativeProbeImage;return c?.dataset.sceneId==='crossroads'&&getComputedStyle(c).backgroundImage.includes(src)&&i.complete&&i.naturalWidth===1672&&document.title==='OZL'})()");if(ready=="true"){
      string evidence=await web.CoreWebView2.ExecuteScriptAsync("(()=>{const i=nativeProbeImage;return {title:document.title,brand:document.querySelector('.brand').innerText,scene:document.querySelector('[data-home-cover]').dataset.sceneId,dataImage:i.src.startsWith('data:image/'),width:i.naturalWidth,height:i.naturalHeight}})()");
      File.WriteAllText(Path.Combine(shots,"native-data-image.json"),"{\"nativeTitle\":\""+form.Text+"\",\"web\":"+evidence+"}");
      using(var stream=File.Create(Path.Combine(shots,"native-data-image.png")))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);captured=true;timer.Stop();
    }}
   }catch(Exception error){failure=error;timer.Stop();}finally{busy=false;}};
   form.Shown+=(s,e)=>timer.Start();Application.Run(form);timer.Dispose();
   if(failure!=null)throw failure;if(!captured||!File.Exists(auto)||File.Exists(auto+".error.txt"))throw new Exception("real WebView data image capture not completed within preview window");
   Console.WriteLine("PASS: actual candidate ModernForm OZL title/header, crossroads data URI decoded 1672x941 and native screenshot; fixture only");return 0;
  }
 }catch(Exception e){Console.WriteLine(e);return 1;}}
}
'@
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'NativeCandidateProbe.cs'),$probeSource,[Text.UTF8Encoding]::new($false))
$candidateRoot=[IO.Path]::GetFullPath($Candidate)
$transcript=Join-Path $PSScriptRoot ('candidate-proof-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.log')
Start-Transcript -LiteralPath $transcript | Out-Null
try{
 Add-Type -AssemblyName System.IO.Compression.FileSystem
 Add-Type -AssemblyName System.Drawing
 function Require($Condition,[string]$Name){if(-not $Condition){throw $Name};Write-Output ('PASS: '+$Name)}
 $old=Join-Path $repo 'dist/1.2.3-github-release'
 Require ((Get-FileHash -LiteralPath (Join-Path $old 'update-manifest.json')).Hash -eq '6F03EF618F3C4AF86FD59078EC612B7E047CD7F56636D3DBFC8CA828AC1A0749') 'published 1.2.3 manifest immutable'
 Require ((Get-FileHash -LiteralPath (Join-Path $old 'update-manifest.sig')).Hash -eq '65B203D0803EB2D36CF898DD4ECFCB3A5AA99FADC4AD4E295A0CFD2DDC77AB6F') 'published 1.2.3 signature immutable'
 $raw=[IO.File]::ReadAllBytes((Join-Path $candidateRoot 'update-manifest.json'));$sig=[IO.File]::ReadAllBytes((Join-Path $candidateRoot 'update-manifest.sig'));$public=[IO.File]::ReadAllText((Join-Path $repo 'release-public-key.xml'))
 # Byte loading only: no old Program.Main or updater is executed.
 $oldAssembly=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $old 'core/橙子启动器.exe')))
 $verified=$oldAssembly.GetType('PZ3D.CoreReleaseProtocol',$true).GetMethod('Verify').Invoke($null,@($raw,$sig,$public,'1.2.3'))
 Require ($verified.version -eq '1.2.4') 'actual published 1.2.3 verifier accepts final signed 1.2.4 manifest'
 $manifest=[Text.Encoding]::UTF8.GetString($raw) | ConvertFrom-Json
 $baseline=Get-Content -LiteralPath (Join-Path $old 'update-manifest.json') -Raw | ConvertFrom-Json
 Require ($manifest.files.Count -eq 18 -and (($manifest.files.path | Sort-Object) -join '|') -ceq (($baseline.files.path | Sort-Object) -join '|')) 'exact old 18 owned paths retained'
 $homeHash='82FCDE2FDAF188629D9ED9903CEEB21FD9C1C99EA16CCFFB123E9A51FA529D01'
 foreach($imagePath in @((Join-Path $repo 'Orange3DTool/Web/home.png'),(Join-Path $candidateRoot 'core/Web/home.png'))){Require ((Get-FileHash -LiteralPath $imagePath).Hash -eq $homeHash) ('original home image unchanged: '+$imagePath)}
 $html=[IO.File]::ReadAllText((Join-Path $candidateRoot 'core/Web/index.html'))
 foreach($webFile in @('app.js','app.css','home.png')){Require ((Get-FileHash -LiteralPath (Join-Path $repo ('Orange3DTool/Web/'+$webFile))).Hash -eq (Get-FileHash -LiteralPath (Join-Path $candidateRoot ('core/Web/'+$webFile))).Hash) ('final source Web bytes match candidate: '+$webFile)}
 Require ([IO.File]::ReadAllText((Join-Path $candidateRoot 'core/CHANGELOG.md')) -ceq [IO.File]::ReadAllText((Join-Path $repo 'CHANGELOG.md'))) 'final source changelog bytes match candidate'
 $guide=[IO.File]::ReadAllText((Join-Path $repo '使用说明.md'))+"`r`n`r`n"+[IO.File]::ReadAllText((Join-Path $repo 'docs/THIRD-PARTY.md'))
 Require ([IO.File]::ReadAllText((Join-Path $candidateRoot 'core/使用说明.txt')) -ceq $guide) 'final source guide with third-party notices matches candidate'
 $json=[regex]::Match($html,'<script id="home-scene-data" type="application/json">(.*?)</script>',[Text.RegularExpressions.RegexOptions]::Singleline).Groups[1].Value | ConvertFrom-Json
 foreach($name in @('crossroads','riverside','safehouse')){
  $bytes=[Convert]::FromBase64String($json.$name.Split(',')[1]);$stream=[IO.MemoryStream]::new($bytes);$image=[Drawing.Image]::FromStream($stream)
  try{Require ($image.Width -eq 1672 -and $image.Height -eq 941) ($name+' embedded candidate decodes 1672x941')}finally{$image.Dispose();$stream.Dispose()}
  $sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()}
  Require ($hash -eq (Get-FileHash -LiteralPath (Join-Path $repo ('docs/assets/cover-candidates/'+$name+'.png'))).Hash) ($name+' embedded bytes equal existing source')
 }
 $zip=Join-Path $candidateRoot 'orange-launcher-v1.2.4-win-x64.zip';$setup=Join-Path $candidateRoot 'orange-launcher-setup-v1.2.4.exe'
 $setupAssembly=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes($setup));$zipStream=$setupAssembly.GetManifestResourceStream('Setup.Core.zip');$memory=[IO.MemoryStream]::new();try{$zipStream.CopyTo($memory);$zipBytes=$memory.ToArray()}finally{$zipStream.Dispose();$memory.Dispose()}
 $sha=[Security.Cryptography.SHA256]::Create();try{$embeddedHash=[BitConverter]::ToString($sha.ComputeHash($zipBytes)).Replace('-','')}finally{$sha.Dispose()}
 Require ($embeddedHash -eq (Get-FileHash -LiteralPath $zip).Hash) 'installer embeds byte-identical final portable ZIP'
 $fixture=Join-Path $repo ('artifacts/TestFixtures/t124-native-'+[Guid]::NewGuid().ToString('N').Substring(0,8));[IO.Compression.ZipFile]::ExtractToDirectory($zip,$fixture)
 foreach($file in $manifest.files){Require ((Get-FileHash -LiteralPath (Join-Path $fixture $file.path)).Hash -eq (Get-FileHash -LiteralPath (Join-Path $candidateRoot ('core/'+$file.path))).Hash) ('portable bytes equal signed source: '+$file.path)}
 foreach($exe in @($setup,(Join-Path $fixture '橙子启动器.exe'),(Join-Path $fixture '橙子更新助手.exe'),(Join-Path $fixture '卸载橙子启动器.exe'))){$info=[Diagnostics.FileVersionInfo]::GetVersionInfo($exe);Require ($info.ProductName -ceq 'Orange zombiod launcher' -and $info.FileDescription -ceq 'OZL' -and $info.FileVersion -eq '1.2.4.0') ('OZL Explorer metadata 1.2.4: '+$exe)}
 $priorTemp=$env:TEMP;$priorTmp=$env:TMP
 try{$temp=Join-Path $fixture 'installer-verification';New-Item -ItemType Directory -Path $temp | Out-Null;$env:TEMP=$temp;$env:TMP=$temp;$process=Start-Process -FilePath $setup -ArgumentList '--verify-setup' -PassThru -Wait -WindowStyle Hidden -RedirectStandardOutput (Join-Path $fixture 'setup.stdout.txt') -RedirectStandardError (Join-Path $fixture 'setup.stderr.txt');Require ($process.ExitCode -eq 0) 'actual final trusted installer extraction/scoped uninstall exit 0 (shortcut callback skipped)'}finally{$env:TEMP=$priorTemp;$env:TMP=$priorTmp}
 Copy-Item -LiteralPath (Join-Path $fixture 'Web/Microsoft.Web.WebView2.Core.dll'),(Join-Path $fixture 'Web/Microsoft.Web.WebView2.WinForms.dll') -Destination $fixture
 $probe=Join-Path $fixture 'native-probe.exe';$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
 Write-Output ('COMMAND: '+$compiler+' /target:exe /out:'+ $probe+' NativeCandidateProbe.cs (WinForms, candidate WebView refs)')
 & $compiler /nologo /target:exe "/out:$probe" /reference:System.Windows.Forms.dll "/reference:$fixture/Web/Microsoft.Web.WebView2.Core.dll" "/reference:$fixture/Web/Microsoft.Web.WebView2.WinForms.dll" (Join-Path $PSScriptRoot 'NativeCandidateProbe.cs')
 Require ($LASTEXITCODE -eq 0) 'native probe compile exit 0'
 $shots=Join-Path $PSScriptRoot ('native-'+[IO.Path]::GetFileName($fixture));Write-Output ('COMMAND: '+$probe+' '+$fixture+' '+$shots)
 $process=Start-Process -FilePath $probe -ArgumentList @($fixture,$shots) -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput (Join-Path $fixture 'probe.stdout.txt') -RedirectStandardError (Join-Path $fixture 'probe.stderr.txt')
 Get-Content -LiteralPath (Join-Path $fixture 'probe.stdout.txt'),(Join-Path $fixture 'probe.stderr.txt')
 Require ($process.ExitCode -eq 0) 'actual native WebView candidate scene capture exit 0'
 Get-Content -LiteralPath (Join-Path $shots 'native-data-image.json')
 Write-Output ('NATIVE SHOTS: '+$shots)
 Get-ChildItem -LiteralPath $candidateRoot -File | ForEach-Object {[pscustomobject]@{Name=$_.Name;Length=$_.Length;SHA256=(Get-FileHash -LiteralPath $_.FullName).Hash} | ConvertTo-Json -Compress}
 Write-Output 'EXIT 0'
}catch{Write-Output 'EXIT 1';throw}finally{Stop-Transcript | Out-Null}
