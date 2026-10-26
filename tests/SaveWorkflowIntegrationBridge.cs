using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
namespace PZ3D {
 // A background-only integration host. No form, game, Steam, or shell is started.
 // Only the folder picker and Workshop transport have fixture implementations.
 static class SaveWorkflowIntegrationBridge {
  static readonly object OutputGate=new object();
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=4194304};
  static readonly ModernDispatchQueue Dispatcher=new ModernDispatchQueue();
  static readonly string[] SaveNames={"CancelFixture","DeleteFixture","SnapshotFixture"};
  static string Root,Owner,Game,User,Data,Destination,Scenario,OriginalSaves;
  static ModernController Controller;static Engine Engine;
  [DllImport("kernel32.dll")]static extern uint SetErrorMode(uint mode);
  static int Downloads,PickerCalls;static bool CancelPicker;
  static readonly ManualResetEvent NetworkStarted=new ManualResetEvent(false),NetworkRelease=new ManualResetEvent(false);static Task<object> HeldNetwork;
  static void Send(object value){lock(OutputGate){Console.WriteLine(Json.Serialize(value));Console.Out.Flush();}}
  static string Safe(string path){string full=Path.GetFullPath(path);if(!full.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Fixture path escaped its unique root.");if(!File.Exists(Path.Combine(Root,".save-workflow-owner"))||File.ReadAllText(Path.Combine(Root,".save-workflow-owner"))!=Owner)throw new IOException("Fixture owner marker changed.");LauncherMaintenance.NoReparse(full);return full;}
  static void Put(string path,string value){path=Safe(path);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value,new UTF8Encoding(false));}
  static string Format(string[] mods){return "VERSION = 1,\r\nmods\r\n{\r\n"+string.Concat(mods.Select(id=>"    mod = "+id+",\r\n"))+"}\r\nmaps\r\n{\r\n    map = Muldraugh, KY,\r\n}\r\n";}
  static Dictionary<string,object> D(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
  static string Save(string name){if(!SaveNames.Contains(name))throw new IOException("Unknown fixture save.");return Safe(Path.Combine(User,"Saves","Sandbox",name));}
  static string HashTree(string path){Safe(path);if(!Directory.Exists(path))return "absent";return DlssInstaller.Hash(Encoding.UTF8.GetBytes(string.Join("\n",Directory.GetFiles(path,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>{Safe(p);return p.Substring(path.Length)+"|"+DlssInstaller.Hash(File.ReadAllBytes(p));}))));}
  static Dictionary<ulong,string> Download(Engine e,ulong[] ids,Action<int,string> progress,CancellationToken token,bool force){
   if(Scenario!="missing")throw new IOException("Unexpected Workshop request in fixture.");
   var result=new Dictionary<ulong,string>();foreach(ulong id in ids){token.ThrowIfCancellationRequested();if(id!=3809306528)throw new IOException("Fixture downloader refuses every other item.");Downloads++;progress(10,"Fixture transport: missing mod bytes (no Steam network)");string item=Safe(Path.Combine(e.Workshop,id.ToString()));Put(Path.Combine(item,"mods","Viewpoint","42","mod.info"),"id=Viewpoint\nname=Fixture downloaded Viewpoint\n");Put(Path.Combine(item,"mods","Viewpoint","common","media","downloaded.txt"),"downloaded fixture bytes "+Owner);Thread.Sleep(180);progress(100,"Fixture transport completed; native preparation continues");result[id]=item;}return result;
  }
  static object Inspect(){
   var snapshot=new SaveEnvironments(Engine).Bound("Sandbox/SnapshotFixture");
   return new{root=Root,owner=Owner,scenario=Scenario,hostAlive=true,busy=Controller.Busy,pickerCalls=PickerCalls,downloads=Downloads,
    cancelHash=HashTree(Save("CancelFixture")),deleteHash=HashTree(Save("DeleteFixture")),snapshotSaveHash=HashTree(Save("SnapshotFixture")),originalSaves=OriginalSaves,
    destinationExists=Directory.Exists(Destination),snapshot=snapshot==null?null:new{id=snapshot.Id,rootPath=snapshot.RootPath,gamePath=snapshot.GamePath,modIds=snapshot.ModIds,defaultModIds=snapshot.DefaultModIds,copiedGame=File.Exists(Path.Combine(snapshot.GamePath,"media","source.bin")),copiedMods=snapshot.ModRoots.Select(m=>new{ids=m.ModIds,path=m.CopiedRoot,exists=Directory.Exists(m.CopiedRoot),files=Directory.GetFiles(m.CopiedRoot,"*",SearchOption.AllDirectories).Select(p=>p.Substring(m.CopiedRoot.Length+1)).ToArray()}).ToArray()}};
  }
  static async Task<object> Execute(string action,Dictionary<string,object> payload){
   Safe(Path.Combine(Root,".save-workflow-owner"));
   if(action=="fixtureInfo")return new{root=Root,owner=Owner,scenario=Scenario,game=Game,user=User,data=Data,destination=Destination,saveIds=SaveNames.Select(n=>"Sandbox/"+n).ToArray(),snapshotJavaProbeSkipped=true,steamAndProcessDiscoveryDisabled=true,deleteBoundary="production SaveDeletion and real Directory.Delete; no injected deletion delegate",workshopTransport="controlled fixture; no real Steam download",initial=D(Inspect())};
   if(action=="fixtureInspect")return Inspect();
   if(action=="fixtureCancelPicker"){CancelPicker=true;return new{ok=true};}
   if(action=="fixtureNetworkBegin"){if(HeldNetwork!=null)throw new IOException("Fixture network hold already used.");HeldNetwork=Dispatcher.ExecuteSnapshot(()=>Controller.Execute("steamConnection",new Dictionary<string,object>()));if(!NetworkStarted.WaitOne(5000))throw new IOException("Fixture network hold did not start.");return new{started=true,busy=Controller.Busy};}
   if(action=="fixtureNetworkRelease"){NetworkRelease.Set();if(HeldNetwork!=null)await HeldNetwork;return new{busy=Controller.Busy};}
   if(action=="coreUpdateStatus")return new{protocol=1,revision=1,state="Idle",currentVersion="1.2.33",latestVersion="1.2.33"};
   string[] allowed={"status","playProfiles","playtimeGet","memoryGet","modProfiles","modCatalog","savesList","saveThumb","taskGet","versionList","versionInstallState","backupSettingsGet","worldEditRecovery","environmentList","environmentPreview","environmentPreviewCancel","environmentCheck","environmentBind","environmentUnbind","environmentChooseCreate","saveDeletePreview","saveDelete","cancel"};
   if(!allowed.Contains(action))throw new IOException("Integration fixture rejects action: "+action);
   if(action=="saveDeletePreview"||action=="saveDelete"){
    if(Scenario!="delete")throw new IOException("Delete API is restricted to the delete fixture.");
    object raw;if(!payload.TryGetValue("id",out raw)||!(raw is string)||!new[]{"Sandbox/CancelFixture","Sandbox/DeleteFixture"}.Contains((string)raw))throw new IOException("Delete API refuses this save.");
    string path=Safe(Path.Combine(User,"Saves",((string)raw).Replace('/',Path.DirectorySeparatorChar)));if(!File.Exists(Path.Combine(path,".save-workflow-owned"))||File.ReadAllText(Path.Combine(path,".save-workflow-owned"))!=Owner)throw new IOException("Delete target is not an owned fixture.");
   }
   if(action=="environmentChooseCreate"){
    var selection=LibraryFolderSelection.Begin(action,payload);if(selection==null)throw new IOException("Production folder mapping was not found.");PickerCalls++;
    if(CancelPicker){CancelPicker=false;return new{cancelled=true};}
    string chosenParent=Safe(Path.Combine(Root,"ChosenSnapshots"));Directory.CreateDirectory(chosenParent);payload=selection.Select(chosenParent);action=selection.Action;Destination=Safe((string)payload["destination"]);
   }
   var request=Json.Serialize(new{id="integration-native",action=action,payload=payload});
   if(action!="environmentCreate")ModernHostPolicy.Validate("https://orange.local/index.html",request);
   if(action=="taskGet"||action=="playtimeGet"||action=="environmentPreview"||action=="environmentPreviewCancel")return await Dispatcher.ExecuteSnapshot(()=>Controller.Execute(action,payload));
   if(action=="cancel"||ModernController.NativeReads.Contains(action))return await Dispatcher.Execute(()=>Controller.Execute(action,payload),action=="cancel");
   return await Dispatcher.ExecuteMutation(()=>Controller.Execute(action,payload));
  }
  static async Task Handle(string line){object id=null;try{var request=Json.Deserialize<Dictionary<string,object>>(line);id=request["id"];string action=(string)request["action"];object raw;var payload=request.TryGetValue("payload",out raw)&&raw!=null?(Dictionary<string,object>)raw:new Dictionary<string,object>();object result=await Execute(action,payload);Send(new{id=id,ok=true,data=result});}catch(Exception ex){Send(new{id=id,ok=false,error=ex.Message,type=ex.GetType().FullName});}}
  public static int Main(string[] args){try{
   SetErrorMode(0x0001|0x0002|0x8000);Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
   if(args.Length!=2||!new[]{"delete","snapshot","missing"}.Contains(args[1]))throw new IOException("Expected unique fixture parent and scenario.");
   Scenario=args[1];string parent=Path.GetFullPath(args[0]);if(!Path.IsPathRooted(parent)||!Directory.Exists(parent))throw new IOException("Fixture parent must already exist.");Owner=Guid.NewGuid().ToString("N");Root=Path.GetFullPath(Path.Combine(parent,"save-workflow-"+Owner));if(!Root.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Directory.Exists(Root))throw new IOException("Invalid unique fixture root.");Directory.CreateDirectory(Root);File.WriteAllText(Path.Combine(Root,".save-workflow-owner"),Owner,new UTF8Encoding(false));
   Game=Safe(Path.Combine(Root,"steamapps","common","FixtureGame"));User=Safe(Path.Combine(Root,"User"));Data=Safe(Path.Combine(Root,"Data"));Destination=Safe(Path.Combine(Root,"SnapshotDestination"));
   Put(Path.Combine(Game,"ProjectZomboid64.exe"),"fixture text: must never execute");Put(Path.Combine(Game,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");Put(Path.Combine(Game,"game-version.txt"),"42.21.0");Put(Path.Combine(Game,"version.txt"),"42.21.0");Put(Path.Combine(Game,"media","source.bin"),"original game fixture bytes "+Owner);
   string[] ids=Scenario=="missing"?new[]{"Viewpoint","Present"}:new[]{"Present"};Put(Path.Combine(User,"mods","Present","42","mod.info"),"id=Present\nname=Present fixture\n"+(Scenario=="missing"?"require=Viewpoint\n":""));Put(Path.Combine(User,"mods","Present","common","media","source.bin"),"original mod fixture bytes "+Owner);Put(Path.Combine(User,"mods","default.txt"),Format(ids));
   foreach(string name in SaveNames){Put(Path.Combine(Save(name),"mods.txt"),Format(ids));Put(Path.Combine(Save(name),"map.bin"),"original live save fixture "+name+" "+Owner);Put(Path.Combine(Save(name),".save-workflow-owned"),Owner);}
   Controller=new ModernController(Game,User,Data,true,Download,(e,a,p,progress,token,batch)=>{if(a=="connection"){NetworkStarted.Set();if(WaitHandle.WaitAny(new[]{NetworkRelease,token.WaitHandle},15000)!=0)throw new OperationCanceledException("Fixture network hold ended without release.");return new{connected=false,message="Controlled read hold; no Steam query"};}if(a!="download")throw new IOException("Integration fixture forbids Steam/network operation: "+a);object raw;ulong id;if(!p.TryGetValue("id",out raw)||!(raw is string)||!ulong.TryParse((string)raw,out id))throw new IOException("Fixture download ID must be exact text.");Download(e,new[]{id},progress,token,true);return new{id=id.ToString(),status="completed",message="Controlled fixture transport; no Steam download"};});
   Engine=(Engine)typeof(ModernController).GetField("engine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(Controller);
   Controller.Progress=(value,text)=>Send(new{@event="progress",data=new{value=value,text=text}});Controller.Event=(name,value)=>Send(new{@event=name,data=value});OriginalSaves=HashTree(Path.Combine(User,"Saves"));
   var tasks=new List<Task>();string line;while((line=Console.ReadLine())!=null){tasks.RemoveAll(t=>t.IsCompleted);tasks.Add(Handle(line));}Task.WaitAll(tasks.ToArray());Controller.Dispose();return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 }
}
