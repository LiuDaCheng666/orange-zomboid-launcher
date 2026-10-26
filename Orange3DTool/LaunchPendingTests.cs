using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Exercises only owned temporary files and synthetic submission callbacks.
 static class LaunchPendingTests {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static bool Rejected(Action action){try{action();return false;}catch(InvalidOperationException){return true;}}
  static Dictionary<string,object> Status(ModernController c){var value=c.Execute("status",new Dictionary<string,object>());return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
  static void Submission(ModernController c,Dictionary<string,object> payload){c.RunStartupPresentation("launch",payload,()=>{c.CommitStartup();return new GameLaunchOutcome{message="synthetic submission"};});}
  static void Gap(string root){using(var c=new ModernController(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true)){
   var payload=GameStartupPresentationTests.Begin(c);Submission(c,payload);
   Check(Rejected(()=>GameStartupPresentationTests.Begin(c)),"native submission gap rejects a second startup before process evidence");
   c.CancelStartup((string)payload["startupId"]);
   Check(Rejected(()=>GameStartupPresentationTests.Begin(c)),"UI finally/cancel cannot discard a submitted request");
   var status=Status(c);var pending=(Dictionary<string,object>)status["startup"];
   Check((bool)pending["pending"]&&(string)pending["startupId"]==(string)payload["startupId"],"native status retains the exact submitted nonce independently of request completion");
   c.ExpireStartup(DateTime.UtcNow.AddSeconds(45));Check(Rejected(()=>GameStartupPresentationTests.Begin(c)),"ordinary Steam startup latency does not end the guard after a short delay");
   c.ExpireStartup(DateTime.UtcNow.AddSeconds(181));
   Check(!((bool)((Dictionary<string,object>)Status(c)["startup"])["pending"]),"submission without client evidence has a bounded lease");
   var next=GameStartupPresentationTests.Begin(c);Check((string)next["startupId"]!=(string)payload["startupId"],"bounded expiry permits a fresh attempt nonce");
  }}
  static void Evidence(string root){using(var c=new ModernController(Path.Combine(root,"identity-game"),Path.Combine(root,"identity-user"),Path.Combine(root,"identity-data"),true)){
   var payload=GameStartupPresentationTests.Begin(c);DateTime before=DateTime.UtcNow;Submission(c,payload);string id=(string)payload["startupId"],game=Path.Combine(root,"identity-game");long birth=DateTime.UtcNow.Ticks;
   var client=new ClientIdentity{Pid=1234,Birth=birth,Image=Path.Combine(game,"ProjectZomboid64.exe")};
   c.ReconcileSubmittedStartup(new string('f',32),DateTime.UtcNow,new[]{client});Check(c.StartupStatus().pending,"different nonce cannot retire a newer pending launch");
   foreach(var wrong in new[]{new ClientIdentity{Pid=1234,Birth=before.AddSeconds(-1).Ticks,Image=client.Image},new ClientIdentity{Pid=1234,Birth=birth,Image=Path.Combine(game,"foreign","ProjectZomboid64.exe")},new ClientIdentity{Pid=1234,Birth=DateTime.UtcNow.AddMinutes(1).Ticks,Image=client.Image},new ClientIdentity{Pid=1234,Birth=birth,Image=Path.Combine(game,"jre64","bin","java.exe"),Java=false},new ClientIdentity{Pid=0,Birth=birth,Image=client.Image}}){c.ReconcileSubmittedStartup(id,DateTime.UtcNow,new[]{wrong});Check(c.StartupStatus().pending,"stale, foreign, future or unverified process evidence cannot clear the guard");}
   c.ReconcileSubmittedStartup(id,DateTime.UtcNow,new[]{client});Check(!c.StartupStatus().pending&&c.StartupStatus().phase=="observed","fresh exact-path client evidence retires submission pending");
   var next=GameStartupPresentationTests.Begin(c);Submission(c,next);c.ReconcileSubmittedStartup((string)next["startupId"],DateTime.UtcNow,new[]{new ClientIdentity{Pid=4321,Birth=DateTime.UtcNow.Ticks,Image=Path.Combine(game,"jre64","bin","javaw.exe"),Java=true}});Check(!c.StartupStatus().pending,"verified game Java client also retires pending");
  }}
  static void Failure(string root){using(var c=new ModernController(Path.Combine(root,"failure-game"),Path.Combine(root,"failure-user"),Path.Combine(root,"failure-data"),true)){
   var payload=GameStartupPresentationTests.Begin(c);bool failed=false;try{c.RunStartupPresentation("launch",payload,()=>{c.CommitStartup();throw new IOException("synthetic URI submission failure");});}catch(IOException){failed=true;}Check(failed&&!c.StartupStatus().pending,"definitive native submission failure releases pending immediately");GameStartupPresentationTests.Begin(c);Check(true,"failure permits an immediate fresh retry");
   bool rejected=false;try{c.Execute("startupStatus",new Dictionary<string,object>{{"path","untrusted"}});}catch{rejected=true;}Check(rejected,"lightweight pending status refuses arbitrary target payloads");
   var snapshot=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(c.Execute("startupStatus",new Dictionary<string,object>())));Check(snapshot.ContainsKey("startup")&&snapshot.ContainsKey("running")&&!snapshot.ContainsKey("components")&&!snapshot.ContainsKey("configStamp"),"pending poll returns live lifecycle and running evidence without full card/hash work");
  }}
  static void Durable(string root){string game=Path.Combine(root,"durable-game"),user=Path.Combine(root,"durable-user"),data=Path.Combine(root,"durable-data");var e=new Engine(game,user,data,true);Directory.CreateDirectory(game);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));File.WriteAllText(e.Config,"{\"vmArgs\":[]}");File.WriteAllText(e.Mods,"mods\n{\n}\nmaps\n{\n}\n");
   LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="normal"});var record=Json.Deserialize<ProfileSessionRecord>(File.ReadAllText(LaunchProfiles.Journal(e)));record.State="launched";File.WriteAllText(LaunchProfiles.Journal(e),Json.Serialize(record));var before=File.ReadAllBytes(e.Mods);
   using(var c=new ModernController(game,user,data,true)){
    Check(Rejected(()=>GameStartupPresentationTests.Begin(c)),"another controller rejects the fresh owned Steam session instead of replacing it");
    var pending=(Dictionary<string,object>)Status(c)["startup"];Check((bool)pending["pending"]&&(string)pending["startupId"]==record.Nonce,"reloaded UI can recover pending state from the validated session nonce");
    Check(Convert.ToBase64String(before)==Convert.ToBase64String(File.ReadAllBytes(e.Mods)),"duplicate rejection preserves staged session bytes");
   }
   record.CreatedUtc=DateTime.UtcNow.AddSeconds(-181).ToString("o");File.WriteAllText(LaunchProfiles.Journal(e),Json.Serialize(record));using(var c=new ModernController(game,user,data,true)){GameStartupPresentationTests.Begin(c);Check(!LaunchProfiles.Pending(e),"first retry after ended Steam grace retains 1.2.23 recovery");}
  }
  static void Disabled(string root){string game=Path.Combine(root,"disabled-game"),user=Path.Combine(root,"disabled-user"),data=Path.Combine(root,"disabled-data");var e=new Engine(game,user,data,true);Directory.CreateDirectory(game);Directory.CreateDirectory(data);File.WriteAllText(e.Config,"{\"vmArgs\":[]}");GameStartupComponent.SetEnabled(e,false);using(var c=new ModernController(game,user,data,true)){int events=0;c.StartupPresentation=_=>events++;var payload=GameStartupPresentationTests.Begin(c);Submission(c,payload);Check(events==0,"disabled loading presentation does not start its native observer");Check(c.StartupStatus().pending&&Rejected(()=>GameStartupPresentationTests.Begin(c)),"loading opt-out preserves native pending duplicate protection");}}
  sealed class StoppedHandle:IClientProcessHandle {
   internal ClientIdentity Identity;internal bool Stopped;public long Birth{get{return Identity.Birth;}}public string Image{get{return Identity.Image;}}public bool Exited{get{return Stopped;}}public bool ClientRole(string game,ClientIdentity value){return true;}public bool TerminateAndWait(){Stopped=true;return true;}public void Dispose(){}
  }
  static void ForceClosePending(string root){string game=Path.Combine(root,"close-game"),user=Path.Combine(root,"close-user"),data=Path.Combine(root,"close-data");var e=new Engine(game,user,data,true);Directory.CreateDirectory(game);Directory.CreateDirectory(Path.GetDirectoryName(e.Mods));File.WriteAllText(e.Config,"{\"vmArgs\":[]}");File.WriteAllText(e.Mods,"mods\n{\n}\nmaps\n{\n}\n");using(var c=new ModernController(game,user,data,true)){
   var payload=GameStartupPresentationTests.Begin(c);c.RunStartupPresentation("launch",payload,()=>{c.CommitStartup();LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="normal",StartupSessionId=(string)payload["startupId"]});var record=Json.Deserialize<ProfileSessionRecord>(File.ReadAllText(LaunchProfiles.Journal(e)));record.State="launched";File.WriteAllText(LaunchProfiles.Journal(e),Json.Serialize(record));return new GameLaunchOutcome();});Check(c.StartupStatus().pending,"owned Steam submission is pending before any client poll");
   var identity=new ClientIdentity{Pid=12345,Birth=DateTime.UtcNow.Ticks,Image=Path.Combine(game,"ProjectZomboid64.exe")};var handle=new StoppedHandle{Identity=identity};var close=(ClientClosePreview)typeof(ModernController).GetField("clientClose",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);close.Discover=_=>new List<ClientIdentity>{identity};close.Open=_=>handle;
   var preview=Json.Deserialize<Dictionary<string,object>>(Json.Serialize(c.Execute("gameProcesses",new Dictionary<string,object>())));c.Execute("forceCloseGame",new Dictionary<string,object>{{"stamp",preview["stamp"]},{"confirmed",true}});
   Check(handle.Stopped&&!c.StartupStatus().pending&&!LaunchProfiles.Pending(e),"verified force stop retires its exact pending nonce even when the polling tick missed the client");GameStartupPresentationTests.Begin(c);Check(true,"first relaunch after a short-lived force-stopped client has no leftover pending guard");
  }}
  public static int Main(){string root=Path.Combine(Path.GetTempPath(),"ozl-launch-pending-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{Gap(root);Durable(root);Evidence(root);Failure(root);Disabled(root);ForceClosePending(root);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}}
 }
}
