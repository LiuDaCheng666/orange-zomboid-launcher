using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class LauncherMultiplayerDataTests {
  static readonly byte[] Sentinel={0,255,73,0,200,17,128,42};
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static string PathIn(string root,string relative){return Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));}
  static void Put(string root,string relative){string file=PathIn(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,Sentinel);}
  static bool Intact(string root,string relative){string file=PathIn(root,relative);return File.Exists(file)&&File.ReadAllBytes(file).SequenceEqual(Sentinel);}
  static string Failure(Action action){try{action();return "";}catch(Exception x){return x.Message;}}
  static Engine Fixture(string name,bool test=true,Func<string,Process[]> inventory=null){
   string root=Path.Combine(LauncherTestPaths.Root,"orange-mp-all-"+name+"-"+Guid.NewGuid().ToString("N"));
   var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),test,SteamLaunchOptions.Unavailable,inventory);
   Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx2g\"]}");return e;
  }
  static string Clear(Engine e){using(var c=new ModernController(e.Game,e.User,e.Data,e.TestMode)){typeof(ModernController).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,e);return new JavaScriptSerializer().Serialize(c.Execute("maintenance",new Dictionary<string,object>{{"kind","cacheAll"},{"confirmed",true}}));}}
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  public static void Run(){WholeTree();EmptyTree();ReparsePreflight();RuntimeAndPartialFailure();LockedFile();ChangedTree();DispatchPolicy();Console.WriteLine("MULTIPLAYER FULL DATA TESTS PASSED");}
  static void WholeTree(){
   var e=Fixture("whole");string mp=PathIn(e.User,"Saves/Multiplayer");
   string[] files={"server/players.db","server/vehicles.db","server/vehicles.db-journal","server/map_symbols.bin","server/map_t.bin","server/map_basements.bin","server/recorded_media.bin","server/serverid.dat","server/InGameMap.ini","server/thumb.png","server/WorldDictionaryReadable.lua","server/map/12/34.bin","second/unknown/plugin-data.custom","orphan.tmp"};foreach(string f in files)Put(mp,f);
   foreach(string d in new[]{"server/apop","server/chunkdata","server/zpop","server/metagrid","server/isoregiondata","server/empty/deep"})Directory.CreateDirectory(PathIn(mp,d));
   string[] keep={"Saves/Sandbox/local/map/12/34.bin","Saves/Sandbox/local/players.db","Saves/MultiplayerOther/local/map_1_2.bin","mods/author/mod.info","options.ini","Server/local.ini"};foreach(string f in keep)Put(e.User,f);
   string result=Clear(e);
   Check(Directory.Exists(mp)&&Directory.GetFileSystemEntries(mp).Length==0,"explicit cacheAll dispatch clears every multiplayer file and nested directory while retaining Multiplayer root");
   Check(result.Contains("14 个文件")&&result.Contains("12 个目录"),"full cleanup reports actual file and directory counts");
   Check(result.Contains("标记")&&result.Contains("无法恢复"),"full cleanup warns that local annotations cannot be restored automatically");
   Check(keep.All(f=>Intact(e.User,f)),"full cleanup preserves exact single player saves, mods, preferences and local server configuration bytes");
   var task=new JavaScriptSerializer().Deserialize<LauncherTaskStore>(File.ReadAllText(PathIn(e.Data,"diagnostics/tasks.json"))).history[0];Check(task.title=="彻底清除多人本地数据"&&task.status=="completed","full cleanup has its own completed task title");
  }
  static void EmptyTree(){
   var e=Fixture("empty");string mp=PathIn(e.User,"Saves/Multiplayer");Directory.CreateDirectory(PathIn(mp,"empty/nested"));string result=Clear(e);
   Check(result.Contains("0 个文件")&&result.Contains("2 个目录")&&Directory.GetFileSystemEntries(mp).Length==0,"full cleanup removes empty nested directories and reports zero files honestly");
   var absent=Fixture("absent");result=Clear(absent);Check(result.Contains("未发现")&&!Directory.Exists(PathIn(absent.User,"Saves/Multiplayer")),"missing multiplayer data reports no work without creating save folders");
  }
  static void ReparsePreflight(){
   var e=Fixture("junction");string mp=PathIn(e.User,"Saves/Multiplayer"),outside=PathIn(e.User,"Saves/Sandbox/keep"),link=PathIn(mp,"server/unknown/deep");Put(mp,"first.bin");Put(outside,"players.db");Directory.CreateDirectory(Path.GetDirectoryName(link));
   using(var p=Process.Start(new ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){p.WaitForExit();Check(p.ExitCode==0,"full cleanup fixture junction created");}
   string error=Failure(()=>Clear(e));Check(error.Contains("未完成")&&error.Contains("0 个文件")&&error.Contains("0 个目录"),"full-tree preflight rejects deep reparse points before any deletion and reports zero changes");
   Check(Intact(mp,"first.bin")&&Intact(outside,"players.db"),"reparse preflight preserves both multiplayer and outside bytes");
  }
  static void RuntimeAndPartialFailure(){
   bool occupied=true;Func<string,Process[]> inventory=name=>occupied&&name=="ProjectZomboid64"?new[]{Process.GetCurrentProcess()}:new Process[0];var e=Fixture("runtime",false,inventory);string mp=PathIn(e.User,"Saves/Multiplayer/server");string[] files={"players.db","vehicles.db","map_symbols.bin"};foreach(string f in files)Put(mp,f);
   Check(Failure(()=>Clear(e))!=""&&files.All(f=>Intact(mp,f)),"full cleanup refuses occupied runtime before changing any local data");
   occupied=false;e.Progress=(v,t)=>occupied=true;string error=Failure(()=>Clear(e));
   Check(error.Contains("未完成")&&error.Contains("1 个文件")&&error.Contains("0 个目录")&&files.Count(f=>Intact(mp,f))==2,"new runtime occupancy stops full cleanup with exact partial deletion counts");
   var task=new JavaScriptSerializer().Deserialize<LauncherTaskStore>(File.ReadAllText(PathIn(e.Data,"diagnostics/tasks.json"))).history[0];Check(task.status=="failed"&&task.progress!=100&&task.message.Contains("1 个文件"),"partially deleted full cleanup remains failed in task history with actual counts");
  }
  static void LockedFile(){
   var e=Fixture("locked");string mp=PathIn(e.User,"Saves/Multiplayer/server");Put(mp,"vehicles.db");string error;
   using(var held=new FileStream(PathIn(mp,"vehicles.db"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))error=Failure(()=>Clear(e));
   Check(error.Contains("未完成")&&error.Contains("0 个文件")&&Intact(mp,"vehicles.db"),"locked database produces a failure with zero deleted files and preserves exact bytes");
  }
  static void ChangedTree(){
   var e=Fixture("changed");string mp=PathIn(e.User,"Saves/Multiplayer");Put(mp,"server/players.db");e.Progress=(v,t)=>Put(mp,"new-data.bin");string error=Failure(()=>Clear(e));
   Check(error.Contains("未完成")&&error.Contains("1 个文件")&&error.Contains("1 个目录")&&Intact(mp,"new-data.bin"),"new files appearing after preflight are preserved and prevent a false full-cleanup success");
  }
  static void DispatchPolicy(){
   Func<string,bool> valid=payload=>Failure(()=>ModernHostPolicy.Validate("https://orange.local/index.html","{\"id\":\"mp-all\",\"action\":\"maintenance\",\"payload\":"+payload+"}"))=="";
   foreach(string kind in new[]{"cache","cacheAll","workshop"})Check(valid("{\"kind\":\""+kind+"\",\"confirmed\":true}"),"maintenance whitelist accepts explicit confirmed kind "+kind);
   foreach(string payload in new[]{"{}","{\"kind\":\"cacheAll\"}","{\"kind\":\"cacheAll\",\"confirmed\":false}","{\"kind\":\"cacheAll\",\"confirmed\":\"true\"}","{\"kind\":\"unknown\",\"confirmed\":true}","{\"kind\":\"cacheAll\",\"confirmed\":true,\"path\":\"C:\\\\outside\"}"})Check(!valid(payload),"maintenance policy rejects missing confirmation, invalid kind or forged target");
   var e=Fixture("confirmation");string mp=PathIn(e.User,"Saves/Multiplayer/server");Put(mp,"players.db");using(var c=new ModernController(e.Game,e.User,e.Data,true)){Check(Failure(()=>c.Execute("maintenance",new Dictionary<string,object>{{"kind","cacheAll"},{"confirmed",false}}))!=""&&Intact(mp,"players.db"),"direct full cleanup controller dispatch requires explicit confirmation");Check(Failure(()=>c.Execute("maintenance",new Dictionary<string,object>{{"kind","cacheAll"},{"confirmed",true},{"path","outside"}}))!=""&&Intact(mp,"players.db"),"direct full cleanup controller dispatch rejects frontend target paths");}
  }
 }
}
