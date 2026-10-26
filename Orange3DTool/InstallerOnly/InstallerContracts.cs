// Installer-only copy of the existing path policy and UI contracts.
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
}
