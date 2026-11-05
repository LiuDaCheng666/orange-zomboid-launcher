using System;
using System.Reflection;
using System.IO;
namespace PZ3D {
 static class BrowserContainmentTests {
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
  // Break caught: same-origin scripts or native binaries added beside signed Web assets remain browser-readable.
  public static void Resources(){
   var method=typeof(ModernHostPolicy).GetMethod("ResourceAllowed",BindingFlags.Static|BindingFlags.NonPublic);
   Check(method!=null,"browser has a resource-role allowlist");
   Func<string,string,bool> allowed=(url,kind)=>(bool)method.Invoke(null,new object[]{url,kind});
   foreach(var pair in new[]{new[]{"index.html?page=home","Document"},new[]{"app.js","Script"},new[]{"app.css","Stylesheet"},new[]{"home.png","Image"},new[]{"orange-app.ico","Image"}})
    Check(allowed("https://orange.local/"+pair[0],pair[1]),"production UI resource allowed in its role: "+pair[0]);
   foreach(var pair in new[]{new[]{"extra.js","Script"},new[]{"payload.log","Script"},new[]{"WebView2Loader.dll","Document"},new[]{"app.css","Script"},new[]{"home.png","Script"},new[]{"app.js","Document"},new[]{"index.html","Fetch"},new[]{"app.js","XmlHttpRequest"},new[]{"%61pp.js","Script"},new[]{"app.js?remote=1","Script"}})
    Check(!allowed("https://orange.local/"+pair[0],pair[1]),"extra, disguised, encoded or misused local resource rejected: "+pair[0]+" / "+pair[1]);
   foreach(string url in new[]{"https://orange.local.evil.test/app.js","https://user@orange.local/app.js","https://orange.local:444/app.js","http://orange.local/app.js","file:///C:/payload.js","https://insightjs.invalid/l.js","https://images.steamusercontent.com/payload.js"})
    Check(!allowed(url,"Script"),"nonlocal script rejected: "+url);
   Check(allowed("https://images.steamusercontent.com/safe.jpg","Image"),"existing Steam image source preserved");
   Check(!allowed("https://images.steamusercontent.com/safe.jpg","Document")&&!allowed("https://orangezl.com/payload.js","Script"),"image exceptions never grant document or script access");
  }
  static void FrozenFiles(){
   string root=Path.Combine(LauncherTestPaths.Root,"browser-files-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(root);
   foreach(string name in new[]{"index.html","app.js","app.css","home.png","orange-app.ico"})File.WriteAllText(Path.Combine(root,name),"original "+name);
   File.WriteAllText(Path.Combine(root,"extra.js"),"unlisted");
   var files=ModernAssets.BrowserFiles(root);File.WriteAllText(Path.Combine(root,"app.js"),"changed after load");
   Check(files.Count==5&&!files.ContainsKey("/extra.js")&&!files.ContainsKey("/WebView2Loader.dll"),"browser byte snapshot exposes only the five UI files");
   Check(System.Text.Encoding.UTF8.GetString(files["/app.js"])=="original app.js","disk changes after initialization cannot replace the browser script snapshot");
   File.Delete(Path.Combine(root,"app.css"));bool rejected=false;try{ModernAssets.BrowserFiles(root);}catch(InvalidDataException){rejected=true;}
   Check(rejected,"missing browser entry fails closed");
  }
  [STAThread]public static int Main(){try{Resources();FrozenFiles();ModernHostTests.Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
