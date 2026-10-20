using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class LaunchTransportTests {
  static void Check(bool value,string label){Console.WriteLine((value?"PASS: ":"FAIL: ")+label);if(!value)throw new Exception(label);}
  static Engine Fixture(){string root=Path.Combine(LauncherTestPaths.Root,"launch-transport-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);Directory.CreateDirectory(e.User);File.WriteAllText(e.Config,"{\"vmArgs\":[]}");return e;}
  static void Stage(Engine e){LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="ozl-transport-probe",UserPassword="synthetic-role-secret",ServerPassword="synthetic-server-secret"});}
  static void Age(Engine e,int seconds){string path=LaunchProfiles.Journal(e);var json=new JavaScriptSerializer();var record=json.Deserialize<ProfileSessionRecord>(File.ReadAllText(path));record.CreatedUtc=DateTime.UtcNow.AddSeconds(-seconds).ToString("o");File.WriteAllText(path,json.Serialize(record));LaunchProfiles.ExpireRequest(e,record.Nonce);}
  public static void Run(){
   var e=Fixture();Stage(e);string request=Path.Combine(e.User,"Lua","OrangeLauncherSession.request.txt"),status=Path.Combine(e.User,"Lua","OrangeLauncherSession.status.txt");
   Check(File.Exists(request)&&File.Exists(status),"request and status use the real game Lua cache directory");
   Check(!File.Exists(Path.Combine(e.User,"OrangeLauncherSession.request")),"new sessions do not write an unreadable request in the user root");
   Check(File.ReadAllText(request).Contains("synthetic-role-secret")&&File.ReadAllText(request).Contains("synthetic-server-secret"),"both password fields survive the producer boundary without logging them");
   File.WriteAllText(status,"connection-failed\n");string state=new JavaScriptSerializer().Serialize(LaunchProfiles.State(e));Check(state.Contains("连接失败"),"game connection failure is surfaced instead of a generic submitted message");
   Age(e,180);Check(File.Exists(request)&&new FileInfo(request).Length>0,"a three-minute game startup does not discard the pending connection request");
   Age(e,601);Check(!File.Exists(request),"abandoned credentials expire after the startup window");
   LaunchProfiles.Recover(e,true);Check(!LaunchProfiles.Pending(e),"session cleanup still completes after request expiration");
   e=Fixture();Stage(e);string legacy=Path.Combine(e.User,"OrangeLauncherSession.request"),oldLua=Path.Combine(e.User,"Lua","OrangeLauncherSession.request");File.WriteAllText(legacy,"legacy synthetic secret");File.WriteAllText(oldLua,"older Lua-cache synthetic secret");LaunchProfiles.Recover(e,true);Check(!File.Exists(legacy)&&!File.Exists(oldLua)&&!File.Exists(Path.Combine(e.User,"Lua","OrangeLauncherSession.request.txt")),"recovery cleans both legacy and corrected request locations");
  }
  public static int Main(string[] args){try{if(args.Length==3&&(args[0]=="--stage-native"||args[0]=="--recover-native")) {string root=Path.GetFullPath(args[2]);var e=new Engine(Path.GetFullPath(args[1]),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.User);if(LaunchProfiles.Pending(e))LaunchProfiles.Recover(e,true);if(args[0]=="--stage-native")Stage(e);Console.WriteLine(args[0]=="--stage-native"?"Staged an isolated native connection probe; no production user files were modified.":"Recovered the isolated native probe; no production user files were modified.");return 0;}Run();return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
 }
}
