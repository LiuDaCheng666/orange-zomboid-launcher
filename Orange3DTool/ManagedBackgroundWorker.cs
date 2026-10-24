using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace PZ3D {
 // Only the existing monitor protocols may execute an authenticated derived image.
 // The source/key overload is internal for signed, isolated compiled fixtures.
 static class ManagedBackgroundWorker {
  const string Launcher="橙子启动器.exe";
  internal static Process Start(string role,string first,string second){
   Validate(role,first,second);
   string source=Path.GetFullPath(typeof(ManagedBackgroundWorker).Assembly.Location);
   using(var current=Process.GetCurrentProcess())if(!CoreDeployment.SamePath(source,current.MainModule.FileName))throw new IOException("后台监控程序来源与当前进程不一致。");
   #if OZL_VELOPACK
   if(DistributionPaths.Enabled)return StartDistribution(source,role,first,second);
#endif
   bool trusted=typeof(ManagedBackgroundWorker).Assembly.GetManifestResourceInfo("Launcher.UpdatePublicKey.xml")!=null;
   string key=trusted?CoreUpdateTrust.PublicKeyXml:null,image=Prepare(source,key),root=Path.GetDirectoryName(source);
   if(!CoreDeployment.SamePath(image,source))using(CoreDeployment.Acquire(root)){
    var file=LauncherRecord(root,key);
    // Deny in-place writes and replacement until CreateProcess has mapped the
    // exact verified image. Neither handle outlives this short launch boundary.
    using(var original=new FileStream(CoreDeployment.Safe(root,Launcher),FileMode.Open,FileAccess.Read,FileShare.Read))
    using(var cached=new FileStream(CoreDeployment.Safe(root,Relative(file)),FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!CoreDeployment.Matches(source,file)||!CoreDeployment.Matches(image,file))throw new IOException("后台监控程序校验失败。");
     return Launch(image,root,role,first,second);
    }
   }
   return Launch(image,root,role,first,second);
  }
#if OZL_VELOPACK
  static Process StartDistribution(string source,string role,string first,string second){
   string target=PrepareDistribution(source,CoreUpdateTrust.PublicKeyXml,DistributionPaths.RuntimeRoot);
   string root=Path.GetDirectoryName(source);var file=DistributionBootstrap.Read(root,CoreUpdateTrust.PublicKeyXml).Single(f=>f.path==Launcher);
   using(var original=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
   using(var cached=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read)){
    DistributionBootstrap.Require(root,file);if(!CoreDeployment.Matches(target,file))throw new IOException("后台监控缓存校验失败。");return Launch(target,Path.GetDirectoryName(target),role,first,second);
   }
  }
  internal static string PrepareDistribution(string source,string key,string runtimeRoot){
   if(Path.GetFileName(source)!=Launcher)throw new IOException("后台监控源不是自有启动器程序。");
   string root=Path.GetDirectoryName(source);var file=DistributionBootstrap.Read(root,key).Single(f=>f.path==Launcher);
   string target=Path.Combine(runtimeRoot,"BackgroundWorkers",file.sha256.ToLowerInvariant(),Launcher);LauncherMaintenance.NoReparse(target);
   using(var original=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read)){
    DistributionBootstrap.Require(root,file);Directory.CreateDirectory(Path.GetDirectoryName(target));
    if(!File.Exists(target)){string temp=target+"."+Guid.NewGuid().ToString("N");try{using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){original.CopyTo(output);output.Flush(true);}if(!CoreDeployment.Matches(temp,file))throw new IOException("后台监控副本校验失败。");File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}
    if(!CoreDeployment.Matches(target,file))throw new IOException("后台监控缓存校验失败。");return target;
   }
  }
#endif
  static Process Launch(string image,string root,string role,string first,string second){
   int trailing=first.Length-first.TrimEnd('\\').Length;
   return Process.Start(new ProcessStartInfo(image,role+" \""+first+new string('\\',trailing)+"\" "+second){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root});
  }
  static void Validate(string role,string first,string second){
   if(role!="--playtime-watch"&&role!="--session-watch")throw new ArgumentException("未知后台监控角色。");
   if(!Regex.IsMatch(second??"",@"\A[a-f0-9]{32}\z"))throw new ArgumentException("后台监控会话标识无效。");
   if(role=="--session-watch"){if(!Regex.IsMatch(first??"",@"\A[A-F0-9]{64}\z"))throw new ArgumentException("启动会话标识无效。");}
   else if(string.IsNullOrWhiteSpace(first)||first.Any(c=>c<' '||c=='\u007f'||c=='\"')||!Path.IsPathRooted(first)||!string.Equals(Path.GetFullPath(first),first,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("计时数据目录无效。");
  }
  static CoreFile LauncherRecord(string root,string key){var file=CoreDeployment.ReadAuthenticated(root,key).files.SingleOrDefault(f=>f.path==Launcher);if(file==null)throw new IOException("已认证安装缺少启动器程序记录。");return file;}
  static string Relative(CoreFile file){return CoreDeployment.Runtime+"/BackgroundWorkers/"+file.sha256.ToLowerInvariant()+"/"+Launcher;}
  internal static string Prepare(string source,string key){
   source=Path.GetFullPath(source);string root=CoreDeployment.Root(Path.GetDirectoryName(source));
   // Metadata is explicit trust evidence, even if malformed. Never downgrade a
   // damaged managed installation to the legacy single-image launch path.
   string ownSource=CoreDeployment.Safe(root,Path.GetFileName(source));
   bool evidence=File.Exists(CoreDeployment.Safe(root,CoreDeployment.RecordName))||Directory.Exists(CoreDeployment.Safe(root,CoreDeployment.RecordName))||File.Exists(CoreDeployment.Safe(root,CoreDeployment.Owner))||Directory.Exists(CoreDeployment.Safe(root,CoreDeployment.Owner))||File.Exists(CoreDeployment.Safe(root,CoreDeployment.Runtime+"/CoreBootstrap"))||Directory.Exists(CoreDeployment.Safe(root,CoreDeployment.Runtime+"/CoreBootstrap"));
   if(key==null){if(evidence)throw new IOException("受管安装缺少可信更新密钥；后台监控未启动。");return ownSource;}
   if(Path.GetFileName(source)!=Launcher)throw new IOException("后台监控源不是自有启动器程序。");
   CoreDeployment.AdoptPortable(root,key);
   using(CoreDeployment.Acquire(root)){
    var file=LauncherRecord(root,key);string relative=Relative(file),target=CoreDeployment.Safe(root,relative);
    using(var original=new FileStream(CoreDeployment.Safe(root,Launcher),FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!CoreDeployment.Matches(source,file))throw new IOException("后台监控源程序校验失败。");
     if(Directory.Exists(target))throw new IOException("后台监控缓存被目录占用。");
     if(!File.Exists(target)){
      if(file.length>int.MaxValue)throw new IOException("后台监控源程序异常过大。");
      byte[] bytes=new byte[(int)file.length];int at=0,n;while(at<bytes.Length&&(n=original.Read(bytes,at,bytes.Length-at))>0)at+=n;
      if(at!=bytes.Length||!string.Equals(CoreReleaseProtocol.Hash(bytes),file.sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("后台监控源程序读取校验失败。");
      CoreDeployment.Atomic(root,relative,bytes,()=>{if(File.Exists(CoreDeployment.Safe(root,relative)))throw new IOException("后台监控缓存已被并发创建。");});
     }
     if(!CoreDeployment.Matches(CoreDeployment.Safe(root,relative),file))throw new IOException("后台监控缓存校验失败；未覆盖现有文件。");
     return target;
    }
   }
  }
 }
}
