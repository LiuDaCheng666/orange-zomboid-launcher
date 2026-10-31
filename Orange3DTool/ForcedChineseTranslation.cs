using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Threading;using System.Web.Script.Serialization;
namespace PZ3D {
 static class ForcedChineseTranslation {
  internal const string Id="B42Trans_CN",WorkshopId="3556544454";
  static string Preference(string data){string path=Path.Combine(data,"user-settings","launch-options.json");LauncherMaintenance.NoReparse(path);return path;}
  internal static bool Get(string data){string path=Preference(data);if(!File.Exists(path))return false;try{if(new FileInfo(path).Length>4096)throw new IOException();var values=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(path,Encoding.UTF8));object value;if(values==null||!values.TryGetValue("forceChineseTranslation",out value)||!(value is bool))throw new IOException();return (bool)value;}catch(Exception ex){throw new IOException("启动选项无法读取；原设置文件已保留。",ex);}}
  internal static bool Set(string data,bool enabled){LauncherComponents.Atomic(Preference(data),Encoding.UTF8.GetBytes("{\"forceChineseTranslation\":"+(enabled?"true":"false")+"}"));return enabled;}
  internal static string Resolve(Engine e,CancellationToken token,string preferredRoot=null){
   token.ThrowIfCancellationRequested();var budget=new LaunchReadBudget(4194304,2000);budget.Checkpoint=(file,size)=>token.ThrowIfCancellationRequested();Version version=e.ModCatalogVersion??GameVersionProbe.Read(e.Game)??new Version(42,0);
   try{if(preferredRoot==null&&e.OwnedLaunch)e.OwnedComponents().SourceHints().TryGetValue(Id,out preferredRoot);if(preferredRoot!=null)return ViewpointAddons.ResolveSource(Path.GetDirectoryName(Path.GetDirectoryName(preferredRoot)),Id,version,budget,preferredRoot).Folder;return ViewpointAddons.Resolve(e,new ViewpointAddon("forced-chinese",3556544454UL,Id,"统一中文汉化",""),version,budget).Folder;}
   catch(OperationCanceledException){throw;}catch(Exception error){throw new IOException("未找到 [B42]统一中文汉化(排序置底)（模组 ID：B42Trans_CN，工坊：3556544454）。请在创意工坊下载该模组后重试，或取消勾选“强制统一中文汉化”。",error);}
  }
 }
}
