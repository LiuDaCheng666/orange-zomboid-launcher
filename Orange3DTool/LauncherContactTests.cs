using System;
using System.Collections.Generic;
namespace PZ3D {
 static class LauncherContactTests {
  static void Check(bool v,string name){if(!v)throw new Exception(name);Console.WriteLine("PASS: "+name);}
  static void Reject(Action a,string name){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected,name);}
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   var p=new Dictionary<string,object>();
   Check(LauncherContact.Resolve(p)=="https://qm.qq.com/q/cuLGbwUx4A","contact resolves fixed official invitation");
   foreach(var key in new[]{"url","path","target"})Reject(()=>LauncherContact.Resolve(new Dictionary<string,object>{{key,"https://example.org"}}),"contact rejects supplied "+key);
   const string request="{\"id\":\"contact\",\"action\":\"openContact\",\"payload\":{}}";
   Check(ModernHostPolicy.Validate("https://orange.local/index.html",request)!=null,"trusted page accepts contact action");
   Reject(()=>ModernHostPolicy.Validate("https://example.org/index.html",request),"untrusted page rejects contact action");
   Reject(()=>ModernHostPolicy.Validate("https://orange.local/index.html",request.Replace("\"payload\":{}","\"payload\":{\"url\":\"x\"}")),"host rejects contact URL payload");
   int launched=0;Reject(()=>LauncherContact.Open(p,true,s=>launched++),"TestMode blocks contact launch");Check(launched==0,"TestMode launches no external program");
   string opened=null;LauncherContact.Open(p,false,s=>opened=s);Check(opened=="https://qm.qq.com/q/cuLGbwUx4A","explicit contact opens only resolved invitation");
   string root=System.IO.Path.Combine(LauncherTestPaths.Root,"contact-"+Guid.NewGuid().ToString("N"));
   using(var controller=new ModernController(System.IO.Path.Combine(root,"Game"),System.IO.Path.Combine(root,"User"),System.IO.Path.Combine(root,"Data"),true)){
    string message="";try{controller.Execute("openContact",p);}catch(Exception e){message=e.Message;}Check(message=="测试模式禁止打开外部页面。","controller contact reaches TestMode guard without launching");
    message="";try{controller.Execute("openContact",new Dictionary<string,object>{{"url","https://example.org"}});}catch(Exception e){message=e.Message;}Check(message=="联系入口不接受网址或路径参数。","controller independently rejects supplied target");
   }
  }
 }
}
