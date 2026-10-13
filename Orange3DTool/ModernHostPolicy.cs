using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ModernHostPolicy {
  static readonly HashSet<string> Actions=new HashSet<string>{"status","componentsAction","memoryGet","memorySave","settingsGet","settingsSave","shadersGet","shaderSelect","importShader","maintenance","launch","chooseGame","cancel","help","theme","logs","reticleGet","reticleSave","open","openOfficial","dlss","savesList","modCatalog","saveGet","saveSave","serverList","serverUpsert","serverDelete","serverQuery","profileLaunch","profileRecover","modCover","javaList","javaSave","updatesGet"};
  public static Dictionary<string,object> Validate(string source,string text){
   Uri uri;if(!Uri.TryCreate(source,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="orange.local"||!uri.IsDefaultPort||uri.UserInfo!=""||uri.AbsolutePath!="/index.html")throw new Exception("拒绝非本地受信界面的请求。");
   if(text==null||text.Length>262144)throw new Exception("界面请求过大或为空。");
   var request=new JavaScriptSerializer{MaxJsonLength=262144,RecursionLimit=24}.Deserialize<Dictionary<string,object>>(text);object id,action,payload;
   if(request==null||!request.TryGetValue("id",out id)||!(id is string)||!Regex.IsMatch((string)id,@"^[A-Za-z0-9_-]{1,96}$")||!request.TryGetValue("action",out action)||!(action is string)||!Actions.Contains((string)action))throw new Exception("未知或无效的界面操作。");
   if(!request.TryGetValue("payload",out payload)||!(payload is Dictionary<string,object>))throw new Exception("操作参数必须是对象。");
   if(((string)action=="chooseGame"||(string)action=="importShader")&&((Dictionary<string,object>)payload).Count>0)throw new Exception("文件与目录必须由本机选择窗口确认，界面不能提供路径。");
   return request;
  }
 }
}
