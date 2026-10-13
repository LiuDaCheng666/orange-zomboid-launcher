using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
namespace PZ3D {
 static class CorePackageTests {
  public static int Main(string[] args) {try {
   CoreFixture.Initialize(); string work=CoreFixture.Root();Directory.CreateDirectory(work);
   string source=Path.Combine(work,"source"),output=Path.Combine(work,"release"),key=Path.Combine(work,"fixture-private.xml"),pub=Path.Combine(work,"public.xml");
   File.WriteAllText(key,CoreFixture.Signer.ToXmlString(true));File.WriteAllText(pub,CoreFixture.Key);
   foreach(string path in CoreReleaseProtocol.OwnedPaths) {string file=CoreFixture.FileAt(source,path);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"fixture "+path);}
   File.WriteAllText(Path.Combine(source,"VERSION"),"1.1.0\n");File.WriteAllText(Path.Combine(source,"release-public-key.xml"),CoreFixture.Key);
   // Missing packaging must fail before these real outputs can be inspected.
   CoreFixture.Call("CoreReleaseTool","Pack",source,output,key,pub,false);
   var manifest=CoreReleaseProtocol.Verify(File.ReadAllBytes(Path.Combine(output,"update-manifest.json")),File.ReadAllBytes(Path.Combine(output,"update-manifest.sig")),CoreFixture.Key,"0.0.0");
   CoreFixture.Call("CoreDeployment","AdoptPortable",Path.Combine(output,"core"),CoreFixture.Key);
   CoreFixture.Check(CoreDeployment.Read(Path.Combine(output,"core")).version=="1.1.0","unpacked core directory is runnable through signed adoption");
   CoreFixture.Check(manifest.version=="1.1.0"&&manifest.files.Count==18,"pack preserves VERSION and complete allowlist");
   CoreFixture.Check(manifest.files.All(f=>File.ReadAllBytes(CoreFixture.FileAt(source,f.path)).SequenceEqual(File.ReadAllBytes(Path.Combine(output,new Uri(f.url).Segments.Last())))),"content addressed assets preserve exact input bytes");
   string extracted=Path.Combine(work,"extracted");ZipFile.ExtractToDirectory(Path.Combine(output,"orange-launcher-v1.1.0-win-x64.zip"),extracted);
   CoreFixture.Check(!Directory.Exists(Path.Combine(extracted,"PresetShaders"))&&!Directory.Exists(Path.Combine(extracted,"Web/author-covers")),"full ZIP excludes presets and covers");
   CoreFixture.Call("CoreDeployment","AdoptPortable",extracted,CoreFixture.Key);
   CoreFixture.Call("CoreDeployment","ReadAuthenticated",extracted,CoreFixture.Key);
   CoreFixture.Check(ModernAssets.PrepareOwned(extracted,CoreFixture.Key)==Path.Combine(extracted,"Web"),"owned independent Web assets resolve without embedded ZIP");
   string forged=Path.Combine(work,"forged");ZipFile.ExtractToDirectory(Path.Combine(output,"orange-launcher-v1.1.0-win-x64.zip"),forged);CoreFixture.Call("CoreDeployment","AdoptPortable",forged,CoreFixture.Key);
   string forgedFile=Path.Combine(forged,"Web/app.js");string oldHash=CoreReleaseProtocol.HashFile(forgedFile);File.WriteAllText(forgedFile,"forged script");string forgedRecord=Path.Combine(forged,"orange-installation.json");File.WriteAllText(forgedRecord,File.ReadAllText(forgedRecord).Replace(oldHash,CoreReleaseProtocol.HashFile(forgedFile)));
   CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","ReadAuthenticated",forged,CoreFixture.Key),"unsigned local record cannot authorize changed sidecar");
   CoreFixture.Check(CoreDeployment.Read(extracted).root==extracted&&CoreDeployment.Read(extracted).schema==2,"signed ZIP binds v2 ownership at actual extraction root");
   CoreFixture.Call("CoreDeployment","AdoptPortable",extracted,CoreFixture.Key);
   string bad=Path.Combine(work,"bad");ZipFile.ExtractToDirectory(Path.Combine(output,"orange-launcher-v1.1.0-win-x64.zip"),bad);File.AppendAllText(Path.Combine(bad,"Web/app.js"),"tamper");
   CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","AdoptPortable",bad,CoreFixture.Key),"modified extracted bytes cannot become owned");
   CoreFixture.Check(!File.Exists(Path.Combine(bad,"orange-installation.json")),"failed adoption publishes no ownership record");
   string wrong=Path.Combine(work,"wrong");ZipFile.ExtractToDirectory(Path.Combine(output,"orange-launcher-v1.1.0-win-x64.zip"),wrong);using(var rsa=new RSACryptoServiceProvider(2048)){rsa.PersistKeyInCsp=false;CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","AdoptPortable",wrong,rsa.ToXmlString(false)),"disk public key cannot authorize portable adoption");}
   File.WriteAllText(Path.Combine(source,"unexpected.txt"),"no");CoreFixture.Reject(()=>CoreFixture.Call("CoreReleaseTool","Pack",source,Path.Combine(work,"unexpected"),key,pub,false),"unexpected source files rejected");File.Delete(Path.Combine(source,"unexpected.txt"));
   CoreFixture.Reject(()=>CoreFixture.Call("CoreReleaseTool","Pack",source,output,key,pub,false),"existing release output never overwritten");
   string unsafePublic=Path.Combine(work,"unsafe-public.xml");File.WriteAllText(unsafePublic,CoreFixture.Signer.ToXmlString(true));
   CoreFixture.Reject(()=>CoreFixture.Call("CoreReleaseTool","Pack",source,Path.Combine(work,"private-public"),key,unsafePublic,false),"public key input rejects private RSA parameters before packing");
   CoreFixture.Reject(()=>CoreFixture.Call("CoreReleaseTool","Pack",source,Path.Combine(work,"not-exe"),key,pub,true),"formal package rejects fake executables");
   string self=Path.Combine(extracted,"卸载橙子启动器.exe");CoreDeployment.Uninstall(extracted,self);
   CoreFixture.Call("CoreDeployment","CompleteUninstall",extracted,CoreDeployment.Read(extracted).installationId,CoreReleaseProtocol.HashFile(self));
   CoreFixture.Check(!File.Exists(self)&&!File.Exists(Path.Combine(extracted,"orange-installation.json")),"delayed owned completion removes only matching self and record");
   CoreFixture.Check(Directory.Exists(Path.Combine(extracted,"OrangeLauncherRuntime")),"completion retains runtime diagnostics");
   string user=Path.Combine(work,"user");Directory.CreateDirectory(user);File.WriteAllText(Path.Combine(user,"viewpoint-live.properties"),"graphics.pack=custom-import\n");
   new ShaderSelection(user,Path.Combine(work,"backups")).DefaultOnce(Path.Combine(work,"shader.marker"),false);
   CoreFixture.Check(File.ReadAllText(Path.Combine(user,"viewpoint-live.properties"))=="graphics.pack=custom-import\n","default selection preserves imported user selection without bundled Eclipse");
   Console.WriteLine("CORE PACKAGE: "+CoreFixture.Passed+" PASS; fixture="+work);return 0;
  }catch(Exception ex){Console.WriteLine(ex);return 1;}}
 }
}
