using System;
using System.IO;
using System.Text;

namespace PZ3D {
 internal static class SnapshotStorage {
  const string SettingName="snapshot-directory.txt";
  static string Normalize(string path){
   if(string.IsNullOrWhiteSpace(path))throw new IOException("请选择快照保存目录的完整路径。");
   bool drive=path.Length>=3&&char.IsLetter(path[0])&&path[1]==':'&&(path[2]=='\\'||path[2]=='/');
   bool unc=path.StartsWith(@"\\",StringComparison.Ordinal)&&!path.StartsWith(@"\\?\",StringComparison.Ordinal)&&!path.StartsWith(@"\\.\",StringComparison.Ordinal);
   if(!drive&&!unc)throw new IOException("快照保存目录必须是完整的绝对路径。");
   string full=Path.GetFullPath(path),root=Path.GetPathRoot(full);
   while(full.Length>root.Length&&(full.EndsWith("\\",StringComparison.Ordinal)||full.EndsWith("/",StringComparison.Ordinal)))full=full.Substring(0,full.Length-1);
   return full;
  }
  static string InstallationRoot(){
#if OZL_VELOPACK
   if(!string.IsNullOrWhiteSpace(DistributionPaths.InstallationRoot))return Normalize(DistributionPaths.InstallationRoot);
   // The SDK normally supplies RootAppDir before the controller is created.
   // Keep the fallback stable if called from an installed current directory.
   string content=Normalize(AppDomain.CurrentDomain.BaseDirectory);
   if(string.Equals(Path.GetFileName(content),"current",StringComparison.OrdinalIgnoreCase))return Normalize(Path.GetDirectoryName(content));
   return content;
#else
   return Normalize(AppDomain.CurrentDomain.BaseDirectory);
#endif
  }
  static string Validate(string path){
   string full=Normalize(path),install=InstallationRoot();
   string prefix=install.EndsWith("\\",StringComparison.Ordinal)?install:install+Path.DirectorySeparatorChar;
   if(string.Equals(full,install,StringComparison.OrdinalIgnoreCase)||full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("此位置会随启动器卸载删除，请选旁边的快照目录。");
   LauncherMaintenance.NoReparse(full);
   if(File.Exists(full))throw new IOException("快照保存位置已被文件占用，请选择目录。");
   return full;
  }
  static string Setting(Engine e){
   if(e==null)throw new ArgumentNullException("e");
   string path=Path.Combine(e.Data,SettingName);LauncherMaintenance.NoReparse(path);
   if(System.IO.Directory.Exists(path))throw new IOException("快照路径设置被目录占用。");
   return path;
  }
  internal static string Directory(Engine e){
   string setting=Setting(e);
   if(!File.Exists(setting))return Validate(InstallationRoot()+"快照");
   using(var input=new FileStream(setting,FileMode.Open,FileAccess.Read,FileShare.Read)){
    if(input.Length>131072)throw new IOException("快照路径设置过大，请重新选择保存目录。");
    using(var reader=new StreamReader(input,new UTF8Encoding(false,true),true))return Validate(reader.ReadToEnd());
   }
  }
  internal static string SaveDirectory(Engine e,string absolute){
   string selected=Validate(absolute),setting=Setting(e);System.IO.Directory.CreateDirectory(e.Data);
   string temp=setting+"."+Guid.NewGuid().ToString("N")+".tmp";
   try{
    byte[] bytes=new UTF8Encoding(false,true).GetBytes(selected);
    using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
    LauncherMaintenance.NoReparse(setting);
    if(File.Exists(setting))File.Replace(temp,setting,null);else File.Move(temp,setting);
   }finally{if(File.Exists(temp))File.Delete(temp);}
   return selected;
  }
 }
}
