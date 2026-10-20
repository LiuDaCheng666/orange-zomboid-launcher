using System;
using System.IO;
using System.Linq;
using System.Text;
namespace PZ3D {
 static class ServerLaunchPolicyTests {
  static void Check(bool ok,string message){Console.WriteLine((ok?"PASS: ":"FAIL: ")+message);if(!ok)throw new Exception(message);}
  static Engine Fixture(){string root=Path.Combine(LauncherTestPaths.Root,"server-policy-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"steamapps","common","ProjectZomboid"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));File.WriteAllText(e.Config,"{\"vmArgs\":[]}");return e;}
  static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
  static void Source(Engine e,string item,string folder,string id){string root=Path.Combine(e.Workshop,item,"mods",folder);Put(Path.Combine(root,"common","mod.info"),"id="+id+"\nname=synthetic translation\n");Put(Path.Combine(root,"42","mod.info"),"id="+id+"\n");Put(Path.Combine(root,"common","media","lua","shared","Translate","CN","UI_CN.txt"),"common-version");Put(Path.Combine(root,"42","media","lua","shared","Translate","CN","UI_CN.txt"),id+"-version");Put(Path.Combine(root,"42","media","lua","shared","Translate","CN","Items_CN.json"),"{\"fixture\":\"translation\"}");Put(Path.Combine(root,"42","media","lua","shared","Translate","CN","Bad.lua"),"must not copy");Put(Path.Combine(root,"42","media","lua","client","UiFix.lua"),"must not copy");Put(Path.Combine(root,"42","media","maps","FakeMap","spawnpoints.lua"),"must not copy");}
  public static void Run(){
   var e=Fixture();Source(e,"3556540080","B42ModTrans_CN_As1","B42ModTrans_CN");Source(e,"3556544454","B42Trans_CN_As1","B42Trans_CN");
   string original="VERSION = 1,\nmods\n{\n mod = OrdinaryLocal,\n mod = B42ModTrans_CN,\n mod = B42Trans_CN,\n}\nmaps\n{\n map = LocalMap,\n}\n";File.WriteAllText(e.Mods,original,new UTF8Encoding(true));byte[] before=File.ReadAllBytes(e.Mods);
   LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"});string[] staged=SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods));
   Check(staged.Contains("B42ModTrans_CN")&&staged.Contains("B42Trans_CN"),"server profile retains enabled unified Chinese independently of 3D readiness");
   Check(!staged.Contains("OrdinaryLocal")&&!File.ReadAllText(e.Mods).Contains("LocalMap"),"server profile excludes ordinary local mods and local maps");
   const string overlayId="OZLServerTranslations_B42Trans_CN";string overlay=Path.Combine(e.User,"mods",overlayId);string translated=Path.Combine(overlay,"42","media","lua","shared","Translate","CN","UI_CN.txt");
   string firstOverlay=Path.Combine(e.User,"mods","OZLServerTranslations_B42ModTrans_CN");
   Check(staged.Count(id=>id==overlayId)==1&&Array.IndexOf(staged,"OZLServerTranslations_B42ModTrans_CN")<Array.IndexOf(staged,overlayId)&&File.ReadAllText(translated)=="B42Trans_CN-version","separate translation mods preserve selected source order");
   Check(File.ReadAllText(Path.Combine(firstOverlay,"common","media","lua","shared","Translate","CN","UI_CN.txt"))=="common-version"&&File.ReadAllText(Path.Combine(firstOverlay,"42","media","lua","shared","Translate","CN","UI_CN.txt"))=="B42ModTrans_CN-version","common and active version files remain separate for native key merging");
   Check(File.Exists(Path.Combine(overlay,"42","media","lua","shared","Translate","CN","Items_CN.json"))&&!Directory.EnumerateFiles(overlay,"*.lua",SearchOption.AllDirectories).Any()&&!Directory.Exists(Path.Combine(overlay,"common","media","maps")),"server translation overlay copies only text and JSON, never Lua or maps");
   Check(!LaunchProfiles.PersistentMods(e,staged).Contains(overlayId),"temporary translation overlay never persists in named or play profiles");
   LaunchProfiles.Recover(e,true);Check(File.ReadAllBytes(e.Mods).SequenceEqual(before)&&!Directory.Exists(overlay)&&!Directory.Exists(firstOverlay),"normal recovery restores exact original defaults and removes owned translation files");
   Check(File.ReadAllText(Path.Combine(e.Workshop,"3556544454","mods","B42Trans_CN_As1","42","media","lua","client","UiFix.lua"))=="must not copy","translation sources remain untouched");
   LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"});File.WriteAllText(translated,"player-edited-text");File.AppendAllText(e.Mods,"\nplayer edit\n");LaunchProfiles.Recover(e,true);
   Check(File.Exists(translated)&&File.ReadAllText(translated)=="player-edited-text"&&File.ReadAllText(e.Mods).Contains("player edit")&&!File.ReadAllText(e.Mods).Contains(overlayId),"recovery preserves edits while removing transient overlay from the edited default list");
   e=Fixture();Source(e,"3556540080","B42ModTrans_CN_As1","B42ModTrans_CN");Put(e.Mods,"mods\n{\n mod = OrdinaryLocal,\n}\nmaps\n{\n}\n");LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"});Check(!File.ReadAllText(e.Mods).Contains("OZLServerTranslations")&&!Directory.Exists(Path.Combine(e.User,"mods",overlayId)),"installed but disabled translations are not enabled automatically");LaunchProfiles.Recover(e,true);
   Additional();VisualWeather();
  }
  static void Additional(){
   var e=Fixture();Source(e,"3556540080","B42ModTrans_CN_As1","B42ModTrans_CN");Source(e,"3556544454","B42Trans_CN_As1","B42Trans_CN");
   Put(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");Put(Path.Combine(e.Game,"ZombieBuddy.jar"),"fixture");Put(Path.Combine(e.Game,"zbNative.dll"),"fixture");
   Put(e.Mods,"mods\n{\n mod = Viewpoint,\n mod = ZombieBuddy,\n mod = B42Trans_CN,\n mod = B42ModTrans_CN,\n mod = OrdinaryLocal,\n}\nmaps\n{\n map = LocalMap,\n}\n");
   LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"});var staged=SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods));
   Check(staged.Count(id=>id=="Viewpoint")==1&&staged.Count(id=>id=="ZombieBuddy")==1,"enabled 3D and Buddy remain present exactly once during server staging");
   Check(Array.IndexOf(staged,"OZLServerTranslations_B42Trans_CN")<Array.IndexOf(staged,"OZLServerTranslations_B42ModTrans_CN"),"reversed Chinese source order remains reversed in generated mod IDs");
   string journal=LaunchProfiles.Journal(e),good=File.ReadAllText(journal);var json=new System.Web.Script.Serialization.JavaScriptSerializer();var record=json.Deserialize<ProfileSessionRecord>(good);record.Files[Path.Combine(ServerLaunchTranslations.Root(e,"B42Trans_CN"),"42","media","lua","client","foreign.lua")]=new string('A',64);File.WriteAllText(journal,json.Serialize(record));byte[] before=File.ReadAllBytes(e.Mods);bool rejected=false;try{LaunchProfiles.Recover(e,true);}catch{rejected=true;}
   Check(rejected&&before.SequenceEqual(File.ReadAllBytes(e.Mods)),"journal refuses nontranslation ownership before changing defaults");File.WriteAllText(journal,good);LaunchProfiles.Recover(e,true);
   LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="save",Mode="Sandbox",Save="Fixture"});
   Check(File.ReadAllText(e.Mods).Contains("OrdinaryLocal")&&File.ReadAllText(e.Mods).Contains("LocalMap")&&!File.ReadAllText(e.Mods).Contains("OZLServerTranslations"),"single-player launch keeps local mods and maps without generating multiplayer translations");LaunchProfiles.Recover(e,true);
   e=Fixture();Put(e.Mods,"mods\n{\n mod = B42Trans_CN,\n}\nmaps\n{\n}\n");int warnings=0;e.Progress=(p,m)=>warnings++;LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"});
   Check(warnings>0&&File.ReadAllText(e.Mods).Contains("B42Trans_CN")&&!File.ReadAllText(e.Mods).Contains("OZLServerTranslations"),"unresolved installed translation warns without a new launch gate");LaunchProfiles.Recover(e,true);
   e=Fixture();Source(e,"3556544454","B42Trans_CN_As1","B42Trans_CN");Put(e.Mods,"mods\n{\n mod = B42Trans_CN,\n}\nmaps\n{\n}\n");LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"});
   Directory.CreateDirectory(Path.Combine(e.User,"Saves","Sandbox","Inherited"));object read=SaveProfiles.Read(e,"Sandbox/Inherited");string[] enabled=(string[])read.GetType().GetProperty("enabled").GetValue(read,null);string stamp=(string)read.GetType().GetProperty("stamp").GetValue(read,null);
   Check(!enabled.Any(id=>id=="OrangeLauncherSession"||ServerLaunchTranslations.IsOverlay(id)),"inherited save editor does not expose temporary launch mods as persistent selections");
   SaveProfiles.Save(e,"Sandbox/Inherited",stamp,SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods)),true);LaunchProfiles.Recover(e,true);var saved=SaveProfiles.DefaultMods(File.ReadAllBytes(Path.Combine(e.User,"Saves","Sandbox","Inherited","mods.txt")));
   Check(saved.SequenceEqual(new[]{"B42Trans_CN"}),"saving inherited defaults never writes transient bridge or translation IDs into a save");
  }
  static void VisualWeather(){
   var e=Fixture();Put(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");Put(Path.Combine(e.Game,"ZombieBuddy.jar"),"fixture");Put(Path.Combine(e.Game,"zbNative.dll"),"fixture");
   string weather=Path.Combine(e.Workshop,"3813908957","mods","ViewpointTrueWeathers","42");Put(Path.Combine(weather,"mod.info"),"id=ViewpointTrueWeathers\njavaJarFile=media/java/client/ViewpointTrueWeathers.jar\njavaPkgName=vptw\n");string jar=Path.Combine(weather,"media","java","client","ViewpointTrueWeathers.jar");Put(jar,"synthetic client JAR without preload declaration");
   string original="mods\n{\n mod = ZombieBuddy,\n mod = Viewpoint,\n mod = ViewpointTrueWeathers,\n mod = ViewpointTrueWeathersGameplay,\n mod = OrdinaryLocal,\n}\nmaps\n{\n}\n";Put(e.Mods,original);
   string properties=Path.Combine(e.User,"viewpoint-live.properties"),pack=Path.Combine(e.User,"viewpoint-shaderpacks","trueweathers","pack.properties");Put(properties,"# player choice\ngraphics.pack=trueweathers\ngraphics.mode=Modern\n");Put(pack,"name=Player installed True Weathers\n");byte[] settingsBefore=File.ReadAllBytes(properties),packBefore=File.ReadAllBytes(pack);
   var request=new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"};LaunchProfiles.Stage(e,request);var staged=SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods));
   Check(staged.Count(id=>id=="ViewpointTrueWeathers")==1,"server startup retains enabled TrueWeathers visual client initializer without requiring premain preload");
   Check(!staged.Contains("ViewpointTrueWeathersGameplay")&&!staged.Contains("OrdinaryLocal"),"visual weather retention never adds gameplay or ordinary local mods to server startup");
   Check(File.ReadAllBytes(properties).SequenceEqual(settingsBefore)&&File.ReadAllBytes(pack).SequenceEqual(packBefore),"server staging preserves installed custom shader bytes and the player's exact choice");LaunchProfiles.Recover(e,true);Check(File.ReadAllText(e.Mods)==original,"weather retention recovery restores exact original defaults");
   Put(e.Mods,"mods\n{\n mod = ZombieBuddy,\n mod = Viewpoint,\n}\nmaps\n{\n}\n");LaunchProfiles.Stage(e,request);Check(!SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods)).Contains("ViewpointTrueWeathers"),"installed disabled weather is not enabled by server launch");LaunchProfiles.Recover(e,true);
   Put(e.Mods,original);File.Delete(jar);LaunchProfiles.Stage(e,request);Check(!SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods)).Contains("ViewpointTrueWeathers"),"missing weather client JAR is not advertised as retained");LaunchProfiles.Recover(e,true);
   Put(jar,"synthetic client JAR");Put(e.Config,"{\"vmArgs\":[]}");LaunchProfiles.Stage(e,request);Check(!SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods)).Contains("ViewpointTrueWeathers"),"weather retention does not enable a component when base 3D is unconfigured");LaunchProfiles.Recover(e,true);
  }
  static void NativeFixture(){
   var e=Fixture();Source(e,"3556540080","B42ModTrans_CN_As1","B42ModTrans_CN");Source(e,"3556544454","B42Trans_CN_As1","B42Trans_CN");
   foreach(string id in ServerLaunchTranslations.Ids){string root=Path.Combine(e.Workshop,id=="B42ModTrans_CN"?"3556540080":"3556544454","mods",id+"_As1");Put(Path.Combine(root,"common","media","lua","shared","Translate","CN","UI.json"),"{\""+id+"_common\":\"kept\",\"overlap\":\""+id+"_common\"}");Put(Path.Combine(root,"42","media","lua","shared","Translate","CN","UI.json"),"{\""+id+"_version\":\"kept\",\"overlap\":\""+id+"_version\"}");}
   foreach(var file in ServerLaunchTranslations.Build(e,ServerLaunchTranslations.Ids)){Directory.CreateDirectory(Path.GetDirectoryName(file.Key));File.WriteAllBytes(file.Key,file.Value);}
   Console.WriteLine("NATIVE_USER="+e.User);
  }
  public static int Main(string[] args){try{if(args.Contains("--native-fixture"))NativeFixture();else Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
 }
}
