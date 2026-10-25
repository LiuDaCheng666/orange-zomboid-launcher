using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 // All files/processes are owned fixtures. No Steam transport or real game is used.
 static class ForceStopRecoveryTests {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static Dictionary<string,object> D(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
  sealed class Fixture:IDisposable {
   internal readonly string Root;internal readonly Engine E;internal readonly byte[] Original;
   internal Fixture(){Root=Path.Combine(Path.GetTempPath(),"ozl-force-stop-"+Guid.NewGuid().ToString("N"));E=new Engine(Path.Combine(Root,"steamapps/common/Game"),Path.Combine(Root,"user"),Path.Combine(Root,"data"),true);Directory.CreateDirectory(E.Game);Directory.CreateDirectory(Path.GetDirectoryName(E.Mods));File.WriteAllText(E.Config,"{\"vmArgs\":[\"-Xmx2g\"]}");File.WriteAllText(Path.Combine(E.Game,"ProjectZomboid64.exe"),"never executed");File.WriteAllText(Path.Combine(E.Game,"game-version.txt"),"42.0");Original=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\ncustom\n{\n keep = yes,\n}\n")).ToArray();File.WriteAllBytes(E.Mods,Original);}
   internal ModernController Controller(Engine engine=null){var c=new ModernController(E.Game,E.User,E.Data,true);typeof(ModernController).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,engine??E);return c;}
   internal ProfileSessionRecord Stage(string kind="normal",string state="launched",int age=200,int pid=43210,long birth=1){LaunchProfiles.Stage(E,new ProfileLaunchRequest{Kind=kind,Mode="Sandbox",Save="Fixture",Host="127.0.0.1",Port=1,Username="synthetic"});var r=Read();r.State=state;r.CreatedUtc=DateTime.UtcNow.AddSeconds(-age).ToString("o");r.Pid=pid;r.Birth=birth;Write(r);return r;}
   internal ProfileSessionRecord Read(){return Json.Deserialize<ProfileSessionRecord>(File.ReadAllText(LaunchProfiles.Journal(E)));}
   internal void Write(ProfileSessionRecord r){File.WriteAllText(LaunchProfiles.Journal(E),Json.Serialize(r));}
   internal string[] Snapshot(){return Directory.GetFiles(Root,"*",SearchOption.AllDirectories).OrderBy(p=>p).Select(p=>p+"|"+Convert.ToBase64String(File.ReadAllBytes(p))+"|"+File.GetLastWriteTimeUtc(p).Ticks).ToArray();}
   public void Dispose(){Directory.Delete(Root,true);}
  }
  static void FirstClick(){foreach(string kind in new[]{"normal","save","server"})using(var f=new Fixture())using(var c=f.Controller()){
   f.Stage(kind,pid:kind=="normal"?0:43210);GameStartupPresentationTests.Begin(c,kind,"fixture");
   Check(!LaunchProfiles.Pending(f.E)&&File.ReadAllBytes(f.E.Mods).SequenceEqual(f.Original),"first "+kind+" launch click restores ended owned session before preflight");
   Check(!File.Exists(Path.Combine(f.E.User,"Lua","OrangeLauncherSession.request.txt"))&&!Directory.Exists(Path.Combine(f.E.User,"mods","OrangeLauncherSession")),"automatic "+kind+" cleanup removes private request and owned bridge");
  }}
  static void FinalPreflight(){using(var f=new Fixture())using(var c=f.Controller()){
   var payload=GameStartupPresentationTests.Begin(c);f.Stage();Exception error=null;try{c.Execute("launch",payload);}catch(Exception ex){error=ex;}
   Check(error!=null&&error.Message.Contains("测试模式")&&!(error is LaunchPreflightBlockedException)&&!LaunchProfiles.Pending(f.E),"authoritative native relaunch recovers stale session then reaches test transport barrier");
  }}
  static void Classification(){using(var f=new Fixture())using(var c=f.Controller()){
   f.Stage("normal","prepared",0,0);var before=f.Snapshot();var report=(LaunchPreflightReport)c.Execute("launchPreflight",new Dictionary<string,object>());
   Check(!report.canLaunch&&report.items.Single(i=>i.id=="recovery").status=="error","recent prepared session remains a recovery blocker");
   Check(report.items.Single(i=>i.id=="launch-config").status=="ok"&&report.items.Single(i=>i.id=="launch-guard").status!="error","pending session alone does not diagnose valid old parameters as abnormal");
   Check(!report.items.Single(i=>i.id=="recovery").message.Contains("Buddy")&&before.SequenceEqual(f.Snapshot()),"unbound diagnostic reports session recovery separately and never mutates files");
  }}
  static void PreparedAndSteamGrace(){foreach(string state in new[]{"prepared","launched"})using(var f=new Fixture())using(var c=f.Controller()){
   f.Stage("normal",state,0,0);var before=f.Snapshot();var payload=GameStartupPresentationTests.Begin(c);var report=(LaunchPreflightReport)c.Execute("launchPreflight",payload);
   Check(!report.canLaunch&&LaunchProfiles.Pending(f.E)&&before.SequenceEqual(f.Snapshot()),"fresh "+state+" session retains its launch race grace and exact files");
  }}
  sealed class Handle:IClientProcessHandle {
   internal ClientIdentity Identity;internal bool Done,Fail;internal Action OnStop;
   public long Birth{get{return Identity.Birth;}}public string Image{get{return Identity.Image;}}public bool Exited{get{return Done;}}
   public bool ClientRole(string game,ClientIdentity identity){return true;}
   public bool TerminateAndWait(){if(Fail)return false;Done=true;if(OnStop!=null)OnStop();return true;}public void Dispose(){}
  }
  static void ForceClose(){foreach(string mode in new[]{"ended","steam-pid-zero","edited","failed","new-session","prepared"})using(var f=new Fixture())using(var c=f.Controller()){
   long birth=DateTime.UtcNow.AddSeconds(-1).Ticks;var record=f.Stage("normal",mode=="prepared"?"prepared":"launched",10,mode=="steam-pid-zero"?0:12345,birth);
   var identity=new ClientIdentity{Pid=12345,Birth=birth,Image=Path.Combine(f.E.Game,"ProjectZomboid64.exe")};var handle=new Handle{Identity=identity,Fail=mode=="failed"};
   var preview=(ClientClosePreview)typeof(ModernController).GetField("clientClose",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);preview.Discover=_=>new List<ClientIdentity>{identity};preview.Open=_=>handle;
   string edited="mods\n{\n mod = PlayerAdded,\n}\nmaps\n{\n map = PlayerMap,\n}\ncustom\n{\n keep = changed,\n}\n";if(mode=="edited")File.WriteAllText(f.E.Mods,edited.Replace(" mod = PlayerAdded,"," mod = OrangeLauncherSession,\n mod = PlayerAdded,"),new UTF8Encoding(true));
   if(mode=="new-session")handle.OnStop=()=>{var newer=f.Read();newer.Nonce=Guid.NewGuid().ToString("N");f.Write(newer);};
   var receipt=D(c.Execute("gameProcesses",new Dictionary<string,object>()));var result=D(c.Execute("forceCloseGame",new Dictionary<string,object>{{"stamp",receipt["stamp"]},{"confirmed",true}}));
   bool keep=mode=="failed"||mode=="new-session"||mode=="prepared";
   Check(LaunchProfiles.Pending(f.E)==keep,"confirmed force close handles "+mode+" session ownership safely");
   if(!keep){byte[] expected=mode=="edited"?new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(edited)).ToArray():f.Original;Check(File.ReadAllBytes(f.E.Mods).SequenceEqual(expected),"force close "+mode+" preserves original/player bytes including BOM maps and custom blocks");var next=GameStartupPresentationTests.Begin(c);Check(((LaunchPreflightReport)c.Execute("launchPreflight",next)).canLaunch,"first relaunch after force close "+mode+" passes real bound preflight");}
   Check(Convert.ToInt32(result["closed"])==(mode=="failed"?0:1)&&Convert.ToInt32(result["failed"])==(mode=="failed"?1:0),"force close preserves truthful per-client outcome for "+mode);
  }}
  static void InvalidRecord(){using(var f=new Fixture())using(var c=f.Controller()){
   f.Stage();var r=f.Read();r.Files[Path.Combine(f.Root,"foreign.txt")]=new string('A',64);f.Write(r);var before=f.Snapshot();GameStartupPresentationTests.Begin(c);
   Check(LaunchProfiles.Pending(f.E)&&before.SequenceEqual(f.Snapshot()),"automatic recovery refuses invalid ownership without overwriting or deleting files");
  }}
  static void Occupied(){foreach(string role in new[]{"OwnedUnknownMain","zombie.network.GameServer","zombie.gameStates.MainScreenState"})using(var f=new Fixture()){
   string exe=Path.Combine(f.E.Game,"jre64/bin/java.exe");Directory.CreateDirectory(Path.GetDirectoryName(exe));
   using(var compiler=System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp")){var options=new System.CodeDom.Compiler.CompilerParameters(new[]{"System.dll"},exe){GenerateExecutable=true,TempFiles=new System.CodeDom.Compiler.TempFileCollection(f.Root,false)};var output=compiler.CompileAssemblyFromSource(options,"using System; class Fixture { static void Main(){Console.WriteLine(\"ready\");Console.In.ReadLine();} }");Check(!output.Errors.HasErrors,"owned inert unknown-role helper compiles");}
   f.Stage();using(var child=Process.Start(new ProcessStartInfo(exe,role){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true}))try{
    Check(child.StandardOutput.ReadLine()=="ready","owned inert helper is ready");var e=new Engine(f.E.Game,f.E.User,f.E.Data,false,SteamLaunchOptions.Unavailable,name=>name=="java"?new[]{Process.GetProcessById(child.Id)}:new Process[0]);
    var before=f.Snapshot();using(var c=f.Controller(e)){GameStartupPresentationTests.Begin(c);bool blocked=role!="zombie.network.GameServer";Check(LaunchProfiles.Pending(e)==blocked,"automatic cleanup protects client/unknown Java and permits verified dedicated server: "+role);if(blocked)Check(before.SequenceEqual(f.Snapshot()),"live client/unknown Java preserves exact session files");}
   }finally{child.StandardInput.WriteLine();Check(child.WaitForExit(5000),"owned helper exits naturally without touching real game processes");}
  }}
  static void RecordedIdentity(){foreach(bool sameBirth in new[]{true,false})using(var f=new Fixture())using(var self=Process.GetCurrentProcess()){
   f.Stage(pid:self.Id,birth:self.StartTime.ToUniversalTime().Ticks+(sameBirth?0:-1));var e=new Engine(f.E.Game,f.E.User,f.E.Data,false,SteamLaunchOptions.Unavailable,_=>new Process[0]);var before=f.Snapshot();using(var c=f.Controller(e)){GameStartupPresentationTests.Begin(c);Check(LaunchProfiles.Pending(e)==sameBirth,"recorded live process is preserved but reused PID with different birth does not block cleanup");if(sameBirth)Check(before.SequenceEqual(f.Snapshot()),"still-live recorded identity preserves all session bytes even when runtime enumeration misses it");}
  }}
  public static int Main(string[] args){if(args.Contains("--regressions")){try{LaunchProfilesTests.Run();LaunchTransportTests.Run();LaunchPreflightTests.Run();GameStartupPresentationTests.Run();typeof(NativeWorkflowRepairTests).GetMethod("ProcessTests",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[0]);return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}int failed=0;var cases=new Dictionary<string,Action>{{"first",FirstClick},{"final",FinalPreflight},{"classification",Classification},{"grace",PreparedAndSteamGrace},{"close",ForceClose},{"invalid",InvalidRecord},{"occupied",Occupied},{"identity",RecordedIdentity}};foreach(var test in cases.Where(p=>args.Length==0||args.Contains(p.Key)))try{test.Value();}catch(Exception ex){failed++;Console.WriteLine(test.Key+": "+ex);}return failed==0?0:1;}
 }
}
