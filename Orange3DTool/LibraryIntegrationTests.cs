using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
  sealed class HouseFixture:IDisposable {
   internal readonly ModernController Controller;internal readonly Engine E;internal readonly string SourceMeta,TargetChunk;internal readonly byte[] Before;
   internal HouseFixture(string root){string game=Game(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");Put(Path.Combine(game,"game-version.txt"),"42.21");Put(Path.Combine(user,"mods","default.txt"),"mods\n{\n}\nmaps\n{\n}\n");string source=Path.Combine(user,"Saves","Sandbox","Source"),target=Path.Combine(user,"Saves","Sandbox","Target");Directory.CreateDirectory(source);Directory.CreateDirectory(target);SourceMeta=Path.Combine(source,"map_meta.bin");File.WriteAllBytes(SourceMeta,SafehouseCatalogTests.Fixture());byte[] dictionary=SafehouseTransferFormatTests.DictionaryFixture(new short[]{1},new[]{"Base.Apple"});File.WriteAllBytes(Path.Combine(source,"WorldDictionary.bin"),dictionary);File.WriteAllBytes(Path.Combine(target,"WorldDictionary.bin"),dictionary);Put(Path.Combine(source,"mods.txt"),"mods\n{\n}\nmaps\n{\n}\n");Put(Path.Combine(target,"mods.txt"),"mods\n{\n}\nmaps\n{\n}\n");string sourceChunk=Path.Combine(source,"map","90","-2.bin");TargetChunk=Path.Combine(target,"map","90","-2.bin");Directory.CreateDirectory(Path.GetDirectoryName(sourceChunk));Directory.CreateDirectory(Path.GetDirectoryName(TargetChunk));File.WriteAllBytes(sourceChunk,SafehouseTransferFormatTests.ChunkFixture(new short[]{1},new[]{3}));Before=SafehouseTransferFormatTests.ChunkFixture(new short[]{1},new[]{1});File.WriteAllBytes(TargetChunk,Before);Controller=new ModernController(game,user,data,true,null,null,null,null,true);E=Selected(Controller);Directory.CreateDirectory(E.Data);typeof(Engine).GetField("ProcessInventory",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(E,(Func<string,System.Diagnostics.Process[]>)(name=>new System.Diagnostics.Process[0]));}
   internal SafehouseTransferPreview Preview(){var catalog=(SafehouseCatalogResult)Controller.Execute("houseCatalogList",P("sourceId","Sandbox/Source"));return (SafehouseTransferPreview)Controller.Execute("houseTransferPreview",new Dictionary<string,object>{{"sourceId","Sandbox/Source"},{"targetId","Sandbox/Target"},{"houseId",catalog.houses[0].id},{"catalogStamp",catalog.stamp},{"marginChunks",0}});}
   internal object Apply(string stamp){return Controller.Execute("houseTransferApply",new Dictionary<string,object>{{"previewStamp",stamp},{"confirmed",true}});}
   public void Dispose(){Controller.Dispose();}
  }
  static void AutomaticHouseBoundaries(string root){
   using(var f=new HouseFixture(Path.Combine(root,"house-complete"))){var preview=f.Preview();var result=(SafehouseTransferResult)f.Apply(preview.previewStamp);Check(result.status=="completed"&&SafehouseTransferFormatTests.ChunkFixture(new short[]{1},new[]{3}).SequenceEqual(File.ReadAllBytes(f.TargetChunk)),"unchanged automatic claim completes the exact native source region transaction");File.WriteAllBytes(f.SourceMeta,SafehouseCatalogTests.Fixture(width:20));Check(SafehouseCatalogTests.Fixture(width:20).SequenceEqual(File.ReadAllBytes(f.SourceMeta)),"successful automatic apply releases the metadata selection lease");Refuses(()=>f.Apply(preview.previewStamp),"a completed automatic wrapper cannot be replayed");}
   using(var f=new HouseFixture(Path.Combine(root,"house-stale"))){var preview=f.Preview();Check(preview.canTransfer&&preview.previewStamp.StartsWith("autoHouse:",StringComparison.Ordinal)&&preview.requestedBounds.minX==720&&preview.requestedBounds.maxX==736,"automatic bridge resolves native saved bounds and issues its own opaque wrapper: "+string.Join("; ",preview.errors));File.WriteAllBytes(f.SourceMeta,SafehouseCatalogTests.Fixture(width:18));Refuses(()=>f.Apply(preview.previewStamp),"changed map metadata rejects an automatic apply");Check(f.Before.SequenceEqual(File.ReadAllBytes(f.TargetChunk)),"stale automatic selection leaves every target chunk untouched");File.WriteAllBytes(f.SourceMeta,SafehouseCatalogTests.Fixture());Refuses(()=>f.Apply(preview.previewStamp),"consumed stale automatic wrapper cannot be replayed after metadata changes back");}
   using(var f=new HouseFixture(Path.Combine(root,"house-late"))){var preview=f.Preview();Check(preview.canTransfer,"late metadata fixture has a valid automatic preview");bool changed=false;f.E.Progress=(value,text)=>{if(!changed&&text.StartsWith("复制目标存档：",StringComparison.Ordinal)){changed=true;File.WriteAllBytes(f.SourceMeta,SafehouseCatalogTests.Fixture(width:19));}};Refuses(()=>f.Apply(preview.previewStamp),"automatic selection metadata remains protected through staging and target publication");Check(changed&&f.Before.SequenceEqual(File.ReadAllBytes(f.TargetChunk)),"late automatic metadata change never publishes a target chunk");}
  }
  public static int Main(){string root=Path.Combine(Path.GetTempPath(),"ozl-library-bridge-"+Guid.NewGuid().ToString("N"));try{
   Host("versionList",P());Host("recommendedServers",new{refresh=true});Host("versionChooseImport",new{name="其他版本"});checks+=3;
   Host("houseCatalogList",new{sourceId="Sandbox/a"});Host("houseTransferPreview",new{sourceId="Sandbox/a",targetId="Sandbox/b",houseId="house-id",catalogStamp="catalog-stamp",marginChunks=0});checks+=2;
   Refuses(()=>Host("houseTransferPreview",new{sourceId="Sandbox/a",targetId="Sandbox/b",houseId="x",marginChunks=0}),"automatic house must bind metadata stamp");
   Refuses(()=>Host("houseTransferPreview",new{sourceId="Sandbox/a",targetId="Sandbox/b",houseId="x",catalogStamp="s",minX=0,minY=0,maxX=8,maxY=8,marginChunks=0}),"automatic house cannot inject raw bounds");
   Refuses(()=>Host("houseCatalogList",new{sourceId="Sandbox/a",path="C:\\Windows"}),"house catalog cannot inject a path");
   Host("forceTakeoverPreview",P());Host("forceTakeoverRecovery",P());Host("forceTakeoverApply",new{stamp="opaque",confirmed=true});Host("forceTakeoverRecover",new{stamp="opaque",confirmed=true});checks+=4;
   Refuses(()=>Host("forceTakeoverApply",new{stamp="opaque",confirmed="true"}),"force apply confirmation must be a boolean");
   Refuses(()=>Host("forceTakeoverRecover",new{stamp="opaque",confirmed="false"}),"force recovery confirmation must be a boolean");
   Refuses(()=>Host("forceTakeoverPreview",new{path="C:\\Windows"}),"force preview cannot inject a filesystem path");
   Refuses(()=>Host("forceTakeoverApply",new{stamp="opaque",confirmed=true,path="C:\\Windows"}),"force apply cannot inject a filesystem path");
   Refuses(()=>Host("forceTakeoverRecover",new{stamp="opaque",confirmed=true,path="C:\\Windows"}),"force recovery cannot inject a filesystem path");
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
   using(var controller=new ModernController(game,user,data,true,null,null,null,null,true)){
    controller.Execute("javaComponentsApply",new Dictionary<string,object>{{"previewStamp","invalid-plan"},{"confirmed",true}});
    var tasks=Json.Deserialize<LauncherTaskStore>(Json.Serialize(controller.Execute("taskGet",null)));Check(tasks.history[0].status=="failed"&&tasks.history[0].message.Contains("组件预览已失效"),"failed Java result journals the actual native reason");
    Refuses(()=>controller.Execute("forceTakeoverApply",new Dictionary<string,object>{{"stamp","invalid-plan"},{"confirmed",false}}),"force apply requires affirmative native confirmation");
    Refuses(()=>controller.Execute("forceTakeoverRecover",new Dictionary<string,object>{{"stamp","invalid-plan"},{"confirmed",false}}),"force recovery requires affirmative native confirmation");
    var forceResult=(ForceTakeover.Result)controller.Execute("forceTakeoverApply",new Dictionary<string,object>{{"stamp","invalid-plan"},{"confirmed",true}});
    tasks=Json.Deserialize<LauncherTaskStore>(Json.Serialize(controller.Execute("taskGet",null)));Check(forceResult.status=="failed"&&tasks.history[0].status=="failed"&&tasks.history[0].message.Contains("[failed]")&&tasks.history[0].message.Contains(forceResult.message),"failed force result journals its native failure rather than a completed task");
    controller.Execute("versionImport",P("path",other,"name","另一个版本"));var library=new VersionLibrary(data,game,user);string id=library.List()[1].Id;
    controller.Execute("versionSelect",P("id",id));var selected=Selected(controller);Check(selected.Game==other&&selected.ManagedVersion,"selection binds exact managed binary");Check(selected.User==library.Selected().UserPath&&selected.User!=user,"selection binds separate user data");
    controller.Execute("setGame",P("path",third));selected=Selected(controller);Check(selected.Game==third&&selected.User==user&&!selected.ManagedVersion,"manual folder selection resets complete legacy identity");Check(new VersionLibrary(data,third,user).Selected().Id=="steam","manual selection persists legacy choice");
   }
   using(var controller=new ModernController(game,user,Path.Combine(root,"dispose-data"),true,null,null,null,null,true)){
    object manager=typeof(ModernController).GetField("javaComponents",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);object managerGate=manager.GetType().GetField("gate",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(manager);
    var cancellation=(CancellationTokenSource)typeof(ModernController).GetField("cancel",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);var token=cancellation.Token;var held=new ManualResetEventSlim();var release=new ManualResetEventSlim();var holder=Task.Run(()=>{lock(managerGate){held.Set();release.Wait();}});Check(held.Wait(2000),"isolated Java service gate is held");var closing=Task.Run(()=>controller.Dispose());try{Check(SpinWait.SpinUntil(()=>token.IsCancellationRequested,2000),"closing cancels work before waiting for Java service disposal");}finally{release.Set();Task.WaitAll(holder,closing);}
   }
   var entered=new ManualResetEventSlim();var completed=new ManualResetEventSlim();
   Func<Engine,string,Dictionary<string,object>,Action<int,string>,CancellationToken,Action<object>,object> network=(e,a,p,progress,cancel,batch)=>{entered.Set();cancel.WaitHandle.WaitOne(5000);completed.Set();cancel.ThrowIfCancellationRequested();return new{};};
   using(var controller=new ModernController(game,user,data,true,null,network,null,null,true)){
    var library=new VersionLibrary(data,game,user);var row=library.List()[1];
    var reading=Task.Run(()=>{try{controller.Execute("serverQuery",P("id","ignored"));}catch(OperationCanceledException){}});Check(entered.Wait(2000),"snapshot read is in flight");
    controller.Execute("versionSelect",P("id",row.Id));Check(!completed.IsSet&&Selected(controller).Game==other,"version selection does not wait for unrelated captured network query");controller.Cancel();Check(reading.Wait(2000)&&!controller.NetworkBusy,"explicit cancellation drains captured query after selection");
   }
   AutomaticHouseBoundaries(root);Console.WriteLine("PASS library integration "+checks+" checks");return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
 }
}
