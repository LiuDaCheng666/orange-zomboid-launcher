using System;using System.IO;using System.Linq;using System.Text;using System.Threading;using System.Threading.Tasks;using System.Reflection;using System.Collections;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace PZ3D {static class ModDetailNative {
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2097152};static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;static int checks,failures;
 static void Check(bool value,string label){checks++;if(!value)failures++;Console.WriteLine((value?"PASS ":"FAIL ")+label);}
 static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
 static Dictionary<string,object> P(params object[] values){var p=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)p.Add((string)values[i],values[i+1]);return p;}
 static Dictionary<string,object> D(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
 static Dictionary<string,object>[] Rows(object value){return ((IEnumerable)D(value)["mods"]).Cast<Dictionary<string,object>>().ToArray();}
 sealed class Reply {internal object Value;internal Exception Error;}
 static Reply Attempt(Func<object> work){try{return new Reply{Value=work()};}catch(Exception e){return new Reply{Error=e};}}
 static Task<Reply> Send(ModernController c,ModernDispatchQueue queue,string action,Dictionary<string,object> payload){var captured=c.CaptureRequest(action,payload);return queue.ExecuteSnapshot(()=>Attempt(captured)).ContinueWith(task=>(Reply)task.Result);}
 static Reply Read(ModernController c,ModernDispatchQueue queue,string action,Dictionary<string,object> payload){var pending=Send(c,queue,action,payload);if(!pending.Wait(5000))throw new TimeoutException(action);return pending.Result;}
 static object Success(Reply reply){if(reply.Error!=null)throw reply.Error;return reply.Value;}
 static string Token(Dictionary<string,object>[] rows,string id,string source="user"){return (string)rows.Single(r=>(string)r["id"]==id&&(string)r["source"]==source)["coverToken"];}
 static Dictionary<string,object> Detail(ModernController c,ModernDispatchQueue queue,string token){return D(Success(Read(c,queue,"modDetail",P("token",token))));}
 static void Refuses(Action work,string label){bool rejected=false;try{work();}catch{rejected=true;}Check(rejected,label);}
 static string[] Files(string root){return Directory.GetFiles(root,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p+"|"+Convert.ToBase64String(File.ReadAllBytes(p))).ToArray();}
 static ModernController Owner(ModernController c){return (ModernController)typeof(ModernController).GetMethod("ConfigurationController",Private).Invoke(c,null);}
 static Engine EngineOf(ModernController c){return (Engine)typeof(ModernController).GetField("engine",Private).GetValue(c);}
 static int Readers(ModernController c){lock(typeof(ModernController).GetField("gate",Private).GetValue(c))return ((IDictionary)typeof(ModernController).GetField("configurationReaders",Private).GetValue(c)).Count;}
 static void Game(string game,string user){Put(Path.Combine(game,"ProjectZomboid64.exe"),"isolated inert marker; never execute");Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx2g\"]}");Put(Path.Combine(game,"game-version.txt"),"42.21.0");Put(Path.Combine(user,"mods/default.txt"),"mods\n{\n}\nmaps\n{\n}\n");Put(Path.Combine(user,"viewpoint-live.properties"),"mouse.invertY=false\n");}
 static void Run(string root){
  string game=Path.Combine(root,"game"),user=Path.Combine(root,"用户"),data=Path.Combine(root,"data");Game(game,user);
  string longText="完整介绍开始："+new string('中',2400)+"<LINE>Unicode tail Ω & <literal>";
  string longInfo=Path.Combine(user,"mods/Long/42/mod.info");Put(longInfo,"id=Long\nname=Long author title\ndescription="+longText+"\n");
  Put(Path.Combine(user,"mods/Inherited/common/mod.info"),"id=Inherited\ndescription=共同第一段\ndescription=共同第二段\n");Put(Path.Combine(user,"mods/Inherited/42/mod.info"),"id=Inherited\nname=Active name\n");
  Put(Path.Combine(user,"mods/Overlay/common/mod.info"),"id=Overlay\ndescription=Common is replaced\n");Put(Path.Combine(user,"mods/Overlay/42/mod.info"),"id=Overlay\ndescription=Active first\ndescription=\ndescription=Active last\n");
  Put(Path.Combine(user,"mods/CommonOnly/common/mod.info"),"id=CommonOnly\ndescription=Common-only complete\n");
  Put(Path.Combine(user,"mods/Same/42/mod.info"),"id=Same\ndescription=User source text\n");
  Put(Path.Combine(user,"mods/Empty/42/mod.info"),"id=Empty\nname=No description\n");
  using(var c=new ModernController(game,user,data,true)){
   var queue=new ModernDispatchQueue();Success(Read(c,queue,"launchConfigurationSelect",P("id","steam")));var owner=Owner(c);var engine=EngineOf(owner);
   Put(Path.Combine(engine.Workshop,"123/mods/Same/42/mod.info"),"id=Same\ndescription=Workshop source text\n");
   var rows=Rows(Success(Read(c,queue,"modCatalog",P())));string token=Token(rows,"Long");
   Check(((string)rows.Single(r=>(string)r["id"]=="Long")["description"]).Length==512,"catalog remains a bounded preview instead of retaining every full body");
   var first=Read(c,queue,"modDetail",P("token",token));Check(first.Error==null,"production modDetail route reads a selected source: "+(first.Error==null?"":first.Error.Message));if(first.Error!=null)return;
   string[] before=Files(root);var detail=D(first.Value);
   Check((string)detail["token"]==token&&(string)detail["id"]=="Long"&&(string)detail["description"]==longText,"long Unicode description preserves its full tail and author markup");
   Check((string)Detail(c,queue,Token(rows,"Inherited"))["description"]=="共同第一段\n共同第二段","common description inherits with every repeated paragraph");
   Check((string)Detail(c,queue,Token(rows,"Overlay"))["description"]=="Active first\n\nActive last","active description replaces common and preserves empty paragraph");
   Check((string)Detail(c,queue,Token(rows,"CommonOnly"))["description"]=="Common-only complete","common-only B42 layout remains readable without a version directory");
   Check((string)Detail(c,queue,Token(rows,"Empty"))["description"]=="","missing author description stays honestly empty");
   string userToken=Token(rows,"Same"),workshopToken=Token(rows,"Same","workshop");
   Check(userToken!=workshopToken&&(string)Detail(c,queue,userToken)["description"]=="User source text"&&(string)Detail(c,queue,workshopToken)["description"]=="Workshop source text","same ID sources retain distinct opaque tokens and correct text");
   var scoped=P("id","steam","action","modDetail","payload",P("token",token),"readId",new string('1',32));
   var scopedValue=D(Success(Read(c,queue,"configurationRequest",scoped)));Check((string)scopedValue["configurationId"]=="steam"&&(string)((Dictionary<string,object>)scopedValue["data"])["description"]==longText,"owned configuration envelope keeps the same full source and owner");
   Check(ModernHostPolicy.IsNativeRead("modDetail",P("token",token))&&ModernHostPolicy.IsNativeRead("configurationRequest",scoped)&&!ModernController.ConfigurationMutation("configurationRequest",scoped),"raw and scoped details are read-only actions");
   foreach(var body in new[]{P("token",token),P("id","steam","action","modDetail","payload",P("token",token),"readId",new string('2',32))}){string action=body.ContainsKey("action")?"configurationRequest":"modDetail";Check(ModernHostPolicy.Validate("https://orange.local/index.html",Json.Serialize(new{id="detail",action=action,payload=body}))!=null,"host accepts strict "+action+" token contract");}
   foreach(var bad in new[]{P(),P("token","../mod.info"),P("token",new string('g',64)),P("token",new string('a',65)),P("token",5),P("token",token,"path",longInfo)})Refuses(()=>ModernHostPolicy.Validate("https://orange.local/index.html",Json.Serialize(new{id="invalid",action="modDetail",payload=bad})),"detail host rejects malformed token or additional path");
   Refuses(()=>Detail(c,queue,new string('0',64)),"unknown token cannot select a filesystem path");
   using(var locked=new FileStream(Path.Combine(user,"mods/Empty/42/mod.info"),FileMode.Open,FileAccess.Read,FileShare.None))Check((string)Detail(c,queue,token)["description"]==longText,"warm detail reads do not need unrelated source files");
   using(var locked=new FileStream(longInfo,FileMode.Open,FileAccess.Read,FileShare.None))Refuses(()=>Detail(c,queue,token),"full description is read on demand instead of a resident full-text cache");
   Check(before.SequenceEqual(Files(root)),"all successful and rejected detail reads preserve every fixture file byte");
   Cancellation(c,queue,token);
   WriterCompatibility(c,queue,token,longText,user);
   string largeInfo=Path.Combine(user,"mods/Large/42/mod.info"),largeText=new string('x',300000)+" complete-large-tail";
   Put(largeInfo,"id=Large\ndescription="+largeText+"\n");var largeRows=Rows(SaveProfiles.Catalog(engine));string largeToken=Token(largeRows,"Large");
   Check((string)D(SaveProfiles.Detail(engine,largeToken))["description"]==largeText,"direct local-detail reader preserves a large body without truncation");
   var cancellableCatalog=D(Success(Read(c,queue,"modCatalog",P("refresh",true))));Console.WriteLine("BOUNDARY existing cancellable catalog has Large="+Rows(cancellableCatalog).Any(r=>(string)r["id"]=="Large")+"; warnings="+Json.Serialize(cancellableCatalog["warnings"]));
   Put(largeInfo,"id=Large\ndescription="+new string('<',400000)+"\n");Rows(SaveProfiles.Catalog(engine));var oversized=Attempt(()=>SaveProfiles.Detail(engine,largeToken));
   Check(oversized.Error is IOException&&oversized.Error.Message.Contains("未截断"),"escaped payload beyond host capacity reports an explicit failure instead of partial text");
   string game2=Path.Combine(root,"other/game"),user2=Path.Combine(root,"other/user");Game(game2,user2);Put(Path.Combine(user2,"mods/Long/42/mod.info"),"id=Long\ndescription=Different owner text\n");
   using(var other=new ModernController(game2,user2,Path.Combine(root,"other/data"),true)){Success(Read(other,queue,"launchConfigurationSelect",P("id","steam")));Success(Read(other,queue,"modCatalog",P()));Refuses(()=>Detail(other,queue,token),"another game/user configuration cannot redeem the original source token");}
   Put(longInfo,"id=ChangedIdentity\ndescription=Changed source\n");Refuses(()=>Detail(c,queue,token),"source identity replacement cannot return text under an old ID");
  }
 }
 static void WriterCompatibility(ModernController c,ModernDispatchQueue queue,string token,string text,string user){
  string stamp=(string)D(Success(Read(c,queue,"settingsGet",P())))["stamp"];var owner=Owner(c);
  using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   c.Progress=(value,message)=>{if(message.StartsWith("正在处理 保存 Viewpoint",StringComparison.Ordinal)){entered.Set();if(!release.Wait(10000))throw new TimeoutException("isolated writer boundary");}};
   var work=c.CaptureRequest("settingsSave",P("stamp",stamp,"values",P("viewpoint-live.properties|mouse.invertY","true")));
   var writer=queue.ExecuteConcurrentMutation(()=>Attempt(work)).ContinueWith(task=>(Reply)task.Result);
   try{Check(entered.Wait(3000)&&owner.Busy,"actual settings writer owns its resource before the detail query");var writerToken=typeof(ModernController).GetField("cancel",Private).GetValue(owner);
    Check((string)Detail(c,queue,token)["description"]==text,"detail remains available while an independent actual writer is held");
    Check(owner.Busy&&!writer.IsCompleted&&ReferenceEquals(writerToken,typeof(ModernController).GetField("cancel",Private).GetValue(owner)),"detail read cannot clear or replace the real writer's occupation");
   }finally{release.Set();Check(writer.Wait(5000)&&writer.Result.Error==null,"independent writer commits and releases normally after detail read");c.Progress=null;}
   Check(!c.Busy&&File.ReadAllText(Path.Combine(user,"viewpoint-live.properties")).Contains("mouse.invertY=true"),"actual independent writer's file result is preserved");
  }
 }
 static void Cancellation(ModernController c,ModernDispatchQueue queue,string token){
  object cacheGate=typeof(DisplayCache).GetField("gate",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var owner=Owner(c);var writerToken=typeof(ModernController).GetField("cancel",Private).GetValue(owner);string readId=Guid.NewGuid().ToString("N");Task<Reply> pending=null;
  Monitor.Enter(cacheGate);try{
   pending=Send(c,queue,"configurationRequest",P("id","steam","action","modDetail","payload",P("token",token),"readId",readId));
   Check(SpinWait.SpinUntil(()=>Readers(c)>0,3000)&&!pending.IsCompleted&&!owner.Busy&&!c.Busy,"held actual detail read never occupies a bound writer or global busy state");
   Check(Read(c,queue,"memoryGet",P()).Error==null,"independent memory query completes while a detail read is held");
   Success(Read(c,queue,"configurationReadCancel",P("id","steam","readId",readId)));
   Check(ReferenceEquals(writerToken,typeof(ModernController).GetField("cancel",Private).GetValue(owner))&&!((CancellationTokenSource)writerToken).IsCancellationRequested,"detail cancellation does not replace or cancel its writer owner");
  }finally{Monitor.Exit(cacheGate);if(pending!=null){Check(pending.Wait(5000)&&pending.Result.Error is OperationCanceledException,"cancelled detail settles after its native cache boundary releases");Check(Readers(c)==0&&!c.Busy,"cancelled detail releases reader registration and page occupation");}}
 }
 public static int Main(string[] args){try{Run(Path.GetFullPath(args[0]));Console.WriteLine("ModDetailNative "+checks+" checks; "+failures+" failures; actual native production code with isolated files, no UI/game/publish");return failures==0?0:1;}catch(Exception error){Console.WriteLine(error);return 1;}}
}}
