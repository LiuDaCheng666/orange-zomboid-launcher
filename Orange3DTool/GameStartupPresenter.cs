using System;
using System.Windows.Forms;
namespace PZ3D {
 // Owns presentation only. Launch transactions and game processes remain independent.
 sealed class GameStartupPresenter:IDisposable {
  readonly Form owner;
  readonly Func<GameStartupLaunch,Action<GameStartupSnapshot>,IGameStartupMonitor> monitorFactory;
  readonly Func<GameStartupLaunch,GameStartupWindow> windowFactory;
  IGameStartupMonitor observation;GameStartupWindow window;string sessionId;bool disposed;
  internal GameStartupWindow CurrentWindow {get{return window;}}

  internal GameStartupPresenter(Form owner):this(owner,(launch,changed)=>new GameStartupMonitor(launch,changed),launch=>new GameStartupWindow(launch,LauncherTheme.Load(launch.DataPath)){StartPosition=FormStartPosition.Manual,Bounds=Screen.FromControl(owner).Bounds}){}
  internal GameStartupPresenter(Form owner,Func<GameStartupLaunch,Action<GameStartupSnapshot>,IGameStartupMonitor> monitorFactory,Func<GameStartupLaunch,GameStartupWindow> windowFactory){
   if(owner==null||monitorFactory==null||windowFactory==null)throw new ArgumentNullException();
   this.owner=owner;this.monitorFactory=monitorFactory;this.windowFactory=windowFactory;
  }

  internal void Handle(GameStartupLaunch launch){
   if(disposed||launch==null||owner.IsDisposed||!owner.IsHandleCreated)return;
   // Preparing is synchronous so the log boundary and observer exist before Steam is opened.
   try{if(owner.InvokeRequired)owner.Invoke(new Action(()=>HandleOnUi(launch)));else HandleOnUi(launch);}
   catch(InvalidOperationException){ /* The launcher may have closed while a task completed. */ }
  }

  void HandleOnUi(GameStartupLaunch launch){
   if(disposed||owner.IsDisposed)return;
   try{
    if(launch.Stage=="preparing"){
     if(sessionId==launch.SessionId&&window!=null)return;
     Clear();sessionId=launch.SessionId;window=windowFactory(launch);
     IGameStartupMonitor selected=null;
     selected=monitorFactory(launch,snapshot=>QueueSnapshot(selected,snapshot));observation=selected;
     window.FormClosed+=(sender,args)=>{if(ReferenceEquals(observation,selected))Clear();};
     window.Show(owner);selected.Start();
    }else if(sessionId==launch.SessionId&&observation!=null){
     if(launch.Stage=="submitted")observation.Submitted();
     else if(launch.Stage=="failed")observation.Fail(launch.Message);
    }
   }catch{Clear(); /* A failed view cannot fail or terminate the actual game launch. */ }
  }

  void QueueSnapshot(IGameStartupMonitor selected,GameStartupSnapshot snapshot){
   if(disposed||snapshot==null||owner.IsDisposed||!owner.IsHandleCreated)return;
   // Never synchronously invoke from the monitor's state lock: closing the form
   // disposes that monitor on the UI thread and must not deadlock the two threads.
   try{owner.BeginInvoke(new Action(()=>{
    if(disposed||!ReferenceEquals(observation,selected)||window==null||window.IsDisposed||snapshot.SessionId!=sessionId)return;
    window.UpdateSnapshot(snapshot);
    if(snapshot.WaitingForInput){if(window.Visible)window.Hide();}
    else if(!window.Visible&&!snapshot.Complete)window.Show(owner);
    if(snapshot.Complete&&!snapshot.Failed&&!snapshot.WaitingForInput)Clear();
   }));}catch(InvalidOperationException){}
  }

  void Clear(){
   var old=observation;var view=window;observation=null;window=null;sessionId=null;
   if(view!=null&&!view.IsDisposed)view.Close();
   if(old!=null)old.Dispose();
  }

  public void Dispose(){if(disposed)return;disposed=true;Clear();}
 }
}
