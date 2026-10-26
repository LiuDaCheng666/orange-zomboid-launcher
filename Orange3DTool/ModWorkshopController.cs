using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
namespace PZ3D {
 static class ModWorkshopLink {
  internal static string Validate(Dictionary<string,object> payload){object raw;ulong id;if(payload==null||payload.Count!=1||!payload.TryGetValue("workshopId",out raw)||!(raw is string)||!Regex.IsMatch((string)raw,@"\A[1-9][0-9]{0,19}\z")||!ulong.TryParse((string)raw,NumberStyles.None,CultureInfo.InvariantCulture,out id))throw new InvalidOperationException("工坊页面仅接受当前目录中的有效工坊条目。没有网址或路径参数。");return id.ToString(CultureInfo.InvariantCulture);}
  internal static string Resolve(Engine engine,Dictionary<string,object> payload){string id=Validate(payload);var warnings=new List<string>();if(!SaveProfiles.Entries(engine,warnings).Any(entry=>entry.workshopId==id&&(entry.source=="workshop"||entry.source=="snapshot")))throw new InvalidOperationException("此工坊条目已不在当前实际模组目录中，请刷新后重试。");return "https://steamcommunity.com/sharedfiles/filedetails/?id="+id;}
  internal static void Open(Engine engine,Dictionary<string,object> payload,bool test,Action<string> open){string target=Resolve(engine,payload);if(test)throw new InvalidOperationException("测试模式禁止打开外部页面。");open(target);}
 }
 sealed partial class ModernController {
  internal static bool ModWorkshopAction(string action){return action=="modOpenWorkshop";}
  internal static void ValidateModWorkshopPayload(string action,Dictionary<string,object> payload){if(ModWorkshopAction(action))ModWorkshopLink.Validate(payload);}
  object DispatchModWorkshop(string action,Dictionary<string,object> payload){if(!ModWorkshopAction(action))throw new InvalidOperationException("未知工坊页面操作。");ValidateModWorkshopPayload(action,payload);cancel.Token.ThrowIfCancellationRequested();ModWorkshopLink.Open(engine,payload,test,Links.Open);return Message("已打开模组工坊原页。");}
 }
}
