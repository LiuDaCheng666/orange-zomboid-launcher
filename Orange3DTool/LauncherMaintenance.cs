using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class LauncherMemoryInfo {
  public int TotalGiB,AvailableGiB,MaximumHeapGiB,RecommendedHeapGiB;
 }
 static class LauncherMaintenance {
  const ulong GiB=1073741824;
  [StructLayout(LayoutKind.Sequential)]class MemoryStatus {public uint Length=(uint)Marshal.SizeOf(typeof(MemoryStatus)),Load;public ulong Total,Available,PageTotal,PageAvailable,VirtualTotal,VirtualAvailable,Extended;}
  [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool GlobalMemoryStatusEx([In,Out]MemoryStatus state);
  public static LauncherMemoryInfo MemoryInfo(){var m=new MemoryStatus();if(!GlobalMemoryStatusEx(m))throw new Exception("无法读取物理内存；未修改配置。");if(m.Total/GiB>int.MaxValue||m.Available>m.Total)throw new Exception("物理内存读取值无效；未修改配置。");return CalculateMemoryInfo((int)(m.Total/GiB),(int)(m.Available/GiB));}
  internal static long TotalPhysicalMemoryBytes(){var m=new MemoryStatus();if(!GlobalMemoryStatusEx(m)||m.Total==0||m.Total>long.MaxValue||m.Available>m.Total)throw new Exception("无法读取物理内存字节数。");return (long)m.Total;}
  internal static LauncherMemoryInfo CalculateMemoryInfo(int totalGiB,int availableGiB){
   if(totalGiB<0||availableGiB<0||availableGiB>totalGiB)throw new ArgumentOutOfRangeException("totalGiB","物理内存读取值无效；未修改配置。");
   int reserve=Math.Max(4,(int)Math.Ceiling(totalGiB*.25));
   int maximum=Math.Max(0,totalGiB-reserve);
   int starting=(int)Math.Round(totalGiB/4.0,MidpointRounding.AwayFromZero);
   int recommended=maximum==0?0:Math.Min(maximum,Math.Max(1,Math.Min(16,starting)));
   return new LauncherMemoryInfo{TotalGiB=totalGiB,AvailableGiB=availableGiB,MaximumHeapGiB=maximum,RecommendedHeapGiB=recommended};
  }
  // Existing callers may display this before starting the isolated worker.
  public static bool WorkshopRebuildSupported {get{return true;}}
  public static string WorkshopRebuildUnavailableReason {get{return "";}}
  // One fresh attribute query per ancestor avoids the previous existence/type/
  // attribute triplet. Missing paths are allowed; unknown failures stay closed.
  [DllImport("kernel32.dll",EntryPoint="GetFileAttributesW",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint PathAttributes(string path);
  internal static void NoReparse(string path){string current=Path.GetFullPath(path);while(current!=null){uint attributes=PathAttributes(current);if(attributes==uint.MaxValue){int error=Marshal.GetLastWin32Error();if(error!=2&&error!=3)throw new IOException("无法核实路径属性："+current,new System.ComponentModel.Win32Exception(error));}else if((attributes&(uint)FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝重解析点 / 联接路径："+current);current=Path.GetDirectoryName(current);}}
  static Dictionary<string,object> Read(Engine e){NoReparse(e.Config);var j=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(e.Config));if(j==null||!j.ContainsKey("vmArgs"))throw new Exception("启动 JSON 缺少 vmArgs。");Arrays(j).ToArray();return j;}
  static IEnumerable<List<string>> Arrays(Dictionary<string,object> config){
   object raw;if(config.TryGetValue("vmArgs",out raw)){var items=raw as IEnumerable;if(raw is string||items==null)throw new Exception("vmArgs 必须是字符串数组。");var args=items.Cast<object>().ToArray();if(args.Any(a=>!(a is string)))throw new Exception("vmArgs 含非字符串参数。");var list=args.Cast<string>().ToList();foreach(string s in list)if(s.StartsWith("-Xmx",StringComparison.Ordinal)||s.StartsWith("-Xms",StringComparison.Ordinal))HeapBytes(s);yield return list;}
   object windows;if(config.TryGetValue("windows",out windows)){var branches=windows as Dictionary<string,object>;if(branches==null)throw new Exception("windows 分支格式错误。");foreach(var branch in branches.Values){var d=branch as Dictionary<string,object>;if(d==null)throw new Exception("windows 启动分支格式错误。");foreach(var a in Arrays(d))yield return a;}}
  }
  static long HeapBytes(string arg){var m=Regex.Match(arg,@"^-Xm[xs]([1-9][0-9]*)([kKmMgG]?)$");long n;if(!m.Success||!long.TryParse(m.Groups[1].Value,out n))throw new Exception("无法识别堆内存参数："+arg);long factor=m.Groups[2].Value.Length==0?1:m.Groups[2].Value.Equals("g",StringComparison.OrdinalIgnoreCase)?(long)GiB:m.Groups[2].Value.Equals("m",StringComparison.OrdinalIgnoreCase)?1048576:1024;if(n>long.MaxValue/factor)throw new Exception("堆内存参数超出范围。");return n*factor;}
  public static int ReadHeapGiB(Engine e){var args=Arrays(Read(e)).First();string last=args.LastOrDefault(a=>a.StartsWith("-Xmx",StringComparison.Ordinal));if(last==null)throw new Exception("启动配置未设置 Xmx；请选择内存后保存。");return checked((int)Math.Ceiling((double)HeapBytes(last)/GiB));}
  static Dictionary<string,object> ProfileConfig(byte[] bytes){if(bytes==null||bytes.Length>2097152)throw new Exception("启动 JSON 缺失或过大。");string text=new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF');var config=PlayProfileJson.Object(text,2097152,64);if(!config.ContainsKey("vmArgs"))throw new Exception("启动 JSON 缺少 vmArgs。");Arrays(config).ToArray();return config;}
  internal static long? ProfileHeapBytes(byte[] bytes){var args=Arrays(ProfileConfig(bytes)).First();string last=args.LastOrDefault(a=>a.StartsWith("-Xmx",StringComparison.Ordinal));return last==null?(long?)null:HeapBytes(last);}
  internal static int? ProfileHeapGiB(byte[] bytes){long? heap=ProfileHeapBytes(bytes);return heap.HasValue?checked((int)Math.Ceiling((double)heap.Value/GiB)):(int?)null;}
  internal static byte[] PrepareProfileHeap(byte[] bytes,int value,Func<LauncherMemoryInfo> memoryProvider){ValidateHeapBudget(value,memoryProvider);var config=ProfileConfig(bytes);Normalize(config,value);return PlayProfileJson.HeapArrays(bytes,config);}
  internal static void CheckProfileHeap(int value,Func<LauncherMemoryInfo> memoryProvider){ValidateHeapBudget(value,memoryProvider);}
  static void Normalize(Dictionary<string,object> config,int value){object raw;if(config.TryGetValue("vmArgs",out raw)){var result=new List<string>();bool xmx=false;foreach(string arg in ((IEnumerable)raw).Cast<string>()){if(arg.StartsWith("-Xmx",StringComparison.Ordinal)){if(!xmx)result.Add("-Xmx"+value+"g");xmx=true;}else if(arg.StartsWith("-Xms",StringComparison.Ordinal)&&HeapBytes(arg)>(long)value*(long)GiB)result.Add("-Xms"+value+"g");else result.Add(arg);}if(!xmx)result.Add("-Xmx"+value+"g");config["vmArgs"]=result.ToArray();}object win;if(config.TryGetValue("windows",out win))foreach(var branch in ((Dictionary<string,object>)win).Values)Normalize((Dictionary<string,object>)branch,value);}
  public static string SetHeapGiB(Engine e,int value){return SetHeapGiB(e,value,MemoryInfo);}
  static void ValidateHeapBudget(int value,Func<LauncherMemoryInfo> memoryProvider){
   if(memoryProvider==null)throw new ArgumentNullException("memoryProvider");var info=memoryProvider();if(info==null)throw new Exception("无法读取物理内存；未修改配置。");
   var policy=CalculateMemoryInfo(info.TotalGiB,info.AvailableGiB);
   if(value<1||value>policy.MaximumHeapGiB)throw new Exception("内存超出按总物理内存计算的设置上限；需为系统保留至少 4 GiB 或 25%。");
  }
  internal static string SetHeapGiB(Engine e,int value,Func<LauncherMemoryInfo> memoryProvider){
   e.GuardHeap();ValidateHeapBudget(value,memoryProvider);NoReparse(e.Config);byte[] before=File.ReadAllBytes(e.Config);var config=Read(e);
   if(!before.SequenceEqual(File.ReadAllBytes(e.Config)))throw new Exception("启动配置已被其他程序修改；请刷新后重试。");
   Normalize(config,value);NoReparse(e.Data);string backupDir=Path.Combine(e.Data,"backups","heap-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));NoReparse(backupDir);Directory.CreateDirectory(backupDir);
   string backup=Path.Combine(backupDir,"ProjectZomboid64.json"),temp=e.Config+".heap-"+Guid.NewGuid().ToString("N")+".tmp";
   try{
    File.WriteAllText(temp,new JavaScriptSerializer().Serialize(config),new UTF8Encoding(false));
    // The game and launcher data may be on different volumes. ReplaceFile cannot
    // move its displaced file to a backup on another volume, so persist the exact
    // snapshot first and keep the atomic replacement entirely beside the game.
    using(var output=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(before,0,before.Length);output.Flush(true);}
    if(e.Progress!=null)e.Progress(-1,"准备原子写入堆内存配置");e.GuardHeap();NoReparse(e.Config);if(!before.SequenceEqual(File.ReadAllBytes(e.Config)))throw new Exception("启动配置已被其他程序修改；请刷新后重试。");ValidateHeapBudget(value,memoryProvider);File.Replace(temp,e.Config,null);e.LastBackup=backupDir;
   }
   finally{if(File.Exists(temp))File.Delete(temp);}
   return "最大堆内存已设为 "+value+" GiB；已备份启动配置："+backup;
  }
  static bool Number(string value){int number;return int.TryParse(value,System.Globalization.NumberStyles.AllowLeadingSign,System.Globalization.CultureInfo.InvariantCulture,out number)&&number.ToString(System.Globalization.CultureInfo.InvariantCulture)==value;}
  internal static string[] ChunkTargets(Engine e){
   string mp=Path.Combine(e.User,"Saves","Multiplayer");NoReparse(mp);var files=new List<string>();if(!Directory.Exists(mp))return files.ToArray();
   foreach(string server in Directory.GetDirectories(mp)){
    NoReparse(server);
    // Legacy clients used save-root map_x_y.bin; never scan arbitrary subfolders.
    foreach(string f in Directory.GetFiles(server)){NoReparse(f);var m=Regex.Match(Path.GetFileName(f),@"\Amap_([^_]+)_([^_]+)\.bin\z",RegexOptions.CultureInvariant);if(m.Success&&Number(m.Groups[1].Value)&&Number(m.Groups[2].Value))files.Add(f);}
    // Verified in B42.21 zombie.ChunkMapFilenames.getFilename(int,int): map/x/y.bin.
    string map=Path.Combine(server,"map");NoReparse(map);if(!Directory.Exists(map))continue;
    foreach(string x in Directory.GetDirectories(map)){NoReparse(x);if(!Number(Path.GetFileName(x)))continue;foreach(string f in Directory.GetFiles(x)){NoReparse(f);if(Path.GetExtension(f)==".bin"&&Number(Path.GetFileNameWithoutExtension(f)))files.Add(f);}}
   }
   return files.ToArray();
  }
  public static string ClearMultiplayerCache(Engine e){
   e.GuardMaintenance();string[] files=ChunkTargets(e);
   if(files.Length==0)return "未发现可清理的联机地图区块缓存；未删除任何文件。仅识别旧版 map_x_y.bin 和新版 map/x/y.bin 区块文件；存档目录、角色和车辆数据库、地图标记及世界元数据会保留。";
   int count=0;foreach(string f in files){e.GuardMaintenance();NoReparse(f);File.Delete(f);count++;if(e.Progress!=null)e.Progress(count*100/files.Length,"清理联机区块缓存 "+count+" / "+files.Length);}
   return "已清理 "+count+" 个已核实的联机地图区块缓存。保留角色、车辆数据库、地图标记、其他存档和本地模组；重连时由服务器重新发送区块。";
  }
  static void MultiplayerChild(string root,string path){if(!Path.GetFullPath(path).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("多人本地数据路径超出允许清理的目录。");NoReparse(path);}
  public static string ClearAllMultiplayerData(Engine e){
   int removedFiles=0,removedDirectories=0;
   try{
    e.GuardMaintenance();string mp=Path.GetFullPath(Path.Combine(e.User,"Saves","Multiplayer"));NoReparse(mp);
    if(!Directory.Exists(mp))return "未发现多人本地数据；未删除任何文件或目录（0 个文件、0 个目录）。";
    var files=new List<string>();var directories=new List<string>();var pending=new Stack<string>();pending.Push(mp);
    // Inspect the complete tree before deleting anything, including unknown subfolders.
    while(pending.Count>0){string dir=pending.Pop();NoReparse(dir);foreach(string f in Directory.GetFiles(dir)){MultiplayerChild(mp,f);files.Add(f);}foreach(string child in Directory.GetDirectories(dir)){MultiplayerChild(mp,child);directories.Add(child);pending.Push(child);}}
    int total=files.Count+directories.Count;if(total==0)return "未发现需要彻底清除的多人本地数据；未删除任何文件或目录（0 个文件、0 个目录）。";
    foreach(string f in files){e.GuardMaintenance();MultiplayerChild(mp,f);if(!File.Exists(f))continue;File.Delete(f);removedFiles++;if(e.Progress!=null)e.Progress((removedFiles+removedDirectories)*100/total,"彻底清除多人本地数据：已删除 "+removedFiles+" 个文件、"+removedDirectories+" 个目录");}
    // Every child is listed after its parent; reverse order permits nonrecursive deletes.
    for(int i=directories.Count-1;i>=0;i--){string dir=directories[i];e.GuardMaintenance();MultiplayerChild(mp,dir);if(!Directory.Exists(dir))continue;Directory.Delete(dir,false);removedDirectories++;if(e.Progress!=null)e.Progress((removedFiles+removedDirectories)*100/total,"彻底清除多人本地数据：已删除 "+removedFiles+" 个文件、"+removedDirectories+" 个目录");}
    e.GuardMaintenance();NoReparse(mp);if(Directory.GetFileSystemEntries(mp).Length!=0)throw new Exception("多人目录在清理期间发生变化，请退出占用后重试。");
    return "已彻底清除多人本地数据：删除 "+removedFiles+" 个文件、"+removedDirectories+" 个目录。下次进服将重新同步服务器数据；服务器未保存的本地角色数据和地图标记无法恢复。";
   }catch(Exception ex){throw new Exception("彻底清除多人本地数据未完成；已删除 "+removedFiles+" 个文件、"+removedDirectories+" 个目录。请退出占用后重试；已删除的本地数据无法自动恢复。原因："+ex.Message,ex);}
  }
  public static string RebuildWorkshop(Engine e,Action<int,string> progress,CancellationToken cancel){e.GuardMaintenance();cancel.ThrowIfCancellationRequested();return LauncherWorkshopRebuild.Start(e,progress,cancel);}
 }
}
