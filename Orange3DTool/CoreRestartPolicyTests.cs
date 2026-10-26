using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
namespace PZ3D {
 static class CoreRestartPolicyTests {
  static BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
  static bool Ready(ModernDispatchQueue queue){var p=typeof(ModernDispatchQueue).GetProperty("CanRestart");CoreFixture.Check(p!=null,"restart readiness is available without reservation");return (bool)p.GetValue(queue,null);}
  static object Preflight(ModernController controller,ModernDispatchQueue queue,bool capture=false){var m=typeof(ModernHostPolicy).GetMethod("CorePreflight",BindingFlags.Static|BindingFlags.NonPublic);CoreFixture.Check(m!=null,"native read-only restart preflight exists");return m.Invoke(null,new object[]{controller,queue,capture});}
  static bool Allowed(object result){return (bool)result.GetType().GetProperty("allowed").GetValue(result,null);}
  public static int Main(string[] args){if(args.Length==1&&args[0]=="--fixture-game"){Thread.Sleep(60000);return 0;}if(args.Length!=0)return 2;try{Run();Console.WriteLine("CORE RESTART POLICY: "+CoreFixture.Passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  static void Run(){
   string root=CoreFixture.Root(),game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");Directory.CreateDirectory(game);Directory.CreateDirectory(user);
   string image=Path.Combine(game,"ProjectZomboid64.exe");File.Copy(typeof(CoreFixture).Assembly.Location,image);File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");
   var e=new Engine(game,user,data,true);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));File.WriteAllText(e.Mods,"VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\n");
   using(var process=Process.Start(new ProcessStartInfo(image,"--fixture-game"){UseShellExecute=false,CreateNoWindow=true}))try{using(var controller=new ModernController(game,user,data,false)){
    var taskJournal=new LauncherJournal(data);taskJournal.Begin("fixture","fixture");taskJournal.Finish("completed","fixture");string taskFile=Path.Combine(data,"diagnostics","tasks.json"),logFile=Path.Combine(data,"diagnostics","launcher.log");byte[] taskBefore=File.ReadAllBytes(taskFile),logBefore=File.ReadAllBytes(logFile);
    byte[] before=File.ReadAllBytes(e.Mods);CoreFixture.Check(GameProcessGuard.IsRunning(game)&&!process.HasExited,"separate fixture game provides real running evidence");
    controller.AssertCanRestart();CoreFixture.Check(!process.HasExited,"core restart permits running game without ending its PID");
    CoreFixture.Reject(()=>new Engine(game,user,data,false,SteamLaunchOptions.Unavailable).GuardMaintenance(),"ordinary game-file mutation still refuses running game");
    var q=new ModernDispatchQueue();CoreFixture.Check(Ready(q)&&Allowed(Preflight(controller,q))&&!q.RestartReserved,"preflight permits restart without reserving or cancelling");
    typeof(ModernController).GetField("busy",Hidden).SetValue(controller,true);CoreFixture.Reject(()=>controller.AssertCanRestart(),"busy mutation still blocks restart");CoreFixture.Check(!Allowed(Preflight(controller,q)),"busy preflight reports blocked");typeof(ModernController).GetField("busy",Hidden).SetValue(controller,false);
    var requests=(List<CancellationTokenSource>)typeof(ModernController).GetField("networkRequests",Hidden).GetValue(controller);using(var token=new CancellationTokenSource()){requests.Add(token);CoreFixture.Reject(()=>controller.AssertCanRestart(),"active network read still blocks restart");Preflight(controller,q);CoreFixture.Check(!token.IsCancellationRequested,"preflight never cancels active network requests");requests.Remove(token);}
    foreach(string journal in new[]{PlayProfileTransaction.Journal(e),Path.Combine(data,"launch-sessions",LaunchProfiles.Key(e)+".json"),(string)typeof(BuddyTakeover).GetMethod("JournalPath",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{e})}){Directory.CreateDirectory(Path.GetDirectoryName(journal));File.WriteAllText(journal,"fixture recovery evidence");controller.AssertCanRestart();CoreFixture.Check(Allowed(Preflight(controller,q))&&File.ReadAllText(journal)=="fixture recovery evidence","idle recovery evidence survives self-update preflight without blocking: "+Path.GetFileName(Path.GetDirectoryName(journal)));File.Delete(journal);}
    CoreFixture.Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(e.Mods)),"restart checks never restore or mutate default list");
    CoreFixture.Check(Convert.ToBase64String(taskBefore)==Convert.ToBase64String(File.ReadAllBytes(taskFile))&&Convert.ToBase64String(logBefore)==Convert.ToBase64String(File.ReadAllBytes(logFile)),"restart and preflight preserve task journal and log bytes");
    using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){var read=q.Execute(()=>{entered.Set();release.Wait(5000);return null;});CoreFixture.Check(entered.Wait(5000),"fixture read occupies queue");var mutation=q.ExecuteMutation(()=>null);CoreFixture.Check(!Ready(q)&&!Allowed(Preflight(controller,q))&&!q.TryReserveRestart(),"queued mutation blocks read-only readiness and restart");release.Set();Task.WaitAll(read,mutation);}
    CoreFixture.Check(q.TryReserveRestart()&&!Ready(q),"restart reservation blocks readiness");q.ReleaseRestart();CoreFixture.Check(Ready(q),"readiness returns after reservation released");CoreFixture.Reject(()=>Preflight(controller,q,true),"capture mode refuses native preflight");
   }}finally{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}}
   ModernHostPolicy.Validate("https://orange.local/index.html","{\"id\":\"p\",\"action\":\"coreUpdatePreflight\",\"payload\":{\"protocol\":1}}");CoreFixture.Check(true,"native policy accepts exact preflight protocol");
   foreach(string payload in new[]{"{}","{\"protocol\":2}","{\"protocol\":1,\"path\":\"x\"}","{\"protocol\":1,\"confirmed\":true}"})CoreFixture.Reject(()=>ModernHostPolicy.Validate("https://orange.local/index.html","{\"id\":\"p\",\"action\":\"coreUpdatePreflight\",\"payload\":"+payload+"}"),"preflight refuses malformed or expanded payload");
  }
 }
}
