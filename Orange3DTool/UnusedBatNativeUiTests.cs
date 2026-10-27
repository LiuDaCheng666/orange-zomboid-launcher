using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Page clicks reach the production controller and native file operations.
 // Only the Steam/process inventory and tiny game/package files are fixtures.
 static class UnusedBatNativeUiTests {
  sealed class LocalEnvironment:BuddyTakeoverEnvironment {
   internal override BuddySteamIdentity Capture(){return new BuddySteamIdentity(SteamLaunchOptions.Unavailable());}
   internal override bool GameRunning(string path){return false;}
   internal override BuddySteamState Inspect(BuddySteamIdentity bound){throw new Exception("No fixture Steam process");}
   internal override void Shutdown(BuddySteamIdentity bound){throw new Exception("No Steam shutdown allowed");}
   internal override bool Reopen(BuddySteamIdentity bound){throw new Exception("No Steam reopen allowed");}
   internal override long Milliseconds{get{return 0;}}
   internal override void Wait(int value){throw new Exception("No Steam wait allowed");}
  }
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=4194304};
  static void Send(object value){Console.WriteLine(Json.Serialize(value));Console.Out.Flush();}
  public static int Main(){try{
   LauncherRuntime.Initialize();Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);
   string root=Path.Combine(LauncherTestPaths.Root,"点击验证-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"游戏"),user=Path.Combine(root,"用户"),data=Path.Combine(root,"数据"),source=Path.Combine(root,"buddy-source");
   Directory.CreateDirectory(game);Directory.CreateDirectory(Path.Combine(user,"mods"));Directory.CreateDirectory(data);
   File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture only; never execute");
   File.WriteAllText(Path.Combine(game,"version.txt"),"42.21.0");
   File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx8g\",\"-javaagent:ZombieBuddy.jar\"],\"mainClass\":\"zombie/gameStates/MainScreenState\",\"classpath\":[\"./\",\"projectzomboid.jar\"]}");
   string bat=Path.Combine(game,"ProjectZomboid64.bat");File.WriteAllText(bat,"@echo off\r\nif exist game.jar (\r\njava -agentlib:zbNative -jar game.jar\r\n)\r\n",new UTF8Encoding(true));
   string before=DlssInstaller.HashFile(bat);File.WriteAllText(Path.Combine(user,"mods","default.txt"),"VERSION = 1,\r\nmods\r\n{\r\n mod = OtherMod,\r\n}\r\nmaps\r\n{\r\n}\r\n");
   string libs=Path.Combine(source,"mods","ZombieBuddy","libs");Directory.CreateDirectory(libs);File.WriteAllText(Path.Combine(libs,"ZombieBuddy.jar"),"fixture loader bytes");File.WriteAllText(Path.Combine(libs,"zbNative.dll"),"fixture native bytes");
   File.WriteAllText(Path.Combine(data,"workshop-paths.json"),Json.Serialize(new Dictionary<string,string>{{"3619862853",source}}));
   var engine=new Engine(game,user,data,false,SteamLaunchOptions.Unavailable,_=>new Process[0]){BuddySource=source};
   using(var controller=new ModernController(game,user,data,false,(owner,ids,progress,cancel,force)=>{throw new Exception("No downloads are allowed by this fixture");},null,new LocalEnvironment())){
    typeof(ModernController).GetField("engine",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,engine);
    Send(new{ready=true,root=root,game=game,user=user,data=data,batBefore=before});
    string line;while((line=Console.ReadLine())!=null){object id=null;try{
     var request=ModernHostPolicy.Validate("https://orange.local/index.html",line);id=request["id"];string action=(string)request["action"];var payload=(Dictionary<string,object>)request["payload"];
     if(action!="buddyPreview"&&action!="buddyTakeover"&&action!="componentsAction")throw new Exception("Native fixture accepts only the tested operations");
     if(action=="componentsAction"&&(!payload.ContainsKey("component")||(string)payload["component"]!="buddy"))throw new Exception("Native fixture modifies only its isolated Buddy component");
     object result=controller.Execute(action,payload);Send(new{id=id,ok=true,data=result,files=new{batUnchanged=DlssInstaller.HashFile(bat)==before,config=File.ReadAllText(engine.Config),mods=File.ReadAllText(engine.Mods),frameworkConfigured=engine.FrameworkConfigured,jarExists=File.Exists(Path.Combine(game,"ZombieBuddy.jar")),nativeExists=File.Exists(Path.Combine(game,"zbNative.dll")),pendingRecovery=BuddyTakeover.Pending(engine)}});
    }catch(Exception error){Send(new{id=id,ok=false,error=error.Message});}}
   }return 0;
  }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
 }
}
