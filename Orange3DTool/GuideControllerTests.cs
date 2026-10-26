using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class GuideControllerTests {
  static int checks;
  static void Check(bool value,string label){checks++;if(!value)throw new Exception("FAIL: "+label);}
  static void Refuses(Action work,string label){bool refused=false;try{work();}catch{refused=true;}Check(refused,label);}
  static object Call(string method,params object[] args){var type=typeof(Engine).Assembly.GetType("PZ3D.GuidePreferences");Check(type!=null,"persistent guide preferences exist");try{return type.GetMethod(method,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static Dictionary<string,object> D(object value){var j=new JavaScriptSerializer();return j.Deserialize<Dictionary<string,object>>(j.Serialize(value));}
  static string Root(){return Path.Combine(LauncherTestPaths.Root,"guide-"+Guid.NewGuid().ToString("N"));}
  static void Put(string path,string value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value,new UTF8Encoding(false));}
  static void Persistence(){string root=Root(),data=Path.Combine(root,"Data");var first=D(Call("Get",data));Check((int)first["schema"]==1&&!(bool)first["dismissed"],"fresh data requests guide once");Check(!Directory.Exists(data),"reading first-run preference creates no files");Put(Path.Combine(data,"unrelated.json"),"keep");var saved=D(Call("Dismiss",data));Check((bool)saved["dismissed"]&&(bool)D(Call("Get",data))["dismissed"],"dismissal persists across independent readers");Check(saved.Count==2&&!saved.ContainsKey("path"),"guide DTO contains only schema and dismissed");Check(File.ReadAllText(Path.Combine(data,"unrelated.json"))=="keep","guide preference leaves other user settings intact");var otherGame=new Engine(Path.Combine(root,"NewVersionGame"),Path.Combine(root,"OtherUser"),data,true);Check((bool)D(Call("Get",otherGame.Data))["dismissed"],"new game/version selection reuses same data preference");Check(!Directory.Exists(otherGame.Game)&&!Directory.Exists(otherGame.User),"guide does not create game or save paths");}
  static void Corruption(){string data=Root(),file=Path.Combine(data,"user-settings/guide.json");foreach(string invalid in new[]{"{bad","{}","{\"schema\":2,\"dismissed\":true}","{\"schema\":1,\"dismissed\":\"true\"}","{\"schema\":1,\"dismissed\":true,\"path\":\"x\"}",new string('x',4097)}){Put(file,invalid);Refuses(()=>Call("Get",data),"invalid preference is refused");Refuses(()=>Call("Dismiss",data),"dismiss never replaces an unrecognized preference");Check(File.ReadAllText(file)==invalid,"corrupt original preference remains byte-preserved");}}
  static void Concurrent(){string data=Root();Exception error=null;var gate=new ManualResetEvent(false);ThreadStart work=()=>{gate.WaitOne();try{Call("Dismiss",data);}catch(Exception x){error=x;}};var a=new Thread(work);var b=new Thread(work);a.Start();b.Start();gate.Set();a.Join();b.Join();if(error!=null)throw error;Check((bool)D(Call("Get",data))["dismissed"],"concurrent dismissals leave one complete preference");Check(!Directory.GetFiles(data,"*.tmp",SearchOption.AllDirectories).Any(),"atomic preference cleans owned temporary files");}
  static void Payloads(){var type=typeof(ModernController);var action=type.GetMethod("GuideAction",BindingFlags.Static|BindingFlags.NonPublic);var validate=type.GetMethod("ValidateGuidePayload",BindingFlags.Static|BindingFlags.NonPublic);Check(action!=null&&validate!=null,"guide native action and validator API exist");Check((bool)action.Invoke(null,new object[]{"guideGet"})&&(bool)action.Invoke(null,new object[]{"guideDismiss"})&&!(bool)action.Invoke(null,new object[]{"guideReset"}),"guide API exposes only get and dismiss");Action<string,Dictionary<string,object>> run=(name,p)=>{try{validate.Invoke(null,new object[]{name,p});}catch(TargetInvocationException x){throw x.InnerException;}};run("guideGet",new Dictionary<string,object>());run("guideDismiss",new Dictionary<string,object>());foreach(string name in new[]{"guideGet","guideDismiss"}){Refuses(()=>run(name,null),"null guide payload refused");Refuses(()=>run(name,new Dictionary<string,object>{{"path","C:\\other"}}),"guide cannot select a persistence path");Refuses(()=>run(name,new Dictionary<string,object>{{"dismissed",false}}),"web cannot reset dismissal");}Check(true,"empty guide payloads accepted");}
  public static int Main(){try{Persistence();Corruption();Concurrent();Payloads();Console.WriteLine("GuideController: "+checks+" assertions passed.");return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
 }
}
