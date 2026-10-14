using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ModernHostPolicy {
  static readonly HashSet<string> Actions=new HashSet<string>{"launchPreflight","status","componentsAction","memoryGet","memorySave","settingsGet","settingsSave","shadersGet","shaderSelect","importShader","maintenance","launch","chooseGame","cancel","help","theme","logs","reticleGet","reticleSave","open","openOfficial","dlss","savesList","modCatalog","saveGet","saveSave","serverList","serverUpsert","serverDelete","serverQuery","profileLaunch","profileRecover","modCover","javaList","javaSave","updatesGet","modProfiles","modProfileGet","modSort","saveThumb","serverThumb","taskGet","playtimeGet","serverFavorites","steamConnection","steamSubscriptions","steamSearch","steamDetails","serverHall","modProfileSave","modProfileDelete","modProfileActivate","steamDownload","favoriteImport","playtimeSettings"};
  public static Dictionary<string,object> Validate(string source,string text){
   Uri uri;if(!Uri.TryCreate(source,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="orange.local"||!uri.IsDefaultPort||uri.UserInfo!=""||uri.AbsolutePath!="/index.html")throw new Exception("拒绝非本地受信界面的请求。");
   if(text==null||text.Length>262144)throw new Exception("界面请求过大或为空。");
   var request=new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=24}.Deserialize<Dictionary<string,object>>(text);object id,action,payload;
   if(request==null||!request.TryGetValue("id",out id)||!(id is string)||!Regex.IsMatch((string)id,@"^[A-Za-z0-9_-]{1,96}$")||!request.TryGetValue("action",out action)||!(action is string)||(!Actions.Contains((string)action)&&!CoreAction((string)action)&&!BuddyAction((string)action)))throw new Exception("未知或无效的界面操作。");
   if(!request.TryGetValue("payload",out payload)||!(payload is Dictionary<string,object>))throw new Exception("操作参数必须是对象。");
   if(CoreAction((string)action)) {
    var p=(Dictionary<string,object>)payload;object protocol,confirmed,drafts;
    bool apply=(string)action=="coreUpdateApply";
    if(!p.TryGetValue("protocol",out protocol)||!(protocol is int)||(int)protocol!=1||p.Count!=(apply?3:1))throw new Exception("启动器页面与本机更新协议不兼容，请重新安装完整版本。");
    if(apply&&(!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed||!p.TryGetValue("drafts",out drafts)||!(drafts is bool)||(bool)drafts))throw new Exception("请先保存编辑，并确认重启启动器。");
   }
   if(((string)action=="chooseGame"||(string)action=="importShader")&&((Dictionary<string,object>)payload).Count>0)throw new Exception("文件与目录必须由本机选择窗口确认，界面不能提供路径。");
   if((string)action=="launchPreflight")ValidatePreflightPayload((Dictionary<string,object>)payload);
   if(BuddyAction((string)action))ValidateBuddyPayload((string)action,(Dictionary<string,object>)payload);
   return request;
  }
  internal static bool BuddyAction(string action){return action=="buddyPreview"||action=="buddyTakeover"||action=="buddyRecover";}
  internal static string ValidatePreflightPayload(Dictionary<string,object> payload){object kind;if(payload.Count==0)return "normal";if(payload.Count==1&&payload.TryGetValue("targetKind",out kind)&&kind is string&&new[]{"normal","save","server"}.Contains((string)kind))return (string)kind;throw new Exception("启动预检只接受空对象或明确的 normal/save/server 类型，界面不能提供路径、凭据或授权决定。");}
  internal static void ValidateBuddyPayload(string action,Dictionary<string,object> p){
   if(action=="buddyPreview"){if(p.Count!=0)throw new Exception("扫描旧启动参数不接受页面目标参数。");return;}
   object stamp,confirmed,exit;if(p.Count!=3||!p.TryGetValue("stamp",out stamp)||!(stamp is string)||string.IsNullOrWhiteSpace((string)stamp)||((string)stamp).Length>512||!p.TryGetValue("confirmed",out confirmed)||!(confirmed is bool)||!(bool)confirmed||!p.TryGetValue("steamExitConfirmed",out exit)||!(exit is bool))throw new Exception("接管或恢复只接受有效预览凭据和两项明确确认；请重新扫描。");
  }
  internal static bool CoreAction(string action){return action=="coreUpdateStatus"||action=="coreUpdateCheck"||action=="coreUpdateDownload"||action=="coreUpdateCancel"||action=="coreUpdateApply";}
 }
}
