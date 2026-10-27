param([string]$EvidenceDirectory='', [string]$SourceGame='', [string]$FrozenGame='')
$ErrorActionPreference='Stop';$repo=Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'build/ProductionSources.ps1')
if(-not $EvidenceDirectory){$EvidenceDirectory=Join-Path $repo ('artifacts/launch-jvm-environment-'+[guid]::NewGuid().ToString('N'))};[IO.Directory]::CreateDirectory([IO.Path]::GetFullPath($EvidenceDirectory))|Out-Null
$sources=@(Get-OzlProductionSources -ProjectDirectory (Join-Path $repo 'Orange3DTool'))+@(Join-Path $repo 'Orange3DTool/LaunchJvmEnvironmentTests.cs')
$sdk=Join-Path $repo 'Dependencies/WebView2/lib/net462';$refs=@('System.Windows.Forms.dll','System.Drawing.dll','System.Web.Extensions.dll','System.Web.dll','System.Management.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Net.Http.dll')|ForEach-Object{"/reference:$_"};$refs+="/reference:$sdk/Microsoft.Web.WebView2.Core.dll";$refs+="/reference:$sdk/Microsoft.Web.WebView2.WinForms.dll"
$binary=Join-Path $EvidenceDirectory 'LaunchJvmEnvironmentTests.exe'
& C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe /nologo /platform:x64 /target:exe /main:PZ3D.LaunchJvmEnvironmentTests "/out:$binary" $refs $sources 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'compile.log');if($LASTEXITCODE -ne 0){throw 'JRE launch environment test compilation failed'}
$gameRoots=@($SourceGame,$FrozenGame)|Where-Object{$_}
if($gameRoots.Count -gt 0){
    $javaSource=Join-Path $EvidenceDirectory 'LaunchArgumentProbe.java'
    $probeSource=@'
package ozl.tests;
import java.nio.file.*;import java.nio.charset.StandardCharsets;import java.util.Base64;
public class LaunchArgumentProbe {
 static void out(String name,String value){System.out.println(name+"="+Base64.getEncoder().encodeToString(value.getBytes(StandardCharsets.UTF_8)));}
 public static void main(String[] args)throws Exception{for(String name:new String[]{"user.home","ozl.environment","ozl.quoted","ozl.precedence","user.dir","java.class.path"})out(name,System.getProperty(name));out("manifest",new String(Files.readAllBytes(Paths.get(System.getProperty("ozl.environment"))),StandardCharsets.UTF_8).replace("\uFEFF",""));out("argc",Integer.toString(args.length));for(int i=0;i<args.length;i++)out("arg"+i,args[i]);}
}
'@
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($javaSource),$probeSource,(New-Object Text.UTF8Encoding($false)))
    & javac.exe --release 8 -encoding UTF-8 -d $EvidenceDirectory $javaSource
    if($LASTEXITCODE -ne 0){throw 'Independent argument probe compilation failed'}
}
$previousClasses=$env:OZL_LAUNCH_ARGUMENT_CLASSES
try{$env:OZL_LAUNCH_ARGUMENT_CLASSES=[IO.Path]::GetFullPath($EvidenceDirectory)
& $binary @gameRoots 2>&1|Tee-Object (Join-Path $EvidenceDirectory 'results.log');if($LASTEXITCODE -ne 0){throw 'JRE launch environment regression failed'}
}finally{$env:OZL_LAUNCH_ARGUMENT_CLASSES=$previousClasses}
