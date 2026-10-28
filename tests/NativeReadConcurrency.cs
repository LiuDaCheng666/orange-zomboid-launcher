using System;using System.IO;using System.Linq;using System.Text;using System.Threading;using System.Threading.Tasks;using System.Reflection;using System.Collections;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace PZ3D {static class NativeReadConcurrency {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2097152};static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;static int checks,failures;
 static void Check(bool value,string label){checks++;if(!value)failures++;Console.WriteLine((value?"PASS ":"FAIL ")+label);}
 static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
 static Dictionary<string,object> P(params object[] items){var result=new Dictionary<string,object>();for(int i=0;i<items.Length;i+=2)result.Add((string)items[i],items[i+1]);return result;}
 static Dictionary<string,object> D(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
 sealed class Reply {internal object Value;internal Exception Error;}
 static Reply Attempt(Func<object> work){try{return new Reply{Value=work()};}catch(Exception error){return new Reply{Error=error};}}
 static Task<Reply> Send(ModernController c,ModernDispatchQueue queue,string action,Dictionary<string,object> payload=null){payload=payload??P();var captured=c.CaptureRequest(action,payload);return (ModernController.ConfigurationMutation(action,payload)?queue.ExecuteConcurrentMutation(()=>Attempt(captured)):queue.ExecuteSnapshot(()=>Attempt(captured))).ContinueWith(task=>(Reply)task.Result);}
 static Reply Read(ModernController c,ModernDispatchQueue queue,string action,Dictionary<string,object> payload=null){var request=Send(c,queue,action,payload);if(!request.Wait(3000))throw new TimeoutException(action+" did not complete");return request.Result;}
 static string Error(Reply result){return result.Error==null?"":result.Error.Message;}
 static ModernController Owner(ModernController c){return (ModernController)typeof(ModernController).GetMethod("ConfigurationController",Private).Invoke(c,null);}
 static CancellationTokenSource Token(ModernController c){return (CancellationTokenSource)typeof(ModernController).GetField("cancel",Private).GetValue(c);}
 static int Readers(ModernController c){lock(typeof(ModernController).GetField("gate",Private).GetValue(c))return ((IDictionary)typeof(ModernController).GetField("configurationReaders",Private).GetValue(c)).Count;}
 static string[] Files(string root){return Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(x=>x,StringComparer.Ordinal).Select(file=>file+"|"+Convert.ToBase64String(File.ReadAllBytes(file))).ToArray();}
 static void Run(string root){string game=Path.Combine(root,"game"),user=Path.Combine(root,"用户"),data=Path.Combine(root,"data");Put(Path.Combine(game,"ProjectZomboid64.exe"),"isolated inert marker; never execute");Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx2g\"]}");Put(Path.Combine(game,"version.txt"),"42.21.0");Put(Path.Combine(game,"game-version.txt"),"42.21.0");Put(Path.Combine(user,"viewpoint-live.properties"),"# preserve comment\nmouse.invertY=false\ngraphics.mode=Modern\ngraphics.pack=normal\n");Put(Path.Combine(user,"mods/default.txt"),"mods\n{\n}\nmaps\n{\n}\n");Put(Path.Combine(user,"mods/Example/42/mod.info"),"id=Example\nname=Isolated example\n");Put(Path.Combine(user,"Saves/Sandbox/Test/mods.txt"),"mods\n{\n}\nmaps\n{\n}\n");var profiles=new ServerProfiles(data);var server=D(profiles.Upsert(P("id","","stamp",D(profiles.List())["stamp"],"name","Isolated server","host","example.invalid","port",16261,"queryPort",0,"username","isolated","description","","useSteamRelay",false)));string id=(string)((IEnumerable)server["servers"]).Cast<Dictionary<string,object>>().Single()["id"];
 using(var c=new ModernController(game,user,data,true)){
  var queue=new ModernDispatchQueue();var status=Read(c,queue,"status");Check(status.Error==null,"actual raw status request succeeds");if(status.Error==null)Check(!(bool)((Dictionary<string,object>)D(status.Value)["task"])["active"],"owned status never reports its own read as an active task");
  Read(c,queue,"launchConfigurationSelect",P("id","steam"));var owner=Owner(c);var token=Token(owner);var rawCatalog=c.CaptureRequest("modCatalog",P());object cacheGate=typeof(DisplayCache).GetField("gate",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);Task<Reply> scan=null;string[] before=Files(root);Monitor.Enter(cacheGate);
  try{
   scan=queue.ExecuteSnapshot(()=>Attempt(rawCatalog)).ContinueWith(task=>(Reply)task.Result);Check(SpinWait.SpinUntil(()=>owner.Busy||Readers(c)>0,3000)&&!scan.IsCompleted,"real raw catalog read reaches the held production display-cache boundary");
   foreach(var action in new[]{"saveThumb","serverThumb","settingsGet","shadersGet"}){var result=Read(c,queue,action,action=="saveThumb"?P("id","Sandbox/Test"):action=="serverThumb"?P("id",id):P());Check(result.Error==null,action+" completes independently of the catalog read: "+Error(result));}
   var duplicate=Enumerable.Range(0,4).Select(_=>Send(c,queue,"serverThumb",P("id",id))).ToArray();Check(Task.WaitAll(duplicate,3000)&&duplicate.All(task=>task.Result.Error==null),"parallel thumbnail requests all return native results");
   Check(ReferenceEquals(token,Token(owner))&&!token.IsCancellationRequested,"independent reads do not replace or cancel the bound writer token");
   var begin=Read(c,queue,"startupBegin",P("targetKind","server","targetId",id));Check(begin.Error==null,"server connection preparation starts during a catalog read: "+Error(begin));
   if(begin.Error==null){string startup=(string)D(begin.Value)["startupId"];var preflight=Read(c,queue,"launchPreflight",P("targetKind","server","startupId",startup));Check(preflight.Error==null,"real server preflight completes while the unrelated scan remains held: "+Error(preflight));if(preflight.Error==null){var activity=((IEnumerable)D(preflight.Value)["items"]).Cast<Dictionary<string,object>>().Single(row=>(string)row["id"]=="activity");Check((string)activity["status"]=="ok","independent catalog reading is not a server launch activity blocker");}Read(c,queue,"startupCancel",P("startupId",startup));}
   Check(before.SequenceEqual(Files(root)),"read and connection-preflight paths leave all fixture file bytes unchanged");
  }finally{Monitor.Exit(cacheGate);if(scan!=null)Check(scan.Wait(5000)&&scan.Result.Error==null,"held native catalog scan completes after release");}
  Check(!owner.Busy,"finished reads leave no bound-controller task occupation");
  var settings=Read(c,queue,"settingsGet");Check(settings.Error==null,"fresh settings snapshot is available");if(settings.Error==null){var saved=Read(c,queue,"settingsSave",P("stamp",D(settings.Value)["stamp"],"values",P("viewpoint-live.properties|mouse.invertY","true")));Check(saved.Error==null&&File.ReadAllText(Path.Combine(user,"viewpoint-live.properties")).Contains("mouse.invertY=true"),"immutable settings read retains its owner-bound write snapshot: "+Error(saved));}
  var shaders=Read(c,queue,"shadersGet");Check(shaders.Error==null,"fresh shader snapshot is available");if(shaders.Error==null){var saved=Read(c,queue,"shaderSelect",P("stamp",D(shaders.Value)["stamp"],"id","vivid"));Check(saved.Error==null&&File.ReadAllText(Path.Combine(user,"viewpoint-live.properties")).Contains("graphics.pack=vivid"),"immutable shader read retains its owner-bound write snapshot: "+Error(saved));}
  settings=Read(c,queue,"settingsGet");Put(Path.Combine(user,"viewpoint-live.properties"),File.ReadAllText(Path.Combine(user,"viewpoint-live.properties"))+"external.keep=unchanged\n");var stale=Read(c,queue,"settingsSave",P("stamp",D(settings.Value)["stamp"],"values",P("viewpoint-live.properties|mouse.invertY","false")));Check(stale.Error!=null&&File.ReadAllText(Path.Combine(user,"viewpoint-live.properties")).Contains("external.keep=unchanged"),"external changes still reject stale setting writes and preserve bytes");
  WriterIsolation(c,queue,user,id);
  CapturedOwner(c,queue,user);
  ScopedCancellation(c,queue);
  CleanupFailure(c,queue,id,profiles);
 }
 }
 // Break caught: a captured settings response grants a write receipt to whichever
 // configuration happens to be selected when its worker eventually runs.
 static void CapturedOwner(ModernController c,ModernDispatchQueue queue,string user){
  var captured=c.CaptureRequest("settingsGet",P());Read(c,queue,"launchConfigurationSelect",P("id","launcher"));var pending=queue.ExecuteSnapshot(()=>Attempt(captured)).ContinueWith(task=>(Reply)task.Result);Check(pending.Wait(3000)&&pending.Result.Error==null,"captured read finishes after a configuration selection change");if(!pending.IsCompleted||pending.Result.Error!=null)return;string stamp=(string)D(pending.Result.Value)["stamp"];
  var wrong=Read(c,queue,"settingsSave",P("stamp",stamp,"values",P("viewpoint-live.properties|mouse.invertY","true")));Check(wrong.Error!=null,"captured settings receipt does not authorize a different configuration owner");Read(c,queue,"launchConfigurationSelect",P("id","steam"));var right=Read(c,queue,"settingsSave",P("stamp",stamp,"values",P("viewpoint-live.properties|mouse.invertY","true")));Check(right.Error==null&&File.ReadAllText(Path.Combine(user,"viewpoint-live.properties")).Contains("mouse.invertY=true"),"captured settings receipt remains usable on its original owner");
 }
 // Break caught: cancelling one immutable read replaces/cancels the bound owner
 // token or keeps the read registered after the worker has observed cancellation.
 static void ScopedCancellation(ModernController c,ModernDispatchQueue queue){
  object cacheGate=typeof(DisplayCache).GetField("gate",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);string id=Guid.NewGuid().ToString("N");var owner=Owner(c);var token=Token(owner);Task<Reply> read=null;Monitor.Enter(cacheGate);
  try{read=Send(c,queue,"configurationRequest",P("id","steam","action","modCatalog","payload",P("refresh",true),"readId",id));Check(SpinWait.SpinUntil(()=>Readers(c)>0,3000)&&!read.IsCompleted,"cancellable scoped catalog read reaches its real native boundary");var cancelled=Read(c,queue,"configurationReadCancel",P("id","steam","readId",id));Check(cancelled.Error==null&&ReferenceEquals(token,Token(owner))&&!token.IsCancellationRequested,"read-specific cancellation preserves the bound owner's token");}
  finally{Monitor.Exit(cacheGate);if(read!=null){Check(read.Wait(5000)&&read.Result.Error is OperationCanceledException,"cancelled read settles as cancellation after the native boundary releases");Check(Readers(c)==0,"cancelled read releases its registration");}}
 }
 // Break caught: a snapshot read clears/replaces a real writer's occupation or
 // cancellation source, or makes the actual running mutation disappear in status.
 static void WriterIsolation(ModernController c,ModernDispatchQueue queue,string user,string id){
  var read=Read(c,queue,"settingsGet");var owner=Owner(c);var captured=c.CaptureRequest("settingsSave",P("stamp",D(read.Value)["stamp"],"values",P("viewpoint-live.properties|mouse.invertY","false")));using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   c.Progress=(value,text)=>{if(text.StartsWith("正在处理 保存 Viewpoint",StringComparison.Ordinal)){entered.Set();if(!release.Wait(10000))throw new TimeoutException("isolated writer hold");}};
   var writer=queue.ExecuteConcurrentMutation(()=>Attempt(captured)).ContinueWith(task=>(Reply)task.Result);
   try{Check(entered.Wait(3000)&&owner.Busy,"actual settings mutation owns its lane before native file write");var token=Token(owner);
    var status=Read(c,queue,"configurationRequest",P("id","steam","action","status","payload",P()));Check(status.Error==null&&(bool)((Dictionary<string,object>)((Dictionary<string,object>)D(status.Value)["data"])["task"])["active"],"read-only status exposes the actual writer instead of hiding it");
    var thumb=Read(c,queue,"serverThumb",P("id",id));Check(thumb.Error==null,"unrelated native thumbnail read remains available during a real writer");
    var competing=Read(c,queue,"settingsSave",P("stamp",D(read.Value)["stamp"],"values",P("viewpoint-live.properties|mouse.invertY","true")));Check(competing.Error!=null,"conflicting second writer cannot modify the same configuration concurrently");
    Check(owner.Busy&&!writer.IsCompleted&&ReferenceEquals(token,Token(owner))&&!token.IsCancellationRequested,"reads and rejected writers preserve the active writer's occupation and cancellation token");
   }finally{release.Set();Check(writer.Wait(5000)&&writer.Result.Error==null,"actual settings writer completes after the controlled boundary releases");c.Progress=null;}
   Check(!owner.Busy&&File.ReadAllText(Path.Combine(user,"viewpoint-live.properties")).Contains("mouse.invertY=false"),"writer releases occupation only after the actual file result");
   var completedStatus=Read(c,queue,"configurationRequest",P("id","steam","action","status","payload",P()));Check(completedStatus.Error==null&&!(bool)((Dictionary<string,object>)((Dictionary<string,object>)D(completedStatus.Value)["data"])["task"])["active"],"finished native mutation is idle on the next status read");
  }
 }
 // Exception injection is limited to post-operation cache cleanup; the server
 // deletion itself runs normally against this test's isolated profile document.
 static void CleanupFailure(ModernController c,ModernDispatchQueue queue,string id,ServerProfiles profiles){
  var engine=(Engine)typeof(ModernController).GetField("engine",Private).GetValue(c);string original=engine.ModelsSource;c.Progress=(value,text)=>{if(value==100)engine.ModelsSource="invalid\0cache-path";};
  try{var deleted=Read(c,queue,"serverDelete",P("id",id,"stamp",D(profiles.List())["stamp"]));Check(deleted.Error is ArgumentException,"controlled post-write cache invalidation failure reaches the caller");Check(!((IEnumerable)D(profiles.List())["servers"]).Cast<object>().Any(),"isolated server deletion committed before cache-cleanup failure");Check(!c.Busy,"cache-cleanup exceptions cannot retain native task occupation");}
  finally{engine.ModelsSource=original;c.Progress=null;}
 }
 public static int Main(string[] args){try{Run(Path.GetFullPath(args[0]));Console.WriteLine("NativeReadConcurrency "+checks+" checks; "+failures+" failures; actual native code with isolated files, no GUI/game/Steam launch");return failures==0?0:1;}catch(Exception error){Console.WriteLine(error);return 1;}}
}}
