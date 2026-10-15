using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
namespace PZ3D {
 static class GameProcessGuardTests {
  static void Check(bool value,string label){Console.WriteLine((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
  public static int Main(string[] args){try{if(args.Contains("--playtime")){ProcessEvidence(true);return 0;}if(args.Contains("--launch")){ProcessEvidence(false);return 0;}Run();return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
  static bool Refuses(Action call){try{call();return false;}catch{return true;}}
  static void ProcessEvidence(bool playtime){
   string root=Path.Combine(LauncherTestPaths.Root,"client-server-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"steamapps","common","game"),exe=Path.Combine(game,"jre64","bin","java.exe");Directory.CreateDirectory(Path.GetDirectoryName(exe));
   using(var compiler=System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp")){var options=new System.CodeDom.Compiler.CompilerParameters(new[]{"System.dll"},exe){GenerateExecutable=true,CompilerOptions="/nologo",TempFiles=new System.CodeDom.Compiler.TempFileCollection(root,false)};var built=compiler.CompileAssemblyFromSource(options,"using System; using System.Threading.Tasks; class Fixture { static void Main(){Console.WriteLine(\"ready\");Task.Factory.StartNew(()=>Console.In.ReadLine()).Wait(45000);} }");Check(!built.Errors.HasErrors,"independent owned Java-image helper compiles under any aggregate entry");}
   var e=new Engine(game,Path.Combine(root,"user"),Path.Combine(root,"data"),false,SteamLaunchOptions.Unavailable);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx2g\"]}");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture-never-executed");File.WriteAllText(e.Mods,"VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\n");
   foreach(string role in new[]{"zombie.network.GameServer","zombie.gameStates.MainScreenState","UnknownMain"}){
    var start=new ProcessStartInfo(exe,"-Dozl.fixture=true -Dnote=zombie.network.GameServer -cp fixture.jar "+role){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true};
    using(var child=Process.Start(start)){try{
     Check(child.StandardOutput.ReadLine()=="ready","owned process fixture ready");
     Check(GameProcessGuard.ReadJavaRole(game,"java",exe,child).ToString()==(role=="zombie.network.GameServer"?"Server":role=="zombie.gameStates.MainScreenState"?"Client":"Unknown"),"PID-specific fixture role confirms actual entry: "+role);
     bool native=false;foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32"})foreach(var p in Process.GetProcessesByName(name))using(p){if(!p.HasExited)native=true;}
     if(!playtime&&native&&role=="zombie.network.GameServer")Console.WriteLine("SKIP: server-only normal-launch allowance: external native client preserves global protection; no real process touched");
     if(playtime){var find=typeof(PlaytimeTracker).GetMethod("Find",BindingFlags.Static|BindingFlags.NonPublic);using(var found=(Process)find.Invoke(null,new object[]{game,0L})){Check(role=="zombie.gameStates.MainScreenState"?found!=null&&found.Id==child.Id:found==null,"playtime confirms actual client only: "+role);}using(var found=(Process)find.Invoke(null,new object[]{game,DateTime.UtcNow.AddMinutes(1).Ticks})){Check(found==null,"playtime retains start-time cutoff");}}
     else {
      Check(GameProcessGuard.IsRunning(game),"all selected Java roles retain broad occupancy: "+role);
      Check(GameProcessGuard.IsClientRunning(game)==(native||role!="zombie.network.GameServer"),"client guard excludes only verified server and preserves native protection: "+role);
      Check(Refuses(e.Guard),"mutation engine guard blocks occupied runtime: "+role);
      Check(Refuses(e.GuardPlayProfile),"profile transaction engine guard remains broad: "+role);
      var launch=typeof(Engine).GetMethod("GuardLaunch");Check(launch!=null,"launch-only engine guard exists");
      Check(Refuses(()=>launch.Invoke(e,null))==(native||role!="zombie.network.GameServer"),"normal engine launch distinguishes server with native protection: "+role);
      var normal=LaunchPreflight.Check(e,false,false,"normal");Check(normal.canLaunch==(!native&&role=="zombie.network.GameServer"),"real preflight normal guard uses client evidence with native protection: "+role);
      Check(!LaunchPreflight.Check(e,false,false,"save").canLaunch&&!LaunchPreflight.Check(e,false,false,"server").canLaunch,"direct target preflight keeps file occupancy: "+role);
      using(var controller=new ModernController(game,e.User,e.Data,true)){
       typeof(ModernController).GetField("engine",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(controller,e);
       var check=typeof(ModernController).GetMethod("Check",BindingFlags.NonPublic|BindingFlags.Instance);Check(check.GetParameters().Length==1,"controller keeps explicit normal-launch check path");
       Check(Refuses(()=>check.Invoke(controller,new object[]{true}))==(native||role!="zombie.network.GameServer"),"controller launch check preserves role distinction and native protection: "+role);
       Check(Refuses(()=>check.Invoke(controller,new object[]{false})),"controller mutation check remains broad: "+role);
       if(role=="zombie.network.GameServer"){
        string bat=Path.Combine(game,"ProjectZomboid64.bat");File.WriteAllText(bat,"java -javaagent:ZombieBuddy.jar -cp pz.jar zombie.gameStates.MainScreenState");Check(Refuses(()=>launch.Invoke(e,null))&&!LaunchPreflight.Check(e,false,false).canLaunch&&Refuses(()=>check.Invoke(controller,new object[]{true})),"server exclusion cannot bypass old Buddy BAT checks");File.Delete(bat);
        string journal=Path.Combine(e.Data,"launch-sessions",LaunchProfiles.Key(e)+".json");Directory.CreateDirectory(Path.GetDirectoryName(journal));File.WriteAllText(journal,"fixture-pending");Check(!LaunchPreflight.Check(e,false,false).canLaunch&&Refuses(()=>check.Invoke(controller,new object[]{true})),"normal launch retains pending-session checks");File.Delete(journal);
       }
      }
     }
    }finally{child.StandardInput.WriteLine("exit");Check(child.WaitForExit(5000),"owned helper exits through stdin without killing any process");}
     Check(GameProcessGuard.ReadJavaRole(game,"java",exe,child)==JavaProcessRole.Unknown,"exited identity cannot produce confirmed role evidence");
    }
   }
  }
  static void Roles(Type type){
   var classify=type.GetMethod("ClassifyJavaCommandLine",BindingFlags.Public|BindingFlags.Static);
   Check(classify!=null,"actual Java entry classifier exists");
   string[,] cases={
    {"java.exe -cp projectzomboid.jar zombie.network.GameServer","Server"},
    {"java.exe -Dnote=zombie.network.GameServer -cp projectzomboid.jar zombie.gameStates.MainScreenState","Client"},
    {"java.exe -cp zombie.network.GameServer zombie.gameStates.MainScreenState","Client"},
    {"\"E:\\Game Dir\\jre64\\bin\\java.exe\" -Xmx4g -XX:+UseZGC -Dname=\"a b\" -classpath \"a b.jar;zombie.gameStates.MainScreenState\" zombie.network.GameServer -password private-fixture","Server"},
    {"javaw.exe --class-path=projectzomboid.jar --add-opens java.base/java.lang=ALL-UNNAMED zombie.gameStates.MainScreenState","Client"},
    {"java.exe -jar unknown.jar","Unknown"},{null,"Unknown"},{"","Unknown"},
    {"java.exe --unknown zombie.network.GameServer","Unknown"},{"java.exe -cp","Unknown"},
    {"java.exe -Xunknown zombie.network.GameServer","Unknown"},
    {"java.exe -Dnote=\nzombie.network.GameServer zombie.gameStates.MainScreenState","Unknown"},
    {"java.exe -Dnote=\u00a0zombie.network.GameServer zombie.gameStates.MainScreenState","Unknown"},
    {"java.exe -Dnote=zombie.network.GameServer OtherMain","Unknown"},
    {"java.exe -cp projectzomboid.jar ZOMBIE.NETWORK.GAMESERVER","Unknown"},
    {"java.exe @arguments.txt zombie.network.GameServer","Unknown"},
    {"java.exe -cp \"unterminated zombie.network.GameServer","Unknown"},
    {"java.exe -cp projectzomboid.jar zombie.network.GameServerAlias","Unknown"},
    {"java.exe --module-path zombie.network.GameServer -m example/main","Unknown"},
    {"java.exe -Dquoted=\"decoy \\\"zombie.network.GameServer\\\"\" -cp pz.jar zombie.gameStates.MainScreenState","Client"}
   };
   for(int i=0;i<cases.GetLength(0);i++)Check(classify.Invoke(null,new object[]{cases[i,0]}).ToString()==cases[i,1],"Java role literal case "+i+" => "+cases[i,1]);
  }
  public static void Run(){
   Type type=typeof(Engine).Assembly.GetType("PZ3D.GameProcessGuard");Check(type!=null,"shared selected-game process guard exists");
   var method=type.GetMethod("MatchesJava",BindingFlags.Public|BindingFlags.Static);string game=@"E:\SteamLibrary\steamapps\common\ProjectZomboid";
   Func<string,string,bool> match=(name,path)=>(bool)method.Invoke(null,new object[]{game,name,path});
   Check(match("java",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre64\bin\java.exe"),"direct selected-game jre64 Java counts as running without native EXE");
   Check(match("javaw",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre64\bin\javaw.exe"),"selected-game windowless Java counts as running");
   Check(match("java.exe",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre\bin\java.exe")&&match("javaw.exe",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre\bin\javaw.exe"),"both bundled jre Java forms count as running");
   Check(match("JAVA",@"e:\steamlibrary\steamapps\common\PROJECTZOMBOID\jre64\bin\JAVA.EXE"),"Java module paths compare Windows case-insensitively");
   Check(!match("java",@"C:\Program Files\Java\jdk-21\bin\java.exe"),"another JDK does not claim selected game is running");
   Check(!match("java",@"E:\SteamLibrary\steamapps\common\ProjectZomboidOther\jre64\bin\java.exe"),"lookalike installation prefix does not match");
   Check(!match("java",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\other\java.exe")&&!match("notepad",@"E:\SteamLibrary\steamapps\common\ProjectZomboid\jre64\bin\java.exe"),"arbitrary image paths and non-Java process names do not match");
   Check(!match("java",null)&&!match("java",""),"missing module path does not fabricate selected-game evidence");
   Roles(type);
   using(var current=Process.GetCurrentProcess()){Check(GameProcessGuard.ReadJavaRole(game,"java",@"C:\unrelated\java.exe",current)==JavaProcessRole.Unknown,"unmatched runtime cannot supply command-line evidence");}
   ProcessEvidence(true);ProcessEvidence(false);
  }
 }
}
