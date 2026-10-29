using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ModernHostPolicy {
  static readonly HashSet<string> Actions=new HashSet<string>{"launchConfigurations","launchConfigurationSelect","configurationTasks","configurationCancel","launchPreflight","status","componentsAction","memoryGet","memorySave","settingsGet","settingsSave","shadersGet","shaderSelect","importShader","maintenance","launch","chooseGame","cancel","help","theme","logs","reticleGet","reticleSave","open","openExternal","openOfficial","dlss","savesList","modCatalog","saveGet","saveSave","serverList","serverUpsert","serverDelete","serverQuery","profileLaunch","profileRecover","modCover","modDetail","javaList","javaSave","updatesGet","modProfiles","modProfileGet","modSort","saveThumb","serverThumb","taskGet","playtimeGet","serverFavorites","steamConnection","steamSubscriptions","steamSearch","steamDetails","serverHall","modAutoSort","modProfileSave","modProfileDelete","modProfileActivate","steamDownload","favoriteImport","playtimeSettings"};
  internal static bool IsWorkshopImage(string url){Uri uri;return url!=null&&url.Length<=8192&&!url.Any(char.IsControl)&&Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo==""&&(uri.Host=="orangezl.com"||uri.Host=="images.steamusercontent.com"||uri.Host=="steamuserimages-a.akamaihd.net");}
  public static Dictionary<string,object> Validate(string source,string text){
   Uri uri;if(!Uri.TryCreate(source,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="orange.local"||!uri.IsDefaultPort||uri.UserInfo!=""||uri.AbsolutePath!="/index.html")throw new Exception("拒绝非本地受信界面的请求。");
   if(text==null||text.Length>262144)throw new Exception("界面请求过大或为空。");
   var request=new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=24}.Deserialize<Dictionary<string,object>>(text);object id,action,payload;
   if(request==null||!request.TryGetValue("id",out id)||!(id is string)||!Regex.IsMatch((string)id,@"^[A-Za-z0-9_-]{1,96}$")||!request.TryGetValue("action",out action)||!(action is string)||(!Actions.Contains((string)action)&&!ModernController.ConfigurationAction((string)action)&&!ModernController.FrozenConfigurationAction((string)action)&&!ModernController.LibraryWebAction((string)action)&&!ModernController.EnvironmentWebAction((string)action)&&!ModernController.ModWorkshopAction((string)action)&&!ModernController.GuideAction((string)action)&&(string)action!="versionChooseImport"&&(string)action!="versionChoosePreserve"&&(string)action!="versionChooseInstall"&&(string)action!="backupChooseDirectory"&&!CoreAction((string)action)&&!BuddyAction((string)action)&&!PlayProfiles.Action((string)action)&&!FeedbackService.Action((string)action)&&!ModernController.StartupOptionsAction((string)action)))throw new Exception("未知或无效的界面操作。");
   if(!request.TryGetValue("payload",out payload)||!(payload is Dictionary<string,object>))throw new Exception("操作参数必须是对象。");
   if(ModernController.ConfigurationAction((string)action))ModernController.ValidateConfigurationPayload((string)action,(Dictionary<string,object>)payload);
   ModernController.ValidateFrozenConfigurationPayload((string)action,(Dictionary<string,object>)payload);
   ModernController.ValidateLibraryWebPayload((string)action,(Dictionary<string,object>)payload);
   ModernController.ValidateEnvironmentWebPayload((string)action,(Dictionary<string,object>)payload);
   ModernController.ValidateModWorkshopPayload((string)action,(Dictionary<string,object>)payload);
   ModernController.ValidateGuidePayload((string)action,(Dictionary<string,object>)payload);
   if(ModernController.ServerWorkshopAction((string)action))ValidateWorkshopPayload((string)action,(Dictionary<string,object>)payload);
   if((string)action=="openContact")LauncherContact.Resolve((Dictionary<string,object>)payload);
   if((string)action=="openExternal")ResolveExternal((Dictionary<string,object>)payload);
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
   if((string)action=="javaList"||(string)action=="javaSave")ValidateJavaApprovalPayload((string)action,(Dictionary<string,object>)payload);
   if(new[]{"modCatalog","modCover","modDetail","modSort","saveGet","saveSave"}.Contains((string)action))ValidateSaveEditorPayload((string)action,(Dictionary<string,object>)payload);
   if((string)action=="serverQuery")ServerProfiles.ValidateQueryPayload((Dictionary<string,object>)payload);
   if((string)action=="steamSearch")SteamLauncherServices.Normalize("search",(Dictionary<string,object>)payload);
   return request;
  }
  internal static string ResolveExternal(Dictionary<string,object> payload){object raw;Uri uri;if(payload==null||payload.Count!=1||!payload.TryGetValue("url",out raw)||!(raw is string)||((string)raw).Length>8192||((string)raw).Any(char.IsControl)||((string)raw).Contains("\\")||!Uri.TryCreate((string)raw,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||string.IsNullOrEmpty(uri.Host))throw new InvalidOperationException("外部链接只接受完整的 HTTP 或 HTTPS 网页地址。");return uri.AbsoluteUri;}
  // Resolve only strictly validated envelopes. Unknown/recursive inner actions never
  // inherit read privileges from the outer configurationRequest name.
  internal static string ReadAction(string action,Dictionary<string,object> payload){if(action!="configurationRequest")return action;ModernController.ValidateConfigurationPayload(action,payload);return (string)payload["action"];}
  internal static bool IsNativeRead(string action,Dictionary<string,object> payload){string inner=ReadAction(action,payload);return action=="configurationRequest"?ModernController.ConfigurationPureReads.Contains(inner):ModernController.NativeReads.Contains(inner)||new[]{"launchConfigurations","configurationRead","configurationReadCancel","configurationTasks"}.Contains(inner);}
  internal static void ValidateModDetailPayload(Dictionary<string,object> p){ValidateSaveEditorPayload("modDetail",p);}
  internal static void ValidateSaveEditorPayload(string action,Dictionary<string,object> p){
   if(p==null)throw new InvalidOperationException("缺少存档模组参数。");
   string[] fields=action=="saveGet"?new[]{"id"}:action=="saveSave"?new[]{"id","stamp","mods","confirmed"}:action=="modCatalog"?new string[0]:action=="modSort"?new[]{"mods"}:new[]{"token"};
   if(action=="modCatalog"&&p.ContainsKey("refresh"))fields=fields.Concat(new[]{"refresh"}).ToArray();
   if(action!="saveGet"&&action!="saveSave"&&p.ContainsKey("saveId"))fields=fields.Concat(new[]{"saveId"}).ToArray();
   if((action=="modCover"||action=="modDetail")&&p.ContainsKey("currentSource")){if(p.ContainsKey("saveId")||!(p["currentSource"] is bool)||!(bool)p["currentSource"])throw new InvalidOperationException("模组来源读取选项无效。");fields=fields.Concat(new[]{"currentSource"}).ToArray();}
   if(p.Count!=fields.Length||fields.Any(key=>!p.ContainsKey(key)))throw new InvalidOperationException("存档模组操作参数无效。");
   foreach(string key in new[]{"id","saveId"})if(p.ContainsKey(key)&&(!(p[key] is string)||!SaveDeletion.ValidId((string)p[key])))throw new InvalidOperationException("存档标识无效。");
   object value;if(p.TryGetValue("token",out value)&&(!(value is string)||!Regex.IsMatch((string)value,@"\A[a-f0-9]{64}\z")))throw new InvalidOperationException("模组详情只接受当前目录的模组标识。");
   if(p.TryGetValue("refresh",out value)&&!(value is bool)||p.TryGetValue("confirmed",out value)&&!(value is bool))throw new InvalidOperationException("存档模组操作选项无效。");
   if(p.TryGetValue("stamp",out value)&&(!(value is string)||((string)value).Length>160))throw new InvalidOperationException("存档读取凭据无效。");
  }
  static ModernHostPolicy(){foreach(string action in new[]{"startupBegin","startupCancel","startupYield","startupResume","startupStatus"})Actions.Add(action);foreach(string action in new[]{"logAnalysisStatus","logAnalysisLocal","logAnalysisImport","logAnalysisFolder","logAnalysisCancel","logAnalysisExport"})Actions.Add(action);Actions.Add("openContact");Actions.Add("gameDiagnostics");Actions.Add("gameProcesses");Actions.Add("forceCloseGame");Actions.Add("saveDeletePreview");Actions.Add("saveDelete");foreach(string action in new[]{"serverWorkshopGet","serverWorkshopImport","serverWorkshopPrepare","serverWorkshopStatus","serverWorkshopCancel"})Actions.Add(action);}
  internal static void ValidateWorkshopPayload(string action,Dictionary<string,object> p){
   if(p==null)throw new Exception("缺少清单参数。");string[] fields=action=="serverWorkshopStatus"?(p.Count==0?new string[0]:new[]{"jobId"}):action=="serverWorkshopCancel"?new[]{"jobId"}:action=="serverWorkshopGet"?new[]{"id"}:action=="serverWorkshopImport"?new[]{"id","stamp","text","confirmed"}:new[]{"id","stamp","confirmed"};if(p.Count!=fields.Length||fields.Any(k=>!p.ContainsKey(k)))throw new Exception("清单操作参数无效。");foreach(string field in fields){if(field=="jobId"&&!StartupId(p[field]))throw new Exception("准备任务标识无效。");if(field=="confirmed"){if(!(p[field] is bool)||!(bool)p[field])throw new Exception("操作需要明确确认。");}else if(!(p[field] is string)||((string)p[field]).Length>(field=="text"?65536:field=="stamp"?64:32))throw new Exception("清单操作参数无效。");}
  }
  internal static void ValidateSaveDeletionPayload(string action,Dictionary<string,object> p){object id,stamp,confirmed;bool preview=action=="saveDeletePreview";if(p.Count!=(preview?1:3)||!p.TryGetValue("id",out id)||!(id is string)||!SaveDeletion.ValidId((string)id))throw new InvalidOperationException("删除存档只接受本机存档标识，不能提供路径。");if(!preview&&(!p.TryGetValue("stamp",out stamp)||!(stamp is string)||!SaveDeletion.ValidStamp((string)stamp)||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed))throw new InvalidOperationException("删除存档需要有效预览凭据和明确确认。");}
  internal static void ValidateJavaApprovalPayload(string action,Dictionary<string,object> p){
   bool save=action=="javaSave";var fields=save?new[]{"stamp","hash","decision"}:new string[0];if(p!=null&&p.ContainsKey("saveId"))fields=fields.Concat(new[]{"saveId"}).ToArray();if(p==null||p.Count!=fields.Length||fields.Any(key=>!p.ContainsKey(key)))throw new InvalidOperationException("Java 授权只接受当前文件凭据和本机存档标识。");
   if(p.ContainsKey("saveId")&&(!(p["saveId"] is string)||!SaveDeletion.ValidId((string)p["saveId"])))throw new InvalidOperationException("Java 授权存档标识无效。");
   if(save&&(!(p["stamp"] is string)||string.IsNullOrWhiteSpace((string)p["stamp"])||((string)p["stamp"]).Length>512||((string)p["stamp"]).Any(char.IsControl)||!(p["hash"] is string)||!Regex.IsMatch((string)p["hash"],@"\A[a-fA-F0-9]{64}\z")||!(p["decision"] is bool)))throw new InvalidOperationException("请重新读取 Java 文件后明确允许或拒绝。");
  }
  internal static bool BuddyAction(string action){return action=="buddyPreview"||action=="buddyTakeover"||action=="buddyRecover";}
  internal static void ValidateAutoSortPayload(Dictionary<string,object> p){object confirmed;if(p.Count!=1||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed)throw new Exception("自动排序需要明确确认，且只调整当前默认清单的顺序。");}
  internal static void ValidateMaintenancePayload(Dictionary<string,object> p){object kind,confirmed;if(p==null||p.Count!=2||!p.TryGetValue("kind",out kind)||!(kind is string)||!new[]{"cache","cacheAll","workshop"}.Contains((string)kind)||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed)throw new Exception("维护操作只接受已明确确认的 cache、cacheAll 或 workshop 类型，界面不能提供目标路径。");}
  internal static bool StartupId(object value){return value is string&&System.Text.RegularExpressions.Regex.IsMatch((string)value,@"\A[a-f0-9]{32}\z");}
  internal static void ValidateStartupPayload(string action,Dictionary<string,object> p){
   object kind,id;if(p==null)throw new Exception("缺少启动参数。");
   if(action=="startupStatus"){if(p.Count!=0)throw new Exception("启动状态读取不接受目标参数。");return;}
   if(action=="startupBegin"){
     if(p.Count!=(p.ContainsKey("serverComponents")?3:2)||!p.TryGetValue("targetKind",out kind)||!(kind is string)||!new[]{"normal","save","server"}.Contains((string)kind)||!p.TryGetValue("targetId",out id)||!(id is string)||((string)id).Length>1024||((string)id).Any(c=>c<' '||c=='\u007f')||((string)kind=="normal"?((string)id)!="":String.IsNullOrWhiteSpace((string)id)))throw new Exception("启动准备只接受明确的本机目标。");ServerMode((string)kind,p);
   }else if(p.Count!=1||!p.TryGetValue("startupId",out id)||!StartupId(id))throw new Exception("启动准备标识无效。");
  }
  internal static string ServerMode(string kind,Dictionary<string,object> p){object raw;if(!p.TryGetValue("serverComponents",out raw))return kind=="server"?"follow-server":null;if(kind!="server"||!(raw is string)||!new[]{"follow-server","force-local"}.Contains((string)raw))throw new Exception("服务器组件模式无效。");return (string)raw;}
  internal static void ValidateLaunchPayload(string action,Dictionary<string,object> p){object id,kind=null,target;if(p==null||!p.TryGetValue("startupId",out id)||!StartupId(id)||p.Count!=(action=="launch"?1:p.ContainsKey("serverComponents")?4:3))throw new Exception("请从启动按钮重新提交本次请求。");if(action=="profileLaunch"&&(!p.TryGetValue("kind",out kind)||!(kind is string)||!new[]{"save","server"}.Contains((string)kind)||!p.TryGetValue("id",out target)||!(target is string)||String.IsNullOrWhiteSpace((string)target)||((string)target).Length>1024))throw new Exception("启动目标无效。");ServerMode(action=="launch"?"normal":(string)kind,p);}
  internal static string ValidatePreflightPayload(Dictionary<string,object> payload){object kind,id;var copy=new Dictionary<string,object>(payload);if(copy.TryGetValue("startupId",out id)){if(!StartupId(id))throw new Exception("启动准备标识无效。");copy.Remove("startupId");}if(copy.Count==0)return "normal";if(copy.Count==(copy.ContainsKey("serverComponents")?2:1)&&copy.TryGetValue("targetKind",out kind)&&kind is string&&new[]{"normal","save","server"}.Contains((string)kind)){ServerMode((string)kind,copy);return (string)kind;}throw new Exception("启动预检只接受明确目标与本次准备标识。");}

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
