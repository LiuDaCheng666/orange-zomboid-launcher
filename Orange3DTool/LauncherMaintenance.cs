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
  public static LauncherMemoryInfo MemoryInfo(){var m=new MemoryStatus();if(!GlobalMemoryStatusEx(m))throw new Exception("无法读取物理内存；未修改配置。");int total=(int)(m.Total/GiB),available=(int)(m.Available/GiB);int reserve=Math.Max(4,(int)Math.Ceiling(total*.25));int max=Math.Max(0,Math.Min(total-reserve,available-2));return new LauncherMemoryInfo{TotalGiB=total,AvailableGiB=available,MaximumHeapGiB=max,RecommendedHeapGiB=Math.Min(8,max)};}
  // Existing callers may display this before starting the isolated worker.
  public static bool WorkshopRebuildSupported {get{return true;}}
  public static string WorkshopRebuildUnavailableReason {get{return "";}}
  internal static void NoReparse(string path){string current=Path.GetFullPath(path);while(current!=null){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝重解析点 / 联接路径："+current);current=Path.GetDirectoryName(current);}}
  static Dictionary<string,object> Read(Engine e){NoReparse(e.Config);var j=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(e.Config));if(j==null||!j.ContainsKey("vmArgs"))throw new Exception("启动 JSON 缺少 vmArgs。");Arrays(j).ToArray();return j;}
  static IEnumerable<List<string>> Arrays(Dictionary<string,object> config){
   object raw;if(config.TryGetValue("vmArgs",out raw)){var items=raw as IEnumerable;if(raw is string||items==null)throw new Exception("vmArgs 必须是字符串数组。");var args=items.Cast<object>().ToArray();if(args.Any(a=>!(a is string)))throw new Exception("vmArgs 含非字符串参数。");var list=args.Cast<string>().ToList();foreach(string s in list)if(s.StartsWith("-Xmx",StringComparison.Ordinal)||s.StartsWith("-Xms",StringComparison.Ordinal))HeapBytes(s);yield return list;}
   object windows;if(config.TryGetValue("windows",out windows)){var branches=windows as Dictionary<string,object>;if(branches==null)throw new Exception("windows 分支格式错误。");foreach(var branch in branches.Values){var d=branch as Dictionary<string,object>;if(d==null)throw new Exception("windows 启动分支格式错误。");foreach(var a in Arrays(d))yield return a;}}
  }
  static long HeapBytes(string arg){var m=Regex.Match(arg,@"^-Xm[xs]([1-9][0-9]*)([kKmMgG]?)$");long n;if(!m.Success||!long.TryParse(m.Groups[1].Value,out n))throw new Exception("无法识别堆内存参数："+arg);long factor=m.Groups[2].Value.Length==0?1:m.Groups[2].Value.Equals("g",StringComparison.OrdinalIgnoreCase)?(long)GiB:m.Groups[2].Value.Equals("m",StringComparison.OrdinalIgnoreCase)?1048576:1024;if(n>long.MaxValue/factor)throw new Exception("堆内存参数超出范围。");return n*factor;}
  public static int ReadHeapGiB(Engine e){var args=Arrays(Read(e)).First();string last=args.LastOrDefault(a=>a.StartsWith("-Xmx",StringComparison.Ordinal));if(last==null)throw new Exception("启动配置未设置 Xmx；请选择内存后保存。");return checked((int)Math.Ceiling((double)HeapBytes(last)/GiB));}
  static void Normalize(Dictionary<string,object> config,int value){object raw;if(config.TryGetValue("vmArgs",out raw)){var result=new List<string>();bool xmx=false;foreach(string arg in ((IEnumerable)raw).Cast<string>()){if(arg.StartsWith("-Xmx",StringComparison.Ordinal)){if(!xmx)result.Add("-Xmx"+value+"g");xmx=true;}else if(arg.StartsWith("-Xms",StringComparison.Ordinal)&&HeapBytes(arg)>(long)value*(long)GiB)result.Add("-Xms"+value+"g");else result.Add(arg);}if(!xmx)result.Add("-Xmx"+value+"g");config["vmArgs"]=result.ToArray();}object win;if(config.TryGetValue("windows",out win))foreach(var branch in ((Dictionary<string,object>)win).Values)Normalize((Dictionary<string,object>)branch,value);}
  public static string SetHeapGiB(Engine e,int value){e.Guard();if(value<1||value>MemoryInfo().MaximumHeapGiB)throw new Exception("内存超出可用物理内存范围；需为系统保留至少 4 GiB 或 25%，当前空闲内存另保留 2 GiB。");NoReparse(e.Config);byte[] before=File.ReadAllBytes(e.Config);var config=Read(e);if(!before.SequenceEqual(File.ReadAllBytes(e.Config)))throw new Exception("启动配置已被其他程序修改；请刷新后重试。");Normalize(config,value);NoReparse(e.Data);string backupDir=Path.Combine(e.Data,"backups","heap-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N"));NoReparse(backupDir);Directory.CreateDirectory(backupDir);string backup=Path.Combine(backupDir,"ProjectZomboid64.json"),temp=e.Config+".heap-"+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(config),new UTF8Encoding(false));if(e.Progress!=null)e.Progress(-1,"准备原子写入堆内存配置");e.Guard();NoReparse(e.Config);if(!before.SequenceEqual(File.ReadAllBytes(e.Config)))throw new Exception("启动配置已被其他程序修改；请刷新后重试。");if(value>MemoryInfo().MaximumHeapGiB)throw new Exception("可用内存已减少；请刷新后重试。");File.Replace(temp,e.Config,backup);e.LastBackup=backupDir;}finally{if(File.Exists(temp))File.Delete(temp);}return "最大堆内存已设为 "+value+" GiB；已备份启动配置："+backup;}
  static bool Number(string value){return Regex.IsMatch(value,@"^-?(0|[1-9][0-9]*)$");}
  internal static string[] ChunkTargets(Engine e){string mp=Path.Combine(e.User,"Saves","Multiplayer");NoReparse(mp);var files=new List<string>();if(!Directory.Exists(mp))return files.ToArray();foreach(string server in Directory.GetDirectories(mp)){NoReparse(server);string map=Path.Combine(server,"map");NoReparse(map);if(!Directory.Exists(map))continue;foreach(string x in Directory.GetDirectories(map)){NoReparse(x);if(!Number(Path.GetFileName(x)))continue;foreach(string f in Directory.GetFiles(x)){NoReparse(f);if(Path.GetExtension(f)==".bin"&&Number(Path.GetFileNameWithoutExtension(f)))files.Add(f);}}}return files.ToArray();}
  public static string ClearMultiplayerCache(Engine e){e.Guard();string[] files=ChunkTargets(e);int count=0;foreach(string f in files){e.Guard();NoReparse(f);File.Delete(f);count++;if(e.Progress!=null)e.Progress(count*100/files.Length,"清理联机区块缓存 "+count+" / "+files.Length);}return "已清理 "+count+" 个已核实的联机地图区块缓存。保留角色、车辆数据库、地图标记、其他存档和本地模组；重连时由服务器重新发送区块。";}
  public static string RebuildWorkshop(Engine e,Action<int,string> progress,CancellationToken cancel){e.Guard();cancel.ThrowIfCancellationRequested();return LauncherWorkshopRebuild.Start(e,progress,cancel);}
 }
}
