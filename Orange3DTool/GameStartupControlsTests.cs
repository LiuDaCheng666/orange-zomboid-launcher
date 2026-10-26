using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class GameStartupControlsTests {
  const string Argument="-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar";
  static readonly string Nonce=new string('a',32);
  static int checks;
  static void Check(bool ok,string why){if(!ok)throw new Exception("FAIL: "+why);checks++;Console.WriteLine("PASS: "+why);}
  static object Call(string method,params object[] args){var found=typeof(GameStartupComponent).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic);if(found==null)throw new Exception("FAIL: startup control exists: "+method);try{return found.Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
  static void Reject(Action action,string why){bool refused=false;try{action();}catch{refused=true;}Check(refused,why);}
  static Dictionary<string,object> State(Engine engine){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(Call("State",engine)));}
  sealed class Fixture:IDisposable {
   internal readonly string Root=Path.Combine(Path.GetTempPath(),"ozl-startup-controls-"+Guid.NewGuid().ToString("N"));internal Engine E;
   internal Fixture(){string game=Path.Combine(Root,"game");Directory.CreateDirectory(game);E=new Engine(game,Path.Combine(Root,"user"),Path.Combine(Root,"data"),true);File.WriteAllText(E.Config,"{\"vmArgs\":[\"-Xmx12g\",\"-Dprivate=keep\"],\"windows\":{\"10\":{\"vmArgs\":[\"-XX:+UseZGC\"]}},\"custom\":42}");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");}
   internal string PathOf(string name){return Path.Combine(E.Game,"OrangeLauncherRuntime","startup",name);}
   public void Dispose(){Directory.Delete(Root,true);}
  }
  static void DefaultsAndToggle(){using(var f=new Fixture()){
   byte[] original=File.ReadAllBytes(f.E.Config);var initial=State(f.E);Check((bool)initial["enabled"]&&!(bool)initial["installed"]&&!(bool)initial["removable"],"fresh startup page defaults enabled without modifying a game");Check(original.SequenceEqual(File.ReadAllBytes(f.E.Config))&&!Directory.Exists(f.E.Data),"status is strictly read-only");
   Call("Prepare",f.E,Nonce);Call("SetEnabled",f.E,false);Check(!(bool)Call("Enabled",f.E)&&!File.ReadAllText(f.E.Config).Contains(Argument)&&File.Exists(f.PathOf("OrangeStartup.jar")),"disable detaches the exact agent and keeps verified component files");
   byte[] disabled=File.ReadAllBytes(f.E.Config);Call("Prepare",f.E,new string('b',32));Check(disabled.SequenceEqual(File.ReadAllBytes(f.E.Config)),"disabled launch cannot silently reinstall the agent");
   var reopen=new Engine(f.E.Game,f.E.User,f.E.Data,true);Check(!(bool)Call("Enabled",reopen),"disabled preference persists across launcher instances");
   Call("SetEnabled",f.E,true);Check(!File.ReadAllText(f.E.Config).Contains(Argument),"enabling waits for a new prepared launch");Call("Prepare",f.E,new string('c',32));Check(File.ReadAllText(f.E.Config).Contains(Argument),"next launch installs the enabled page again");
   string raw="{\"vmArgs\":[\"-Xmx12g\",\""+Argument+"\",\""+Argument+"\",\"-javaagent:foreign.jar\"],\"windows\":{\"10\":{\"vmArgs\":[\""+Argument+"\",\"-Dbranch=keep\"]}},\"custom\":42}";File.WriteAllText(f.E.Config,raw);Call("SetEnabled",f.E,false);string changed=File.ReadAllText(f.E.Config);Check(!changed.Contains(Argument)&&changed.Contains("foreign.jar")&&changed.Contains("-Xmx12g")&&changed.Contains("-Dbranch=keep")&&changed.Contains("custom"),"disable removes only exact args from every branch");
  }}
  static void SharedPreference(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);string other=Path.Combine(f.Root,"other-game");Directory.CreateDirectory(other);var second=new Engine(other,f.E.User,f.E.Data,true);File.WriteAllText(second.Config,"{\"vmArgs\":[\"-Xmx8g\",\"-Dother=keep\"]}");Call("Prepare",second,new string('e',32));Call("SetEnabled",f.E,false);Check(File.ReadAllText(second.Config).Contains(Argument),"shared preference fixture retains another previously prepared game's argument");byte[] jar=File.ReadAllBytes(Path.Combine(other,"OrangeLauncherRuntime","startup","OrangeStartup.jar"));
   Call("Prepare",second,new string('f',32));Check(!File.ReadAllText(second.Config).Contains(Argument)&&File.ReadAllText(second.Config).Contains("-Xmx8g")&&File.ReadAllText(second.Config).Contains("-Dother=keep"),"disabled Prepare detaches the exact stale agent after switching games");Check(jar.SequenceEqual(File.ReadAllBytes(Path.Combine(other,"OrangeLauncherRuntime","startup","OrangeStartup.jar"))),"disabled Prepare preserves another game's existing component payload");
  }}
  static void Removal(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);File.WriteAllText(f.PathOf("foreign.txt"),"preserve");var state=State(f.E);Check((bool)state["installed"]&&(bool)state["removable"],"owned page exposes uninstall availability");Call("Remove",f.E);
   Check(!(bool)Call("Enabled",f.E)&&!File.ReadAllText(f.E.Config).Contains(Argument),"uninstall detaches and persists disabled");Check(new[]{"OrangeStartup.jar","session.properties","issues.txt","ownership.json"}.All(name=>!File.Exists(f.PathOf(name)))&&File.ReadAllText(f.PathOf("foreign.txt"))=="preserve","uninstall deletes exactly owned files and preserves unknown files");
   Call("Prepare",f.E,new string('d',32));Check(!File.Exists(f.PathOf("OrangeStartup.jar")),"uninstalled page stays removed on later launches");Call("Remove",f.E);Check(File.ReadAllText(f.PathOf("foreign.txt"))=="preserve","repeat uninstall is harmless");
  }
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);Call("Remove",f.E);Check(!Directory.Exists(f.PathOf("")),"uninstall prunes only empty managed directories");}
   foreach(string name in new[]{"OrangeStartup.jar","session.properties","issues.txt","ownership.json"})using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);File.AppendAllText(f.PathOf(name),"foreign");byte[] config=File.ReadAllBytes(f.E.Config);var state=State(f.E);Check(!(bool)state["removable"]&&!string.IsNullOrEmpty((string)state["warning"]),"status reports foreign ownership conflict: "+name);Reject(()=>Call("Remove",f.E),"uninstall refuses changed owned file: "+name);Check(File.ReadAllText(f.PathOf(name)).EndsWith("foreign")&&config.SequenceEqual(File.ReadAllBytes(f.E.Config))&&(bool)Call("Enabled",f.E),"uninstall refusal leaves files, configuration and preference intact");Call("SetEnabled",f.E,false);Check(!File.ReadAllText(f.E.Config).Contains(Argument)&&File.ReadAllText(f.PathOf(name)).EndsWith("foreign"),"disable can detach safely without deleting edited file");
   }
  }
  static void Refusals(){
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);File.WriteAllText(f.E.Config,"{\"vmArgs\":[],\"vmArgs\":[\""+Argument+"\"]}");byte[] before=File.ReadAllBytes(f.E.Config);Reject(()=>Call("SetEnabled",f.E,false),"ambiguous duplicate vmArgs is refused");Check(before.SequenceEqual(File.ReadAllBytes(f.E.Config))&&(bool)Call("Enabled",f.E),"invalid configuration cannot partly save disabled preference");}
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);string pending=LaunchProfiles.Journal(f.E);Directory.CreateDirectory(Path.GetDirectoryName(pending));File.WriteAllText(pending,"pending");byte[] config=File.ReadAllBytes(f.E.Config);Reject(()=>Call("Remove",f.E),"pending launch recovery blocks uninstall");Reject(()=>Call("SetEnabled",f.E,false),"pending launch recovery blocks disable");Check(config.SequenceEqual(File.ReadAllBytes(f.E.Config)),"recovery refusal keeps configuration");}
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);byte[] config=File.ReadAllBytes(f.E.Config);using(var held=new FileStream(f.E.Config,FileMode.Open,FileAccess.Read,FileShare.Read)){Reject(()=>Call("SetEnabled",f.E,false),"configuration write failure is reported");}Check(config.SequenceEqual(File.ReadAllBytes(f.E.Config))&&(bool)Call("Enabled",f.E),"failed detachment cannot leave disabled preference");}
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);Directory.CreateDirectory(f.E.Data);string preference=Path.Combine(f.E.Data,"game-startup-enabled.txt");File.WriteAllText(preference,"foreign");byte[] config=File.ReadAllBytes(f.E.Config);Reject(()=>Call("SetEnabled",f.E,false),"unknown preference contents are preserved");Check(config.SequenceEqual(File.ReadAllBytes(f.E.Config))&&File.ReadAllText(preference)=="foreign","invalid preference cannot partly detach configuration");Check(!string.IsNullOrEmpty((string)State(f.E)["warning"]),"invalid preference surfaces a read-only status warning");}
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);Call("SetEnabled",f.E,true);byte[] config=File.ReadAllBytes(f.E.Config);using(var held=new FileStream(Path.Combine(f.E.Data,"game-startup-enabled.txt"),FileMode.Open,FileAccess.Read,FileShare.Read)){Reject(()=>Call("SetEnabled",f.E,false),"preference write failure is reported");}Check(config.SequenceEqual(File.ReadAllBytes(f.E.Config))&&(bool)Call("Enabled",f.E),"failed preference persistence restores exact prior configuration");}
   using(var f=new Fixture()){Call("Prepare",f.E,Nonce);string text=File.ReadAllText(f.PathOf("session.properties"));text=text.Replace("game="+Convert.ToBase64String(Encoding.UTF8.GetBytes(f.E.Game)),"game="+Convert.ToBase64String(Encoding.UTF8.GetBytes(f.Root)));File.WriteAllText(f.PathOf("session.properties"),text,new UTF8Encoding(false));var json=new JavaScriptSerializer();var owner=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f.PathOf("ownership.json")));owner["session"]=DlssInstaller.Hash(File.ReadAllBytes(f.PathOf("session.properties")));File.WriteAllText(f.PathOf("ownership.json"),json.Serialize(owner),new UTF8Encoding(false));var state=State(f.E);Check(!(bool)state["removable"]&&!string.IsNullOrEmpty((string)state["warning"]),"status cannot offer uninstall for a mismatched game-bound receipt");Reject(()=>Call("Remove",f.E),"uninstall refuses a mismatched game binding");}
  }
  static void InterruptedRemoval(){for(int cut=0;cut<8;cut++)using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);Call("SetEnabled",f.E,false);string[] files={"OrangeStartup.jar","session.properties","issues.txt"};var hashes=files.Select(name=>DlssInstaller.Hash(File.ReadAllBytes(f.PathOf(name)))).ToArray();File.WriteAllText(f.PathOf("ownership.json"),new JavaScriptSerializer().Serialize(new{version="OZL1-PENDING",kind="remove",jar=hashes[0],session=hashes[1],issues=hashes[2],nextJar=(string)null,nextSession=(string)null,nextIssues=(string)null}),new UTF8Encoding(false));for(int i=0;i<3;i++)if((cut&(1<<i))!=0)File.Delete(f.PathOf(files[i]));
   Check((bool)State(f.E)["removable"],"interrupted removal remains retryable at boundary "+cut);Call("Remove",f.E);Check(!Directory.Exists(f.PathOf(""))&&!(bool)Call("Enabled",f.E),"interrupted removal resumes safely at boundary "+cut);
  }}
  [STAThread] public static int Main(string[] args){try{if(args.Contains("--component-regression")){GameStartupComponentTests.Run();return 0;}DefaultsAndToggle();SharedPreference();Removal();Refusals();InterruptedRemoval();Console.WriteLine("PASS "+checks+" startup page preference/uninstall checks");return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
