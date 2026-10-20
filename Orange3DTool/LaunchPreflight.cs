using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace PZ3D {
 sealed class LaunchPreflightItem {
  public string id,status,title,message,route;
  public string[] details;
  public bool canAutoSort;
 }
 sealed class LaunchPreflightReport {
  public bool canLaunch;
  public string checkedUtc,message;
  public LaunchPreflightItem[] items;
 }
 sealed class LaunchReadBudget {
  internal sealed class Exhausted:IOException {internal Exhausted():base("启动检查读取量超过上限，请查看模组目录详情。") {}}
  int remainingDirectories=4096;long remainingBytes;
  public bool Incomplete {get;private set;}
  public LaunchReadBudget(long maximumBytes){if(maximumBytes<0)throw new ArgumentOutOfRangeException("maximumBytes");remainingBytes=maximumBytes;}
  internal void Unknown(){Incomplete=true;}
  internal void Charge(long bytes,long maximum=long.MaxValue){if(bytes<0||bytes>maximum){Incomplete=true;throw new IOException("预检文件超出大小限制。");}if(bytes>remainingBytes){Incomplete=true;throw new Exhausted();}remainingBytes-=bytes;}
  internal string[] Directories(string path){try{LauncherMaintenance.NoReparse(path);if(!Directory.Exists(path))return new string[0];var result=Directory.EnumerateDirectories(path).Take(remainingDirectories+1).ToArray();if(result.Length>remainingDirectories){remainingDirectories=0;throw new IOException("预检目录超出累计数量上限。");}remainingDirectories-=result.Length;return result;}catch{Incomplete=true;throw;}}
  internal byte[] Read(string path,int maximum){try{LauncherMaintenance.NoReparse(path);if(!File.Exists(path))return null;using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){Charge(input.Length,maximum);var bytes=new byte[(int)input.Length];int offset=0,count;while(offset<bytes.Length&&(count=input.Read(bytes,offset,bytes.Length-offset))>0)offset+=count;if(offset!=bytes.Length)throw new IOException("预检文件读取未完成。");return bytes;}}catch{Incomplete=true;throw;}}
 }
 static class LaunchPreflight {
  static LaunchPreflightItem Item(string id,string status,string title,string message,string route,string[] details=null,bool canAutoSort=false){return new LaunchPreflightItem{id=id,status=status,title=title,message=message,route=route,details=(details??new string[0]).Select(s=>s.Length<=512?s:s.Substring(0,512)).Distinct(StringComparer.Ordinal).Take(100).ToArray(),canAutoSort=canAutoSort};}
  public static LaunchPreflightReport Check(Engine e,bool busy,bool running){return Check(e,busy,running,"normal");}
  public static LaunchPreflightReport Check(Engine e,bool busy,bool running,string targetKind){
   if(targetKind!="normal"&&targetKind!="save"&&targetKind!="server")throw new ArgumentException("未知启动预检类型。");
   var items=new List<LaunchPreflightItem>();bool safe=true,config=false,pending=false;
   items.Add(Item("activity",busy||running?"error":"ok","任务与游戏进程",busy?"本机任务尚未结束，请完成或取消后再启动。":running?"游戏进程仍在运行，请先退出游戏。":"没有阻止启动的任务或已知游戏进程。","home"));
   try{foreach(string path in new[]{e.Game,e.Config,e.User,e.Mods,e.Data,e.Workshop,Path.Combine(e.Game,"ProjectZomboid64.exe"),Path.Combine(e.Game,"ZombieBuddy.jar"),Path.Combine(e.Game,"zbNative.dll"),Path.Combine(e.Data,"launch-sessions",LaunchProfiles.Key(e)+".json"),Path.Combine(e.Data,"workshop-rebuild.pending"),Path.Combine(e.Data,"workshop-paths.json")})LauncherMaintenance.NoReparse(path);}
   catch{safe=false;}
   if(safe){pending=LaunchProfiles.Pending(e)||BuddyTakeover.Pending(e)||PlayProfileTransaction.Pending(e);items.Add(Item("recovery",pending?"error":"ok","启动会话与参数恢复",pending?"有尚未恢复的启动会话、游玩配置或旧 Buddy 参数接管，请先完成恢复。":"未发现待恢复的启动会话或参数接管。","maintenance"));bool workshop=File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending"));items.Add(Item("workshop",workshop?"error":"ok","工坊维护",workshop?"工坊重新下载尚未完成，请先完成维护。":"未发现未完成的工坊重新下载。","maintenance"));}
   else{items.Add(Item("recovery","error","路径安全","已知检查路径含联接或重解析点，未继续读取。请核对所选目录。","maintenance"));items.Add(Item("workshop","warning","工坊维护","检查路径不安全，未读取工坊维护状态。","maintenance"));}
   string reason="";
   if(safe){try{if(File.Exists(e.Config)&&new FileInfo(e.Config).Length<=2097152){string text=File.ReadAllText(e.Config);var cleanup=BuddyLaunchArguments.CleanJson(text);if(!cleanup.Safe||cleanup.Changed)reason="启动 JSON 存在不明确或重复的 Buddy 参数，请在组件或维护页核对。";else reason=LauncherLaunch.Reason(e,false,false);}else reason="游戏启动配置缺失或超出安全读取上限，请核对游戏目录。";config=reason=="";}catch{reason="游戏启动配置无法安全读取，请核对游戏目录与 JSON。";}}
   else reason="检查路径不安全，未读取启动配置。";
   items.Add(Item("launch-config",config?"ok":"error","游戏与启动配置",config?"游戏启动文件和参数正常。":reason,"components"));
   bool guarded=false;if(safe&&config&&!pending){try{e.GuardLaunch();guarded=true;}catch{}}
   items.Add(Item("launch-guard",guarded?"ok":safe&&config&&pending?"warning":"error","旧启动参数与 Steam 检查",guarded?"现有 Buddy、备用 BAT 和 Steam 启动项安全检查通过。":pending?"请完成待恢复会话后重新检查启动参数。":"启动参数安全检查未通过，请在维护页核对旧 Buddy、备用 BAT 与 Steam 状态。","maintenance"));
   bool modsChecked=false;
   if(safe){try{
    var budget=new LaunchReadBudget(16777216);byte[] bytes=SaveProfiles.DefaultBytes(e);string[] mods=ModConfigurations.ValidatePreflightMods(SaveProfiles.DefaultMods(bytes)),defaultMods=mods;byte[] parsed=SaveProfiles.ReplaceDefaultMods(bytes,mods);
    if(targetKind=="server"){string[] retained=LaunchProfiles.Retained(e,budget);LaunchProfiles.BuildDefault(new UTF8Encoding(false,true).GetString(bytes??parsed).TrimStart('\uFEFF'),"server",retained);mods=mods.Where(retained.Contains).ToArray();}
    var sorted=ModConfigurations.SortForPreflight(e,mods,budget);bool invalid=sorted.errors.Length>0||sorted.changed,canAutoSort=sorted.errors.Length==0&&sorted.changed;
    if(canAutoSort&&targetKind=="server"){var defaults=ModConfigurations.SortForPreflight(e,defaultMods);canAutoSort=defaults.errors.Length==0&&defaults.changed;}
    string message=sorted.errors.Length>0?"本次启动模组存在缺失、依赖或冲突问题，请按下列详情核对。":sorted.changed?"本次启动模组加载顺序与作者声明不一致，请按下列详情排序后继续启动。":sorted.warnings.Length>0?"部分模组信息读取不完整，请查看下方详情。":targetKind=="server"?"客户端组件检查通过，将使用服务器模组清单连接。":"已知本地来源的默认模组依赖与加载顺序通过。";
    items.Add(Item("mods",invalid?"error":sorted.warnings.Length>0?"warning":"ok",targetKind=="server"?"服务器保留模组依赖与顺序":"默认模组依赖与顺序",message,"mods",sorted.errors.Concat(sorted.violations).Concat(sorted.warnings).ToArray(),canAutoSort));modsChecked=true;
   }catch(Exception ex){items.Add(Item("mods","error","默认模组依赖与顺序","默认模组清单或已知来源无法安全读取，请在模组配置页核对。","mods",new[]{ex.Message}));modsChecked=true;}}
   if(!modsChecked)items.Add(Item("mods","error","默认模组依赖与顺序","默认模组清单或已知来源无法安全读取，请在模组配置页核对。","mods"));
   var approvals=safe&&config&&guarded&&!pending&&modsChecked?JavaApprovals.CheckForPreflight(e):null;
   items.Add(Item("java-approvals",approvals!=null&&approvals.supported&&approvals.pending==0&&approvals.denied==0?"ok":"warning","当前 Java 文件授权",approvals==null||!approvals.supported?"请打开 Java 授权页查看文件授权状态。":approvals.pending>0?"有 Java 文件等待授权，请打开授权页处理。":approvals.denied>0?"当前 Java 文件有明确拒绝的项目，相关模组可能不加载；请在授权页核对。":"当前 Java 文件授权已处理。","java"));
   string memoryStatus="warning",memoryMessage="未读取到启动内存，请打开内存设置。";
   if(safe&&config){try{int heap=LauncherMaintenance.ReadHeapGiB(e);var memory=LauncherMaintenance.MemoryInfo();if(heap>memory.MaximumHeapGiB)memoryMessage="配置的堆内存超过按总物理内存计算的建议上限，请为系统保留至少 4 GiB 或 25%。";else if(heap>memory.AvailableGiB)memoryMessage="当前可用物理内存低于配置堆上限；请关闭其他程序后核对，未修改堆或分页文件。";else{memoryStatus="ok";memoryMessage="启动内存在建议范围内。";}}catch{memoryMessage="未读取到 Xmx 或系统内存信息，请打开内存设置。";}}
   items.Add(Item("memory",memoryStatus,"堆内存建议",memoryMessage,"memory"));
   bool allowed=items.All(i=>i.status!="error");return new LaunchPreflightReport{canLaunch=allowed,checkedUtc=DateTime.UtcNow.ToString("o"),items=items.ToArray(),message=allowed?"启动检查通过。":items.First(i=>i.status=="error").message};
  }
 }
}
