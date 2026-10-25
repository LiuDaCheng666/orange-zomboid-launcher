using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
namespace PZ3D {
 static class GameStartupPresenterTests {
  sealed class Observation:IGameStartupMonitor {
   internal Action<GameStartupSnapshot> Changed;internal int Starts,Submits,Disposals;internal string Id;
   public void Start(){Starts++;}public void Submitted(){Submits++;}
   public void Fail(string message){Changed(new GameStartupSnapshot{SessionId=Id,Stage="failed",Message=message,Failed=true,Lines=new string[0]});}
   public void Dispose(){Disposals++;}
  }
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static GameStartupLaunch Request(string id,string stage){return new GameStartupLaunch{SessionId=id,Stage=stage,TargetKind="normal",StartedUtc=DateTime.UtcNow,DataPath="",Message="owned test request"};}
  static string Text(Control root,string name){return root.Controls.Find(name,true).Single().Text;}
  public static void Run(){
   var policy=new GameStartupWindowPolicy();
   Check(policy.Decide(10,true,false)=="cover","new verified game foreground is covered once");
   Check(policy.Decide(10,true,false)=="yield","returning to same game respects user switch");
   Check(policy.Decide(11,false,true)=="yield","yield never reclaims replacement window");
   var background=new GameStartupWindowPolicy();Check(background.Decide(10,false,false)=="yield","unrelated foreground app disables automatic cover");

   using(var owner=new Form()){
    IntPtr handle=owner.Handle;var observations=new List<Observation>();int windows=0,handoffs=0;
    using(var presenter=new GameStartupPresenter(owner,(request,callback)=>{var watch=new Observation{Id=request.SessionId,Changed=callback};observations.Add(watch);return watch;},request=>{
     windows++;return new GameStartupWindow(request,LauncherTheme.Create(false)){StartPosition=FormStartPosition.Manual,Location=new Point(-22000,-22000),ShowInTaskbar=false};
    },(request,snapshot,view)=>{handoffs++;})){
     presenter.Handle(Request("first","preflight"));Application.DoEvents();
     var early=presenter.CurrentWindow;
     Check(early!=null&&early.Visible&&observations.Count==0,"preflight immediately shows loading without starting game monitor");
     presenter.Handle(Request("first","preparing"));Application.DoEvents();
     Check(ReferenceEquals(early,presenter.CurrentWindow),"actual launch adopts same preflight window");
     Check(windows==1&&observations[0].Starts==1&&presenter.CurrentWindow!=null&&presenter.CurrentWindow.Visible,"preparing creates and starts one independent native observer window");
     presenter.Handle(Request("first","preparing"));Check(windows==1,"duplicate preparing event cannot create another window");
     presenter.Handle(Request("first","submitted"));Check(observations[0].Submits==1,"submitted lifecycle reaches active observer");
     var first=presenter.CurrentWindow;
     var worker=new Thread(()=>observations[0].Changed(new GameStartupSnapshot{SessionId="first",Stage="mods",Message="真实后台阶段",Lines=new[]{"registered module"}}));worker.Start();worker.Join();Application.DoEvents();
     Check(Text(first,"startup-message")=="真实后台阶段","background monitor snapshots reach native UI thread");
     observations[0].Changed(new GameStartupSnapshot{SessionId="first",Stage="approval",Message="请在游戏中核对授权",WaitingForInput=true});Application.DoEvents();Check(handoffs==1&&!first.Visible&&!first.IsDisposed,"fullscreen observer yields while game requires authorization");
     observations[0].Changed(new GameStartupSnapshot{SessionId="first",Stage="mods",Message="继续加载模组"});Application.DoEvents();Check(handoffs==1&&!first.Visible,"authorization yield remains permanent and never covers game input again");
     observations[0].Changed(new GameStartupSnapshot{SessionId="forged",Complete=true});Application.DoEvents();Check(presenter.CurrentWindow==first,"wrong session cannot close active loading window");
     presenter.Handle(Request("first","failed"));Application.DoEvents();Check(presenter.CurrentWindow==first&&!first.IsDisposed,"failed startup remains available for error inspection");
     first.Close();Application.DoEvents();Check(presenter.CurrentWindow==null&&observations[0].Disposals==1,"closing window disposes only the observer exactly once");
     presenter.Handle(Request("second","preparing"));Application.DoEvents();var second=presenter.CurrentWindow;
     observations[0].Changed(new GameStartupSnapshot{SessionId="first",Complete=true});Application.DoEvents();Check(presenter.CurrentWindow==second,"late completed snapshot from closed session cannot close new one");
     observations[1].Changed(new GameStartupSnapshot{SessionId="second",Stage="ready",Complete=true});Application.DoEvents();Check(presenter.CurrentWindow==null&&second.IsDisposed&&observations[1].Disposals==1,"actual ready closes loading window and releases observer");
     presenter.Handle(Request("manual","preparing"));Application.DoEvents();observations[2].Changed(new GameStartupSnapshot{SessionId="manual",Stage="manual"});Application.DoEvents();Check(presenter.CurrentWindow==null,"bounded manual handoff closes loading without ready");
     presenter.Handle(Request("third","preparing"));Application.DoEvents();var third=presenter.CurrentWindow;presenter.Dispose();Check(third.IsDisposed&&observations[3].Disposals==1,"launcher disposal releases its independent window and watcher");
     presenter.Handle(Request("fourth","preparing"));Check(windows==4,"disposed presenter never reopens a loading window");
    }
   }
  }
  static void DelayedPreflight(){
   string root=Path.Combine(Path.GetTempPath(),"ozl-immediate-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");Directory.CreateDirectory(game);Directory.CreateDirectory(Path.Combine(user,"mods"));File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx2g\"]}");File.WriteAllText(Path.Combine(user,"mods","default.txt"),"mods\n{\n}\nmaps\n{\n}\n");
   foreach(bool cancel in new[]{false,true})using(var owner=new Form())using(var entered=new ManualResetEvent(false))using(var release=new ManualResetEvent(false))using(var controller=new ModernController(game,user,data,true)){
    IntPtr handle=owner.Handle;Observation watch=null;int starts=0;
    var bound=new Engine(game,user,data,true,()=>{entered.Set();if(!release.WaitOne(5000))throw new Exception("fixture scan timed out");return new SteamLaunchOptionsSnapshot(null,0,null,SteamLaunchOptionsState.Ready,value:"");});typeof(ModernController).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,bound);
    using(var presenter=new GameStartupPresenter(owner,(request,callback)=>{starts++;watch=new Observation{Id=request.SessionId,Changed=callback};return watch;},request=>new GameStartupWindow(request,LauncherTheme.Create(false)){StartPosition=FormStartPosition.Manual,Location=new Point(-22000,-22000),ShowInTaskbar=false})){
     presenter.PrelaunchClosed=controller.CancelStartup;controller.StartupPresentation=presenter.Handle;
     var payload=GameStartupPresentationTests.Begin(controller);string id=(string)payload["startupId"];var first=presenter.CurrentWindow;Check(first!=null&&first.Visible&&starts==0,"explicit begin paints before actual preflight scan");
     Exception error=null;object result=null;var worker=new Thread(()=>{try{result=controller.Execute("launchPreflight",new Dictionary<string,object>{{"startupId",id}});}catch(Exception ex){error=ex;}});worker.Start();Check(entered.WaitOne(5000),"actual preflight reaches held Steam evidence read");Check(first.Visible&&starts==0,"delayed real preflight keeps same canvas without process observer");
     if(cancel)first.Close();release.Set();for(int i=0;i<300&&worker.IsAlive;i++){Application.DoEvents();Thread.Sleep(10);}Check(!worker.IsAlive,"preflight worker finishes without UI-lock deadlock");
     if(cancel){Check(error is OperationCanceledException&&presenter.CurrentWindow==null,"Esc during scan cancels late successful result and leaves no orphan");bool rejected=false;try{controller.RunStartupPresentation("launch",payload,()=>{controller.CommitStartup();return new object();});}catch{rejected=true;}Check(rejected&&starts==0,"cancelled preflight cannot later start launch monitor");}
     else{Check(error==null&&((LaunchPreflightReport)result).canLaunch,"held real preflight completes successfully");controller.RunStartupPresentation("launch",payload,()=>{controller.CommitStartup();return new object();});Check(ReferenceEquals(first,presenter.CurrentWindow)&&starts==1,"commit attaches original canvas/id and starts exactly one observer");watch.Changed(new GameStartupSnapshot{SessionId=id,Complete=true,Stage="ready"});Application.DoEvents();Check(presenter.CurrentWindow==null,"ready closes adopted window exactly once");}
     controller.StartupPresentation=null;
    }
   }
   using(var owner=new Form())using(var controller=new ModernController(game,user,data,true)){
    IntPtr handle=owner.Handle;int starts=0;using(var presenter=new GameStartupPresenter(owner,(request,callback)=>{starts++;return new Observation();},request=>new GameStartupWindow(request,LauncherTheme.Create(false)){Location=new Point(-22000,-22000)})){
     controller.StartupPresentation=presenter.Handle;var payload=GameStartupPresentationTests.Begin(controller);File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"invalid-json");var blocked=(LaunchPreflightReport)controller.Execute("launchPreflight",payload);Check(!blocked.canLaunch&&presenter.CurrentWindow==null&&starts==0,"blocked actual preflight closes native cover before returning repair report");controller.StartupPresentation=null;
    }
   }
  }
  [STAThread] public static int Main(){try{Run();DelayedPreflight();return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
