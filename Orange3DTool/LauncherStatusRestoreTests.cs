using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class LauncherStatusRestoreTests {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
  static Dictionary<string,object> D(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
  sealed class Fixture {
   internal readonly string Root,Game,User,Data,Config,Mods;
   internal Fixture(){Root=Path.Combine(LauncherTestPaths.Root,"status-restore-"+Guid.NewGuid().ToString("N"));Game=Path.Combine(Root,"steamapps","common","Game");User=Path.Combine(Root,"user");Data=Path.Combine(Root,"data");Config=Path.Combine(Game,"ProjectZomboid64.json");Mods=Path.Combine(User,"mods","default.txt");Put(Config,"{\"vmArgs\":[\"-Xmx6g\",\"-Dfixture=keep\"]}");Put(Path.Combine(Game,"ProjectZomboid64.exe"),"fixture only");Put(Mods,"VERSION = 1,\nmods\n{\n mod = OtherMod,\n}\nmaps\n{\n map = OtherMap,\n}\n");}
   internal ModernController Controller(){return new ModernController(Game,User,Data,true);}
  }
  static void Put(string file,string text){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,text,new UTF8Encoding(false));}
  static Dictionary<string,object> VersionStatus(ModernController c){var status=D(c.Execute("status",null));Check(status.ContainsKey("gameVersion"),"serialized status exposes gameVersion");return (Dictionary<string,object>)status["gameVersion"];}
  static void StatusVersion(){var f=new Fixture();Put(Path.Combine(f.Game,"version.txt"),"42.21.0 4a0e9546ec\nrevision=4a0e9546ec pzbullet=1.0.0.28\n");byte[] config=File.ReadAllBytes(f.Config),mods=File.ReadAllBytes(f.Mods);using(var c=f.Controller()){var v=VersionStatus(c);Check((string)v["value"]=="42.21.0"&&(string)v["state"]=="known"&&(string)v["source"]=="version.txt"&&(string)v["warning"]=="","root version returns only numeric game version");}Check(config.SequenceEqual(File.ReadAllBytes(f.Config))&&mods.SequenceEqual(File.ReadAllBytes(f.Mods))&&!Directory.Exists(f.Data),"status metadata preserves launch/default bytes and creates no preferences");}
  static void Unknown(Dictionary<string,object> v,string name){Check((string)v["value"]==""&&(string)v["state"]=="unknown"&&(string)v["source"]=="version.txt"&&!string.IsNullOrWhiteSpace((string)v["warning"]),name);}
  static void VersionEdges(){
   var f=new Fixture();string file=Path.Combine(f.Game,"version.txt");
   Put(Path.Combine(f.Game,"jre64","release"),"JAVA_VERSION=17.0.12");Put(Path.Combine(f.Game,"logs","version.txt"),"99.88.77");
   using(var c=f.Controller()){
    Unknown(VersionStatus(c),"missing root does not discover unrelated runtime/log versions");
    foreach(string text in new[]{"","\n42.21.0","revision=4a0e9546ec pzbullet=1.0.0.28\n42.21.0","42.21.0oops","42.21.0 4a0e9546ec pzbullet=1.0.0.28","42.999999999999999999.0","42.21.0.1.2"}){Put(file,text);Unknown(VersionStatus(c),"empty or malformed first line rejected: "+text.Replace('\n','|'));}
    Put(file,"42.21.0\n"+new string('x',4097));Unknown(VersionStatus(c),"oversized root version rejected");
    File.WriteAllBytes(file,new byte[]{0xff,0xfe,0xff});Unknown(VersionStatus(c),"invalid UTF8 rejected");
    Put(file,"42.21.0");using(var locked=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Unknown(VersionStatus(c),"unreadable root version becomes unknown without crashing status");
    Put(file,"42.21.0\n"+new string('x',4088));Check((string)VersionStatus(c)["value"]=="42.21.0","4096 byte metadata boundary accepted");
    Put(file,"\uFEFF42.21.0 4a0e9546ec\r\npzbullet=1.0.0.28\n");using(var writer=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.ReadWrite))Check((string)VersionStatus(c)["value"]=="42.21.0","BOM CRLF and shared read tolerate existing writer");
    string other=Path.Combine(f.Root,"other-game");Put(Path.Combine(other,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");Put(Path.Combine(other,"ProjectZomboid64.exe"),"fixture");Put(Path.Combine(other,"version.txt"),"42.22.1\n");c.Execute("setGame",new Dictionary<string,object>{{"path",other}});Check((string)VersionStatus(c)["value"]=="42.22.1","status reads newly selected root without stale version cache");
   }
  }
  static void HexRevision(){var f=new Fixture();using(var c=f.Controller()){foreach(string revision in new[]{"a",new string('a',64)}){Put(Path.Combine(f.Game,"version.txt"),"42.21.0 "+revision+"\n");Check((string)VersionStatus(c)["value"]=="42.21.0","optional hex revision length does not alter numeric display version");}}}
  static void Reparse(){var f=new Fixture();Put(Path.Combine(f.Game,"version.txt"),"42.21.0");string link=Path.Combine(f.Root,"linked-game");var start=new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+f.Game+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var p=System.Diagnostics.Process.Start(start)){string output=p.StandardOutput.ReadToEnd(),error=p.StandardError.ReadToEnd();if(!p.WaitForExit(5000)||p.ExitCode!=0)throw new Exception("Fixture junction creation failed: "+output+error);Check(true,"synthetic root junction created");}Unknown(D(LauncherStatusMetadata.GameVersion(link)),"root reparse path rejected without reading target metadata");Check(File.ReadAllText(Path.Combine(f.Game,"version.txt"))=="42.21.0","reparse refusal preserves target");}
  static readonly string[] Managed={"ZombieBuddy","Viewpoint","PZVoxelStudioViewpoint","LaoYuTranslationJava","ShootToThrillMultiplayer","ProjectViewpointShelves","ViewpointSurface","ViewpointDoor","ViewpointTileFix","ViewpointForaging","FishingView","ViewpointTrueWeathers","ProjectViewpointNearVegetation"};
  static Fixture Configured(){var f=new Fixture();Put(f.Config,"{\"vmArgs\":[\"-Xmx6g\",\"-Dfixture=keep\",\"-agentlib:zbNative\",\"-javaagent:OtherZombieBuddy.jar\"],\"windows\":{\"10\":{\"vmArgs\":[\"-Xmx5g\",\"-Dbranch=keep\",\"-javaagent:ZombieBuddy.jar\"]}}}");Put(f.Mods,"VERSION = 1,\nmods\n{\n mod = OtherMod,\n"+string.Join("",Managed.Select(id=>" mod = "+id+",\n"))+"}\nmaps\n{\n map = OtherMap,\n}\n");foreach(string loader in new[]{"ZombieBuddy.jar","zbNative.dll","ZombieBuddy.jar.new"})Put(Path.Combine(f.Game,loader),"managed fixture "+loader);
   var e=new Engine(f.Game,f.User,f.Data,true);foreach(var a in ViewpointAddons.All)Put(Path.Combine(e.Workshop,a.Item.ToString(),"mods",a.Id,"42","mod.info"),"id="+a.Id+"\nmodversion=fixture\n");
   Put(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"),"download retained");Put(Path.Combine(e.BuddyLib,"zbNative.dll"),"download retained");Put(Path.Combine(e.Workshop,"3809306528","mods","Viewpoint","mod.info"),"id=Viewpoint\n");Put(Path.Combine(e.Workshop,"3810302175","mods","PZVoxelStudioViewpoint","mod.info"),"id=PZVoxelStudioViewpoint\n");Put(Path.Combine(e.Workshop,"3813032735","mods","ShootToThrillMultiplayer","common","mod.info"),"id=ShootToThrillMultiplayer\n");Put(Path.Combine(e.Workshop,"3810637496","mods","LaoYuTranslationJava","common","mod.info"),"id=LaoYuTranslationJava\n");
   foreach(string relative in new[]{"viewpoint-live.properties","options.ini",".zombie_buddy/mod_approvals.json","Saves/Survivor/fixture/map_p.bin","Saves/Survivor/fixture/mods.txt","Server/fixture.ini","mods/OtherMod/42/mod.info","viewpoint-shaderpacks/player.zip"})Put(Path.Combine(f.User,relative),"user bytes retained: "+relative);
   return f;
  }
  static Dictionary<string,byte[]> Snapshot(string root){return Directory.GetFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);}
  static bool Unchanged(Dictionary<string,byte[]> files){return files.All(p=>File.Exists(p.Key)&&p.Value.SequenceEqual(File.ReadAllBytes(p.Key)));}
  static bool ScopeUnchanged(Fixture f,Dictionary<string,byte[]> files){return Unchanged(files)&&Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).Count(p=>!p.StartsWith(f.Data+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))==files.Count;}
  static Dictionary<string,object> RestorePayload(bool confirmed,bool roofs){return new Dictionary<string,object>{{"component","all"},{"operation","uninstall"},{"confirmed",confirmed},{"roofRemovalAcknowledged",roofs}};}
  static void Refuses(ModernController c,Dictionary<string,object> payload,string expected,string name){string failure="";try{c.Execute("componentsAction",payload);}catch(Exception ex){failure=ex.Message;}Check(failure.Contains(expected),name);}
  static LauncherTaskRecord Latest(ModernController c){return Json.Deserialize<LauncherTaskStore>(Json.Serialize(c.Execute("taskGet",null))).history.First();}
  static void Restore(){var f=Configured();var before=Snapshot(f.Root);var e=new Engine(f.Game,f.User,f.Data,true);var downloads=Snapshot(e.Workshop);var preserved=Snapshot(f.User).Where(p=>p.Key!=f.Mods).ToDictionary(p=>p.Key,p=>p.Value);
   using(var c=f.Controller()){
    Refuses(c,RestorePayload(false,true),"确认","restore-all refuses missing explicit confirmation");Check(ScopeUnchanged(f,before),"no-confirmation refusal writes no game/download/user bytes");
    Refuses(c,RestorePayload(true,false),"屋顶","restore-all refuses roof removal without separate acknowledgement");Check(ScopeUnchanged(f,before),"roof refusal writes no game/download/user bytes");
    var phases=new List<int>();c.Progress=(value,text)=>phases.Add(value);string result=(string)D(c.Execute("componentsAction",RestorePayload(true,true)))["message"];Check(!result.Contains("[失败]"),"confirmed restore-all reports all managed steps completed");
    string mods=File.ReadAllText(f.Mods);Check(Managed.All(id=>!mods.Contains("mod = "+id+","))&&mods.Contains("mod = OtherMod,")&&mods.Contains("map = OtherMap,"),"restore-all removes base models Chinese shoot and every available addon while preserving ordinary mod/map");
    Check(new[]{"ZombieBuddy.jar","zbNative.dll","ZombieBuddy.jar.new"}.All(p=>!File.Exists(Path.Combine(f.Game,p))),"restore-all removes deployed managed loaders");
    var config=D(Json.DeserializeObject(File.ReadAllText(f.Config)));string[] top=((System.Collections.IEnumerable)config["vmArgs"]).Cast<string>().ToArray();var branch=(Dictionary<string,object>)((Dictionary<string,object>)config["windows"])["10"];string[] windows=((System.Collections.IEnumerable)branch["vmArgs"]).Cast<string>().ToArray();Check(top.SequenceEqual(new[]{"-Xmx6g","-Dfixture=keep","-javaagent:OtherZombieBuddy.jar"})&&windows.SequenceEqual(new[]{"-Xmx5g","-Dbranch=keep"}),"restore-all preserves heap/other arguments and removes exact loaders in root and Windows branch");
    Check(Unchanged(downloads)&&Unchanged(preserved)&&Snapshot(e.Workshop).Count==downloads.Count&&Snapshot(f.User).Count==preserved.Count+1,"restore-all preserves downloaded sources settings Java approvals saves ordinary mods and shaders byte-for-byte");
    var record=Latest(c);Check(record.status=="completed"&&record.action=="componentsAction"&&record.events.Count>0&&phases.Contains(-1)&&phases.Contains(100),"restore-all records actual phase progress and completed durable journal");Check(!Convert.ToBoolean(D(c.Execute("status",null))["configured"]),"status reflects completed restore-all");
   }
  }
  static void PartialRestore(){var f=Configured();string mutexName="Local\\OrangeLaunch-"+DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(f.User).ToUpperInvariant()));using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   Exception workerError=null;var worker=new Thread(()=>{try{using(var mutex=new Mutex(false,mutexName)){mutex.WaitOne();try{entered.Set();release.Wait();}finally{mutex.ReleaseMutex();}}}catch(Exception ex){workerError=ex;entered.Set();}});worker.Start();Check(entered.Wait(5000)&&workerError==null,"synthetic competing addon transaction holds existing native mutex");
   try{using(var c=f.Controller()){int lastProgress=0;c.Progress=(value,text)=>lastProgress=value;string result=(string)D(c.Execute("componentsAction",RestorePayload(true,true)))["message"];Check(result.Contains("[失败] shelves")&&result.Contains("其他启动")&&result.Contains("已关闭 3D"),"existing addon lock failure is reported while independent core removal continues");var task=Latest(c);Check(task.status=="partial"&&task.message.Contains("[失败]")&&lastProgress==-1,"restore-all failure retains truthful partial journal and non-success progress");string mods=File.ReadAllText(f.Mods);Check(mods.Contains("mod = ProjectViewpointShelves,")&&mods.Contains("mod = OtherMod,")&&!mods.Contains("mod = Viewpoint,"),"partial result leaves refused addon visible and preserves normal mod while core is disabled");Check(File.ReadAllText(Path.Combine(f.Data,"diagnostics","tasks.json")).Contains("partial"),"partial result is durable on disk");}}
   finally{release.Set();Check(worker.Join(5000)&&workerError==null,"synthetic competing transaction released");}
  }}
  public static int Main(string[] args){try{if(args.Contains("--integration")){ModernControllerTests.Run();ComponentBatchTests.Run();ComponentEvidenceTests.Run();RoofRemovalTests.Run();FullLauncherIntegrationTests.Run();return 0;}if(args.Contains("--hex-revision")){HexRevision();return 0;}StatusVersion();VersionEdges();HexRevision();Reparse();Restore();PartialRestore();return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
 }
}
