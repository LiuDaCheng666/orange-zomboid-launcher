using System;
using System.Collections.Generic;
using System.IO;
namespace PZ3D {
 static class InstallWorkflow {
  public static bool KeepModels(Engine engine,bool selected){return selected||(File.Exists(engine.Mods)&&System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(engine.Mods),@"(?m)^\s*mod\s*=\s*PZVoxelStudioViewpoint\s*,"));}
  public static string Source(Engine engine,ulong id){string file=Path.Combine(engine.Data,"workshop-paths.json");try{if(File.Exists(file)){var paths=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,string>>(File.ReadAllText(file));string p;if(paths.TryGetValue(id.ToString(),out p)&&Directory.Exists(p))return Path.GetFullPath(p);}}catch{}return Path.Combine(engine.Workshop,id.ToString());}
  public static ulong[] Requested(bool models){return models?new ulong[]{3619862853,3809306528,3810302175}:new ulong[]{3619862853,3809306528};}
  public static void Apply(Engine engine,Dictionary<ulong,string> paths){string p;if(paths.TryGetValue(3619862853,out p))engine.BuddySource=p;if(paths.TryGetValue(3809306528,out p))engine.ViewSource=p;if(paths.TryGetValue(3810302175,out p))engine.ModelsSource=p;}
  public static string Enable(Engine engine,bool models,Func<ulong[],Dictionary<ulong,string>> fetch){engine.Guard();if(!engine.HasView||!File.Exists(Path.Combine(engine.BuddyLib,"ZombieBuddy.jar"))||!File.Exists(Path.Combine(engine.BuddyLib,"zbNative.dll"))||(models&&!engine.HasModels)){var paths=fetch(Requested(models));Apply(engine,paths);}return engine.Switch(true,models);}
 }
}
