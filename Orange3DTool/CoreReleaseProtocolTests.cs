using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace PZ3D {
 // Reflection keeps the initial RED runnable before the protocol types exist.
 static class CoreReleaseProtocolTests {
  static Type protocol; static int passed;
  static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
  const string Digest = "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD";
  const string Asset = "https://github.com/LiuDaCheng666/orange-zomboid-launcher/releases/download/v1.1.1/app.js";
  const string Entry = "{\"path\":\"Web/app.js\",\"sha256\":\"" + Digest + "\",\"url\":\"" + Asset + "\",\"length\":3}";
  const string Fixture = "{\"schema\":1,\"version\":\"1.1.1\",\"platform\":\"win-x64\",\"minimumUpdaterVersion\":\"1.0.0\",\"notes\":\"test release\",\"files\":[" + Entry + "]}";
  static void Check(bool yes, string name) { if(!yes) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
  static object Call(string name, params object[] args) { try { return protocol.GetMethod(name, BindingFlags.Public|BindingFlags.Static).Invoke(null,args); } catch(TargetInvocationException e) { throw e.InnerException; } }
  static object Field(object value,string name) { return value.GetType().GetField(name).GetValue(value); }
  static void Reject(Action action,string name) { bool rejected=false; try { action(); } catch(InvalidDataException) { rejected=true; } catch(CryptographicException) { rejected=true; } catch(ArgumentException) { rejected=true; } Check(rejected,name); }
  static byte[] Sign(RSACryptoServiceProvider key,byte[] raw) { return key.SignData(raw,CryptoConfig.MapNameToOID("SHA256")); }
  static object Signed(RSACryptoServiceProvider key,string json,string current) { byte[] raw=Utf8.GetBytes(json); return Call("Verify",raw,Sign(key,raw),key.ToXmlString(false),current); }
  public static int Main(string[] args) { try { Run(args.Contains("--protocol-only")); Console.WriteLine("CORE PROTOCOL: " + passed + " PASS"); return 0; } catch(Exception e) { Console.WriteLine(e); return 1; } }
  public static void Run(bool protocolOnly=false) {
   var assembly=Assembly.GetExecutingAssembly();
   // Removing resource/attribute generation must break the built executable contract.
   if(!protocolOnly) using(var stream=assembly.GetManifestResourceStream("Launcher.Version.txt")) {
    Check(stream!=null,"runtime version resource is embedded");
    string version; using(var reader=new StreamReader(stream,Utf8))version=reader.ReadToEnd().Trim();
    var app=assembly.GetType("PZ3D.AppVersion"); Check(app!=null,"runtime version API exists");
    Check((string)app.GetProperty("Current").GetValue(null,null)==version,"runtime version derives from embedded VERSION");
    Check((string)app.GetProperty("ReleaseTag").GetValue(null,null)=="v"+version,"release tag derives from runtime version");
    Check(assembly.GetName().Version.ToString()==version+".0","assembly version derives from VERSION");
    Check(((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(assembly,typeof(AssemblyFileVersionAttribute))).Version==version+".0","file version derives from VERSION");
   }
   protocol=assembly.GetType("PZ3D.CoreReleaseProtocol"); Check(protocol!=null,"signed release protocol exists");
   using(var key=new RSACryptoServiceProvider(2048)) using(var wrongKey=new RSACryptoServiceProvider(2048)) {
    key.PersistKeyInCsp=false; wrongKey.PersistKeyInCsp=false;
    byte[] raw=Utf8.GetBytes(Fixture),sig=Sign(key,raw);
    var result=Call("Verify",raw,sig,key.ToXmlString(false),"1.1.0");
    Check((string)Field(result,"version")=="1.1.1","independently signed newer version is accepted");
    Check((string)Field(Signed(key,Fixture,"1.1.1"),"version")=="1.1.1","same version is authentic metadata");
    Check((bool)Call("IsNewer","1.1.1","1.1.0") && !(bool)Call("IsNewer","1.1.1","1.1.1"),"same version is not offered as an update");
    Check((bool)Call("IsNewer","1.10.0","1.9.0"),"version comparison is numeric");
    Reject(()=>Call("Verify",Utf8.GetBytes(Fixture.Replace("test release","altered text")),sig,key.ToXmlString(false),"1.1.0"),"tampering with signed bytes is rejected");
    Reject(()=>Call("Verify",raw,sig,wrongKey.ToXmlString(false),"1.1.0"),"untrusted signing key is rejected");
    Reject(()=>Call("Verify",raw,new byte[32],key.ToXmlString(false),"1.1.0"),"checksum-sized data cannot substitute for a signature");
    Reject(()=>Signed(key,Fixture,"1.1.2"),"older signed release is rejected");
    Reject(()=>Signed(key,Fixture,"bad"),"invalid current version is rejected");
    string[] invalid = {
     Fixture.Replace("\"schema\":1","\"schema\":2"), Fixture.Replace("win-x64","win-arm64"),
     Fixture.Replace("\"minimumUpdaterVersion\":\"1.0.0\"","\"minimumUpdaterVersion\":\"1.0.1\""),
     Fixture.Replace("\"minimumUpdaterVersion\":\"1.0.0\"","\"minimumUpdaterVersion\":\"garbage\""),
     Fixture.Replace("1.1.1","1.1"), Fixture.Replace("1.1.1","01.1.1"),
     Fixture.Replace("\"schema\":1","\"schema\":1,\"schema\":1"),
     Fixture.Replace("\"schema\":1","\"schema\":1,\"sch\\u0065ma\":1"),
     Fixture.Replace("\"schema\":1","\"schema\":1,\"Schema\":1"),
     Fixture.Replace("\"schema\":1","\"schema\":1,\"untrusted\":1"),
     Fixture.Replace("\"length\":3","\"length\":3,\"length\":3"),
     Fixture.Replace("\"length\":3","\"length\":3,\"extra\":1"),
     Fixture.Replace("\"notes\":\"test release\",",""),
     Fixture.Replace("\"length\":3","\"length\":0"), Fixture.Replace("\"length\":3","\"length\":67108865"),
     Fixture.Replace("\"length\":3","\"length\":3.0"), Fixture.Replace("\"length\":3","\"length\":03"),
     Fixture.Replace(Digest,"not a sha256"), Fixture.Replace(Entry,Entry+","+Entry.Replace("Web/app.js","web/APP.js")),
     Fixture.Replace(Entry,""), Fixture.Replace(Entry,string.Join(",",Enumerable.Repeat(Entry,33))),
     Fixture.Replace("Web/app.js","ProjectZomboid64.json"),
     Fixture.Replace("https:","http:"), Fixture.Replace("github.com/","evil.example/"),
     Fixture.Replace("LiuDaCheng666/","attacker/"), Fixture.Replace("/v1.1.1/","/v1.1.0/"),
     Fixture.Replace("/app.js\"","/app.js?redirect=bad\""), Fixture.Replace("/app.js\"","/app.js#fragment\""),
     Fixture.Replace("github.com/","github.com@evil.example/"), Fixture.Replace("/app.js\"","/../app.js\""),
     Fixture.Replace("/app.js\"","/%2e%2e%2fapp.js\""), Fixture.Replace("/app.js\"","/folder/app.js\""),
     Fixture.Replace("\"schema\":1","\"schema\":true"), Fixture+"{}", Fixture.Replace("test release","\\ud800")
    };
    for(int i=0;i<invalid.Length;i++) { int n=i; Reject(()=>Signed(key,invalid[n],"1.1.0"),"strict signed document rejection " + n); }
    var badUtf8=new byte[]{0xc3,0x28}; Reject(()=>Call("Verify",badUtf8,Sign(key,badUtf8),key.ToXmlString(false),"1.1.0"),"invalid UTF8 is rejected");
    byte[] large=Utf8.GetBytes(Fixture.Replace("test release",new string('x',1048576))); Reject(()=>Call("Verify",large,Sign(key,large),key.ToXmlString(false),"1.1.0"),"oversized document is rejected");
    string total=Fixture.Replace(Entry,string.Join(",",new[]{Entry,Entry.Replace("Web/app.js","Web/app.css"),Entry.Replace("Web/app.js","Web/index.html")})).Replace("\"length\":3","\"length\":67108864");
    Reject(()=>Signed(key,total,"1.1.0"),"total file length limit is enforced");
    var envelope=result.GetType().GetProperty("VerifiedEnvelope",BindingFlags.Instance|BindingFlags.NonPublic);
    Check(envelope!=null && envelope.GetValue(result,null)!=null,"verified result retains internal signed provenance");
    Check(!new JavaScriptSerializer().Serialize(result).Contains("VerifiedEnvelope"),"authenticated provenance is not public JSON");
    var unverified=new JavaScriptSerializer().Deserialize(Fixture,result.GetType()); Call("Validate",unverified,"1.1.0");
    Check(envelope.GetValue(unverified,null)==null,"Validate cannot grant authentication");
    var proof=envelope.GetValue(result,null); var bytes=(byte[])proof.GetType().GetProperty("Raw").GetValue(proof,null);
    raw[0]=0; sig[0]^=1; bytes[0]=0;
    Check(((byte[])proof.GetType().GetProperty("Raw").GetValue(proof,null))[0]==(byte)'{',"signed provenance defends its raw bytes against mutation");
    var serial=(byte[])Call("SerializeManifest",unverified); Check((string)Field(Signed(key,Utf8.GetString(serial),"1.1.0"),"version")=="1.1.1","manifest serialization stays within strict public schema");
   }
   string root=Path.Combine(LauncherTestPaths.Root,"core-protocol-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
   Check((string)Call("Target",root,"Web/app.js")==Path.Combine(root,"Web","app.js"),"owned path resolves beneath installation root");
   Reject(()=>Call("Target","E:","VERSION"),"drive-relative installation root is rejected");
   Reject(()=>Call("Target","\\launcher","VERSION"),"current-drive-relative installation root is rejected");
   foreach(string path in new[]{"../game/ProjectZomboid64.json","Web/../app.js","Web\\app.js","C:/Web/app.js","/Web/app.js","Web/app.js:evil","Web/CON","Web/app.js.","Web/app.js ","web/app.js","unknown.exe","Data/settings.json"}) {
    string item=path; Reject(()=>Call("Target",root,item),"reject unsafe or unowned path " + item);
   }
   Check((string)Call("Hash",Encoding.ASCII.GetBytes("abc"))==Digest,"SHA256 matches independent known vector in uppercase");
   string file=Path.Combine(root,"hash.txt"); File.WriteAllText(file,"abc",new UTF8Encoding(false)); Check((string)Call("HashFile",file)==Digest,"file hashing streams the actual bytes");
   // Existing junction ancestors must not turn an owned path into an external write.
   string outside=Path.Combine(LauncherTestPaths.Root,"core-protocol-outside-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(outside);
   var psi=new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+Path.Combine(root,"Web")+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var process=System.Diagnostics.Process.Start(psi)) { process.WaitForExit(); Check(process.ExitCode==0,"junction rejection fixture created"); }
   Reject(()=>Call("Target",root,"Web/app.js"),"junction parent is rejected"); Reject(()=>Call("Target",Path.Combine(root,"Web","nested"),"VERSION"),"junction in root ancestry is rejected");
  }
 }
}
