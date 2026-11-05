using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class DlssRetirementTests {
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
  static bool Accepted(Dictionary<string,object> payload){try{ModernHostPolicy.Validate("https://orange.local/index.html",new JavaScriptSerializer().Serialize(new{id="retired-dlss",action="dlss",payload=payload}));return true;}catch{return false;}}
  // Break caught: a cached or forged old page can still request DLSS downloads/installation.
  public static void Policy(){
   foreach(string operation in new[]{"enable","download","prepare","import","anything"})
    Check(!Accepted(new Dictionary<string,object>{{"operation",operation},{"confirmed",true}}),"retired DLSS operation rejected: "+operation);
   Check(Accepted(new Dictionary<string,object>{{"operation","disable"},{"confirmed",true}}),"existing deployment can still be explicitly restored");
   foreach(var payload in new[]{new Dictionary<string,object>{{"operation","disable"}},new Dictionary<string,object>{{"operation","disable"},{"confirmed",false}},new Dictionary<string,object>{{"operation","disable"},{"confirmed","true"}},new Dictionary<string,object>{{"operation","disable"},{"confirmed",true},{"path","C:\\forged-cache"}}})
    Check(!Accepted(payload),"restoration rejects absent consent, wrong types and caller paths");
  }
  // Break caught: bypassing the WebView validator still reaches installation, or old recovery state stays stale.
  public static void Controller(){
   string root=Path.Combine(LauncherTestPaths.Root,"dlss-controller-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");
   FeatureTests.Seed(game,data);Directory.CreateDirectory(user);var json=new JavaScriptSerializer();
   using(var c=new ModernController(game,user,data,true)){
    foreach(string operation in new[]{"enable","download","prepare","import"}){
     bool refused=false;try{c.Execute("dlss",new Dictionary<string,object>{{"operation",operation},{"confirmed",true}});}catch(InvalidOperationException){refused=true;}
     Check(refused&&File.ReadAllText(Path.Combine(game,"opengl32.dll"))=="inert old deployment fixture","direct controller refuses retired operation before mutation: "+operation);
    }
    Func<bool> visible=()=>Convert.ToBoolean(json.Deserialize<Dictionary<string,object>>(json.Serialize(c.Execute("settingsGet",new Dictionary<string,object>())))["dlssRecovery"]);
    Check(visible(),"settings exposes existing recovery only");
    c.Execute("dlss",new Dictionary<string,object>{{"operation","disable"},{"confirmed",true}});
    Check(!visible()&&!File.Exists(Path.Combine(game,"opengl32.dll")),"same controller recovery removes files and clears settings state");
    Check(!Directory.Exists(Path.Combine(data,"DLSS-Cache")),"recovery never creates a component download cache");
   }
  }
  // Queue tests run before constructing WinForms controls installs a UI synchronization context.
  [STAThread] public static int Main(){try{Policy();FeatureTests.Run();Controller();ModernHostTests.Run();PortableTests.Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
