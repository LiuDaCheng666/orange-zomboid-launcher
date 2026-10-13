using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
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
  static void WriteOwned(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);Directory.CreateDirectory(Path.GetDirectoryName(path));if(File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes))return;string temp=path+".extract-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temp,bytes);LauncherMaintenance.NoReparse(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public static string Prepare(){byte[] bundle=Resource("Modern.Web.zip");string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"OrangeLauncherRuntime","ui-"+Hash(bundle).Substring(0,16));LauncherMaintenance.NoReparse(root);Directory.CreateDirectory(root);
   using(var stream=new MemoryStream(bundle))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)){foreach(var entry in archive.Entries)if(entry.Name.Length>0)EntryTarget(root,entry.FullName);foreach(var entry in archive.Entries){if(entry.Name.Length==0)continue;using(var reader=entry.Open())using(var output=new MemoryStream()){reader.CopyTo(output);WriteOwned(EntryTarget(root,entry.FullName),output.ToArray());}}}
   foreach(string name in new[]{"Microsoft.Web.WebView2.Core.dll","Microsoft.Web.WebView2.WinForms.dll","WebView2Loader.dll"})WriteOwned(EntryTarget(root,name),Resource("Modern."+name));
   AppDomain.CurrentDomain.AssemblyResolve+=(sender,args)=>{string name=new AssemblyName(args.Name).Name;if(name=="Microsoft.Web.WebView2.Core"||name=="Microsoft.Web.WebView2.WinForms")return Assembly.LoadFrom(Path.Combine(root,name+".dll"));return null;};
   return root;
  }
 }
}
