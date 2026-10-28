using System;
using System.Collections.Generic;
using System.Linq;
namespace PZ3D {
 sealed class GameStartupPending {
  public string startupId,phase,deadlineUtc,message;public bool pending;
 }
 sealed partial class ModernController {
  internal Action<GameStartupLaunch> StartupPresentation;
  string startupSessionId;

  sealed class StartupAttempt {
   internal GameStartupLaunch Launch;internal Engine Engine,ResolvedEngine;internal string TargetId,Phase;internal DateTime Deadline;internal bool Reserved;
  }
  readonly object startupGate=new object();StartupAttempt startupAttempt;System.Threading.Timer startupLease;
  internal static bool StartupControl(string action){return action=="startupBegin"||action=="startupCancel"||action=="startupYield"||action=="startupResume"||action=="startupStatus";}
  internal object StartupControlRequest(string action,Dictionary<string,object> payload){
   ModernHostPolicy.ValidateStartupPayload(action,payload);StartupAttempt selected;
   if(action=="startupStatus")return new{startup=StartupStatus(),running=ClientRunning()};
   if(action=="startupBegin"){
    lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");if(localMutationBusy)throw new InvalidOperationException("正在执行任务，请稍后启动。");RefreshSubmittedStartup(DateTime.UtcNow);lock(startupGate){if(startupAttempt!=null&&startupAttempt.Engine==engine&&ActiveStartup(startupAttempt))throw new InvalidOperationException("本次启动仍在等待游戏进程，请勿重复启动。");}RecoverStartup();}
    lock(startupGate){
     if(startupAttempt!=null&&startupAttempt.Engine==engine&&ActiveStartup(startupAttempt))throw new InvalidOperationException("已有启动准备正在进行。");
     selected=new StartupAttempt{Engine=engine,TargetId=Text(payload,"targetId"),Phase="preflight",Deadline=DateTime.UtcNow.AddSeconds(30),Launch=new GameStartupLaunch{SessionId=Guid.NewGuid().ToString("N"),GamePath=engine.Game,UserPath=user,DataPath=data,TargetKind=Text(payload,"targetKind"),StartedUtc=DateTime.UtcNow}};startupAttempt=selected;
     if(startupLease==null)startupLease=new System.Threading.Timer(_=>ExpireStartup(DateTime.UtcNow),null,1000,1000);
    }
    PublishStartup(selected.Launch,"preflight","正在核对启动条件…");
    lock(startupGate){RequireAttempt(selected.Launch.SessionId);}
    return new{startupId=selected.Launch.SessionId};
   }
   string id=Text(payload,"startupId");
   if(action=="startupCancel"){CancelStartup(id);return new{cancelled=true};}
   lock(startupGate){selected=RequireAttempt(id);if(selected.Reserved)throw new InvalidOperationException("启动已经提交，不能切换准备画面。");selected.Phase=action=="startupYield"?"yielded":"preflight";selected.Deadline=DateTime.UtcNow.AddSeconds(action=="startupYield"?300:30);}
   PublishStartup(selected.Launch,action=="startupYield"?"yield":"resume","");return new{startupId=id};
  }
  StartupAttempt RequireAttempt(string id){
   var current=startupAttempt;
   if(current==null||current.Launch.SessionId!=id||current.Engine!=engine||!ActiveStartup(current)||current.Phase=="submitted"||current.Deadline<DateTime.UtcNow)throw new OperationCanceledException("本次启动准备已取消或失效，请重新点击启动。");
   return current;
  }
  internal void CancelStartup(string id){StartupAttempt selected;lock(startupGate){selected=startupAttempt;if(selected==null||selected.Launch.SessionId!=id||selected.Phase=="committed"||selected.Phase=="submitted"||selected.Phase=="cancelled")return;selected.Phase="cancelled";}PublishStartup(selected.Launch,"cancelled","");}
  internal void CancelPendingStartup(){string id;lock(startupGate)id=startupAttempt==null?null:startupAttempt.Launch.SessionId;if(id!=null)CancelStartup(id);}
  internal void ExpireStartup(DateTime now){RefreshSubmittedStartup(now,false);string id=null;lock(startupGate){if(startupAttempt!=null&&startupAttempt.Phase!="committed"&&startupAttempt.Phase!="submitted"&&ActiveStartup(startupAttempt)&&startupAttempt.Deadline<=now)id=startupAttempt.Launch.SessionId;}if(id!=null)CancelStartup(id);}
  static bool ActiveStartup(StartupAttempt selected){return selected.Phase=="preflight"||selected.Phase=="yielded"||selected.Phase=="committed"||selected.Phase=="submitted";}
  // URI acceptance is not client appearance. Keep the same bounded Steam grace
  // as the durable session watcher, and bind only verified path/birth evidence.
  internal void ReconcileSubmittedStartup(string id,DateTime now,IEnumerable<ClientIdentity> clients){lock(startupGate){var selected=startupAttempt;if(selected==null||selected.Engine!=engine||selected.Launch.SessionId!=id||selected.Phase!="submitted")return;
   bool found=clients.Any(client=>client.Pid>0&&client.Birth>=selected.Launch.StartedUtc.Ticks&&client.Birth<=now.AddSeconds(5).Ticks&&(GameProcessGuard.MatchesNative(selected.Launch.GamePath,client.Image)||client.Java&&GameProcessGuard.MatchesJava(selected.Launch.GamePath,System.IO.Path.GetFileNameWithoutExtension(client.Image),client.Image)));
   if(found)selected.Phase="observed";else if(selected.Deadline<=now)selected.Phase="timeout";
  }}
  void RefreshSubmittedStartup(DateTime now,bool adoptDurable=true){StartupAttempt selected;Engine selectedEngine=engine;if(disposed||selectedEngine==null)return;lock(startupGate)selected=startupAttempt;
   if(adoptDurable&&(selected==null||selected.Engine!=selectedEngine||!ActiveStartup(selected))){
    GameStartupLaunch durable=LaunchProfiles.PendingStartup(selectedEngine,now);
    if(durable!=null)lock(startupGate){if(startupAttempt==selected&&(selected==null||selected.Engine!=selectedEngine||selected.Launch.SessionId!=durable.SessionId)){selected=new StartupAttempt{Engine=selectedEngine,Launch=durable,Phase="submitted",Reserved=true,Deadline=durable.StartedUtc.AddSeconds(180)};startupAttempt=selected;if(startupLease==null)startupLease=new System.Threading.Timer(_=>ExpireStartup(DateTime.UtcNow),null,1000,1000);}}
   }
   if(selected==null||selected.Engine!=selectedEngine||selected.Phase!="submitted")return;
   var clients=new List<ClientIdentity>();if(!selectedEngine.TestMode)try{clients=GameProcessGuard.Clients(selected.Launch.GamePath,selectedEngine.ProcessInventory);}catch{ /* Unknown evidence retains the lease only until its bounded deadline. */ }
   ReconcileSubmittedStartup(selected.Launch.SessionId,now,clients);
  }
  internal GameStartupPending StartupStatus(){if(configurationReadOnly)return new GameStartupPending{startupId="",phase="idle",deadlineUtc="",message="",pending=false};RefreshSubmittedStartup(DateTime.UtcNow);lock(startupGate){var selected=startupAttempt;if(selected==null||selected.Engine!=engine)return new GameStartupPending{startupId="",phase="idle",deadlineUtc="",message="",pending=false};bool waiting=selected.Phase=="submitted"||selected.Phase=="committed";return new GameStartupPending{startupId=selected.Launch.SessionId,phase=selected.Phase,deadlineUtc=selected.Deadline.ToString("o"),pending=waiting,message=waiting?"启动请求已提交，正在等待本次游戏进程…":selected.Phase=="timeout"?"等待游戏进程超时，请核对 Steam 或游戏窗口后重试。":""};}}
  bool SubmittedStartupPending(){return StartupStatus().pending;}
  internal void DisposeStartup(){CancelPendingStartup();lock(startupGate){if(startupLease!=null){startupLease.Dispose();startupLease=null;}}}
  internal LaunchPreflightReport StartupPreflight(Dictionary<string,object> payload){
   string kind=ModernHostPolicy.ValidatePreflightPayload(payload);string id=payload.ContainsKey("startupId")?Text(payload,"startupId"):"";StartupAttempt selected=null;Engine selectedEngine;bool blocked;
   lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");selectedEngine=engine;blocked=busy||networkRequests.Count>0;}
   if(id!="")lock(startupGate){selected=RequireAttempt(id);if(selected.Launch.TargetKind!=kind||selected.Reserved)throw new InvalidOperationException("启动目标已变化。");selected.Deadline=DateTime.UtcNow.AddSeconds(120);}
   try{
    if(selected!=null){selectedEngine=ResolveEnvironmentEngine(selected.Engine,kind,selected.TargetId);selected.ResolvedEngine=selectedEngine;selected.Launch.GamePath=selectedEngine.Game;selected.Launch.UserPath=selectedEngine.User;RecoverLaunchEnvironment(selectedEngine,kind,selected.TargetId);}
    var result=LaunchPreflight.CheckLaunch(selectedEngine,blocked,ClientRunning(),kind,selected==null?null:selected.TargetId,false);
    if(selected!=null){lock(startupGate){RequireAttempt(id);selected.Deadline=DateTime.UtcNow.AddSeconds(30);}if(!result.canLaunch)CancelStartup(id);}
    return result;
   }catch{if(id!="")CancelStartup(id);throw;}
  }
  internal void CommitStartup(){
   StartupAttempt selected;lock(startupGate){selected=RequireAttempt(startupSessionId);if(!selected.Reserved||selected.Phase=="committed")throw new InvalidOperationException("启动请求已被使用。");selected.Phase="committed";selected.Launch.StartedUtc=DateTime.UtcNow;}
   var watch=System.Diagnostics.Stopwatch.StartNew();PublishStartup(selected.Launch,"preparing","正在启动游戏…");LaunchTiming("startup-observer",watch.ElapsedMilliseconds);
  }

