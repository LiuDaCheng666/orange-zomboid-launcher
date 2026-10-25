using System;
using System.Collections.Generic;
namespace PZ3D {
 sealed partial class ModernController {
  internal Action<GameStartupLaunch> StartupPresentation;
  string startupSessionId;

  sealed class StartupAttempt {
   internal GameStartupLaunch Launch;internal Engine Engine;internal string TargetId,Phase;internal DateTime Deadline;internal bool Reserved;
  }
  readonly object startupGate=new object();StartupAttempt startupAttempt;System.Threading.Timer startupLease;
  internal static bool StartupControl(string action){return action=="startupBegin"||action=="startupCancel"||action=="startupYield"||action=="startupResume";}
  internal object StartupControlRequest(string action,Dictionary<string,object> payload){
   ModernHostPolicy.ValidateStartupPayload(action,payload);StartupAttempt selected;
   if(action=="startupBegin"){
    lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");if(localMutationBusy)throw new InvalidOperationException("正在执行任务，请稍后启动。");}
    lock(startupGate){
     if(startupAttempt!=null&&startupAttempt.Phase!="cancelled"&&startupAttempt.Phase!="submitted")throw new InvalidOperationException("已有启动准备正在进行。");
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
   if(current==null||current.Launch.SessionId!=id||current.Engine!=engine||current.Phase=="cancelled"||current.Phase=="submitted"||current.Deadline<DateTime.UtcNow)throw new OperationCanceledException("本次启动准备已取消或失效，请重新点击启动。");
   return current;
  }
  internal void CancelStartup(string id){StartupAttempt selected;lock(startupGate){selected=startupAttempt;if(selected==null||selected.Launch.SessionId!=id||selected.Phase=="committed"||selected.Phase=="submitted"||selected.Phase=="cancelled")return;selected.Phase="cancelled";}PublishStartup(selected.Launch,"cancelled","");}
  internal void CancelPendingStartup(){string id;lock(startupGate)id=startupAttempt==null?null:startupAttempt.Launch.SessionId;if(id!=null)CancelStartup(id);}
  internal void ExpireStartup(DateTime now){string id=null;lock(startupGate){if(startupAttempt!=null&&startupAttempt.Phase!="committed"&&startupAttempt.Phase!="submitted"&&startupAttempt.Phase!="cancelled"&&startupAttempt.Deadline<=now)id=startupAttempt.Launch.SessionId;}if(id!=null)CancelStartup(id);}
  internal void DisposeStartup(){CancelPendingStartup();lock(startupGate){if(startupLease!=null){startupLease.Dispose();startupLease=null;}}}
  internal LaunchPreflightReport StartupPreflight(Dictionary<string,object> payload){
   string kind=ModernHostPolicy.ValidatePreflightPayload(payload);string id=payload.ContainsKey("startupId")?Text(payload,"startupId"):"";StartupAttempt selected=null;Engine selectedEngine;bool blocked;
   lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");selectedEngine=engine;blocked=busy||networkRequests.Count>0;}
   if(id!="")lock(startupGate){selected=RequireAttempt(id);if(selected.Launch.TargetKind!=kind||selected.Reserved)throw new InvalidOperationException("启动目标已变化。");selected.Deadline=DateTime.UtcNow.AddSeconds(120);}
   try{
    var result=LaunchPreflight.Check(selectedEngine,blocked,ClientRunning(),kind);
    if(selected!=null){lock(startupGate){RequireAttempt(id);selected.Deadline=DateTime.UtcNow.AddSeconds(30);}if(!result.canLaunch)CancelStartup(id);}
    return result;
   }catch{if(id!="")CancelStartup(id);throw;}
  }
  internal void CommitStartup(){
   StartupAttempt selected;lock(startupGate){selected=RequireAttempt(startupSessionId);if(!selected.Reserved||selected.Phase=="committed")throw new InvalidOperationException("启动请求已被使用。");selected.Phase="committed";selected.Launch.StartedUtc=DateTime.UtcNow;}
   PublishStartup(selected.Launch,"preparing","正在启动游戏…");
  }

  // Presentation is an observer: it never owns the launch or its configuration transaction.
  internal object RunStartupPresentation(string action,Dictionary<string,object> payload,Func<object> launch){
   if(action!="launch"&&action!="profileLaunch")return launch();
   string id=Text(payload??new Dictionary<string,object>(),"startupId");
   StartupAttempt selected;string kind=action=="launch"?"normal":Text(payload,"kind"),target=action=="launch"?"":Text(payload,"id");
   lock(startupGate){selected=RequireAttempt(id);if(selected.Reserved||selected.Phase=="yielded"||selected.Launch.TargetKind!=kind||selected.TargetId!=target)throw new InvalidOperationException("启动准备与目标不一致或已被使用。");selected.Reserved=true;selected.Deadline=DateTime.UtcNow.AddSeconds(120);}
   try{startupSessionId=id;object result=launch();lock(startupGate){if(selected.Phase!="committed")throw new OperationCanceledException("本次启动未提交。");selected.Phase="submitted";}PublishStartup(selected.Launch,"submitted","游戏正在启动…");return result;}
   catch(Exception error){bool committed;lock(startupGate){committed=selected.Phase=="committed";selected.Phase="cancelled";}PublishStartup(selected.Launch,committed?"failed":"cancelled",GameDiagnostics.Sanitize(error.Message));throw;}
   finally{startupSessionId=null;}
  }
  void PublishStartup(GameStartupLaunch request,string stage,string message){
   var callback=StartupPresentation;if(callback==null)return;
   try{callback(new GameStartupLaunch{SessionId=request.SessionId,GamePath=request.GamePath,UserPath=request.UserPath,DataPath=request.DataPath,TargetKind=request.TargetKind,StartedUtc=request.StartedUtc,Stage=stage,Message=message});}
   catch(Exception error){try{journal.Diagnostic("启动加载窗口未能更新："+GameDiagnostics.Sanitize(error.Message));}catch{}}
  }
 }
}
