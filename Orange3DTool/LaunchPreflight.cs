using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace PZ3D {
 sealed class LaunchPreflightItem {
  public string id,status,title,message,route;
 }
 sealed class LaunchPreflightReport {
  public bool canLaunch;
  public string checkedUtc,message;
  public LaunchPreflightItem[] items;
 }
 static class LaunchPreflight {
  static LaunchPreflightItem Item(string id,string status,string title,string message,string route){return new LaunchPreflightItem{id=id,status=status,title=title,message=message,route=route};}
  public static LaunchPreflightReport Check(Engine e,bool busy,bool running){return Check(e,busy,running,"normal");}
  public static LaunchPreflightReport Check(Engine e,bool busy,bool running,string targetKind){
   if(targetKind!="normal"&&targetKind!="save"&&targetKind!="server")throw new ArgumentException("未知启动预检类型。");
   var items=new List<LaunchPreflightItem>();bool safe=true,config=false,pending=false;
   items.Add(Item("activity",busy||running?"error":"ok","任务与游戏进程",busy?"本机任务尚未结束，请完成或取消后再启动。":running?"游戏进程仍在运行，请先退出游戏。":"没有阻止启动的任务或已知游戏进程。","home"));
   try{foreach(string path in new[]{e.Game,e.Config,e.User,e.Mods,e.Data,e.Workshop,Path.Combine(e.Game,"ProjectZomboid64.exe"),Path.Combine(e.Game,"ZombieBuddy.jar"),Path.Combine(e.Game,"zbNative.dll"),Path.Combine(e.Data,"launch-sessions",LaunchProfiles.Key(e)+".json"),Path.Combine(e.Data,"workshop-rebuild.pending"),Path.Combine(e.Data,"workshop-paths.json")})LauncherMaintenance.NoReparse(path);}
   catch{safe=false;}
   if(safe){pending=LaunchProfiles.Pending(e)||BuddyTakeover.Pending(e);items.Add(Item("recovery",pending?"error":"ok","启动会话与参数恢复",pending?"有尚未恢复的启动会话或旧 Buddy 参数接管，请先完成恢复。":"未发现待恢复的启动会话或参数接管。","maintenance"));bool workshop=File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending"));items.Add(Item("workshop",workshop?"error":"ok","工坊维护",workshop?"工坊重新下载尚未完成，请先完成维护。":"未发现未完成的工坊重新下载。","maintenance"));}
   else{items.Add(Item("recovery","error","路径安全","已知检查路径含联接或重解析点，未继续读取。请核对所选目录。","maintenance"));items.Add(Item("workshop","warning","工坊维护","检查路径不安全，未读取工坊维护状态。","maintenance"));}
   string reason="";
   if(safe){try{if(File.Exists(e.Config)&&new FileInfo(e.Config).Length<=2097152){string text=File.ReadAllText(e.Config);var cleanup=BuddyLaunchArguments.CleanJson(text);if(!cleanup.Safe||cleanup.Changed)reason="启动 JSON 存在不明确或重复的 Buddy 参数，请在组件或维护页核对。";else reason=LauncherLaunch.Reason(e,false,false);}else reason="游戏启动配置缺失或超出安全读取上限，请核对游戏目录。";config=reason=="";}catch{reason="游戏启动配置无法安全读取，请核对游戏目录与 JSON。";}}
   else reason="检查路径不安全，未读取启动配置。";
   items.Add(Item("launch-config",config?"ok":"error","游戏与启动配置",config?"游戏启动文件和参数通过本次静态检查；加载效果仍由游戏确认。":reason,"components"));
   bool guarded=false;if(safe&&config&&!pending){try{e.Guard();guarded=true;}catch{}}
   items.Add(Item("launch-guard",guarded?"ok":safe&&config&&pending?"warning":"error","旧启动参数与 Steam 检查",guarded?"现有 Buddy、备用 BAT 和 Steam 启动项安全检查通过。":pending?"请完成待恢复会话后重新检查启动参数。":"启动参数安全检查未通过，请在维护页核对旧 Buddy、备用 BAT 与 Steam 状态。","maintenance"));
   bool modsChecked=false;
   if(safe){try{byte[] bytes=SaveProfiles.DefaultBytes(e);string[] mods=SaveProfiles.DefaultMods(bytes);byte[] parsed=SaveProfiles.ReplaceDefaultMods(bytes,mods);if(targetKind=="server"){string[] retained=LaunchProfiles.Retained(e);LaunchProfiles.BuildDefault(new UTF8Encoding(false,true).GetString(bytes??parsed).TrimStart('\uFEFF'),"server",retained);mods=mods.Where(retained.Contains).ToArray();}var sorted=ModConfigurations.SortForPreflight(e,mods);bool invalid=sorted.errors.Length>0||sorted.changed;items.Add(Item("mods",invalid?"error":sorted.warnings.Length>0?"warning":"ok",targetKind=="server"?"服务器保留模组依赖与顺序":"默认模组依赖与顺序",invalid?"本次启动模组存在缺失、依赖、冲突或加载顺序问题，请在模组配置页检查并明确保存。":sorted.warnings.Length>0?"部分模组来源或元数据未核实，请在模组配置页核对；未断言这些文件缺失。":targetKind=="server"?"服务器启动保留的已知客户端模组通过；普通本地模组会排除，服务器新增模组仍由游戏处理。":"已知本地来源的默认模组依赖与加载顺序通过。","mods"));modsChecked=true;}catch{}}
   if(!modsChecked)items.Add(Item("mods","error","默认模组依赖与顺序","默认模组清单或已知来源无法安全读取，请在模组配置页核对。","mods"));
   var approvals=safe&&config&&guarded&&!pending&&modsChecked?JavaApprovals.CheckForPreflight(e):null;
   items.Add(Item("java-approvals",approvals!=null&&approvals.supported&&approvals.pending==0&&approvals.denied==0?"ok":"warning","当前 Java 文件授权",approvals==null||!approvals.supported?"当前 Java 授权状态未核实；请打开 Java 授权逐项核对。新存档或服务器文件仍由游戏询问。":approvals.pending>0?"当前已知 Java 文件有待明确授权的项目，请逐项核对；工具不会自动允许。":approvals.denied>0?"当前 Java 文件有明确拒绝的项目，相关模组可能不加载；请在授权页核对。":"当前已知文件没有待决定项目；新文件和服务器授权仍由游戏处理。","java"));
   string memoryStatus="warning",memoryMessage="本次未核实堆内存，请在内存页核对。";
   if(safe&&config){try{int heap=LauncherMaintenance.ReadHeapGiB(e);var memory=LauncherMaintenance.MemoryInfo();if(heap>memory.MaximumHeapGiB)memoryMessage="配置的堆内存超过按总物理内存计算的建议上限，请为系统保留至少 4 GiB 或 25%。";else if(heap>memory.AvailableGiB)memoryMessage="当前可用物理内存低于配置堆上限；请关闭其他程序后核对，未修改堆或分页文件。";else{memoryStatus="ok";memoryMessage="配置堆内存在当前物理内存建议范围内；实际占用取决于游戏和模组。";}}catch{memoryMessage="启动配置未设置可核实的 Xmx，或内存信息不可用；可在内存页明确设置后再核对。";}}
   items.Add(Item("memory",memoryStatus,"堆内存建议",memoryMessage,"memory"));
   bool allowed=items.All(i=>i.status!="error");return new LaunchPreflightReport{canLaunch=allowed,checkedUtc=DateTime.UtcNow.ToString("o"),items=items.ToArray(),message=allowed?"本次启动检查通过；授权提示、实际加载、读档和连接结果仍以游戏为准。":items.First(i=>i.status=="error").message};
  }
 }
}
