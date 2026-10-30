using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class GuideState {public int schema=1;public bool dismissed;}
 static class GuidePreferences {
  static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  static string FilePath(string data){string file=Path.GetFullPath(Path.Combine(data,"user-settings","guide.json"));LauncherMaintenance.NoReparse(file);return file;}
  internal static GuideState Get(string data){string file=FilePath(data);if(!File.Exists(file))return new GuideState();try{if(new FileInfo(file).Length>4096)throw new IOException();byte[] bytes=File.ReadAllBytes(file);if(bytes.Length>4096)throw new IOException();var value=new JavaScriptSerializer{MaxJsonLength=4096,RecursionLimit=4}.Deserialize<Dictionary<string,object>>(Utf8.GetString(bytes));object schema,dismissed;if(value==null||value.Count!=2||!value.TryGetValue("schema",out schema)||!(schema is int)||(int)schema!=1||!value.TryGetValue("dismissed",out dismissed)||!(dismissed is bool))throw new IOException();return new GuideState{dismissed=(bool)dismissed};}catch(Exception error){throw new IOException("使用引导的显示设置无法读取；原文件已保留。",error);}}
  internal static GuideState Dismiss(string data){string file=FilePath(data),key=DlssInstaller.Hash(Utf8.GetBytes(file.ToUpperInvariant()));using(var mutex=new Mutex(false,"Local\\OrangeGuide-"+key)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("使用引导设置正在保存，请稍后重试。");Get(data);LauncherMaintenance.NoReparse(file);Directory.CreateDirectory(Path.GetDirectoryName(file));string temporary=file+".orange-"+Guid.NewGuid().ToString("N")+".tmp";try{LauncherMaintenance.NoReparse(temporary);byte[] bytes=Utf8.GetBytes("{\"schema\":1,\"dismissed\":true}");using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}LauncherMaintenance.NoReparse(file);if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}finally{LauncherMaintenance.NoReparse(temporary);if(File.Exists(temporary))File.Delete(temporary);}return new GuideState{dismissed=true};}finally{if(held)mutex.ReleaseMutex();}}}
 }
 sealed partial class ModernController {
  internal static readonly string[] GuideReads={"guideGet","guideDismiss","updateAnnouncementGet","updateAnnouncementDismiss"};
  internal static bool GuideAction(string action){return action=="guideGet"||action=="guideDismiss"||action=="updateAnnouncementGet"||action=="updateAnnouncementDismiss";}
  internal static void ValidateGuidePayload(string action,Dictionary<string,object> payload){if(action=="updateAnnouncementDismiss"){object version;if(payload==null||payload.Count!=1||!payload.TryGetValue("version",out version)||!(version is string)||(string)version!=AppVersion.Current)throw new InvalidOperationException("更新公告只接受当前安装版本，不能指定路径或其他版本。");return;}if(GuideAction(action)&&(payload==null||payload.Count!=0))throw new InvalidOperationException("使用引导和更新公告不接受网址、路径或额外参数。");}
  object DispatchGuide(string action,Dictionary<string,object> payload){if(!GuideAction(action))throw new InvalidOperationException("未知使用引导操作。");ValidateGuidePayload(action,payload);if(action=="updateAnnouncementGet"||action=="updateAnnouncementDismiss"){var announcement=UpdateAnnouncementService.Current(data);return action=="updateAnnouncementGet"?announcement.Get():announcement.Dismiss();}return action=="guideGet"?GuidePreferences.Get(data):GuidePreferences.Dismiss(data);}
 }
}
