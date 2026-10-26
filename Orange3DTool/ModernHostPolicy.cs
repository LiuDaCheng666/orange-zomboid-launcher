using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ModernHostPolicy {
  static readonly HashSet<string> Actions=new HashSet<string>{"launchPreflight","status","componentsAction","memoryGet","memorySave","settingsGet","settingsSave","shadersGet","shaderSelect","importShader","maintenance","launch","chooseGame","cancel","help","theme","logs","reticleGet","reticleSave","open","openOfficial","dlss","savesList","modCatalog","saveGet","saveSave","serverList","serverUpsert","serverDelete","serverQuery","profileLaunch","profileRecover","modCover","javaList","javaSave","updatesGet","modProfiles","modProfileGet","modSort","saveThumb","serverThumb","taskGet","playtimeGet","serverFavorites","steamConnection","steamSubscriptions","steamSearch","steamDetails","serverHall","modAutoSort","modProfileSave","modProfileDelete","modProfileActivate","steamDownload","favoriteImport","playtimeSettings"};
  internal static bool IsWorkshopImage(string url){Uri uri;return url!=null&&url.Length<=8192&&!url.Any(char.IsControl)&&Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&(uri.Host=="orangezl.com"||uri.Host=="images.steamusercontent.com"||uri.Host=="steamuserimages-a.akamaihd.net");}
  public static Dictionary<string,object> Validate(string source,string text){
   Uri uri;if(!Uri.TryCreate(source,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="orange.local"||!uri.IsDefaultPort||uri.UserInfo!=""||uri.AbsolutePath!="/index.html")throw new Exception("拒绝非本地受信界面的请求。");
   if(text==null||text.Length>262144)throw new Exception("界面请求过大或为空。");
   var request=new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=24}.Deserialize<Dictionary<string,object>>(text);object id,action,payload;
   if(request==null||!request.TryGetValue("id",out id)||!(id is string)||!Regex.IsMatch((string)id,@"^[A-Za-z0-9_-]{1,96}$")||!request.TryGetValue("action",out action)||!(action is string)||(!Actions.Contains((string)action)&&!ModernController.LibraryWebAction((string)action)&&(string)action!="versionChooseImport"&&(string)action!="versionChoosePreserve"&&(string)action!="versionChooseInstall"&&(string)action!="backupChooseDirectory"&&!CoreAction((string)action)&&!BuddyAction((string)action)&&!PlayProfiles.Action((string)action)&&!FeedbackService.Action((string)action)&&!ModernController.StartupOptionsAction((string)action)))throw new Exception("未知或无效的界面操作。");
   if(!request.TryGetValue("payload",out payload)||!(payload is Dictionary<string,object>))throw new Exception("操作参数必须是对象。");
   ModernController.ValidateLibraryWebPayload((string)action,(Dictionary<string,object>)payload);
   if(ModernController.ServerWorkshopAction((string)action))ValidateWorkshopPayload((string)action,(Dictionary<string,object>)payload);
   if((string)action=="openContact")LauncherContact.Resolve((Dictionary<string,object>)payload);
   if((string)action=="maintenance")ValidateMaintenancePayload((Dictionary<string,object>)payload);
   if(PlayProfiles.Action((string)action))PlayProfiles.ValidatePayload((string)action,(Dictionary<string,object>)payload);
   if(CoreAction((string)action)) {
    var p=(Dictionary<string,object>)payload;object protocol,confirmed,drafts;
    bool apply=(string)action=="coreUpdateApply";
    if(!p.TryGetValue("protocol",out protocol)||!(protocol is int)||(int)protocol!=1||p.Count!=(apply?3:1))throw new Exception("启动器页面与本机更新协议不兼容，请重新安装完整版本。");
    if(apply&&(!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed||!p.TryGetValue("drafts",out drafts)||!(drafts is bool)||(bool)drafts))throw new Exception("请先保存编辑，并确认重启启动器。");
   }
   if(((string)action=="chooseGame"||(string)action=="importShader")&&((Dictionary<string,object>)payload).Count>0)throw new Exception("文件与目录必须由本机选择窗口确认，界面不能提供路径。");
   if(ModernController.StartupControl((string)action))ValidateStartupPayload((string)action,(Dictionary<string,object>)payload);if((string)action=="launch"||(string)action=="profileLaunch")ValidateLaunchPayload((string)action,(Dictionary<string,object>)payload);if((string)action=="launchPreflight")ValidatePreflightPayload((Dictionary<string,object>)payload);if((string)action=="modAutoSort")ValidateAutoSortPayload((Dictionary<string,object>)payload);
   if((string)action=="gameDiagnostics")GameDiagnostics.ValidatePayload((Dictionary<string,object>)payload);
   if(ModernController.LogAnalysisAction((string)action))ModernController.ValidateLogAnalysisPayload((Dictionary<string,object>)payload);
   if(FeedbackService.Action((string)action))FeedbackService.Validate((string)action,(Dictionary<string,object>)payload);
   if(ModernController.StartupOptionsAction((string)action))ModernController.ValidateStartupOptions((string)action,(Dictionary<string,object>)payload);
   if(BuddyAction((string)action))ValidateBuddyPayload((string)action,(Dictionary<string,object>)payload);
   if((string)action=="saveDeletePreview"||(string)action=="saveDelete")ValidateSaveDeletionPayload((string)action,(Dictionary<string,object>)payload);
   return request;
  }
  static ModernHostPolicy(){foreach(string action in new[]{"startupBegin","startupCancel","startupYield","startupResume","startupStatus"})Actions.Add(action);foreach(string action in new[]{"logAnalysisStatus","logAnalysisLocal","logAnalysisImport","logAnalysisFolder","logAnalysisCancel","logAnalysisExport"})Actions.Add(action);Actions.Add("openContact");Actions.Add("gameDiagnostics");Actions.Add("gameProcesses");Actions.Add("forceCloseGame");Actions.Add("saveDeletePreview");Actions.Add("saveDelete");foreach(string action in new[]{"serverWorkshopGet","serverWorkshopImport","serverWorkshopPrepare","serverWorkshopStatus","serverWorkshopCancel"})Actions.Add(action);}
  internal static void ValidateWorkshopPayload(string action,Dictionary<string,object> p){
   if(p==null)throw new Exception("缺少清单参数。");string[] fields=action=="serverWorkshopStatus"?new string[0]:action=="serverWorkshopCancel"?new[]{"jobId"}:action=="serverWorkshopGet"?new[]{"id"}:action=="serverWorkshopImport"?new[]{"id","stamp","text","confirmed"}:new[]{"id","stamp","confirmed"};if(p.Count!=fields.Length||fields.Any(k=>!p.ContainsKey(k)))throw new Exception("清单操作参数无效。");foreach(string field in fields){if(field=="confirmed"){if(!(p[field] is bool)||!(bool)p[field])throw new Exception("操作需要明确确认。");}else if(!(p[field] is string)||((string)p[field]).Length>(field=="text"?65536:field=="stamp"?64:32))throw new Exception("清单操作参数无效。");}
  }
  internal static void ValidateSaveDeletionPayload(string action,Dictionary<string,object> p){object id,stamp,confirmed;bool preview=action=="saveDeletePreview";if(p.Count!=(preview?1:3)||!p.TryGetValue("id",out id)||!(id is string)||!SaveDeletion.ValidId((string)id))throw new InvalidOperationException("删除存档只接受本机存档标识，不能提供路径。");if(!preview&&(!p.TryGetValue("stamp",out stamp)||!(stamp is string)||!SaveDeletion.ValidStamp((string)stamp)||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed))throw new InvalidOperationException("删除存档需要有效预览凭据和明确确认。");}
  internal static bool BuddyAction(string action){return action=="buddyPreview"||action=="buddyTakeover"||action=="buddyRecover";}
  internal static void ValidateAutoSortPayload(Dictionary<string,object> p){object confirmed;if(p.Count!=1||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed)throw new Exception("自动排序需要明确确认，且只调整当前默认清单的顺序。");}
  internal static void ValidateMaintenancePayload(Dictionary<string,object> p){object kind,confirmed;if(p==null||p.Count!=2||!p.TryGetValue("kind",out kind)||!(kind is string)||!new[]{"cache","cacheAll","workshop"}.Contains((string)kind)||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed)throw new Exception("维护操作只接受已明确确认的 cache、cacheAll 或 workshop 类型，界面不能提供目标路径。");}
  internal static bool StartupId(object value){return value is string&&System.Text.RegularExpressions.Regex.IsMatch((string)value,@"\A[a-f0-9]{32}\z");}
  internal static void ValidateStartupPayload(string action,Dictionary<string,object> p){
   object kind,id;if(p==null)throw new Exception("缺少启动参数。");
   if(action=="startupStatus"){if(p.Count!=0)throw new Exception("启动状态读取不接受目标参数。");return;}
   if(action=="startupBegin"){
    if(p.Count!=2||!p.TryGetValue("targetKind",out kind)||!(kind is string)||!new[]{"normal","save","server"}.Contains((string)kind)||!p.TryGetValue("targetId",out id)||!(id is string)||((string)id).Length>1024||((string)id).Any(c=>c<' '||c=='\u007f')||((string)kind=="normal"?((string)id)!="":String.IsNullOrWhiteSpace((string)id)))throw new Exception("启动准备只接受明确的本机目标。");
   }else if(p.Count!=1||!p.TryGetValue("startupId",out id)||!StartupId(id))throw new Exception("启动准备标识无效。");
  }
  internal static void ValidateLaunchPayload(string action,Dictionary<string,object> p){object id,kind,target;if(p==null||!p.TryGetValue("startupId",out id)||!StartupId(id)||p.Count!=(action=="launch"?1:3))throw new Exception("请从启动按钮重新提交本次请求。");if(action=="profileLaunch"&&(!p.TryGetValue("kind",out kind)||!(kind is string)||!new[]{"save","server"}.Contains((string)kind)||!p.TryGetValue("id",out target)||!(target is string)||String.IsNullOrWhiteSpace((string)target)||((string)target).Length>1024))throw new Exception("启动目标无效。");}
  internal static string ValidatePreflightPayload(Dictionary<string,object> payload){object kind,id;var copy=new Dictionary<string,object>(payload);if(copy.TryGetValue("startupId",out id)){if(!StartupId(id))throw new Exception("启动准备标识无效。");copy.Remove("startupId");}if(copy.Count==0)return "normal";if(copy.Count==1&&copy.TryGetValue("targetKind",out kind)&&kind is string&&new[]{"normal","save","server"}.Contains((string)kind))return (string)kind;throw new Exception("启动预检只接受明确目标与本次准备标识。");}

  internal static void ValidateBuddyPayload(string action,Dictionary<string,object> p){
   if(action=="buddyPreview"){if(p.Count!=0)throw new Exception("扫描旧启动参数不接受页面目标参数。");return;}
   object stamp,confirmed,exit;if(p.Count!=3||!p.TryGetValue("stamp",out stamp)||!(stamp is string)||string.IsNullOrWhiteSpace((string)stamp)||((string)stamp).Length>512||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed||!p.TryGetValue("steamExitConfirmed",out exit)||!(exit is bool))throw new Exception("接管或恢复只接受有效预览凭据和两项明确确认；请重新扫描。");
  }
  internal static object CorePreflight(ModernController controller,ModernDispatchQueue queue,bool capture){
   if(capture)throw new InvalidOperationException("截图模式不执行更新操作。");
   try{if(!queue.CanRestart)throw new InvalidOperationException("存在排队或执行中的修改，请完成后重试。");controller.AssertCanRestart();return new{protocol=1,allowed=true,message=""};}
   catch(InvalidOperationException e){return new{protocol=1,allowed=false,message=e.Message};}
  }
  internal static bool CoreAction(string action){return action=="coreUpdateStatus"||action=="coreUpdatePreflight"||action=="coreUpdateCheck"||action=="coreUpdateDownload"||action=="coreUpdateCancel"||action=="coreUpdateApply";}
 }
}
