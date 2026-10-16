using System;
using System.Reflection;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class GameProcessGuardTests {
  static void Check(bool value,string label){Console.WriteLine((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
  public static int Main(string[] args){try{if(args.Contains("--playtime")){ProcessEvidence(true);return 0;}if(args.Contains("--launch")){ProcessEvidence(false);return 0;}Run();return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
  static bool Refuses(Action call){try{call();return false;}catch{return true;}}
  static Dictionary<string,object> D(object value){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}
  static void RuntimeWrites(ModernController controller,Engine e,string role){
   e.Switch(false,false);Check(File.ReadAllText(e.Config).Contains("-Xmx2g"),"component configuration write preserves unrelated memory beside "+role);
   string properties=Path.Combine(e.User,"viewpoint-live.properties");File.WriteAllText(properties,"# keep\nmouse.invertY=false\ngraphics.mode=Modern\ngraphics.pack=vivid\n");
   var settings=D(controller.Execute("settingsGet",null));controller.Execute("settingsSave",new Dictionary<string,object>{{"stamp",settings["stamp"]},{"values",new Dictionary<string,object>{{"viewpoint-live.properties|mouse.invertY","true"}}}});Check(File.ReadAllText(properties).Contains("mouse.invertY=true"),"controller saves settings beside "+role);
   var shader=D(controller.Execute("shadersGet",null));controller.Execute("shaderSelect",new Dictionary<string,object>{{"stamp",shader["stamp"]},{"id","normal"}});Check(File.ReadAllText(properties).Contains("graphics.pack=normal"),"controller switches shader beside "+role);
   byte[] before=File.ReadAllBytes(properties);settings=D(controller.Execute("settingsGet",null));using(var locked=new FileStream(properties,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){var snapshot=settings;Check(Refuses(()=>controller.Execute("settingsSave",new Dictionary<string,object>{{"stamp",snapshot["stamp"]},{"values",new Dictionary<string,object>{{"viewpoint-live.properties|mouse.invertY","false"}}}})),"actual exclusive file lock refuses truthfully beside "+role);}Check(File.ReadAllBytes(properties).SequenceEqual(before),"actual lock preserves exact settings bytes beside "+role);
   LaunchProfiles.Stage(new Engine(e.Game,e.User,e.Data,true),new ProfileLaunchRequest{Kind="save",Id="Sandbox/Fixture",Mode="Sandbox",Save="Fixture"});settings=D(controller.Execute("settingsGet",null));controller.Execute("settingsSave",new Dictionary<string,object>{{"stamp",settings["stamp"]},{"values",new Dictionary<string,object>{{"viewpoint-live.properties|mouse.invertY","false"}}}});Check(LaunchProfiles.Pending(e)&&File.ReadAllText(properties).Contains("mouse.invertY=false"),"active direct-launch session permits settings edits beside "+role);
   File.AppendAllText(e.Mods,"\n# deliberate edit\n");byte[] edited=System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(e.Mods).Replace("    mod = OrangeLauncherSession,\r\n",""));var testEngine=new Engine(e.Game,e.User,e.Data,true);LaunchProfiles.Recover(testEngine,true);Check(!LaunchProfiles.Pending(e)&&File.ReadAllBytes(e.Mods).SequenceEqual(edited),"session cleanup preserves deliberate edits and retires journal beside "+role);
  }
  static void ProcessEvidence(bool playtime){
   string root=Path.Combine(LauncherTestPaths.Root,"client-server-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"steamapps","common","game"),exe=Path.Combine(game,"jre64","bin","java.exe");Directory.CreateDirectory(Path.GetDirectoryName(exe));
   using(var compiler=System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp")){var options=new System.CodeDom.Compiler.CompilerParameters(new[]{"System.dll"},exe){GenerateExecutable=true,CompilerOptions="/nologo",TempFiles=new System.CodeDom.Compiler.TempFileCollection(root,false)};var built=compiler.CompileAssemblyFromSource(options,"using System; using System.Threading.Tasks; class Fixture { static void Main(){Console.WriteLine(\"ready\");Task.Factory.StartNew(()=>Console.In.ReadLine()).Wait(45000);} }");Check(!built.Errors.HasErrors,"independent owned Java-image helper compiles under any aggregate entry");}
   var e=new Engine(game,Path.Combine(root,"user"),Path.Combine(root,"data"),false,SteamLaunchOptions.Unavailable);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx2g\"]}");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture-never-executed");File.WriteAllText(e.Mods,"VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\n");
   foreach(string role in new[]{"zombie.network.GameServer","zombie.gameStates.MainScreenState","UnknownMain"}){
    var start=new ProcessStartInfo(exe,"-Dozl.fixture=true -Dnote=zombie.network.GameServer -Xlog:gc*=debug -Xlog:safepoint=off -cp fixture.jar "+role){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true};
    using(var child=Process.Start(start)){try{
     Check(child.StandardOutput.ReadLine()=="ready","owned process fixture ready");
     Check(GameProcessGuard.ReadJavaRole(game,"java",exe,child).ToString()==(role=="zombie.network.GameServer"?"Server":role=="zombie.gameStates.MainScreenState"?"Client":"Unknown"),"PID-specific fixture role confirms actual entry: "+role);
     bool native=false;foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32"})foreach(var p in Process.GetProcessesByName(name))using(p){if(!p.HasExited)native=true;}
     if(!playtime){
      Check(!native||GameProcessGuard.IsClientRunning(game),"live native observation retains global client protection");
      var ctor=typeof(Engine).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(string),typeof(string),typeof(bool),typeof(Func<SteamLaunchOptionsSnapshot>),typeof(Func<string,Process[]>)},null);
      Check(ctor!=null,"engine accepts a scoped process inventory without changing launch policy");
      Func<string,Process[]> observe=name=>name=="java"?new[]{Process.GetProcessById(child.Id)}:new Process[0];
      e=(Engine)ctor.Invoke(new object[]{game,e.User,e.Data,false,(Func<SteamLaunchOptionsSnapshot>)SteamLaunchOptions.Unavailable,observe});
     }
     if(playtime){var find=typeof(PlaytimeTracker).GetMethod("Find",BindingFlags.Static|BindingFlags.NonPublic);using(var found=(Process)find.Invoke(null,new object[]{game,0L})){Check(role=="zombie.gameStates.MainScreenState"?found!=null&&found.Id==child.Id:found==null,"playtime confirms actual client only: "+role);}using(var found=(Process)find.Invoke(null,new object[]{game,DateTime.UtcNow.AddMinutes(1).Ticks})){Check(found==null,"playtime retains start-time cutoff");}}
     else {
      Check(GameProcessGuard.IsRunning(game,e.ProcessInventory),"selected Java alone retains broad occupancy: "+role);
      Check(GameProcessGuard.IsClientRunning(game,e.ProcessInventory)==(role!="zombie.network.GameServer"),"selected Java client guard excludes only verified server: "+role);
      Check(!Refuses(e.Guard),"configuration engine guard permits occupied runtime: "+role);
      Check(!Refuses(e.GuardPlayProfile),"profile transaction guard permits occupied runtime: "+role);
      var maintenance=typeof(Engine).GetMethod("GuardMaintenance",BindingFlags.Instance|BindingFlags.NonPublic);Check(maintenance!=null&&Refuses(()=>maintenance.Invoke(e,null)),"destructive cache/workshop maintenance retains its separate runtime guard: "+role);
      var launch=typeof(Engine).GetMethod("GuardLaunch");Check(launch!=null,"launch-only engine guard exists");
      Check(Refuses(()=>launch.Invoke(e,null))==(role!="zombie.network.GameServer"),"normal engine allows only verified Server beside client/unknown blocking: "+role);
      var normal=LaunchPreflight.Check(e,false,false,"normal");Check(normal.canLaunch==(role=="zombie.network.GameServer"),"real normal preflight allows verified Server and blocks Client/Unknown: "+role);
      Check(LaunchPreflight.Check(e,false,false,"save").canLaunch==(role=="zombie.network.GameServer")&&LaunchPreflight.Check(e,false,false,"server").canLaunch==(role=="zombie.network.GameServer"),"direct target preflight keeps duplicate-client protection: "+role);
      using(var controller=new ModernController(game,e.User,e.Data,true)){
       typeof(ModernController).GetField("engine",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(controller,e);
       var check=typeof(ModernController).GetMethod("Check",BindingFlags.NonPublic|BindingFlags.Instance);Check(check.GetParameters().Length==1,"controller keeps explicit normal-launch check path");
       Check(Refuses(()=>check.Invoke(controller,new object[]{true}))==(role!="zombie.network.GameServer"),"controller normal check allows verified Server and blocks Client/Unknown: "+role);
       Check(!Refuses(()=>check.Invoke(controller,new object[]{false})),"controller configuration check permits occupied runtime: "+role);
       RuntimeWrites(controller,e,role);
       if(role=="zombie.network.GameServer"){
        string nativeExe=Path.Combine(root,"native","ProjectZomboid64.exe");Directory.CreateDirectory(Path.GetDirectoryName(nativeExe));File.Copy(exe,nativeExe);
        using(var nativeHelper=Process.Start(new ProcessStartInfo(nativeExe){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})){try{Check(nativeHelper.StandardOutput.ReadLine()=="ready","owned native-name helper ready");Func<string,Process[]> both=name=>name=="ProjectZomboid64"?new[]{Process.GetProcessById(nativeHelper.Id)}:name=="java"?new[]{Process.GetProcessById(child.Id)}:new Process[0];Check(GameProcessGuard.IsClientRunning(game,both)&&GameProcessGuard.IsRunning(game,both),"actual native-name fixture retains global protection alongside verified Server");var occupied=new Engine(game,e.User,e.Data,false,SteamLaunchOptions.Unavailable,both);Check(Refuses(occupied.GuardLaunch)&&!LaunchPreflight.Check(occupied,false,false).canLaunch,"native inventory still blocks real normal engine/preflight policy");}finally{nativeHelper.StandardInput.WriteLine("exit");Check(nativeHelper.WaitForExit(5000),"owned native fixture exits naturally");}}
        Check(!LaunchPreflight.Check(e,true,false).canLaunch&&!LaunchPreflight.Check(e,false,true).canLaunch,"server-only allowance retains independent busy/client activity checks");
        string bat=Path.Combine(game,"ProjectZomboid64.bat");File.WriteAllText(bat,"java -javaagent:ZombieBuddy.jar -cp pz.jar zombie.gameStates.MainScreenState");Check(Refuses(()=>launch.Invoke(e,null))&&!LaunchPreflight.Check(e,false,false).canLaunch&&Refuses(()=>check.Invoke(controller,new object[]{true})),"allowed server-only baseline reaches old Buddy BAT refusal");File.Delete(bat);
        Check(!Refuses(()=>launch.Invoke(e,null))&&LaunchPreflight.Check(e,false,false).canLaunch&&!Refuses(()=>check.Invoke(controller,new object[]{true})),"removing only old Buddy BAT restores server-only normal allowance");
        string journal=Path.Combine(e.Data,"launch-sessions",LaunchProfiles.Key(e)+".json");Directory.CreateDirectory(Path.GetDirectoryName(journal));File.WriteAllText(journal,"fixture-pending");Check(!LaunchPreflight.Check(e,false,false).canLaunch&&Refuses(()=>check.Invoke(controller,new object[]{true})),"allowed server-only baseline reaches pending-session refusal");File.Delete(journal);
        Check(LaunchPreflight.Check(e,false,false).canLaunch&&!Refuses(()=>check.Invoke(controller,new object[]{true})),"removing only pending journal restores server-only normal allowance");
        var unreadable=new Engine(game,e.User,e.Data,false,()=>new SteamLaunchOptionsSnapshot(null,0,null,SteamLaunchOptionsState.Invalid),e.ProcessInventory);Check(Refuses(unreadable.GuardLaunch)&&!LaunchPreflight.Check(unreadable,false,false).canLaunch,"allowed server-only baseline reaches unreadable Steam safety refusal");
        controller.Cancel();Check(Refuses(()=>check.Invoke(controller,new object[]{true})),"allowed server-only baseline reaches cancellation refusal");
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
    {"java.exe -Xlog:gc*=debug -Xlog:safepoint=off -cp pz.jar zombie.network.GameServer","Server"},
    {"java.exe -Xlog -cp pz.jar zombie.network.GameServer","Server"},
    {"java.exe -Xlog:gc:file=\"zombie.network.GameServer log.txt\" -cp pz.jar zombie.gameStates.MainScreenState","Client"},
    {"java.exe -Xlog:gc*=debug -Xlog:safepoint=off -cp pz.jar OtherMain","Unknown"},
    {"java.exe -Xlogger -cp pz.jar zombie.network.GameServer","Unknown"},
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
   for(int i=0;i<cases.GetLength(0);i++){string actual=classify.Invoke(null,new object[]{cases[i,0]}).ToString();Check(actual==cases[i,1],"Java role literal case "+i+" expected "+cases[i,1]+", actual "+actual);}
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
