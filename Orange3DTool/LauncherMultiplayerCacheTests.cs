using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace PZ3D {
 static class LauncherMultiplayerCacheTests {
  static readonly byte[] Sentinel={0,255,73,0,200,17,128,42};
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static bool Denied(Action action){try{action();return false;}catch{return true;}}
  static string PathIn(string root,string relative){return Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar));}
  static void Put(string root,string relative){string file=PathIn(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,Sentinel);}
  static bool Intact(string root,string relative){string file=PathIn(root,relative);return File.Exists(file)&&File.ReadAllBytes(file).SequenceEqual(Sentinel);}
  static Engine Fixture(string name,bool test=true,Func<string,Process[]> inventory=null){
   string root=Path.Combine(LauncherTestPaths.Root,"orange-cache-"+name+"-"+Guid.NewGuid().ToString("N"));
   var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),test,SteamLaunchOptions.Unavailable,inventory);
   Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx2g\"]}");return e;
  }
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  public static void Run(){VerifiedLayouts();EmptyTargets();ReparsePreflight();RunningGuard();Console.WriteLine("MULTIPLAYER CACHE TESTS PASSED");}
  static void VerifiedLayouts(){
   var e=Fixture("layouts");string mp=PathIn(e.User,"Saves/Multiplayer");
   // Literal fixtures follow legacy map_x_y.bin and B42 map/x/y.bin filenames.
   string[] chunks={"legacy/map_12_34.bin","legacy/map_-1_0.bin","legacy/map_2147483647_-2147483648.bin","b42/map/12/34.bin","b42/map/-1/0.bin","b42/map/2147483647/-2147483648.bin"};
   string[] protectedFiles={"legacy/players.db","legacy/players.db-wal","legacy/players.db-shm","legacy/vehicles.db","legacy/vehicles.db-journal","legacy/map_symbols.bin","legacy/map_t.bin","legacy/map_basements.bin","legacy/map_meta.bin","legacy/map_zone.bin","legacy/recorded_media.bin","legacy/serverid.dat","legacy/thumb.png","legacy/WorldDictionaryReadable.lua","legacy/unknown.bin","legacy/map_1_2.bin.tmp","legacy/map_01_2.bin","legacy/map_1_02.bin","legacy/map_-0_2.bin","legacy/map_2147483648_2.bin","legacy/custom/map_1_2.bin","b42/map/12/note.bin","b42/map/12/34.bin.tmp","b42/map/12/034.bin","b42/map/12/-0.bin","b42/map/12/2147483648.bin","b42/map/012/34.bin","b42/map/word/34.bin","b42/map/12/sub/34.bin","b42/map/map_1_2.bin","b42/chunkdata/12/34.bin","b42/apop/12/34.bin","b42/isoregiondata/datachunk_1_2.bin","b42/metagrid/12/34.bin","b42/zpop/12/34.bin"};
   foreach(string f in chunks.Concat(protectedFiles))Put(mp,f);
   string[] otherFiles={"Saves/Sandbox/local/map_12_34.bin","Saves/Sandbox/local/map/12/34.bin","mods/author/map_12_34.bin","mods/author/mod.info"};foreach(string f in otherFiles)Put(e.User,f);
   string result=LauncherMaintenance.ClearMultiplayerCache(e);
   Check(chunks.All(f=>!File.Exists(PathIn(mp,f))),"legacy flat and B42 signed-int map chunks are deleted across multiplayer saves");
   Check(result.Contains(" 6 "),"cleanup reports exactly the six deleted chunk files");
   Check(protectedFiles.All(f=>Intact(mp,f)),"player and vehicle databases, annotations, metadata, unknown layouts and unrelated caches retain exact bytes");
   Check(otherFiles.All(f=>Intact(e.User,f)),"single player saves and local mods retain exact bytes");
   Check(Directory.Exists(PathIn(mp,"b42/map/12"))&&Directory.Exists(PathIn(mp,"legacy")),"cleanup retains save and cache directories");
  }
  static void EmptyTargets(){
   var e=Fixture("empty");string mp=PathIn(e.User,"Saves/Multiplayer/server");
   foreach(string d in new[]{"map","chunkdata","apop","zpop","metagrid","isoregiondata"})Directory.CreateDirectory(PathIn(mp,d));
   string[] files={"map_basements.bin","map_symbols.bin","map_t.bin","recorded_media.bin","serverid.dat","vehicles.db","vehicles.db-journal","InGameMap.ini","thumb.png","WorldDictionaryReadable.lua"};foreach(string f in files)Put(mp,f);
   string result=LauncherMaintenance.ClearMultiplayerCache(e);
   Check(result.Contains("未发现")&&result.Contains("未删除")&&!result.Contains("已清理"),"empty real B42 layout reports no targets without claiming cleanup succeeded");
   Check(files.All(f=>Intact(mp,f)),"empty chunk folders never cause metadata deletion");
   var missing=Fixture("missing");string absent=LauncherMaintenance.ClearMultiplayerCache(missing);
   Check(absent.Contains("未发现")&&!Directory.Exists(PathIn(missing.User,"Saves")),"missing multiplayer saves report no targets without creating folders");
  }
  static void ReparsePreflight(){
   var e=Fixture("junction");string mp=PathIn(e.User,"Saves/Multiplayer/server"),outside=PathIn(e.Data,"outside"),link=PathIn(mp,"map/99");Put(mp,"map_1_2.bin");Put(outside,"9.bin");Directory.CreateDirectory(Path.GetDirectoryName(link));
   using(var p=Process.Start(new ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){p.WaitForExit();Check(p.ExitCode==0,"owned fixture junction created");}
   Check(Denied(()=>LauncherMaintenance.ClearMultiplayerCache(e)),"cache discovery rejects reparse paths before deleting any target");
   Check(Intact(mp,"map_1_2.bin")&&Intact(outside,"9.bin"),"junction rejection preserves both ordinary targets and outside bytes");
  }
  static void RunningGuard(){
   bool occupied=true;Func<string,Process[]> inventory=name=>occupied&&name=="ProjectZomboid64"?new[]{Process.GetCurrentProcess()}:new Process[0];
   var e=Fixture("guard",false,inventory);string mp=PathIn(e.User,"Saves/Multiplayer/server");string[] files={"map_1_2.bin","map_3_4.bin","map_5_6.bin"};foreach(string f in files)Put(mp,f);
   Check(Denied(()=>LauncherMaintenance.ClearMultiplayerCache(e))&&files.All(f=>Intact(mp,f)),"occupied runtime blocks cleanup before any chunk is deleted");
   occupied=false;e.Progress=(v,t)=>occupied=true;
   Check(Denied(()=>LauncherMaintenance.ClearMultiplayerCache(e)),"cleanup rechecks occupancy before each chunk deletion");
   Check(files.Count(f=>Intact(mp,f))==2,"new runtime occupancy stops cleanup after the first chunk");
  }
 }
}
