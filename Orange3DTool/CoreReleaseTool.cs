using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
namespace PZ3D {
 public static class CoreReleaseTool {
  public static int Main(string[] args) {try {
   if(args.Length==3&&args[0]=="keygen")GenerateKey(args[1],args[2]);
   else if(args.Length==5&&args[0]=="pack")Pack(args[1],args[2],args[3],args[4],true);
   else if(args.Length==4&&args[0]=="validate")ValidateDirectory(args[1],File.ReadAllBytes(Path.Combine(args[2],"update-manifest.json")),File.ReadAllBytes(Path.Combine(args[2],"update-manifest.sig")),File.ReadAllText(args[3]),true);
   else throw new ArgumentException("keygen <private.xml> <public.xml> | pack <core> <output> <private.xml> <public.xml> | validate <core> <metadata-dir> <public.xml>");
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex.Message);return 1;}}
  public static void GenerateKey(string privatePath,string publicPath) {
   if(File.Exists(privatePath)||File.Exists(publicPath))throw new IOException("Key output already exists; keys are never overwritten.");
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(privatePath)));
   var security=new FileSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));
   using(var rsa=new RSACryptoServiceProvider(3072)) {rsa.PersistKeyInCsp=false;
    using(var stream=new FileStream(privatePath,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.WriteThrough,security)) {byte[] bytes=Encoding.UTF8.GetBytes(rsa.ToXmlString(true));stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
    using(var stream=new FileStream(publicPath,FileMode.CreateNew,FileAccess.Write))using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))writer.Write(rsa.ToXmlString(false));
    Console.WriteLine("Public key SHA256: "+CoreReleaseProtocol.Hash(Encoding.UTF8.GetBytes(rsa.ToXmlString(false))));
   }
  }
  static List<string> Enumerate(string root) {
   var result=new List<string>();var pending=new Queue<string>();pending.Enqueue(root);
   while(pending.Count>0){string dir=pending.Dequeue();LauncherMaintenance.NoReparse(dir);foreach(string item in Directory.GetFileSystemEntries(dir)){LauncherMaintenance.NoReparse(item);if(Directory.Exists(item))pending.Enqueue(item);else result.Add(item.Substring(root.Length+1).Replace('\\','/'));}}
   return result;
  }
  static void RequirePublic(string xml){using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(xml);if(!rsa.PublicOnly||rsa.KeySize<2048||xml.Trim()!=rsa.ToXmlString(false))throw new CryptographicException("Public key must contain only canonical RSA public parameters.");}}
  public static void ValidateDirectory(string root,byte[] raw,byte[] signature,string key,bool binaries) {
   RequirePublic(key);root=CoreDeployment.Root(root);var manifest=CoreReleaseProtocol.Verify(raw,signature,key,"0.0.0");
   var paths=Enumerate(root);if(paths.Contains(CoreDeployment.BootstrapManifest)||paths.Contains(CoreDeployment.BootstrapSignature)){if(!File.ReadAllBytes(CoreDeployment.Safe(root,CoreDeployment.BootstrapManifest)).SequenceEqual(raw)||!File.ReadAllBytes(CoreDeployment.Safe(root,CoreDeployment.BootstrapSignature)).SequenceEqual(signature))throw new CryptographicException("Bootstrap metadata mismatch.");paths.Remove(CoreDeployment.BootstrapManifest);paths.Remove(CoreDeployment.BootstrapSignature);}
   if(paths.Count!=CoreReleaseProtocol.OwnedPaths.Count||paths.Except(CoreReleaseProtocol.OwnedPaths,StringComparer.Ordinal).Any()||manifest.files.Count!=paths.Count)throw new InvalidDataException("Core directory must contain exactly the complete owned file set.");
   foreach(var file in manifest.files)CoreDeployment.RequireFile(root,file.path,file);
   if(File.ReadAllText(Path.Combine(root,"VERSION")).Trim()!=manifest.version)throw new InvalidDataException("VERSION mismatch.");
   string publicFile=File.ReadAllText(Path.Combine(root,"release-public-key.xml"));RequirePublic(publicFile);if(publicFile.Trim()!=key.Trim())throw new CryptographicException("Public key mismatch.");
   if(binaries)foreach(string name in new[]{"橙子启动器.exe","橙子更新助手.exe","卸载橙子启动器.exe"}) {
    var assembly=Assembly.Load(File.ReadAllBytes(Path.Combine(root,name)));string[] resources=assembly.GetManifestResourceNames();
    if(resources.Any(r=>r.EndsWith(".zip",StringComparison.OrdinalIgnoreCase)||r.StartsWith("Modern.",StringComparison.Ordinal)))throw new InvalidDataException("Embedded asset bundles are forbidden in formal binaries.");
    using(var input=assembly.GetManifestResourceStream("Launcher.UpdatePublicKey.xml")) {if(input==null)throw new InvalidDataException("Missing embedded publication key.");using(var reader=new StreamReader(input))if(reader.ReadToEnd().Trim()!=key.Trim())throw new CryptographicException("Embedded key mismatch.");}
    using(var input=assembly.GetManifestResourceStream("Launcher.Version.txt")){if(input==null)throw new InvalidDataException("Missing embedded version.");using(var reader=new StreamReader(input))if(reader.ReadToEnd().Trim()!=manifest.version)throw new InvalidDataException("Embedded version mismatch.");}
    if(assembly.GetName().Version.ToString()!=manifest.version+".0")throw new InvalidDataException("Executable version mismatch.");
   }
  }
  public static void Pack(string source,string output,string privatePath,string publicPath,bool binaries) {
   source=CoreDeployment.Root(source);output=Path.GetFullPath(output);if(Directory.Exists(output)||File.Exists(output))throw new IOException("Release destination exists; use a fresh output directory.");
   string key=File.ReadAllText(publicPath),version=File.ReadAllText(Path.Combine(source,"VERSION")).Trim();RequirePublic(key);CoreReleaseProtocol.ParseVersion(version);
   var manifest=new CoreManifest{schema=1,version=version,platform="win-x64",minimumUpdaterVersion=CoreReleaseProtocol.UpdaterVersion,notes=File.ReadAllText(Path.Combine(source,"CHANGELOG.md")),files=new List<CoreFile>()};
   foreach(string path in CoreReleaseProtocol.OwnedPaths){string file=CoreReleaseProtocol.Target(source,path);string hash=CoreReleaseProtocol.HashFile(file);manifest.files.Add(new CoreFile{path=path,length=new FileInfo(file).Length,sha256=hash,url=CoreReleaseProtocol.ReleaseBase+"v"+version+"/"+hash.ToLowerInvariant()+".bin"});}
   byte[] raw=CoreReleaseProtocol.SerializeManifest(manifest),signature;
   using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(File.ReadAllText(privatePath));string actual=rsa.ToXmlString(false);using(var trusted=new RSACryptoServiceProvider()){trusted.PersistKeyInCsp=false;trusted.FromXmlString(key);if(actual!=trusted.ToXmlString(false))throw new CryptographicException("Signing key does not match tracked public key.");}signature=rsa.SignData(raw,CryptoConfig.MapNameToOID("SHA256"));}
   ValidateDirectory(source,raw,signature,key,binaries);
   Directory.CreateDirectory(output);string core=Path.Combine(output,"core");Directory.CreateDirectory(core);
   foreach(var file in manifest.files){byte[] bytes=File.ReadAllBytes(CoreReleaseProtocol.Target(source,file.path));if(bytes.LongLength!=file.length||CoreReleaseProtocol.Hash(bytes)!=file.sha256)throw new IOException("Source changed during packing.");string target=CoreReleaseProtocol.Target(core,file.path);Directory.CreateDirectory(Path.GetDirectoryName(target));File.WriteAllBytes(target,bytes);string asset=Path.Combine(output,file.sha256.ToLowerInvariant()+".bin");if(!File.Exists(asset))File.WriteAllBytes(asset,bytes);}
   File.WriteAllBytes(Path.Combine(output,"update-manifest.json"),raw);File.WriteAllBytes(Path.Combine(output,"update-manifest.sig"),signature);
   CoreDeployment.WriteNew(core,CoreDeployment.BootstrapManifest,raw);CoreDeployment.WriteNew(core,CoreDeployment.BootstrapSignature,signature);
   using(var zip=ZipFile.Open(Path.Combine(output,"orange-launcher-v"+version+"-win-x64.zip"),ZipArchiveMode.Create)) {
    foreach(var file in manifest.files)Add(zip,file.path,File.ReadAllBytes(CoreReleaseProtocol.Target(core,file.path)));
    Add(zip,CoreDeployment.BootstrapManifest,raw);Add(zip,CoreDeployment.BootstrapSignature,signature);
   }
   Console.WriteLine("Packed v"+version+": "+manifest.files.Count+" files; manifest SHA256 "+CoreReleaseProtocol.Hash(raw));
  }
  static void Add(ZipArchive zip,string path,byte[] bytes){var entry=zip.CreateEntry(path,CompressionLevel.Optimal);entry.LastWriteTime=new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero);using(var stream=entry.Open())stream.Write(bytes,0,bytes.Length);}
 }
}
