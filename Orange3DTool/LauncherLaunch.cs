using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class LauncherLaunch {
  static bool ValidHeap(string arg){var m=Regex.Match(arg,@"^-Xm[xs]([1-9]\d*)([kKmMgG]?)$");long value;if(!m.Success||!long.TryParse(m.Groups[1].Value,out value))return false;string unit=m.Groups[2].Value.ToLowerInvariant();long factor=unit=="g"?1073741824L:unit=="m"?1048576L:unit=="k"?1024L:1L;return value<=long.MaxValue/factor;}
  static IEnumerable<string> Arguments(Dictionary<string,object> cfg){
   object value;if(cfg.TryGetValue("vmArgs",out value)){if(value is string||!(value is IEnumerable))throw new Exception("vmArgs必须是字符串数组");foreach(object raw in (IEnumerable)value){if(!(raw is string))throw new Exception("vmArgs含非字符串");string arg=(string)raw;if(arg.StartsWith("-Xmx",StringComparison.Ordinal)||arg.StartsWith("-Xms",StringComparison.Ordinal)){if(!ValidHeap(arg))throw new Exception("堆内存参数无效");}yield return arg;}}
   if(cfg.TryGetValue("windows",out value)){var branches=value as Dictionary<string,object>;if(branches==null)throw new Exception("windows分支格式无效");foreach(object raw in branches.Values){var branch=raw as Dictionary<string,object>;if(branch==null)throw new Exception("windows分支格式无效");foreach(string arg in Arguments(branch))yield return arg;}}
  }
  public static string Reason(Engine e,bool busy,bool running){return ReasonCore(e,busy,running,false);}
  internal static string PreparationReason(Engine e,bool busy,bool running){return ReasonCore(e,busy,running,true);}
  static string ReasonCore(Engine e,bool busy,bool running,bool preparing){
   if(PlayProfileTransaction.Pending(e))return PlayProfileTransaction.PendingMessage;
   if(BuddyTakeover.Pending(e))return "旧 Buddy 参数接管尚未恢复，请先检查维护页恢复状态。";
   if(busy)return "正在执行任务，请等待完成或取消后再启动。";
   if(running)return "游戏已经运行，请切回游戏或退出后再启动。";
   if(File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending")))return "工坊重新下载尚未完成，不能标记为就绪。请完成重新下载。";
   if(!File.Exists(Path.Combine(e.Game,"ProjectZomboid64.exe"))||!File.Exists(e.Config))return "游戏目录不完整，请重新选择游戏目录。";
   try{var cfg=new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(File.ReadAllText(e.Config));if(cfg==null||!cfg.ContainsKey("vmArgs")||!(cfg["vmArgs"] is IEnumerable)||cfg["vmArgs"] is string)return "启动配置无有效 vmArgs，请修复游戏启动配置。";
    var args=Arguments(cfg).ToList();
    if(args.Any(a=>a.IndexOf("-javaagent:OrangeShootPresentation",StringComparison.OrdinalIgnoreCase)>=0))return "检测到旧开镜 Java 启动插件，请先在组件页重新安装修复旧启动项。";
    if(args.Any(a=>a.StartsWith("-javaagent:",StringComparison.OrdinalIgnoreCase)&&!GameStartupComponent.IsOwnedArgument(e,a)&&!(preparing&&GameStartupComponent.CanPrepareOwnedArgument(e,a))))return "检测到旧式或未支持的 Java 启动插件；本工具的 Buddy 使用 -agentlib:zbNative。请先重新安装基础组件或自行核对其他插件，工具未改动这些配置。";
    int loaders=args.Count(a=>a.IndexOf("zbNative",StringComparison.OrdinalIgnoreCase)>=0);if(loaders>1)return "启动配置存在重复 Buddy 参数，请在组件页安装修复。";
    if(loaders>0&&(!File.Exists(Path.Combine(e.Game,"ZombieBuddy.jar"))||!File.Exists(Path.Combine(e.Game,"zbNative.dll"))))return "Buddy 启动文件缺失，请先安装基础组件或卸载启用配置。";
   }catch{return "启动配置无法解析，请修复 ProjectZomboid64.json。";}
   return "";
  }
 }
}
