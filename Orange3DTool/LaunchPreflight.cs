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
  internal LaunchReadSet Evidence;
 }
 sealed class LaunchPreflightBlockedException:Exception {
  public readonly LaunchPreflightReport Report;
  internal LaunchPreflightBlockedException(LaunchPreflightReport report):base(report.message){Report=report;}
 }
 // Invocation-local evidence only. Reopen bytes even when size/time are unchanged;
 // identity files and directory membership also protect against a newly introduced duplicate.
 sealed class LaunchReadSet {
  readonly Dictionary<string,byte[]> files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
  readonly Dictionary<string,string[]> directories=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
  readonly List<Action> checks=new List<Action>();
  internal void File(string path,byte[] bytes){byte[] previous;if(files.TryGetValue(path,out previous)&&!Same(previous,bytes))throw Changed();files[path]=bytes;}
  internal void Directory(string path,string[] entries){string[] previous;entries=entries.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray();if(directories.TryGetValue(path,out previous)&&!previous.SequenceEqual(entries,StringComparer.OrdinalIgnoreCase))throw Changed();directories[path]=entries;}
  internal void Expect(Action check){checks.Add(check);}
  static bool Same(byte[] a,byte[] b){return a==null?b==null:b!=null&&a.SequenceEqual(b);}
  static IOException Changed(){return new IOException("模组来源或配置在启动检查期间改变，请重新启动。");}
  internal void Validate(){Validate(null);}
  internal void Validate(Action checkpoint){
   foreach(var item in files){
    if(checkpoint!=null)checkpoint();
    LauncherMaintenance.NoReparse(item.Key);
    if(item.Value==null){if(System.IO.File.Exists(item.Key))throw Changed();continue;}
    using(var input=new FileStream(item.Key,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(input.Length!=item.Value.Length)throw Changed();
     var bytes=new byte[item.Value.Length];int offset=0,count;
     while(offset<bytes.Length&&(count=input.Read(bytes,offset,bytes.Length-offset))>0)offset+=count;
     if(offset!=bytes.Length||!Same(bytes,item.Value))throw Changed();
    }
   }
   foreach(var item in directories){
    if(checkpoint!=null)checkpoint();
    LauncherMaintenance.NoReparse(item.Key);
    var entries=System.IO.Directory.Exists(item.Key)?System.IO.Directory.EnumerateDirectories(item.Key).Take(item.Value.Length+1).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToArray():new string[0];
    if(!item.Value.SequenceEqual(entries,StringComparer.OrdinalIgnoreCase))throw Changed();
   }
   foreach(var check in checks){if(checkpoint!=null)checkpoint();check();}
  }
 }
 sealed class LaunchReadBudget {
  internal sealed class Exhausted:IOException {internal Exhausted():base("启动检查读取量超过上限，请查看模组目录详情。") {}}
  int remainingDirectories=4096;long remainingBytes;
  public bool Incomplete {get;private set;}
  internal LaunchReadSet Evidence;
  internal Action<string,long> Checkpoint;
  internal void Capture(){Evidence=new LaunchReadSet();}
  internal void Expect(Action check){if(Evidence!=null)Evidence.Expect(check);}
  internal void ObserveMissing(string path){if(Evidence!=null&&!File.Exists(path))Evidence.File(path,null);}
  public LaunchReadBudget(long maximumBytes):this(maximumBytes,4096){}
  internal LaunchReadBudget(long maximumBytes,int maximumDirectories){if(maximumBytes<0||maximumDirectories<0)throw new ArgumentOutOfRangeException("maximumBytes");remainingBytes=maximumBytes;remainingDirectories=maximumDirectories;}
  internal void Unknown(){Incomplete=true;}
  internal void Charge(long bytes,long maximum=long.MaxValue){if(Checkpoint!=null)Checkpoint(null,0);if(bytes<0||bytes>maximum){Incomplete=true;throw new IOException("预检文件超出大小限制。");}if(bytes>remainingBytes){Incomplete=true;throw new Exhausted();}remainingBytes-=bytes;}
  internal string[] Directories(string path){try{if(Checkpoint!=null)Checkpoint(null,0);LauncherMaintenance.NoReparse(path);if(!Directory.Exists(path)){if(Evidence!=null)Evidence.Directory(path,new string[0]);return new string[0];}var result=Directory.EnumerateDirectories(path).Select(directory=>{if(Checkpoint!=null)Checkpoint(null,0);return directory;}).Take(remainingDirectories+1).ToArray();if(result.Length>remainingDirectories){remainingDirectories=0;throw new IOException("预检目录超出累计数量上限。");}remainingDirectories-=result.Length;if(Evidence!=null)Evidence.Directory(path,result);return result;}catch{Incomplete=true;throw;}}
  internal byte[] Read(string path,int maximum){try{if(Checkpoint!=null)Checkpoint(null,0);LauncherMaintenance.NoReparse(path);if(!File.Exists(path)){if(Evidence!=null)Evidence.File(path,null);return null;}using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){Charge(input.Length,maximum);if(Checkpoint!=null)Checkpoint(path,input.Length);var bytes=new byte[(int)input.Length];int offset=0,count;while(offset<bytes.Length&&(count=input.Read(bytes,offset,bytes.Length-offset))>0){offset+=count;if(Checkpoint!=null)Checkpoint(null,0);}if(offset!=bytes.Length)throw new IOException("预检文件读取未完成。");if(Evidence!=null)Evidence.File(path,bytes);return bytes;}}catch{Incomplete=true;throw;}}
 }
 static class LaunchPreflight {
  static LaunchPreflightItem Item(string id,string status,string title,string message,string route,string[] details=null,bool canAutoSort=false){return new LaunchPreflightItem{id=id,status=status,title=title,message=message,route=route,details=(details??new string[0]).Select(s=>s.Length<=512?s:s.Substring(0,512)).Distinct(StringComparer.Ordinal).Take(100).ToArray(),canAutoSort=canAutoSort};}
  public static LaunchPreflightReport Check(Engine e,bool busy,bool running){return Check(e,busy,running,"normal");}
  public static LaunchPreflightReport Check(Engine e,bool busy,bool running,string targetKind){return CheckLaunch(e,busy,running,targetKind,null,false);}
  internal static LaunchPreflightReport CheckLaunch(Engine e,bool busy,bool running,string targetKind,string targetId,bool capture){
   if(targetKind!="normal"&&targetKind!="save"&&targetKind!="server")throw new ArgumentException("未知启动预检类型。");
   if(e.OwnedLaunch)return CheckOwned(e,busy,running,targetKind,targetId,capture);
   var items=new List<LaunchPreflightItem>();bool safe=true,config=false,pending=false,sessionPending=false,otherRecovery=false;var budget=new LaunchReadBudget(16777216);if(capture)budget.Capture();
   items.Add(Item("activity",busy||running?"error":"ok","任务与游戏进程",busy?"本机任务尚未结束，请完成或取消后再启动。":running?"游戏进程仍在运行，请先退出游戏。":"没有阻止启动的任务或已知游戏进程。","home"));
   try{foreach(string path in new[]{e.Game,e.Config,e.User,e.Mods,e.Data,e.Workshop,Path.Combine(e.Game,"ProjectZomboid64.exe"),Path.Combine(e.Game,"ZombieBuddy.jar"),Path.Combine(e.Game,"zbNative.dll"),Path.Combine(e.Data,"launch-sessions",LaunchProfiles.Key(e)+".json"),Path.Combine(e.Data,"workshop-rebuild.pending"),Path.Combine(e.Data,"workshop-paths.json")})LauncherMaintenance.NoReparse(path);}
   catch{safe=false;}
   if(safe){sessionPending=LaunchProfiles.Pending(e);otherRecovery=BuddyTakeover.Pending(e)||PlayProfileTransaction.Pending(e);pending=sessionPending||otherRecovery;items.Add(Item("recovery",pending?"error":"ok",sessionPending&&!otherRecovery?"启动会话恢复":"启动会话与参数恢复",sessionPending&&!otherRecovery?"上一次启动会话尚未结束或清理，请等待游戏退出后重试；必要时恢复启动状态。":pending?"有尚未恢复的游玩配置或旧 Buddy 参数接管，请先完成恢复。":"未发现待恢复的启动会话或参数接管。","maintenance"));bool workshop=File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending"));items.Add(Item("workshop",workshop?"error":"ok","工坊维护",workshop?"工坊重新下载尚未完成，请先完成维护。":"未发现未完成的工坊重新下载。","maintenance"));}
   else{items.Add(Item("recovery","error","路径安全","已知检查路径含联接或重解析点，未继续读取。请核对所选目录。","maintenance"));items.Add(Item("workshop","warning","工坊维护","检查路径不安全，未读取工坊维护状态。","maintenance"));}
   string reason="";
   if(safe){try{if(File.Exists(e.Config)&&new FileInfo(e.Config).Length<=2097152)reason=LauncherLaunch.PreparationReason(e,false,false);else reason="游戏启动配置缺失或超出安全读取上限，请核对游戏目录。";config=reason=="";}catch{reason="游戏启动配置无法安全读取，请核对游戏目录与 JSON。";}}
   else reason="检查路径不安全，未读取启动配置。";
   items.Add(Item("launch-config",config?"ok":"error","游戏与启动配置",config?"游戏启动文件和参数正常。":reason,"components"));
   bool guarded=false;string guardReason="启动参数无法核实，请检查当前游戏配置。";if(safe&&config&&!pending){try{e.GuardLaunch();guarded=true;}catch(Exception ex){guardReason=LauncherJournal.Sanitize(ex.Message);if(guardReason.Length>512)guardReason=guardReason.Substring(0,512);}}
   items.Add(Item("launch-guard",guarded?"ok":safe&&(!config||pending)?"warning":"error","旧启动参数与 Steam 检查",guarded?"当前启动配置检查通过。":pending?"请完成待恢复会话后重新检查启动参数。":safe&&!config?"请先修复游戏启动配置，再检查启动参数。":guardReason,"maintenance"));
   bool modsChecked=false;
   if(safe){try{
    byte[] bytes=budget.Read(e.Mods,1048576);string[] mods=ModConfigurations.ValidatePreflightMods(SaveProfiles.DefaultMods(bytes)),defaultMods=mods;byte[] parsed=SaveProfiles.ReplaceDefaultMods(bytes,mods);
    if(targetKind=="server"){string[] retained=LaunchProfiles.Retained(e,budget);LaunchProfiles.BuildDefault(new UTF8Encoding(false,true).GetString(bytes??parsed).TrimStart('\uFEFF'),"server",retained);mods=mods.Where(id=>retained.Contains(id.TrimStart('\\'))).ToArray();budget.Expect(()=>{if(!LaunchProfiles.Retained(e).SequenceEqual(retained))throw new IOException("服务器保留模组在检查期间改变，请重新启动。");});}
    // Captured active policies override save provenance; automatic binding validates that provenance before preflight.
    var warnings=new List<string>();string[] savedMods=targetKind=="save"&&targetId!=null?(e.FrozenEnvironment!=null&&e.FrozenEnvironment.ActiveManagedIds!=null?mods:SaveProfiles.LaunchMods(e,targetId,budget)):null;var catalog=SaveProfiles.PreflightEntries(e,mods.Concat(savedMods??new string[0]).ToArray(),warnings,budget);ModConfigurations.SortResult sorted;
    if(e.FrozenEnvironment!=null){var captured=LaunchProfiles.PersistentMods(e,mods.Concat(savedMods??new string[0]).ToArray()).Distinct(StringComparer.Ordinal).ToArray();var dependencies=SnapshotPreparation.Inspect(e,captured,catalog,null,System.Threading.CancellationToken.None);sorted=new ModConfigurations.SortResult{mods=mods,errors=dependencies.Missing.Select(SnapshotPreparation.Describe).ToArray(),warnings=warnings.ToArray(),violations=new string[0],changed=false};if(budget.Incomplete)throw new IOException("快照模组来源未能完整读取，请重新核对文件。");}
    else{sorted=ModConfigurations.SortPreflight(e,mods,budget,catalog,warnings);if(savedMods!=null){var saved=ModConfigurations.SortSavedPreflight(e,savedMods,catalog,warnings);sorted.errors=sorted.errors.Concat(saved.errors).Concat(saved.violations).ToArray();sorted.warnings=sorted.warnings.Concat(saved.warnings).Distinct(StringComparer.Ordinal).ToArray();}}sorted.warnings=sorted.warnings.Where(w=>w!="作者元数据超过显示限制；目录保留有界原文摘要。"&&w!="封面路径无效。").ToArray();bool invalid=sorted.errors.Length>0||sorted.changed,canAutoSort=sorted.errors.Length==0&&sorted.changed;
    if(canAutoSort&&targetKind=="server"){catalog.Select(defaultMods);var defaults=ModConfigurations.SortPreflight(e,defaultMods,budget,catalog,warnings);canAutoSort=defaults.errors.Length==0&&defaults.changed;}
    string message=sorted.errors.Length>0?"本次启动模组存在缺失、依赖或冲突问题，请按下列详情核对。":sorted.changed?"本次启动模组加载顺序与作者声明不一致，请按下列详情排序后继续启动。":sorted.warnings.Length>0?"部分模组信息读取不完整，请查看下方详情。":targetKind=="server"?"客户端组件检查通过，将使用服务器模组清单连接。":e.FrozenEnvironment!=null?"已核对快照模组文件，将按捕获的原清单启动。":"已知本地来源的默认模组依赖与加载顺序通过。";
    items.Add(Item("mods",invalid?"error":sorted.warnings.Length>0?"warning":"ok",targetKind=="server"?"服务器保留模组依赖与顺序":e.FrozenEnvironment!=null?"快照模组文件":"默认模组依赖与顺序",message,"mods",sorted.errors.Concat(sorted.violations).Concat(sorted.warnings).ToArray(),canAutoSort));modsChecked=true;
   }catch(Exception ex){items.Add(Item("mods","error","默认模组依赖与顺序","默认模组清单或已知来源无法安全读取，请在模组配置页核对。","mods",new[]{ex.Message}));modsChecked=true;}}
   if(!modsChecked)items.Add(Item("mods","error","默认模组依赖与顺序","默认模组清单或已知来源无法安全读取，请在模组配置页核对。","mods"));
   var approvals=safe&&config&&guarded&&!pending&&modsChecked?JavaApprovals.CheckForPreflight(e):null;
   items.Add(Item("java-approvals",approvals!=null&&approvals.supported&&approvals.pending==0&&approvals.denied==0?"ok":"warning","当前 Java 文件授权",approvals==null||!approvals.supported?"请打开 Java 授权页查看文件授权状态。":approvals.pending>0?"有 Java 文件等待授权，请打开授权页处理。":approvals.denied>0?"当前 Java 文件有明确拒绝的项目，相关模组可能不加载；请在授权页核对。":"当前 Java 文件授权已处理。","java"));
   string memoryStatus="warning",memoryMessage="未读取到启动内存，请打开内存设置。";
   if(safe&&config){try{int heap=LauncherMaintenance.ReadHeapGiB(e);var memory=LauncherMaintenance.MemoryInfo();if(heap>memory.MaximumHeapGiB)memoryMessage="配置的堆内存超过按总物理内存计算的建议上限，请为系统保留至少 4 GiB 或 25%。";else if(heap>memory.AvailableGiB)memoryMessage="当前可用物理内存低于配置堆上限；请关闭其他程序后核对，未修改堆或分页文件。";else{memoryStatus="ok";memoryMessage="启动内存在建议范围内。";}}catch{memoryMessage="未读取到 Xmx 或系统内存信息，请打开内存设置。";}}
   items.Add(Item("memory",memoryStatus,"堆内存建议",memoryMessage,"memory"));
   bool allowed=items.All(i=>i.status!="error");return new LaunchPreflightReport{canLaunch=allowed,checkedUtc=DateTime.UtcNow.ToString("o"),items=items.ToArray(),Evidence=budget.Evidence,message=allowed?"启动检查通过。":items.First(i=>i.status=="error").message};
  }
  static LaunchPreflightReport CheckOwned(Engine e,bool busy,bool running,string kind,string target,bool capture){
   if(capture)e.OwnedConfiguration.Initialize();
   var items=new List<LaunchPreflightItem>();var evidence=capture?new LaunchReadSet():null;
   string reason=LauncherLaunch.PreparationReason(e,busy,running);items.Add(Item("launch-config",reason==""?"ok":"error","启动配置",reason==""?"启动器配置已就绪。":reason,"home"));
   try{e.GuardLaunch();if(LaunchProfiles.Pending(e))throw new IOException("上一次启动会话尚未结束或清理。");
    if(e.OwnedConfiguration.Mode!="vanilla"){
     var components=e.OwnedComponents();var current=components.CaptureCurrent(System.Threading.CancellationToken.None);var warnings=new List<string>();var catalog=SaveProfiles.Entries(e,warnings);string[] ordinary=kind=="save"&&e.OwnedConfiguration.ProfileId==null?SaveProfiles.LaunchMods(e,target,new LaunchReadBudget(1048576)):null;
     var plan=components.PlanEnabled(current,catalog.ToArray(),System.Threading.CancellationToken.None,ordinary);
     foreach(var row in current.Rows.Where(r=>r.Enabled&&r.Jar!=null&&r.Id!="ZombieBuddy"))if(row.Decision!=true)throw new IOException(row.Name+"：请先确认当前 Java 文件授权。");
     if(evidence!=null)evidence.Expect(()=>components.RequireCurrent(current));
     items.Add(Item("mods","ok","配置模组","将使用此配置的普通模组与已启用组件。","mods",plan.errors.Concat(plan.warnings).Concat(plan.violations).ToArray()));
    }
    if(evidence!=null){int memory=e.OwnedConfiguration.MemoryMb;evidence.Expect(()=>{if(e.OwnedConfiguration.MemoryMb!=memory)throw new IOException("启动内存已变化，请重试。");});}
   }catch(Exception error){items.Add(Item("configuration","error","启动配置",error.Message,"components"));}
   bool allowed=items.All(x=>x.status!="error");return new LaunchPreflightReport{canLaunch=allowed,checkedUtc=DateTime.UtcNow.ToString("o"),items=items.ToArray(),Evidence=evidence,message=allowed?"启动检查通过。":items.First(x=>x.status=="error").message};
  }

 }
}
