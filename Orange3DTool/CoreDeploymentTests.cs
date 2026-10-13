using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PZ3D {
 static class CoreFixture {
  internal static int Passed;
  internal static string Key;
  internal static RSACryptoServiceProvider Signer;
  internal static readonly Dictionary<string,byte[]> A=new Dictionary<string,byte[]> {
   {"橙子启动器.exe",Encoding.UTF8.GetBytes("launcher-a")}, {"Web/app.js",Encoding.UTF8.GetBytes("old-js")}, {"Web/app.css",Encoding.UTF8.GetBytes("old-css")}
  };
  internal static void Check(bool yes,string name) { if(!yes)throw new Exception("FAIL: "+name); Passed++; Console.WriteLine("PASS: "+name); }
  internal static object Call(string type,string method,params object[] args) {
   Type t=typeof(CoreFixture).Assembly.GetType("PZ3D."+type); if(t==null)throw new Exception("FAIL: "+type+" behavior exists");
   var m=t.GetMethods(BindingFlags.Static|BindingFlags.Public).Single(x=>x.Name==method&&x.GetParameters().Length==args.Length);
   try{return m.Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}
  }
  internal static object Field(object value,string name) {return value.GetType().GetField(name).GetValue(value);}
  internal static void Reject(Action action,string name) {bool rejected=false;try{action();}catch(Exception e){if(e.Message.StartsWith("FAIL:"))throw;rejected=true;}Check(rejected,name);}
  internal static string Root() {return Path.Combine(LauncherTestPaths.Root,"core-owned-"+Guid.NewGuid().ToString("N"));}
  internal static string FileAt(string root,string path) {return Path.Combine(root,path.Replace('/',Path.DirectorySeparatorChar));}
  internal static byte[] Bytes(string text) {return Encoding.UTF8.GetBytes(text);}
  internal static Dictionary<string,byte[]> Changed() {return new Dictionary<string,byte[]>{{"Web/app.js",Bytes("new-js")}};}
  internal static Dictionary<string,byte[]> B() {var b=new Dictionary<string,byte[]>(A);b["Web/app.js"]=Bytes("new-js");return b;}
  internal static CoreManifest Manifest(string version,Dictionary<string,byte[]> files) {
   var entries=files.Select(f=>new {path=f.Key,sha256=Digest(f.Value),length=f.Value.LongLength,url=CoreReleaseProtocol.ReleaseBase+"v"+version+"/"+Uri.EscapeDataString(Path.GetFileName(f.Key))}).ToArray();
   byte[] raw=Bytes(new JavaScriptSerializer().Serialize(new {schema=1,version=version,platform="win-x64",minimumUpdaterVersion="1.0.0",notes="fixture",files=entries}));
   return CoreReleaseProtocol.Verify(raw,Signer.SignData(raw,CryptoConfig.MapNameToOID("SHA256")),Key,"0.0.0");
  }
  static string Digest(byte[] data) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","");}
  internal static string Install() {string root=Root();Call("CoreDeployment","Install",root,Manifest("1.1.0",A),A,Key);return root;}
  internal static void Same(string root,Dictionary<string,byte[]> files,string name) {Check(files.All(f=>File.Exists(FileAt(root,f.Key))&&File.ReadAllBytes(FileAt(root,f.Key)).SequenceEqual(f.Value)),name);}
  internal static void Initialize() {Signer=new RSACryptoServiceProvider(2048);Signer.PersistKeyInCsp=false;Key=Signer.ToXmlString(false);}
 }
 static class CoreDeploymentTests {
  public static int Main() {try{CoreFixture.Initialize();Run();Console.WriteLine("CORE DEPLOYMENT: "+CoreFixture.Passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  internal static void Run() {
   string root=CoreFixture.Install();CoreFixture.Same(root,CoreFixture.A,"installation writes verified nested files");
   var record=(CoreInstallRecord)CoreFixture.Call("CoreDeployment","Read",root);
   CoreFixture.Check(record.schema==2&&record.version=="1.1.0"&&record.root==root,"v2 record binds root and version");
   CoreFixture.Check((bool)CoreFixture.Call("CoreDeployment","Managed",root),"owned root is managed");
   File.WriteAllText(Path.Combine(root,"personal.txt"),"keep");
   CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","Install",root,CoreFixture.Manifest("1.1.0",CoreFixture.A),CoreFixture.A,CoreFixture.Key),"install refuses occupied root");
   CoreFixture.Call("CoreDeployment","Uninstall",root,null);
   CoreFixture.Check(!File.Exists(CoreFixture.FileAt(root,"Web/app.js"))&&File.ReadAllText(Path.Combine(root,"personal.txt"))=="keep","owned uninstall preserves personal files");
   string modified=CoreFixture.Install();File.WriteAllText(CoreFixture.FileAt(modified,"Web/app.js"),"independent");
   CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","Uninstall",modified,null),"uninstall fails closed on modified owned file");
   CoreFixture.Check(File.Exists(CoreFixture.FileAt(modified,"橙子启动器.exe")),"uninstall preflight preserves all files on conflict");
   string locked=CoreFixture.Install();using(var handle=new FileStream(CoreFixture.FileAt(locked,"Web/app.js"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
    CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","Uninstall",locked,null),"occupied owned file blocks uninstall");
    CoreFixture.Check(File.Exists(CoreFixture.FileAt(locked,"橙子启动器.exe")),"occupied uninstall has no partial deletion");
   }
   var plain=CoreReleaseProtocol.ParseManifest(CoreFixture.Manifest("1.1.0",CoreFixture.A).VerifiedEnvelope.Raw);
   CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","Install",CoreFixture.Root(),plain,CoreFixture.A,CoreFixture.Key),"unsigned install rejected");
   var mutated=CoreFixture.Manifest("1.1.0",CoreFixture.A);mutated.files[1].sha256=new string('0',64);mutated.version="9.0.0";
   string fresh=CoreFixture.Root();CoreFixture.Call("CoreDeployment","Install",fresh,mutated,CoreFixture.A,CoreFixture.Key);
   CoreFixture.Check(((CoreInstallRecord)CoreFixture.Call("CoreDeployment","Read",fresh)).version=="1.1.0","install exclusively uses freshly verified model");
   string missing=CoreFixture.Install();File.Delete(CoreFixture.FileAt(missing,"Web/app.js"));
   var plan=CoreFixture.Call("CoreUpdateTransaction","Plan",missing,CoreFixture.Manifest("1.1.0",CoreFixture.A),CoreFixture.Key);
   CoreFixture.Check(((IEnumerable<CoreFile>)CoreFixture.Field(plan,"Changed")).Single().path=="Web/app.js","missing owned file can be restored at same version");
   string duplicate=CoreFixture.Install();string recordPath=Path.Combine(duplicate,"orange-installation.json");
   File.WriteAllText(recordPath,File.ReadAllText(recordPath).Replace("\"schema\":2","\"schema\":1,\"schema\":2"));
   CoreFixture.Check(!(bool)CoreFixture.Call("CoreDeployment","Managed",duplicate),"ambiguous duplicate install fields are not ownership");
  }
 }
}
