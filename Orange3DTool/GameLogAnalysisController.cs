using System;
using System.Collections.Generic;
namespace PZ3D {
 sealed partial class ModernController {
  GameLogAnalysisService logAnalysis;
  internal static bool LogAnalysisAction(string action){return action=="logAnalysisStatus"||action=="logAnalysisLocal"||action=="logAnalysisImport"||action=="logAnalysisFolder"||action=="logAnalysisCancel"||action=="logAnalysisExport";}
  internal static void ValidateLogAnalysisPayload(Dictionary<string,object> payload){if(payload==null||payload.Count!=0)throw new InvalidOperationException("日志分析只接受空参数；来源和导出位置由本机选择窗口确认。");}
  GameLogAnalysisService Logs(){if(disposed)throw new ObjectDisposedException("ModernController");if(logAnalysis==null)logAnalysis=new GameLogAnalysisService(user,engine.Game);return logAnalysis;}
  internal GameLogAnalysisStatus LogAnalysisImport(string[] paths){lock(gate)return Logs().StartImport(paths);}
  internal GameLogAnalysisStatus LogAnalysisStatus(){lock(gate)return Logs().Status();}
  object ExecuteLogAnalysis(string action,Dictionary<string,object> payload){
   ValidateLogAnalysisPayload(payload);lock(gate){var service=Logs();if(action=="logAnalysisStatus")return service.Status();if(action=="logAnalysisCancel")return service.Cancel();if(action=="logAnalysisLocal")return service.StartLocal();throw new InvalidOperationException("此操作需要从本机日志选择窗口执行。");}
  }
  void DisposeLogAnalysis(){GameLogAnalysisService selected;lock(gate){selected=logAnalysis;logAnalysis=null;}if(selected!=null)selected.Dispose();}
 }
}
