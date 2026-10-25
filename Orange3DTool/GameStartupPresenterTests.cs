using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
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
   using(var owner=new Form()){
    IntPtr handle=owner.Handle;var observations=new List<Observation>();int windows=0;
    using(var presenter=new GameStartupPresenter(owner,(request,callback)=>{var watch=new Observation{Id=request.SessionId,Changed=callback};observations.Add(watch);return watch;},request=>{
     windows++;return new GameStartupWindow(request,LauncherTheme.Create(false)){StartPosition=FormStartPosition.Manual,Location=new Point(-22000,-22000),ShowInTaskbar=false};
    })){
     presenter.Handle(Request("first","preparing"));Application.DoEvents();
     Check(windows==1&&observations[0].Starts==1&&presenter.CurrentWindow!=null&&presenter.CurrentWindow.Visible,"preparing creates and starts one independent native observer window");
     presenter.Handle(Request("first","preparing"));Check(windows==1,"duplicate preparing event cannot create another window");
     presenter.Handle(Request("first","submitted"));Check(observations[0].Submits==1,"submitted lifecycle reaches active observer");
     var first=presenter.CurrentWindow;
     var worker=new Thread(()=>observations[0].Changed(new GameStartupSnapshot{SessionId="first",Stage="mods",Message="真实后台阶段",Lines=new[]{"registered module"}}));worker.Start();worker.Join();Application.DoEvents();
     Check(Text(first,"startup-message")=="真实后台阶段","background monitor snapshots reach native UI thread");
     observations[0].Changed(new GameStartupSnapshot{SessionId="first",Stage="approval",Message="请在游戏中核对授权",WaitingForInput=true});Application.DoEvents();Check(!first.Visible&&!first.IsDisposed,"fullscreen observer yields while game requires authorization");
     observations[0].Changed(new GameStartupSnapshot{SessionId="first",Stage="mods",Message="继续加载模组"});Application.DoEvents();Check(first.Visible,"loading canvas returns after authorization completes");
     observations[0].Changed(new GameStartupSnapshot{SessionId="forged",Complete=true});Application.DoEvents();Check(presenter.CurrentWindow==first,"wrong session cannot close active loading window");
     presenter.Handle(Request("first","failed"));Application.DoEvents();Check(presenter.CurrentWindow==first&&!first.IsDisposed,"failed startup remains available for error inspection");
     first.Close();Application.DoEvents();Check(presenter.CurrentWindow==null&&observations[0].Disposals==1,"closing window disposes only the observer exactly once");
     presenter.Handle(Request("second","preparing"));Application.DoEvents();var second=presenter.CurrentWindow;
     observations[0].Changed(new GameStartupSnapshot{SessionId="first",Complete=true});Application.DoEvents();Check(presenter.CurrentWindow==second,"late completed snapshot from closed session cannot close new one");
     observations[1].Changed(new GameStartupSnapshot{SessionId="second",Stage="ready",Complete=true});Application.DoEvents();Check(presenter.CurrentWindow==null&&second.IsDisposed&&observations[1].Disposals==1,"actual ready closes loading window and releases observer");
     presenter.Handle(Request("third","preparing"));Application.DoEvents();var third=presenter.CurrentWindow;presenter.Dispose();Check(third.IsDisposed&&observations[2].Disposals==1,"launcher disposal releases its independent window and watcher");
     presenter.Handle(Request("fourth","preparing"));Check(windows==3,"disposed presenter never reopens a loading window");
    }
   }
  }
  [STAThread] public static int Main(){try{Run();return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
