using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Script.Serialization;

namespace PZ3D {
 internal sealed class InstallerDriveFact {
  internal string Root;internal DriveType Type;internal bool Ready;internal long AvailableFreeSpace;
 }
 internal sealed class InstallerPathProbe {internal bool Exists;internal string Error;}
 internal sealed class InstallerDestination {internal string Root,Reason,Error;internal bool UsesUserProgramsFallback;}
 internal static class InstallerDestinationPolicy {
  internal const int MaximumCandidates=100;
  internal static InstallerDestination Recommend(IEnumerable<InstallerDriveFact> drives,string systemPath,string setupPath,string fallbackProgramsDirectory,Func<string,InstallerPathProbe> probe) {
   string system=Path.GetPathRoot(systemPath),setup=Path.GetPathRoot(setupPath);
   var eligible=drives.Where(d=>d.Ready&&d.Type==DriveType.Fixed&&!string.Equals(d.Root,system,StringComparison.OrdinalIgnoreCase)).ToArray();
   var chosen=eligible.FirstOrDefault(d=>string.Equals(d.Root,setup,StringComparison.OrdinalIgnoreCase))??eligible.OrderByDescending(d=>d.AvailableFreeSpace).ThenBy(d=>d.Root,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
   bool fallback=chosen==null;string parent=fallback?fallbackProgramsDirectory:chosen.Root;
   var result=FromParent(parent,probe);result.UsesUserProgramsFallback=fallback;
   result.Reason=fallback?"未发现可用的非系统固定盘，使用用户应用目录（通常位于系统盘）。可更改位置。":string.Equals(chosen.Root,setup,StringComparison.OrdinalIgnoreCase)?"优先使用安装包所在的非系统固定盘。可更改位置。":"使用可用空间最多的非系统固定盘。可更改位置。";
   return result;
  }
  internal static InstallerDestination FromParent(string parent,Func<string,InstallerPathProbe> probe) {
   string root=Path.Combine(parent,"OrangeLauncher");
   for(int index=1;index<=MaximumCandidates;index++) {
    string candidate=index==1?root:root+"-"+index;var state=probe(candidate);
    if(!string.IsNullOrEmpty(state.Error))return new InstallerDestination{Root=candidate,Error=state.Error};
    if(!state.Exists)return new InstallerDestination{Root=candidate};
   }
   return new InstallerDestination{Root=root,Error="推荐目录已被占用，且未找到空闲后缀。请更改安装位置。"};
  }
  internal static string ValidateInstallRoot(string root) {
   root=CoreDeployment.Root(root);
   if(File.Exists(root))throw new IOException("目标路径是文件，请选择独立的工具目录。");
   if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("目标目录不为空。已有安装请从启动器更新，或选择新的独立空目录；不会覆盖已有文件。");
   return root;
  }
 }
 internal static class InstallerPlatform {
  internal static InstallerDriveFact[] ReadDriveFacts() {
   var result=new List<InstallerDriveFact>();
   foreach(var drive in DriveInfo.GetDrives()) {
    var fact=new InstallerDriveFact{Root=drive.Name};
    try{fact.Type=drive.DriveType;fact.Ready=drive.IsReady;if(fact.Ready&&fact.Type==DriveType.Fixed)fact.AvailableFreeSpace=drive.AvailableFreeSpace;}
    catch(IOException){fact.Ready=false;}catch(UnauthorizedAccessException){fact.Ready=false;}catch(System.Security.SecurityException){fact.Ready=false;}
    result.Add(fact);
   }
   return result.ToArray();
  }
  internal static InstallerPathProbe ProbeRecommendation(string root) {
   try{root=CoreDeployment.Root(root);return new InstallerPathProbe{Exists=Directory.Exists(root)||File.Exists(root)};}
   catch(Exception error){return new InstallerPathProbe{Error="无法使用推荐位置："+error.Message+" 请更改安装位置。"};}
  }
 }
 internal enum InstallerPhase {ValidatePackage,ReadArchive,Deploy,VerifyInstalledFiles,Complete}
 internal enum InstallerProgressUnit {None,Bytes,Files}
 internal sealed class InstallerProgress {
  internal readonly InstallerPhase Phase;internal readonly string Message;internal readonly long Current,Total;internal readonly InstallerProgressUnit Unit;
  internal InstallerProgress(InstallerPhase phase,string message,long current=0,long total=0,InstallerProgressUnit unit=InstallerProgressUnit.None){Phase=phase;Message=message;Current=current;Total=total;Unit=unit;}
 }
 internal sealed class InstallerInstallResult {
  internal readonly string Root,Version,InstallationId,LauncherPath,UninstallerPath,EvidenceDirectory;
  internal InstallerInstallResult(string root,string version,string id){Root=root;Version=version;InstallationId=id;LauncherPath=Path.Combine(root,"橙子启动器.exe");UninstallerPath=Path.Combine(root,"卸载橙子启动器.exe");EvidenceDirectory=Path.Combine(root,CoreDeployment.Runtime);}
  internal InstallerInstallResult(string root,string version,string id,string launcher,string uninstaller):this(root,version,id){LauncherPath=launcher;UninstallerPath=uninstaller;}
 }
 internal sealed class InstallerInstallException:IOException {
  internal readonly InstallerPhase Phase;internal readonly string Root,EvidenceDirectory;
  internal InstallerInstallException(InstallerPhase phase,string root,string evidenceDirectory,Exception inner):base(inner.Message,inner){Phase=phase;Root=root;EvidenceDirectory=evidenceDirectory;}
 }
 // Embedded resources and the trust key are supplied by InstallerProgram. There is
 // no filesystem-feed, capture or arbitrary-command mode in the installer service.
 internal sealed class InstallerInstallService {
  readonly Func<string,byte[]> readResource;readonly string publicKeyXml,expectedVersion;readonly bool legacy;
  internal InstallerInstallService(Func<string,byte[]> resource,string key,string version){readResource=resource;publicKeyXml=key;expectedVersion=version;}
  InstallerInstallService(Func<string,byte[]> resource,string version):this(resource,null,version){legacy=true;}
  internal static InstallerInstallService Legacy(Func<string,byte[]> resource,string version){return new InstallerInstallService(resource,version);}
  static void Report(Action<InstallerProgress> progress,InstallerPhase phase,string message,long current=0,long total=0,InstallerProgressUnit unit=InstallerProgressUnit.None){if(progress!=null)progress(new InstallerProgress(phase,message,current,total,unit));}
  internal InstallerInstallResult Install(string root,Action<InstallerProgress> progress) {
   InstallerPhase phase=InstallerPhase.ValidatePackage;
   try {
    Report(progress,phase,"校验安装位置与安装包签名");root=InstallerDestinationPolicy.ValidateInstallRoot(root);
    if(legacy)return InstallLegacy(root,progress,ref phase);
    var manifest=CoreReleaseProtocol.Verify(readResource("Setup.Manifest.json"),readResource("Setup.Manifest.sig"),publicKeyXml,"0.0.0");
    if(manifest.version!=expectedVersion)throw new InvalidDataException("安装包签名版本与安装器版本不一致。");
    if(!manifest.files.Any(f=>f.path=="橙子启动器.exe")||!manifest.files.Any(f=>f.path=="卸载橙子启动器.exe"))throw new InvalidDataException("安装包缺少启动器或卸载器。");
    byte[] packed=readResource("Setup.Core.zip");long total=manifest.files.Sum(f=>f.length),current=0;var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
    phase=InstallerPhase.ReadArchive;Report(progress,phase,"读取并释放准备（解压字节）",0,total,InstallerProgressUnit.Bytes);
    using(var input=new MemoryStream(packed))using(var archive=new ZipArchive(input,ZipArchiveMode.Read)) {
     if(archive.Entries.Count!=manifest.files.Count+2)throw new InvalidDataException("安装包条目数量不匹配。");
     var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);byte[] buffer=new byte[32768];
     foreach(var entry in archive.Entries) {
      if(!seen.Add(entry.FullName))throw new InvalidDataException("安装包条目重复。");
      if(entry.FullName==CoreDeployment.BootstrapManifest||entry.FullName==CoreDeployment.BootstrapSignature)continue;
      var file=manifest.files.SingleOrDefault(f=>f.path==entry.FullName);
      if(file==null||entry.Length!=file.length)throw new InvalidDataException("安装包文件与签名清单不匹配。");
      using(var stream=entry.Open())using(var bytes=new MemoryStream((int)file.length)) {
       int count;long copied=0;while((count=stream.Read(buffer,0,(int)Math.Min(buffer.Length,file.length-copied+1)))>0) {
        if(copied+count>file.length)throw new InvalidDataException("解压文件长度超过签名长度。");
        bytes.Write(buffer,0,count);copied+=count;current+=count;Report(progress,phase,"释放准备："+file.path,current,total,InstallerProgressUnit.Bytes);
       }
       if(copied!=file.length)throw new InvalidDataException("解压文件长度不足。");files.Add(file.path,bytes.ToArray());
      }
     }
     if(!seen.Contains(CoreDeployment.BootstrapManifest)||!seen.Contains(CoreDeployment.BootstrapSignature))throw new InvalidDataException("安装包缺少签名引导资料。");
    }
    phase=InstallerPhase.Deploy;Report(progress,phase,"事务部署中：安全写入并提交核心文件");
    var installed=CoreDeployment.Install(root,manifest,files,publicKeyXml);
    phase=InstallerPhase.VerifyInstalledFiles;Report(progress,phase,"认证安装记录并核验落盘文件",0,manifest.files.Count,InstallerProgressUnit.Files);
    var authenticated=CoreDeployment.ReadAuthenticated(root,publicKeyXml);RequireIdentity(authenticated,installed.installationId,manifest.version);
    int verified=0;foreach(var file in authenticated.files){CoreDeployment.RequireFile(root,file.path,file);verified++;Report(progress,phase,"落盘核验："+file.path,verified,authenticated.files.Count,InstallerProgressUnit.Files);}
    RequireIdentity(CoreDeployment.ReadAuthenticated(root,publicKeyXml),installed.installationId,manifest.version);
    var result=new InstallerInstallResult(root,manifest.version,installed.installationId);
    Report(progress,InstallerPhase.Complete,"安装完成，签名、记录和自有文件已核验",1,1);return result;
   }catch(Exception error){throw new InstallerInstallException(phase,root,Path.IsPathRooted(root??"")?Path.Combine(root,CoreDeployment.Runtime):"",error);}
  }
  void RequireIdentity(CoreInstallRecord record,string id,string version){if(record.installationId!=id||record.version!=version)throw new InvalidDataException("安装记录身份或版本已变化。");}
  Dictionary<string,byte[]> LegacyFiles(){return new Dictionary<string,byte[]>{{"橙子启动器.exe",readResource("Setup.Launcher.exe")},{"卸载橙子启动器.exe",readResource("Setup.Uninstall.exe")},{"使用说明.txt",readResource("Setup.Guide.md")}};}
  InstallerInstallResult InstallLegacy(string root,Action<InstallerProgress> progress,ref InstallerPhase phase){var files=LegacyFiles();phase=InstallerPhase.Deploy;Report(progress,phase,"部署兼容版启动器文件");PortableDeployment.Install(root,files);phase=InstallerPhase.VerifyInstalledFiles;Report(progress,phase,"核验兼容版安装记录与文件",0,files.Count,InstallerProgressUnit.Files);string marker=VerifyLegacy(root,files,progress);var result=new InstallerInstallResult(root,expectedVersion,marker);Report(progress,InstallerPhase.Complete,"兼容版安装完成，记录与自有文件已核验",1,1);return result;}
  string VerifyLegacy(string root,Dictionary<string,byte[]> files,Action<InstallerProgress> progress=null){
   string recordPath=Path.Combine(root,CoreDeployment.RecordName);LauncherMaintenance.NoReparse(recordPath);
   if(!File.Exists(recordPath)||new FileInfo(recordPath).Length>65536)throw new InvalidDataException("兼容版安装记录无效。");
   var record=new JavaScriptSerializer().Deserialize<DeploymentRecord>(File.ReadAllText(recordPath));
   if(record==null||record.Root!=root||record.Files==null||record.Files.Count!=files.Count||string.IsNullOrEmpty(record.RuntimeMarker))throw new InvalidDataException("兼容版安装记录不匹配。");
   string marker=Path.Combine(root,CoreDeployment.Runtime,".orange-installed-runtime");LauncherMaintenance.NoReparse(marker);if(!File.Exists(marker)||File.ReadAllText(marker)!=record.RuntimeMarker)throw new InvalidDataException("兼容版安装所有权标记不匹配。");
   int current=0;foreach(var file in files){string hash;string path=Path.Combine(root,file.Key);LauncherMaintenance.NoReparse(path);if(!record.Files.TryGetValue(file.Key,out hash)||hash!=DlssInstaller.Hash(file.Value)||!File.Exists(path)||DlssInstaller.HashFile(path)!=hash)throw new InvalidDataException("兼容版自有文件不匹配："+file.Key);Report(progress,InstallerPhase.VerifyInstalledFiles,"落盘核验："+file.Key,++current,files.Count,InstallerProgressUnit.Files);}return record.RuntimeMarker;
  }
  internal ProcessStartInfo LauncherStartInfo(InstallerInstallResult result){
   if(result==null)throw new InvalidDataException("安装结果不存在。");string root=CoreDeployment.Root(result.Root);
   if(legacy){if(VerifyLegacy(root,LegacyFiles())!=result.InstallationId)throw new InvalidDataException("兼容版安装身份已变化。");}
   else{var record=CoreDeployment.ReadAuthenticated(root,publicKeyXml);RequireIdentity(record,result.InstallationId,result.Version);var entry=record.files.Single(f=>f.path=="橙子启动器.exe");CoreDeployment.RequireFile(root,entry.path,entry);}
   string launcher=CoreReleaseProtocol.Target(root,"橙子启动器.exe");if(!CoreDeployment.SamePath(launcher,result.LauncherPath))throw new InvalidDataException("启动器路径不匹配。");
   return new ProcessStartInfo(launcher){WorkingDirectory=root,UseShellExecute=false};
  }
 }
}
