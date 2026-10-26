using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace PZ3D {
 sealed partial class ModernController {
  Engine rememberedEnvironment;DateTime environmentWarning=DateTime.MinValue;
  Engine LaunchEngineContext {get{lock(startupGate){return startupSessionId!=null&&startupAttempt!=null&&startupAttempt.Launch.SessionId==startupSessionId&&startupAttempt.ResolvedEngine!=null?startupAttempt.ResolvedEngine:engine;}}}
  void ResetEnvironmentEngines(){rememberedEnvironment=null;}
  void RememberEnvironment(Engine selected){if(selected!=null&&selected.FrozenEnvironment!=null)rememberedEnvironment=selected;}
  string[] EnvironmentGamePaths(){
   try{return new[]{engine.Game}.Concat(new SaveEnvironments(engine).ProcessGames()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
   catch(Exception error){if(DateTime.UtcNow>=environmentWarning){environmentWarning=DateTime.UtcNow.AddSeconds(10);try{journal.Diagnostic("快照进程目录读取未完成："+error.Message);}catch{}}return new[]{engine.Game}.Concat(rememberedEnvironment==null?new string[0]:new[]{rememberedEnvironment.Game}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  }
  // Enumerate named processes once per observation, reopening only those PIDs
  // for later path checks because GameProcessGuard disposes each observation.
  Func<string,System.Diagnostics.Process[]> EnvironmentInventory(){
   var ids=new Dictionary<string,int[]>(StringComparer.OrdinalIgnoreCase);var source=engine;
   return name=>{int[] known;if(!ids.TryGetValue(name,out known)){var first=source.ProcessInventory(name);var captured=new List<int>();foreach(var process in first)try{captured.Add(process.Id);}catch{}ids[name]=captured.ToArray();return first;}var result=new List<System.Diagnostics.Process>();foreach(int id in known)try{result.Add(System.Diagnostics.Process.GetProcessById(id));}catch{}return result.ToArray();};
  }
  bool EnvironmentRunning(bool clientOnly){if(test)return false;var inventory=EnvironmentInventory();return EnvironmentGamePaths().Any(game=>clientOnly?GameProcessGuard.IsClientRunning(game,inventory):GameProcessGuard.IsRunning(game,inventory));}
  Engine RuntimeEngine(string game){if(VersionLibrary.Same(engine.Game,game))return engine;if(rememberedEnvironment!=null&&VersionLibrary.Same(rememberedEnvironment.Game,game))return rememberedEnvironment;var selected=new Engine(game,engine.User,data,test,engine.ReadSteamOptions,engine.ProcessInventory){ManagedVersion=true};if(selected.FrozenEnvironment==null)throw new IOException("快照运行目录未通过登记校验。");RememberEnvironment(selected);return selected;}
  Engine CurrentRuntimeEngine(){
   var paths=EnvironmentGamePaths();if(!test){var inventory=EnvironmentInventory();foreach(string game in paths)if(GameProcessGuard.IsClientRunning(game,inventory))return RuntimeEngine(game);}
   lock(startupGate){if(startupAttempt!=null&&startupAttempt.Engine==engine&&startupAttempt.ResolvedEngine!=null&&startupAttempt.ResolvedEngine.FrozenEnvironment!=null&&(startupAttempt.Phase=="observed"||startupAttempt.Phase=="submitted"||startupAttempt.Phase=="committed"))return startupAttempt.ResolvedEngine;}
   foreach(string game in paths){string token=DlssInstaller.Hash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToUpperInvariant()+"|"+Path.GetFullPath(engine.User).ToUpperInvariant()));string journalPath=Path.Combine(data,"launch-sessions",token+".json");LauncherMaintenance.NoReparse(journalPath);if(File.Exists(journalPath))return RuntimeEngine(game);}
   return engine;
  }
  void RecoverEnvironmentSessions(){
   foreach(string game in EnvironmentGamePaths()){
    string token=DlssInstaller.Hash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToUpperInvariant()+"|"+Path.GetFullPath(engine.User).ToUpperInvariant()));string path=Path.Combine(data,"launch-sessions",token+".json");LauncherMaintenance.NoReparse(path);if(!File.Exists(path))continue;
    try{string recovered=LaunchProfiles.Recover(RuntimeEngine(game));if(recovered!="")lock(gate)log.Add(recovered);}catch(Exception error){lock(gate)log.Add("启动恢复未完成："+error.Message);}
   }
  }
  Engine ResolveEnvironmentEngine(Engine source,string kind,string target){
   // No catalog means there cannot be a binding. Keep ordinary launch behavior
   // independent of snapshot inventory and preserve the early loading canvas.
   if(kind!="save"||!File.Exists(Path.Combine(source.Data,"save-environments","index.json")))return source;
   var selected=SaveEnvironmentRuntime.ForSave(source,target);RememberEnvironment(selected);return selected;
  }
  void RecoverLaunchEnvironment(Engine selected,string kind,string target){
   if(selected.FrozenEnvironment==null){RecoverStartup();return;}
   string recovered=LaunchProfiles.Recover(selected);if(recovered!="")lock(gate)log.Add(recovered);
   if(LaunchProfiles.Pending(selected))throw new IOException("此存档的上次启动尚未结束，请先退出游戏再重试。");
   if(kind!="save")throw new IOException("游戏与模组快照只用于原绑定的单人存档。");
   SaveEnvironmentRuntime.PrepareForLaunch(selected,target);
  }
  void ValidateEnvironmentBinding(Engine selected){
   StartupAttempt attempt;lock(startupGate)attempt=startupAttempt;
   if(attempt==null||attempt.Launch.TargetKind!="save")return;
   var current=ResolveEnvironmentEngine(attempt.Engine,"save",attempt.TargetId);
   if(!VersionLibrary.Same(current.Game,selected.Game)||!VersionLibrary.Same(current.User,selected.User)||(current.FrozenEnvironment==null)!=(selected.FrozenEnvironment==null)||current.FrozenEnvironment!=null&&current.FrozenEnvironment.Id!=selected.FrozenEnvironment.Id)throw new IOException("此存档使用的游戏与模组快照已变化，请重新启动。");
   SaveEnvironmentRuntime.Validate(selected,attempt.TargetId);
  }
 }
}
