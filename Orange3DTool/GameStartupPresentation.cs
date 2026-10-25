using System;
using System.Collections.Generic;
namespace PZ3D {
 sealed partial class ModernController {
  internal Action<GameStartupLaunch> StartupPresentation;
  string startupSessionId;

  // Presentation is an observer: it never owns the launch or its configuration transaction.
  internal object RunStartupPresentation(string action,Dictionary<string,object> payload,Func<object> launch){
   if(action!="launch"&&action!="profileLaunch")return launch();
   string kind=action=="launch"?"normal":Text(payload??new Dictionary<string,object>(),"kind");
   if(kind!="save"&&kind!="server")kind="normal";
   var request=new GameStartupLaunch{SessionId=Guid.NewGuid().ToString("N"),GamePath=engine.Game,UserPath=user,DataPath=data,TargetKind=kind,StartedUtc=DateTime.UtcNow};
   PublishStartup(request,"preparing","正在核对启动条件，准备本次游戏…");
   try{startupSessionId=request.SessionId;object result=launch();PublishStartup(request,"submitted","启动请求已提交，正在等待本次游戏进程…");return result;}
   catch(Exception error){PublishStartup(request,"failed",GameDiagnostics.Sanitize(error.Message));throw;}
   finally{startupSessionId=null;}
  }

  void PublishStartup(GameStartupLaunch request,string stage,string message){
   var callback=StartupPresentation;if(callback==null)return;
   try{callback(new GameStartupLaunch{SessionId=request.SessionId,GamePath=request.GamePath,UserPath=request.UserPath,DataPath=request.DataPath,TargetKind=request.TargetKind,StartedUtc=request.StartedUtc,Stage=stage,Message=message});}
   catch(Exception error){try{journal.Diagnostic("启动加载窗口未能更新："+GameDiagnostics.Sanitize(error.Message));}catch{}}
  }
 }
}
