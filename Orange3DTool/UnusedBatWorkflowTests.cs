using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Real native file operations in an isolated fixture. No real Steam or game starts.
 static class UnusedBatWorkflowTests {
  sealed class LocalEnvironment:BuddyTakeoverEnvironment {
   internal override BuddySteamIdentity Capture(){return new BuddySteamIdentity(SteamLaunchOptions.Unavailable());}
   internal override bool GameRunning(string game){return false;}
   internal override BuddySteamState Inspect(BuddySteamIdentity bound){throw new Exception("No Steam process belongs to this fixture");}
   internal override void Shutdown(BuddySteamIdentity bound){throw new Exception("Must not stop Steam");}
   internal override bool Reopen(BuddySteamIdentity bound){throw new Exception("Must not reopen Steam");}
   internal override long Milliseconds{get{return 0;}}
   internal override void Wait(int value){throw new Exception("Must not wait for Steam");}
  }
  static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  public static int Main(string[] args){try{
   LauncherRuntime.Initialize();
   string[] scripts={
    "@echo off\r\nif exist game.jar (\r\n java -agentlib:zbNative -jar game.jar\r\n)\r\n",
    "@echo off\r\nset JAVA_OPTS=-agentlib:zbNative\r\njava %JAVA_OPTS% ^\r\n -cp \"%PZ_CLASSPATH%\" zombie.gameStates.MainScreenState\r\n",
    "java -agentlib:zbNative -jar first.jar | java -javaagent:ZombieBuddy.jar -jar second.jar\r\n",
    "@echo off\r\nrem 中文备用启动说明\r\njava -javaagent:ZombieBuddy.jar -jar game.jar\r\n",
    new string(' ',2097153)+"\r\n",
    "java -javaagent:ZombieBuddy.jar -jar game.jar\r\n"
   };
   for(int i=0;i<scripts.Length;i++){
    string root=Path.Combine(LauncherTestPaths.Root,"备用脚本回归-"+Guid.NewGuid().ToString("N"));
    var e=new Engine(Path.Combine(root,"游戏"),Path.Combine(root,"用户"),Path.Combine(root,"数据"),false,SteamLaunchOptions.Unavailable,_=>new Process[0]);
    Directory.CreateDirectory(e.Game);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));
    File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx8g\",\"-agentlib:zbNative\"],\"custom\":42}");
    File.WriteAllText(e.Mods,"VERSION = 1,\r\nmods\r\n{\r\n mod = OtherMod,\r\n}\r\nmaps\r\n{\r\n}\r\n");
    string bat=Path.Combine(e.Game,i%2==0?"ProjectZomboid64.bat":"ProjectZomboid32.bat");File.WriteAllText(bat,scripts[i],i==3?Encoding.GetEncoding(936):i==1?(Encoding)new UnicodeEncoding(false,true):new UTF8Encoding(true));byte[] original=File.ReadAllBytes(bat);
    if(i<3)Check(!BuddyLaunchArguments.CleanBat(scripts[i]).Safe,"fixture reproduces unsupported BAT syntax "+i);
    FileStream lease=i==5?new FileStream(bat,FileMode.Open,FileAccess.Read,FileShare.None):null;
    try{
    if(args.Contains("--guard")){
     e.Guard();e.SwitchComponent("buddy",false);
     string libs=Path.Combine(root,"buddy-source","mods","ZombieBuddy","libs");Directory.CreateDirectory(libs);File.WriteAllText(Path.Combine(libs,"ZombieBuddy.jar"),"isolated loader bytes");File.WriteAllText(Path.Combine(libs,"zbNative.dll"),"isolated native bytes");e.BuddySource=Path.Combine(root,"buddy-source");
     e.SwitchComponent("buddy",true);
     Check(e.FrameworkConfigured&&File.Exists(Path.Combine(e.Game,"zbNative.dll"))&&File.ReadAllText(e.Mods).Contains("OtherMod"),"native disable then enable writes actual component files and retains ordinary mods "+i);
     e.GuardLaunch();
    }else{
     File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx8g\",\"-javaagent:ZombieBuddy.jar\"],\"custom\":42}");
     var env=new LocalEnvironment();var preview=BuddyTakeover.Preview(e,env);
     Check(preview.supported&&preview.hasConflicts,"scan still offers cleanup of active JSON despite unsupported spare BAT "+i);
     var dto=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new JavaScriptSerializer().Serialize(preview));
     Check(dto.ContainsKey("untouchedFiles")&&new JavaScriptSerializer().Serialize(dto["untouchedFiles"]).Contains(Path.GetFileName(bat)),"scan names the preserved spare BAT rather than claiming it was cleaned "+i);
     var result=BuddyTakeover.Apply(e,preview.stamp,true,false,null,CancellationToken.None,env);
     Console.WriteLine("Takeover result: "+result.status+" · "+result.message);
     Check(result.cleanupCompleted&&!BuddyTakeover.Pending(e)&&!File.ReadAllText(e.Config).Contains("-javaagent:"),"takeover completes active JSON cleanup without a stuck recovery "+i);
     Check(result.untouchedFiles.Contains(Path.GetFileName(bat))&&result.untouchedReasons.ContainsKey(Path.GetFileName(bat)),"completed result retains preserved BAT details "+i);
     var again=BuddyTakeover.Preview(e,env);Check(again.supported&&!again.hasConflicts,"rescan stays usable after cleanup "+i);
    }
    }finally{if(lease!=null)lease.Dispose();}
    Check(original.SequenceEqual(File.ReadAllBytes(bat)),"spare BAT bytes including encoding remain unchanged "+i);
   }
   return 0;
  }catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
