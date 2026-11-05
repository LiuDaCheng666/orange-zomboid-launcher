using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;
namespace PZ3D {
 static class ModernAssets {
  public static string EntryTarget(string root,string entry){
   if(string.IsNullOrEmpty(entry)||entry.IndexOf(':')>=0||entry.StartsWith("/")||entry.StartsWith("\\")||Path.IsPathRooted(entry))throw new Exception("非法资源路径。");
   string path=Path.GetFullPath(Path.Combine(root,entry.Replace('/',Path.DirectorySeparatorChar))),basePath=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!path.StartsWith(basePath,StringComparison.OrdinalIgnoreCase))throw new Exception("资源路径越界。");LauncherMaintenance.NoReparse(path);return path;
  }
  static byte[] Resource(string name){using(var source=typeof(ModernAssets).Assembly.GetManifestResourceStream(name)){if(source==null)throw new Exception("缺少内置界面资源："+name);using(var memory=new MemoryStream()){source.CopyTo(memory);return memory.ToArray();}}}
  static string Hash(byte[] bytes){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
  // Freeze only the browser's five entry files. Native DLLs and added files are never exposed.
  internal static Dictionary<string,byte[]> BrowserFiles(string root){
   List<CoreFile> authenticated=null;string installed=Path.GetDirectoryName(Path.GetFullPath(root));
#if OZL_VELOPACK
   if(DistributionPaths.Enabled)authenticated=DistributionBootstrap.Read(installed,CoreUpdateTrust.PublicKeyXml);
#endif
   if(authenticated==null&&typeof(ModernAssets).Assembly.GetManifestResourceInfo("Launcher.UpdatePublicKey.xml")!=null)authenticated=CoreDeployment.ReadAuthenticated(installed,CoreUpdateTrust.PublicKeyXml).files;
   var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
   foreach(string name in new[]{"index.html","app.js","app.css","home.png","orange-app.ico"}){
    string path=EntryTarget(root,name);if(!File.Exists(path)||new FileInfo(path).Length>67108864)throw new InvalidDataException("界面文件缺失或过大："+name);
    byte[] bytes=File.ReadAllBytes(path);
    if(authenticated!=null){var expected=authenticated.SingleOrDefault(f=>f.path=="Web/"+name);if(expected==null||expected.length!=bytes.Length||!string.Equals(expected.sha256,Hash(bytes),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("界面文件校验失败："+name);}
    result.Add("/"+name,bytes);
   }return result;
  }
  static void WriteOwned(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes))return;string temp=path+".extract-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temp,bytes);LauncherMaintenance.NoReparse(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public static string Prepare(){
#if OZL_VELOPACK
 if(DistributionPaths.Enabled){string installed=AppDomain.CurrentDomain.BaseDirectory;DistributionBootstrap.Read(installed,CoreUpdateTrust.PublicKeyXml);string web=Path.Combine(installed,"Web");AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{string name=new AssemblyName(args.Name).Name;if(name=="Microsoft.Web.WebView2.Core"||name=="Microsoft.Web.WebView2.WinForms")return Assembly.LoadFrom(Path.Combine(web,name+".dll"));return null;};return web;}
#endif
if(typeof(ModernAssets).Assembly.GetManifestResourceInfo("Launcher.UpdatePublicKey.xml")!=null){string installed=AppDomain.CurrentDomain.BaseDirectory;CoreDeployment.AdoptPortable(installed);return PrepareOwned(installed);}
   byte[] bundle=Resource("Modern.Web.zip");string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"OrangeLauncherRuntime","ui-"+Hash(bundle).Substring(0,16));LauncherMaintenance.NoReparse(root);Directory.CreateDirectory(root);
   using(var stream=new MemoryStream(bundle))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)){foreach(var entry in archive.Entries)if(entry.Name.Length>0)EntryTarget(root,entry.FullName);foreach(var entry in archive.Entries){if(entry.Name.Length==0)continue;using(var reader=entry.Open())using(var output=new MemoryStream()){reader.CopyTo(output);WriteOwned(EntryTarget(root,entry.FullName),output.ToArray());}}}
   foreach(string name in new[]{"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll"})WriteOwned(EntryTarget(root,name),Resource("Modern."+name));
   AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{string name=new AssemblyName(args.Name).Name;if(name=="Microsoft.Web.WebView2.Core"||name=="Microsoft.Web.WebView2.WinForms")return Assembly.LoadFrom(Path.Combine(root,name+".dll"));return null;};
   return root;
  }
  public static string PrepareOwned(string installed){return PrepareOwned(installed,CoreUpdateTrust.PublicKeyXml);}
  public static string PrepareOwned(string installed,string key){var record=CoreDeployment.ReadAuthenticated(installed,key);foreach(string path in CoreReleaseProtocol.OwnedPaths.Where(p=>p.StartsWith("Web/",StringComparison.Ordinal))){var file=record.files.SingleOrDefault(f=>f.path==path);if(file==null)throw new InvalidDataException("Missing owned Web asset: "+path);CoreDeployment.RequireFile(installed,path,file);}string root=CoreDeployment.Safe(CoreDeployment.Root(installed),"Web");AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{string name=new AssemblyName(args.Name).Name;if(name=="Microsoft.Web.WebView2.Core"||name=="Microsoft.Web.WebView2.WinForms")return Assembly.LoadFrom(Path.Combine(root,name+".dll"));return null;};return root;}
 }
}
