// Installer-only copy of the existing 21-file RSA bootstrap verifier.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
namespace PZ3D {
 internal static class DistributionBootstrap {
  internal static readonly string[] Owned=CoreReleaseProtocol.OwnedPaths.Where(p=>p!="橙子更新助手.exe"&&p!="卸载橙子启动器.exe").Concat(new[]{"Velopack.dll","Newtonsoft.Json.dll","ThirdPartyNotices/Velopack.txt","ThirdPartyNotices/Newtonsoft.Json.txt"}).ToArray();
  internal static string Target(string root,string name){if(!Owned.Contains(name))throw new InvalidDataException("Unknown distribution file.");string p=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar)));LauncherMaintenance.NoReparse(p);return p;}
  internal static List<CoreFile> Read(string root,string key){
   string rawPath=Path.Combine(root,"velopack-bootstrap.json"),sigPath=Path.Combine(root,"velopack-bootstrap.sig");LauncherMaintenance.NoReparse(rawPath);LauncherMaintenance.NoReparse(sigPath);
   byte[] raw=CoreDeployment.ReadBytes(rawPath,1048576),signature=CoreDeployment.ReadBytes(sigPath,1024);
   using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(key);if(rsa.KeySize<2048||!rsa.VerifyData(raw,CryptoConfig.MapNameToOID("SHA256"),signature))throw new CryptographicException("Bootstrap signature is invalid.");}
   var m=CoreReleaseProtocol.ReadStoredObject(raw,"schema","version","files");if(!(m["schema"] is long)||(long)m["schema"]!=1||!Equals(m["version"],AppVersion.Current))throw new InvalidDataException("Bootstrap version mismatch.");var files=CoreReleaseProtocol.Files(m["files"]);
   if(files.Count!=Owned.Length||files.Select(f=>f.path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=Owned.Length)throw new InvalidDataException("Incomplete bootstrap inventory.");
   foreach(var f in files){if(f.length<=0||f.length>1073741824||f.url!=""||!Owned.Contains(f.path))throw new InvalidDataException("Invalid bootstrap file.");Require(root,f);}return files;
  }
  internal static void Require(string root,CoreFile file){string p=Target(root,file.path);if(!File.Exists(p)||new FileInfo(p).Length!=file.length||!string.Equals(CoreReleaseProtocol.HashFile(p),file.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Distribution file verification failed: "+file.path);}
  internal static byte[] Asset(string name){string root=AppDomain.CurrentDomain.BaseDirectory;var file=Read(root,CoreUpdateTrust.PublicKeyXml).Single(f=>f.path==name);using(var stream=new FileStream(Target(root,name),FileMode.Open,FileAccess.Read,FileShare.Read)){var bytes=new byte[(int)file.length];int at=0,n;while(at<bytes.Length&&(n=stream.Read(bytes,at,bytes.Length-at))>0)at+=n;if(at!=bytes.Length||!string.Equals(CoreReleaseProtocol.Hash(bytes),file.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Asset changed while reading.");return bytes;}}
 }
}
