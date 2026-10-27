using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Legacy automatic-binding compatibility tests intentionally exercise the bound legacy adapter.
 // Unified selected-configuration dispatch is covered by OwnedLaunchIntegrationTests.
 static class SaveEnvironmentLaunchTests {
  static int checks;static JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=4194304};
  static void Check(bool value,string reason){if(!value)throw new Exception("FAIL "+reason);checks++;}
  static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
  static Dictionary<string,object> P(params string[] values){var result=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)result.Add(values[i],values[i+1]);return result;}
  static object Call(object target,string method,params object[] args){try{return target.GetType().GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name==method&&m.GetParameters().Length==args.Length).Invoke(target,args);}catch(TargetInvocationException error){throw error.InnerException;}}
  static Dictionary<string,object> D(object v){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(v));}
  static Engine Context(ModernController c){return (Engine)typeof(ModernController).GetProperty("LaunchEngineContext",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c,null);}
  static void Refuses(Action work,string text){bool refused=false;try{work();}catch{refused=true;}Check(refused,text);}
  static Dictionary<string,object> Begin(ModernController c){var result=D(c.Execute("startupBegin",P("targetKind","save","targetId","Sandbox/Original")));return P("startupId",(string)result["startupId"],"kind","save","id","Sandbox/Original");}
  static void Flow(string root){Check(typeof(Engine).Assembly.GetManifestResourceInfo("OrangeEnvironment.jar")==null,"integration uses only existing startup asset container, without a new external/bootstrap agent path");byte[] nested=BundledAssets.ReadOwnAsset("OrangeEnvironment.jar");Check(nested.Length>1000&&nested[0]==80&&nested[1]==75,"required agent is extracted from the existing authenticated startup container");string game=Path.Combine(root,"source","steamapps","common","ProjectZomboid"),user=Path.Combine(root,"live-user"),data=Path.Combine(root,"data");
   Put(Path.Combine(game,"ProjectZomboid64.exe"),"never execute fixture");Put(Path.Combine(game,"jre64","bin","java.exe"),"never execute fixture");Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"mainClass\":\"zombie/gameStates/MainScreenState\",\"classpath\":[\".\",\"projectzomboid.jar\"],\"vmArgs\":[\"-Xmx4g\"]}");Put(Path.Combine(game,"version.txt"),"42.21.0");Put(Path.Combine(game,"game-version.txt"),"42.21.0");
   string mod=Path.Combine(root,"source","steamapps","workshop","content","108600","123456789","mods","Example");Put(Path.Combine(mod,"42","mod.info"),"id=Example\nname=Frozen Example\n");Put(Path.Combine(mod,"common","media","frozen.bin"),"captured");
   string mods="VERSION = 1,\nmods\n{\n mod = Example,\n}\nmaps\n{\n}\n";Put(Path.Combine(user,"mods","default.txt"),mods);Put(Path.Combine(user,"mods","loaded.txt"),"keep original loaded\n");string save=Path.Combine(user,"Saves","Sandbox","Original");Put(Path.Combine(save,"mods.txt"),mods);Put(Path.Combine(save,"map.bin"),"original progressing world");byte[] world=File.ReadAllBytes(Path.Combine(save,"map.bin")),originalDefaults=File.ReadAllBytes(Path.Combine(user,"mods","default.txt"));
   using(var c=new ModernController(game,user,data,true,null,null,null,null,true)){
    Engine original=Context(c);var preview=D(c.Execute("environmentPreview",P("saveId","Sandbox/Original")));Check((bool)preview["ready"],"real controller action previews the complete snapshot");
    var created=D(c.Execute("environmentCreate",P("saveId","Sandbox/Original","name","Original environment","destination",Path.Combine(root,"snapshot"),"stamp",(string)preview["stamp"])));string id=(string)((Dictionary<string,object>)created["snapshot"])["id"];var record=new SaveEnvironments(original).Get(id);Check(new SaveEnvironments(original).Bound("Sandbox/Original").Id==id,"actual dispatch atomically binds the created environment");
    foreach(string action in new[]{"environmentList","environmentPreview","environmentCheck"})Check(ModernController.NativeReads.Contains(action),"host dispatch classifies native read "+action);
    ModernHostPolicy.Validate("https://orange.local/index.html",Json.Serialize(new{id="snapshot",action="environmentChooseCreate",payload=P("saveId","Sandbox/Original","name","new","stamp",(string)preview["stamp"])}));
    Refuses(()=>ModernHostPolicy.Validate("https://orange.local/index.html",Json.Serialize(new{id="bad",action="environmentCreate",payload=P("destination",root)})),"web cannot bypass host folder picker");
    Put(Path.Combine(mod,"42","mod.info"),"id=ChangedByWorkshop\n");Put(Path.Combine(user,"mods","unrelated","42","mod.info"),"id=Example\nrequire=Missing\n");Directory.Delete(game,true);Directory.Delete(mod,true);
    var pending=Begin(c);var pre=(LaunchPreflightReport)c.Execute("launchPreflight",P("startupId",(string)pending["startupId"],"targetKind","save"));Check(pre.canLaunch,"early controller preflight uses copied files after original game and Workshop deletion");
    var watched=new List<GameStartupLaunch>();c.StartupPresentation=launch=>watched.Add(launch);Engine used=null;
    c.RunStartupPresentation("profileLaunch",pending,()=>{
     used=Context(c);Check(used.FrozenEnvironment.Id==id&&used.Game==record.GamePath&&used.User==user,"reserved save launch resolves copied game and original live save directory");
     var report=(LaunchPreflightReport)Call(c,"RequireLaunchPreflight","save","Sandbox/Original");Check(report.canLaunch,"fresh launch evidence ignores changed live mod metadata");
     var start=LaunchProfiles.BuildStartInfo(used);Check(start.FileName==Path.Combine(record.GamePath,"jre64","bin","java.exe")&&start.WorkingDirectory==record.GamePath&&!start.UseShellExecute&&start.CreateNoWindow,"frozen launch uses its own Unicode Java launcher without a console window");Check(start.Arguments.Contains("-cachedir="+user)&&start.EnvironmentVariables["SteamAppId"]=="108600","frozen launch preserves original cachedir and Steam application identity");
     Call(c,"PrepareStartupComponent");Check(!File.Exists(Path.Combine(used.Game,"OrangeLauncherRuntime","startup","OrangeStartup.jar")),"optional loading absent at capture is not introduced by launching");
     LaunchProfiles.Stage(used,new ProfileLaunchRequest{Kind="save",Id="Sandbox/Original",Mode="Sandbox",Save="Original",StartupSessionId=(string)pending["startupId"],ValidateBeforeStage=()=>Call(c,"ValidateLaunchEvidence",report)});
     Check(File.Exists(LaunchProfiles.Journal(used))&&!File.Exists(LaunchProfiles.Journal(original)),"session transaction belongs to copied game rather than selected source");Check(SaveProfiles.DefaultMods(File.ReadAllBytes(used.Mods)).Contains("OrangeLauncherSession"),"frozen boot list includes the native save-launch bridge");return new GameLaunchOutcome();
    });
    Check(ReferenceEquals(Context(c),original)&&original.Game==game,"launch completion never changes the launcher's selected game version");Check(c.StartupStatus().pending,"snapshot submission keeps repeat-launch guard pending");
    c.ReconcileSubmittedStartup((string)pending["startupId"],DateTime.UtcNow,new[]{new ClientIdentity{Pid=123,Birth=DateTime.UtcNow.Ticks,Image=Path.Combine(record.GamePath,"ProjectZomboid64.exe")}});Check(!c.StartupStatus().pending&&c.StartupStatus().phase=="observed","observed exact snapshot client releases the submission guard");Check(((Engine)Call(c,"CurrentRuntimeEngine")).Game==record.GamePath,"game-process controls target the observed snapshot path");
    Check(world.SequenceEqual(File.ReadAllBytes(Path.Combine(save,"map.bin")))&&originalDefaults.SequenceEqual(File.ReadAllBytes(Path.Combine(user,"mods","default.txt")))&&File.ReadAllText(Path.Combine(user,"mods","loaded.txt"))=="keep original loaded\n","launch preparation preserves original world and global mod lists");
    var journal=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(LaunchProfiles.Journal(used)));journal["CreatedUtc"]=DateTime.UtcNow.AddMinutes(-10).ToString("o");Put(LaunchProfiles.Journal(used),Json.Serialize(journal));
    var next=Begin(c);c.RunStartupPresentation("profileLaunch",next,()=>{used=Context(c);var report=(LaunchPreflightReport)Call(c,"RequireLaunchPreflight","save","Sandbox/Original");Check(report.canLaunch&&!LaunchProfiles.Pending(used),"next save launch recovers an ended snapshot transaction before validation");c.CommitStartup();return new GameLaunchOutcome();});
    Check(world.SequenceEqual(File.ReadAllBytes(Path.Combine(save,"map.bin"))),"repeated startup never restores or rewinds world progression");
    LaunchProfiles.Stage(used,new ProfileLaunchRequest{Kind="save",Id="Sandbox/Original",Mode="Sandbox",Save="Original",StartupSessionId=new string('9',32)});
    using(var restarted=new ModernController(game,user,data,true,null,null,null,null,true)){var active=(Engine)Call(restarted,"CurrentRuntimeEngine");Check(active.Game==used.Game&&LaunchProfiles.Pending(active),"restarted controller discovers snapshot journal without an in-memory attempt");restarted.Execute("profileRecover",P());Check(!LaunchProfiles.Pending(used)&&!Directory.Exists(Path.Combine(user,"mods","OrangeLauncherSession")),"visible repair action recovers the exact pending snapshot and removes its bridge");}
    Check(world.SequenceEqual(File.ReadAllBytes(Path.Combine(save,"map.bin")))&&originalDefaults.SequenceEqual(File.ReadAllBytes(Path.Combine(user,"mods","default.txt"))),"explicit snapshot repair preserves original world and default list");
    string manifest=File.ReadAllText(record.ManifestPath);Put(record.ManifestPath,"temporarily unreadable payload metadata");Check(new SaveEnvironments(original).ProcessGames().SequenceEqual(new[]{record.GamePath}),"process index remains lightweight and does not read historical payload manifests");Put(record.ManifestPath,manifest);
    int enumerations=0;typeof(ModernController).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,new Engine(game,user,data,true,null,name=>{enumerations++;return new[]{Process.GetCurrentProcess()};}));var inventory=(Func<string,Process[]>)Call(c,"EnvironmentInventory");foreach(var process in inventory("java"))process.Dispose();foreach(var process in inventory("java")){Check(!process.HasExited,"later path checks reopen a live PID after the guard disposed its prior handle");process.Dispose();}Check(enumerations==1,"process names are enumerated once per multi-snapshot observation");

   }
  }
  public static int Main(string[] args){if(args.Length!=0)return 2;string root=Path.Combine(LauncherTestPaths.Root,"envlaunch-"+Guid.NewGuid().ToString("N").Substring(0,8));try{Directory.CreateDirectory(root);Flow(root);Console.WriteLine("SaveEnvironmentLaunch PASS: "+checks+" assertions");return 0;}catch(Exception error){Console.WriteLine(error);return 1;}finally{string full=Path.GetFullPath(root),parent=Path.GetFullPath(LauncherTestPaths.Root)+Path.DirectorySeparatorChar;if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("envlaunch-"))throw new IOException("Unsafe fixture cleanup");if(Directory.Exists(full))Directory.Delete(full,true);}}
 }
}