  // Presentation is an observer: it never owns the launch or its configuration transaction.
  internal object RunStartupPresentation(string action,Dictionary<string,object> payload,Func<object> launch){
   if(action!="launch"&&action!="profileLaunch")return launch();
   string id=Text(payload??new Dictionary<string,object>(),"startupId");
   StartupAttempt selected;string kind=action=="launch"?"normal":Text(payload,"kind"),target=action=="launch"?"":Text(payload,"id");
   lock(startupGate){selected=RequireAttempt(id);if(selected.Reserved||selected.Phase=="yielded"||selected.Launch.TargetKind!=kind||selected.TargetId!=target)throw new InvalidOperationException("启动准备与目标不一致或已被使用。");selected.Reserved=true;selected.Deadline=DateTime.UtcNow.AddSeconds(120);}
   var watch=System.Diagnostics.Stopwatch.StartNew();
   try{startupSessionId=id;selected.ResolvedEngine=ResolveEnvironmentEngine(selected.Engine,kind,target);selected.Launch.GamePath=selected.ResolvedEngine.Game;selected.Launch.UserPath=selected.ResolvedEngine.User;RememberEnvironment(selected.ResolvedEngine);object result=launch();lock(startupGate){if(selected.Phase!="committed")throw new OperationCanceledException("本次启动未提交。");selected.Phase="submitted";selected.Deadline=selected.Launch.StartedUtc.AddSeconds(180);}var submitted=System.Diagnostics.Stopwatch.StartNew();PublishStartup(selected.Launch,"submitted","游戏正在启动…");LaunchTiming("startup-submitted-observer",submitted.ElapsedMilliseconds);var outcome=result as GameLaunchOutcome;if(outcome!=null)outcome.startup=StartupStatus();return result;}
   catch(Exception error){bool committed;lock(startupGate){committed=selected.Phase=="committed";selected.Phase="cancelled";}PublishStartup(selected.Launch,committed?"failed":"cancelled",GameDiagnostics.Sanitize(error.Message));throw;}
   finally{LaunchTiming("native-launch-total",watch.ElapsedMilliseconds);startupSessionId=null;}
  }
  void PublishStartup(GameStartupLaunch request,string stage,string message){
   var callback=StartupPresentation;if(callback==null)return;
   try{if(stage!="cancelled"&&!GameStartupComponent.Enabled(engine))return;callback(new GameStartupLaunch{SessionId=request.SessionId,GamePath=request.GamePath,UserPath=request.UserPath,DataPath=request.DataPath,TargetKind=request.TargetKind,StartedUtc=request.StartedUtc,Stage=stage,Message=message});}
   catch(Exception error){try{journal.Diagnostic("启动加载窗口未能更新："+GameDiagnostics.Sanitize(error.Message));}catch{}}
  }
 }
}
