#if OZL_VELOPACK
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Compiled separately with a freshly generated test key, never the release private key.
 static class BrowserContainmentSignedTests {
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
  static void Reject(Action action,string name){bool rejected=false;try{action();}catch(CryptographicException){rejected=true;}catch(InvalidDataException){rejected=true;}Check(rejected,name);}
  public static int Main(string[] args){try{
   if(args.Length!=2)throw new ArgumentException("test-only private key and isolated output directory required");
   string root=Path.Combine(Path.GetFullPath(args[1]),"signed-browser-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(root);
   var files=new List<CoreFile>();
   foreach(string name in DistributionBootstrap.Owned){string path=DistributionBootstrap.Target(root,name);Directory.CreateDirectory(Path.GetDirectoryName(path));byte[] bytes=Encoding.UTF8.GetBytes("test-only "+name);File.WriteAllBytes(path,bytes);files.Add(new CoreFile{path=name,url="",length=bytes.Length,sha256=CoreReleaseProtocol.Hash(bytes)});}
   byte[] raw=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{schema=1,version=AppVersion.Current,files=files}));
   using(var signer=new RSACryptoServiceProvider()){signer.PersistKeyInCsp=false;signer.FromXmlString(File.ReadAllText(args[0]));Check(signer.ToXmlString(false).Trim()==CoreUpdateTrust.PublicKeyXml.Trim(),"signed test uses its own embedded test identity");File.WriteAllBytes(Path.Combine(root,"velopack-bootstrap.json"),raw);File.WriteAllBytes(Path.Combine(root,"velopack-bootstrap.sig"),signer.SignData(raw,CryptoConfig.MapNameToOID("SHA256")));}
   string web=Path.Combine(root,"Web"),app=Path.Combine(web,"app.js");byte[] original=File.ReadAllBytes(app);var frozen=ModernAssets.BrowserFiles(web);
   Check(frozen.Count==5&&frozen["/app.js"].SequenceEqual(original),"actual Velopack marker branch verifies bootstrap and captures authenticated UI bytes");
   File.WriteAllText(app,"tampered script");Check(frozen["/app.js"].SequenceEqual(original),"authenticated browser snapshot remains unchanged after disk tampering");
   Reject(()=>ModernAssets.BrowserFiles(web),"signed branch rejects changed UI and never falls back to unsigned files");
   File.WriteAllBytes(app,original);byte[] signature=File.ReadAllBytes(Path.Combine(root,"velopack-bootstrap.sig"));signature[0]^=1;File.WriteAllBytes(Path.Combine(root,"velopack-bootstrap.sig"),signature);
   Reject(()=>ModernAssets.BrowserFiles(web),"signed branch rejects invalid bootstrap signature without fallback");
   Console.WriteLine("SIGNED BROWSER EVIDENCE "+root);return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
#endif
