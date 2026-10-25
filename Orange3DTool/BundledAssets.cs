using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
namespace PZ3D {
 static class BundledAssets {
  const string Resource="Orange3D.Bundle.zip";
  static readonly object sync=new object();
  public static byte[] ReadOwnAsset(string name){if(name!="OrangeShootPresentation.jar"&&name!="OrangeLaunchBridge.lua"&&name!="OrangeStartup.jar")throw new InvalidDataException("Unknown bridge asset.");
#if OZL_VELOPACK
 if(DistributionPaths.Enabled)return DistributionBootstrap.Asset("Assets/"+name);
#endif
if(Assembly.GetExecutingAssembly().GetManifestResourceInfo("Launcher.UpdatePublicKey.xml")!=null){string root=CoreDeployment.Root(AppDomain.CurrentDomain.BaseDirectory),path="Assets/"+name;var record=CoreDeployment.ReadAuthenticated(root);var file=record.files.Find(f=>f.path==path);if(file==null)throw new InvalidDataException("Missing owned bridge asset.");CoreDeployment.RequireFile(root,path,file);return File.ReadAllBytes(CoreReleaseProtocol.Target(root,path));}using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(input==null)throw new InvalidDataException("Missing bridge resource.");using(var output=new MemoryStream()){input.CopyTo(output);return output.ToArray();}}}
  public static bool Embedded{get{return Assembly.GetExecutingAssembly().GetManifestResourceInfo(Resource)!=null;}}
  public static string PresetPath{get{return Path.Combine(CurrentRoot,"PresetShaders");}}
  public static string NoticePath{get{return Path.Combine(CurrentRoot,"ThirdPartyNotices");}}
  static string resolved;
  static string CurrentRoot{get{if(!Embedded)return AppDomain.CurrentDomain.BaseDirectory;lock(sync){if(resolved==null)resolved=Ensure(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PZ3DManager"));return resolved;}}}
  public static string Ensure(string dataRoot){lock(sync){if(!Embedded)throw new Exception("此开发程序未包含单文件资源。");string id;using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource))using(var hash=SHA256.Create())id=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").Substring(0,24);string root=Path.Combine(Path.GetFullPath(dataRoot),"bundled-assets",id);Directory.CreateDirectory(root);using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream(Resource))using(var zip=new ZipArchive(input,ZipArchiveMode.Read)){foreach(var entry in zip.Entries){string rel=entry.FullName.Replace('\\','/').TrimEnd('/');if(rel.Length==0)continue;if(!rel.StartsWith("PresetShaders/",StringComparison.Ordinal)&&!rel.StartsWith("ThirdPartyNotices/",StringComparison.Ordinal)&&rel!="PresetShaders"&&rel!="ThirdPartyNotices")throw new Exception("集成资源含未知目录。");string target=DlssInstaller.SafePath(root,rel);if(entry.FullName.EndsWith("/")||entry.FullName.EndsWith("\\")){Directory.CreateDirectory(target);continue;}string expected;using(var content=entry.Open())using(var hash=SHA256.Create())expected=BitConverter.ToString(hash.ComputeHash(content)).Replace("-","");if(File.Exists(target)&&new FileInfo(target).Length==entry.Length&&DlssInstaller.HashFile(target)==expected)continue;Directory.CreateDirectory(Path.GetDirectoryName(target));string temp=target+".new-"+Guid.NewGuid().ToString("N");try{using(var content=entry.Open())using(var output=File.Create(temp))content.CopyTo(output);if(DlssInstaller.HashFile(temp)!=expected)throw new Exception("集成资源校验失败："+rel);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}}return root;}}
 }
}
