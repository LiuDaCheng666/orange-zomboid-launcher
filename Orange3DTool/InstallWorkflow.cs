using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace PZ3D {
 static class InstallWorkflow {
  public static bool KeepModels(Engine engine,bool selected){return selected||(File.Exists(engine.Mods)&&System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(engine.Mods),@"(?m)^\s*mod\s*=\s*PZVoxelStudioViewpoint\s*,"));}
  internal static string FrozenSource(Engine engine,ulong id){var wrappers=engine.FrozenEnvironment.ModRoots.Where(row=>row.WorkshopId==id.ToString()).Select(row=>Path.GetDirectoryName(Path.GetDirectoryName(row.CopiedRoot))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(wrappers.Length>1)throw new IOException("此工坊条目来自多个独立快照来源，无法唯一确定组件目录。");return wrappers.Length==0?Path.Combine(engine.Workshop,id.ToString()):wrappers[0];}
  public static string Source(Engine engine,ulong id){if(engine.FrozenEnvironment!=null)return FrozenSource(engine,id);string file=Path.Combine(engine.Data,"workshop-paths.json");try{if(File.Exists(file)){var paths=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(file));string p;if(paths.TryGetValue(id.ToString(),out p)&&Directory.Exists(p))return Path.GetFullPath(p);}}catch{}return Path.Combine(engine.Workshop,id.ToString());}
  internal static string Source(Engine engine,ulong id,LaunchReadBudget budget){if(engine.FrozenEnvironment!=null)return FrozenSource(engine,id);if(budget==null)return Source(engine,id);try{byte[] bytes=budget.Read(Path.Combine(engine.Data,"workshop-paths.json"),1048576);if(bytes!=null){var paths=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=8}.Deserialize<Dictionary<string,string>>(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));if(paths==null||paths.Count>2000)throw new IOException("工坊来源记录超出预检限制。");string path;if(paths.TryGetValue(id.ToString(),out path)){if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path))throw new IOException("工坊来源记录无效。");LauncherMaintenance.NoReparse(path);if(Directory.Exists(path))return Path.GetFullPath(path);}}return Path.Combine(engine.Workshop,id.ToString());}catch{budget.Unknown();throw;}}
  public static ulong[] Requested(bool models){return models?new ulong[]{3619862853,3809306528,3810302175}:new ulong[]{3619862853,3809306528};}
  public static void Apply(Engine engine,Dictionary<ulong,string> paths){string p;if(paths.TryGetValue(3619862853,out p))engine.BuddySource=p;if(paths.TryGetValue(3809306528,out p))engine.ViewSource=p;if(paths.TryGetValue(3810302175,out p))engine.ModelsSource=p;}
  public static string Enable(Engine engine,bool models,Func<ulong[],Dictionary<ulong,string>> fetch){engine.Guard();if(!engine.HasView||!File.Exists(Path.Combine(engine.BuddyLib,"ZombieBuddy.jar"))||!File.Exists(Path.Combine(engine.BuddyLib,"zbNative.dll"))||(models&&!engine.HasModels)){var paths=fetch(Requested(models));Apply(engine,paths);}return engine.Switch(true,models);}
 }
}
