using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace PZ3D {
 // Owns presentation only. Launch transactions and game processes remain independent.
 sealed class GameStartupPresenter:IDisposable {
  readonly Form owner;
  internal Action<string> PrelaunchClosed;
  readonly HashSet<string> ended=new HashSet<string>();
  readonly Queue<string> endedOrder=new Queue<string>();
  bool clearing;
  readonly Action<GameStartupLaunch,GameStartupSnapshot,GameStartupWindow> handoff;
  GameStartupWindowPolicy windowPolicy;readonly bool coordinate;GameStartupLaunch active;GameStartupSnapshot latest;bool handedOff;
  readonly Func<GameStartupLaunch,Action<GameStartupSnapshot>,IGameStartupMonitor> monitorFactory;
  readonly Func<GameStartupLaunch,GameStartupWindow> windowFactory;
  IGameStartupMonitor observation;GameStartupWindow window;string sessionId;bool disposed,yielded;
  internal GameStartupWindow CurrentWindow {get{return window;}}

  internal GameStartupPresenter(Form owner):this(owner,(launch,changed)=>new GameStartupMonitor(launch,changed),launch=>new GameStartupWindow(launch,LauncherTheme.Load(launch.DataPath)){StartPosition=FormStartPosition.Manual,Bounds=Screen.FromControl(owner).Bounds},GameStartupHandoff.Release,true){}
  internal GameStartupPresenter(Form owner,Func<GameStartupLaunch,Action<GameStartupSnapshot>,IGameStartupMonitor> monitorFactory,Func<GameStartupLaunch,GameStartupWindow> windowFactory,Action<GameStartupLaunch,GameStartupSnapshot,GameStartupWindow> handoff=null,bool coordinate=false){
   if(owner==null||monitorFactory==null||windowFactory==null)throw new ArgumentNullException();
   this.coordinate=coordinate;this.handoff=handoff;this.owner=owner;this.monitorFactory=monitorFactory;this.windowFactory=windowFactory;
  }

  internal void Handle(GameStartupLaunch launch){
   if(disposed||launch==null||owner.IsDisposed||!owner.IsHandleCreated)return;
   // Preparing is synchronous so the log boundary and observer exist before Steam is opened.
   try{if(owner.InvokeRequired)owner.Invoke(new Action(()=>HandleOnUi(launch)));else HandleOnUi(launch);}
   catch(InvalidOperationException){ /* The launcher may have closed while a task completed. */ }
  }

  void RememberEnd(string id){if(id==null||!ended.Add(id))return;endedOrder.Enqueue(id);while(endedOrder.Count>128)ended.Remove(endedOrder.Dequeue());}
  void HandleOnUi(GameStartupLaunch launch){
   if(disposed||owner.IsDisposed)return;
   try{
    if(launch.Stage=="cancelled"){RememberEnd(launch.SessionId);if(sessionId==launch.SessionId)Clear();return;}
    if(ended.Contains(launch.SessionId))return;
    if(launch.Stage=="preflight"||launch.Stage=="preparing"){
     if(sessionId!=launch.SessionId||window==null){
      Clear();yielded=false;handedOff=false;windowPolicy=new GameStartupWindowPolicy();active=launch;latest=null;sessionId=launch.SessionId;window=windowFactory(launch);
      var view=window;string id=launch.SessionId;
      view.FormClosing+=(sender,args)=>{if(!clearing&&ReferenceEquals(window,view)){Release();if(observation==null){RememberEnd(id);var callback=PrelaunchClosed;if(callback!=null)callback(id);}}};
      view.FormClosed+=(sender,args)=>{if(ReferenceEquals(window,view)){Release();Clear();}};
      window.Show(owner);if(owner.Visible&&owner.ContainsFocus)window.Activate();window.Refresh();
     }
     if(launch.Stage=="preparing"&&observation==null){
      active=launch;IGameStartupMonitor selected=null;selected=monitorFactory(launch,snapshot=>QueueSnapshot(selected,snapshot));observation=selected;selected.Start();
     }
    }else if(sessionId==launch.SessionId&&window!=null){
     if(launch.Stage=="yield"){window.Hide();return;}
     if(launch.Stage=="resume"){if(!yielded){window.Show(owner);if(owner.Visible&&owner.ContainsFocus)window.Activate();}return;}
     if(observation!=null){if(launch.Stage=="submitted")observation.Submitted();else if(launch.Stage=="failed")observation.Fail(launch.Message);}
    }
   }catch{Clear();}
  }

  void QueueSnapshot(IGameStartupMonitor selected,GameStartupSnapshot snapshot){
   if(disposed||snapshot==null||owner.IsDisposed||!owner.IsHandleCreated)return;
   // Never synchronously invoke from the monitor's state lock: closing the form
   // disposes that monitor on the UI thread and must not deadlock the two threads.
   try{owner.BeginInvoke(new Action(()=>{
    if(disposed||!ReferenceEquals(observation,selected)||window==null||window.IsDisposed||snapshot.SessionId!=sessionId)return;
    latest=snapshot;window.UpdateSnapshot(snapshot);
    if(coordinate&&!yielded&&!snapshot.Complete&&!snapshot.Failed&&!snapshot.WaitingForInput&&snapshot.Stage!="manual"&&snapshot.ProcessId.HasValue){
     if(!GameStartupHandoff.Cover(active,snapshot,window,windowPolicy)){yielded=true;window.Hide();}
    }
    if(snapshot.WaitingForInput){yielded=true;Release();if(window.Visible)window.Hide();}
    else if(!yielded&&!window.Visible&&!snapshot.Complete)window.Show(owner);
    if((snapshot.Complete&&!snapshot.Failed&&!snapshot.WaitingForInput)||snapshot.Stage=="manual"){Release();Clear();}
   }));}catch(InvalidOperationException){}
  }

  void Release(){if(handedOff||disposed)return;handedOff=true;if(handoff!=null)try{handoff(active,latest,window);}catch{} }

  void Clear(){
   var old=observation;var view=window;observation=null;window=null;sessionId=null;
   clearing=true;try{if(view!=null&&!view.IsDisposed)view.Close();}finally{clearing=false;}
   if(old!=null)old.Dispose();
  }

  public void Dispose(){if(disposed)return;disposed=true;Clear();}
 }
 sealed class GameStartupWindowPolicy {
  long lastHandle;bool released;
  internal string Decide(long handle,bool gameForeground,bool loadingForeground){
   if(released)return "yield";
   if(!gameForeground&&!loadingForeground){released=true;return "yield";}
   if(gameForeground&&lastHandle==handle){released=true;return "yield";}
   if(lastHandle!=handle){lastHandle=handle;return "cover";}
   return "keep";
  }
 }
 // This never hides, reparents, minimizes, or terminates a game. Windows can
 // refuse activation; no focus retry loop runs after the user switches apps.
 static class GameStartupHandoff {
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
  internal static bool Cover(GameStartupLaunch launch,GameStartupSnapshot snapshot,GameStartupWindow view,GameStartupWindowPolicy policy){
   try{
    using(var process=Process.GetProcessById(snapshot.ProcessId.Value)){
     IntPtr handle=VerifiedHandle(launch,snapshot,process);if(handle==IntPtr.Zero)return true;
     IntPtr foreground=GetForegroundWindow();string decision=policy.Decide(handle.ToInt64(),foreground==handle,foreground==view.Handle);
     if(decision=="yield")return false;
     if(decision=="cover"){
      // Preserve rendering and taskbar access. Position only the verified game
      // immediately behind our own loading window, without activation or resize.
      if(!SetWindowPos(handle,view.Handle,0,0,0,0,0x0010|0x0001|0x0002))return false;
      if(foreground==handle&&!SetForegroundWindow(view.Handle))return false;
     }
     return true;
    }
   }catch{return false;}
  }
  static IntPtr VerifiedHandle(GameStartupLaunch launch,GameStartupSnapshot snapshot,Process process){
   if(process.HasExited)return IntPtr.Zero;string name=process.ProcessName,image=process.MainModule.FileName;bool java=name=="java"||name=="javaw";
   if(!IdentityMatches(launch,snapshot,process.Id,process.StartTime.ToUniversalTime().Ticks,name,image,!java||GameProcessGuard.ReadJavaRole(launch.GamePath,name,image,process)==JavaProcessRole.Client))return IntPtr.Zero;
   IntPtr handle=process.MainWindowHandle;uint pid;return handle!=IntPtr.Zero&&GetWindowThreadProcessId(handle,out pid)!=0&&pid==process.Id?handle:IntPtr.Zero;
  }
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
  [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr window,int command);
  internal static bool IdentityMatches(GameStartupLaunch launch,GameStartupSnapshot snapshot,int pid,long birth,string name,string image,bool javaClient){return launch!=null&&snapshot!=null&&snapshot.SessionId==launch.SessionId&&snapshot.ProcessId==pid&&snapshot.ProcessBirth==birth&&birth>0&&GameStartupMonitor.MatchesClientIdentity(launch.GamePath,launch.StartedUtc,name,image,new DateTime(birth,DateTimeKind.Utc),javaClient);}
  internal static void Release(GameStartupLaunch launch,GameStartupSnapshot snapshot,GameStartupWindow view){
   if(view==null||view.IsDisposed||!view.IsHandleCreated||GetForegroundWindow()!=view.Handle||snapshot==null||!snapshot.ProcessId.HasValue)return;
   using(var process=Process.GetProcessById(snapshot.ProcessId.Value)){
    IntPtr handle=VerifiedHandle(launch,snapshot,process);if(handle==IntPtr.Zero)return;
    if(IsIconic(handle))ShowWindowAsync(handle,9);
    SetForegroundWindow(handle);
   }
  }
 }
}
