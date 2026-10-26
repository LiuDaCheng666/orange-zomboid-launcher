using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class LibraryIntegrationTests {
  static int checks;static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
  static void Refuses(Action action,string message){try{action();}catch{checks++;return;}throw new Exception("Accepted: "+message);}
  static Dictionary<string,object> P(params string[] values){var result=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)result[values[i]]=values[i+1];return result;}
  static void Host(string action,object payload){ModernHostPolicy.Validate("https://orange.local/index.html",Json.Serialize(new{id="test",action=action,payload=payload}));}
  static Engine Selected(ModernController controller){return (Engine)typeof(ModernController).GetField("engine",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);}
  static void Put(string file,string value){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,value);}
  static string Game(string root,string name){string path=Path.Combine(root,name);Put(Path.Combine(path,"ProjectZomboid64.exe"),"fixture");Put(Path.Combine(path,"ProjectZomboid64.json"),"{\"mainClass\":\"zombie.gameStates.MainScreenState\",\"vmArgs\":[\"-Xmx2g\"],\"classPath\":[\".\"]}");return path;}
  public static int Main(){string root=Path.Combine(Path.GetTempPath(),"ozl-library-bridge-"+Guid.NewGuid().ToString("N"));try{
   Host("versionList",P());Host("recommendedServers",new{refresh=true});Host("versionChooseImport",new{name="其他版本"});checks+=3;
   Host("javaComponentsList",P());Host("javaComponentsPreview",new{token="opaque",operation="uninstall",stamp="inventory"});Host("worldEditRecovery",P());checks+=3;
   Refuses(()=>Host("javaComponentsApply",new{previewStamp="opaque",confirmed="true"}),"invalid Java confirmation");
   Refuses(()=>Host("javaComponentsPreview",new{token="opaque",operation="uninstall",stamp="inventory",path="C:\\Windows"}),"injected Java file path");
   Refuses(()=>Host("houseTransferPreview",new{sourceId="Sandbox/a",targetId="Sandbox/b",minX=1.5,minY=0,maxX=8,maxY=8,marginChunks=0}),"noninteger map coordinate");
   Refuses(()=>Host("versionImport",new{path="C:\\Windows",name="x"}),"raw import bypass");
   Refuses(()=>Host("versionPreserve",new{destination="C:\\Windows",name="x"}),"raw copy bypass");
   Refuses(()=>Host("versionInstallBegin",new{major=41,destination="C:\\Windows",name="x"}),"raw install bypass");
   Refuses(()=>Host("versionChooseImport",new{name="x",path="C:\\Windows"}),"injected chooser path");
   Refuses(()=>Host("backupSettingsSave",new{directory="C:\\Windows",retention=10,afterExit=false}),"injected backup location");
   Refuses(()=>Host("backupRestore",new{id="a/b",backupId="b",confirmed="true"}),"invalid restore confirmation");
   string game=Game(root,"steam-game"),other=Game(root,"other-game"),third=Game(root,"third-game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");Directory.CreateDirectory(user);
   using(var controller=new ModernController(game,user,data,true)){
    controller.Execute("javaComponentsApply",new Dictionary<string,object>{{"previewStamp","invalid-plan"},{"confirmed",true}});
    var tasks=Json.Deserialize<LauncherTaskStore>(Json.Serialize(controller.Execute("taskGet",null)));Check(tasks.history[0].status=="failed"&&tasks.history[0].message.Contains("组件预览已失效"),"failed Java result journals the actual native reason");
    controller.Execute("versionImport",P("path",other,"name","另一个版本"));var library=new VersionLibrary(data,game,user);string id=library.List()[1].Id;
    controller.Execute("versionSelect",P("id",id));var selected=Selected(controller);Check(selected.Game==other&&selected.ManagedVersion,"selection binds exact managed binary");Check(selected.User==library.Selected().UserPath&&selected.User!=user,"selection binds separate user data");
    controller.Execute("setGame",P("path",third));selected=Selected(controller);Check(selected.Game==third&&selected.User==user&&!selected.ManagedVersion,"manual folder selection resets complete legacy identity");Check(new VersionLibrary(data,third,user).Selected().Id=="steam","manual selection persists legacy choice");
   }
   using(var controller=new ModernController(game,user,Path.Combine(root,"dispose-data"),true)){
    object manager=typeof(ModernController).GetField("javaComponents",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);object managerGate=manager.GetType().GetField("gate",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(manager);
    var cancellation=(CancellationTokenSource)typeof(ModernController).GetField("cancel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);var token=cancellation.Token;var held=new ManualResetEventSlim();var release=new ManualResetEventSlim();var holder=Task.Run(()=>{lock(managerGate){held.Set();release.Wait();}});Check(held.Wait(2000),"isolated Java service gate is held");var closing=Task.Run(()=>controller.Dispose());try{Check(SpinWait.SpinUntil(()=>token.IsCancellationRequested,2000),"closing cancels work before waiting for Java service disposal");}finally{release.Set();Task.WaitAll(holder,closing);}
   }
   var entered=new ManualResetEventSlim();var completed=new ManualResetEventSlim();
   Func<Engine,string,Dictionary<string,object>,Action<int,string>,CancellationToken,Action<object>,object> network=(e,a,p,progress,cancel,batch)=>{entered.Set();cancel.WaitHandle.WaitOne(5000);completed.Set();cancel.ThrowIfCancellationRequested();return new{};};
   using(var controller=new ModernController(game,user,data,true,null,network)){
    var library=new VersionLibrary(data,game,user);var row=library.List()[1];
    var reading=Task.Run(()=>{try{controller.Execute("serverQuery",P("id","ignored"));}catch(OperationCanceledException){}});Check(entered.Wait(2000),"snapshot read is in flight");
    controller.Execute("versionSelect",P("id",row.Id));Check(completed.IsSet&&reading.Wait(2000),"version selection drains cancelled snapshot before changing engine");Check(Selected(controller).Game==other&&!controller.NetworkBusy,"selection finishes with no stale network lane");
   }
   Console.WriteLine("PASS library integration "+checks+" checks");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
 }
}
