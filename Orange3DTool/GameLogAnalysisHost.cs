using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class ModernForm {
  bool HandleLogAnalysisRequest(string id,string action,Dictionary<string,object> payload){
   if(!ModernController.LogAnalysisAction(action))return false;
   ModernController.ValidateLogAnalysisPayload(payload);
   if(action=="logAnalysisImport"){
    using(var picker=new OpenFileDialog{Title="导入玩家游戏日志",Filter="游戏日志 TXT、LOG、ZIP|*.txt;*.log;*.zip",Multiselect=true,CheckFileExists=true}){
     if(picker.ShowDialog(this)!=DialogResult.OK){Post(new{id=id,ok=true,data=new{cancelled=true}});return true;}
     Post(new{id=id,ok=true,data=controller.LogAnalysisImport(picker.FileNames)});return true;
    }
   }
   if(action=="logAnalysisFolder"){
    using(var picker=new FolderBrowserDialog{Description="选择玩家日志目录：读取 console、DebugLog、connections 和日志 ZIP，不读取存档或聊天记录",ShowNewFolderButton=false}){
     if(picker.ShowDialog(this)!=DialogResult.OK){Post(new{id=id,ok=true,data=new{cancelled=true}});return true;}
     Post(new{id=id,ok=true,data=controller.LogAnalysisImport(new[]{picker.SelectedPath})});return true;
    }
   }
   if(action=="logAnalysisExport"){
    var status=controller.LogAnalysisStatus();if(status.state!="completed"||status.report==null)throw new InvalidOperationException("请先完成一次日志分析，再导出报告。");
    using(var picker=new SaveFileDialog{Title="导出脱敏日志分析报告",Filter="文本报告|*.txt",FileName="OZL-日志分析-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt",AddExtension=true,DefaultExt="txt",OverwritePrompt=true}){
     if(picker.ShowDialog(this)!=DialogResult.OK){Post(new{id=id,ok=true,data=new{cancelled=true}});return true;}
     LauncherMaintenance.NoReparse(picker.FileName);File.WriteAllText(picker.FileName,status.report.reportText??"",new UTF8Encoding(true));Post(new{id=id,ok=true,data=new{message="脱敏报告已导出。"}});return true;
    }
   }
   // These methods enqueue only background read work or return immutable snapshots.
   Post(new{id=id,ok=true,data=controller.Execute(action,payload)});return true;
  }
 }
}
