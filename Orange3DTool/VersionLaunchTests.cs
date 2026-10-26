using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections;
using System.Collections.Generic;
namespace PZ3D {
 static class VersionLaunchTests {
  static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;}
  static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text);}
  static void Jar(string root,string game,int major){byte[] bytes=(byte[])typeof(GameVersionProbeTests).GetMethod("Compile",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{root,"new GameVersion("+major+",78,\".16\")"});string path=Path.Combine(game,"projectzomboid.jar");if(File.Exists(path))File.Delete(path);using(var archive=ZipFile.Open(path,ZipArchiveMode.Create))using(var stream=archive.CreateEntry("zombie/core/Core.class").Open())stream.Write(bytes,0,bytes.Length);}
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  public static void Run(){string root=Path.Combine(LauncherTestPaths.Root,"version-launch-"+Guid.NewGuid().ToString("N"));try{
   string game=Path.Combine(root,"B41 game"),user=Path.Combine(root,"B41 user 中文"),data=Path.Combine(root,"data");Put(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");Jar(root,game,41);var engine=new Engine(game,user,data,true){ManagedVersion=true};
   var build=typeof(LaunchProfiles).GetMethod("BuildStartInfo",BindingFlags.Static|BindingFlags.NonPublic);Check(build!=null,"selected binary start-info builder exists");var start=(ProcessStartInfo)build.Invoke(null,new object[]{engine});Check(start.FileName==Path.Combine(game,"ProjectZomboid64.exe")&&start.WorkingDirectory==game&&!start.UseShellExecute,"managed version launches exact selected binary and working directory");Check(start.Arguments=="\"-cachedir="+user+"\"","one quoted cachedir argument binds actual game saves/settings/mods to selected version");Check(start.EnvironmentVariables["SteamAppId"]=="108600"&&start.EnvironmentVariables["SteamGameId"]=="108600","managed child Steam identity is scoped to process environment");engine.ManagedVersion=false;Check(((ProcessStartInfo)build.Invoke(null,new object[]{engine})).Arguments=="","legacy launch receives no new cache override");engine.ManagedVersion=true;
   Put(Path.Combine(game,"game-version.txt"),"42.21 stale marker");Put(Path.Combine(user,"mods","Flat","mod.info"),"id=B41Flat\nname=B41\n");Put(Path.Combine(user,"mods","Flat","42","mod.info"),"id=B42Only\n");Put(Path.Combine(user,"mods","NewOnly","common","mod.info"),"id=FutureShared\n");Put(Path.Combine(user,"mods","NewOnly","42","mod.info"),"id=FutureOnly\n");
   var json=new JavaScriptSerializer();var catalog=json.Deserialize<Dictionary<string,object>>(json.Serialize(SaveProfiles.Catalog(engine)));var rows=((ArrayList)catalog["mods"]).Cast<Dictionary<string,object>>().ToArray();Check(rows.Any(row=>(string)row["id"]=="B41Flat")&&!rows.Any(row=>((string)row["id"]).StartsWith("B42")||((string)row["id"]).StartsWith("Future")),"B41 catalog chooses root mod.info and excludes B42/common-only layouts");
   Put(engine.Mods,"VERSION = 1,\nmods\n{\n mod = B41Flat,\n}\nmaps\n{\n}\n");byte[] before=File.ReadAllBytes(engine.Mods);LaunchProfiles.Stage(engine,new ProfileLaunchRequest{Kind="normal"});string bridge=Path.Combine(user,"mods","OrangeLauncherSession");Check(File.Exists(Path.Combine(bridge,"mod.info"))&&File.Exists(Path.Combine(bridge,"media","lua","client","OrangeLaunchBridge.lua"))&&!Directory.Exists(Path.Combine(bridge,"42")),"B41 bridge uses supported flat Lua mod layout");LaunchProfiles.Recover(engine,true);Check(File.ReadAllBytes(engine.Mods).SequenceEqual(before)&&!Directory.Exists(bridge),"B41 session restores exact original mod list and cleans owned flat files");
   Check(LauncherLaunch.Reason(engine,false,false)=="","plain B41 installation without Buddy remains launchable");Console.WriteLine("VersionLaunch PASS: "+checks+" assertions");
  }finally{string full=Path.GetFullPath(root),allowed=Path.GetFullPath(LauncherTestPaths.Root)+Path.DirectorySeparatorChar;if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("version-launch-"))throw new IOException("Unsafe fixture cleanup");if(Directory.Exists(full))Directory.Delete(full,true);}}
 }
}
