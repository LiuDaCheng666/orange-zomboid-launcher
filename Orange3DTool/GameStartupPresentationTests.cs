using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class GameStartupPresentationTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  internal static Dictionary<string,object> Begin(ModernController controller,string kind="normal",string target=""){
   var result=controller.Execute("startupBegin",new Dictionary<string,object>{{"targetKind",kind},{"targetId",kind=="normal"?"":target}});
   var id=(string)new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new JavaScriptSerializer().Serialize(result))["startupId"];
   return kind=="normal"?new Dictionary<string,object>{{"startupId",id}}:new Dictionary<string,object>{{"startupId",id},{"kind",kind},{"id",target}};
  }
  static void Reject(Action action,string message){bool rejected=false;try{action();}catch{rejected=true;}Assert(rejected,message);}
  public static void Run(){
   Assert(typeof(LaunchProfiles).GetMethod("LaunchNormal",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).ReturnType!=typeof(object),"normal launch returns typed readable outcome");
   string root=Path.Combine(Path.GetTempPath(),"ozl-startup-presentation-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(root);
   try{using(var controller=new ModernController(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true)){
    var events=new List<GameStartupLaunch>();controller.StartupPresentation=value=>events.Add(value);
    foreach(string kind in new[]{"normal","save","server"}){
     events.Clear();string action=kind=="normal"?"launch":"profileLaunch";
     var payload=Begin(controller,kind,"private-target");
     object expected=new object();DateTime before=DateTime.UtcNow;
     object result=controller.RunStartupPresentation(action,payload,()=>{Assert(events.Count==1&&events[0].Stage=="preflight","window request precedes original "+kind+" launch");controller.CommitStartup();return expected;});
     Assert(ReferenceEquals(result,expected),"original "+kind+" launch result preserved");
     Assert(events.Count==3&&events[1].Stage=="preparing"&&events[2].Stage=="submitted","successful "+kind+" launch submits observer");
     Assert(events[0].SessionId==events[1].SessionId&&events[0].SessionId.Length==32,"one identity for "+kind+" lifecycle");
     Assert(events[0].TargetKind==kind&&events[1].StartedUtc>=before&&events[0].StartedUtc.Kind==DateTimeKind.Utc,"target and launch birth recorded for "+kind);
     Assert(!ReferenceEquals(events[0],events[1])&&events[0].Stage=="preflight","previous lifecycle event remains immutable");
     Assert(events[0].Message.IndexOf("must-not-appear",StringComparison.Ordinal)<0&&events[0].Message.IndexOf("private-target",StringComparison.Ordinal)<0,"startup display excludes private target credentials");
    }
    events.Clear();var failure=new IOException("Failed access_token=private-token");Exception actual=null;
    var failurePayload=Begin(controller);try{controller.RunStartupPresentation("launch",failurePayload,()=>{controller.CommitStartup();throw failure;});}catch(Exception error){actual=error;}
    Assert(ReferenceEquals(actual,failure),"original startup exception preserved");
    Assert(events.Count==3&&events[2].Stage=="failed"&&!events[2].Message.Contains("private-token"),"failed launch keeps safe diagnostic in same startup session");
    events.Clear();controller.RunStartupPresentation("status",null,()=>new object());Assert(events.Count==0,"nonlaunch reads never create startup windows");
    controller.StartupPresentation=value=>{throw new InvalidOperationException("observer unavailable");};int launched=0;
    var observerPayload=Begin(controller);controller.RunStartupPresentation("launch",observerPayload,()=>{controller.CommitStartup();launched++;return new object();});Assert(launched==1,"presentation failure cannot prevent game launch");
    controller.StartupPresentation=value=>events.Add(value);
    var cancelled=Begin(controller);string cancelledId=(string)cancelled["startupId"];controller.CancelStartup(cancelledId);
    Reject(()=>controller.RunStartupPresentation("launch",cancelled,()=>{throw new Exception("must not run");}),"preflight close prevents delayed launch");
    var stale=Begin(controller);controller.ExpireStartup(DateTime.UtcNow.AddMinutes(3));Reject(()=>controller.RunStartupPresentation("launch",stale,()=>new object()),"orphan lease prevents late launch");
    var wrong=Begin(controller,"server","server-id");Reject(()=>controller.RunStartupPresentation("profileLaunch",new Dictionary<string,object>{{"startupId",wrong["startupId"]},{"kind","server"},{"id","other"}},()=>new object()),"attempt binds exact target");controller.CancelStartup((string)wrong["startupId"]);
    var yielded=Begin(controller);var idOnly=new Dictionary<string,object>{{"startupId",yielded["startupId"]}};controller.Execute("startupYield",idOnly);Reject(()=>controller.RunStartupPresentation("launch",yielded,()=>new object()),"yielded confirmation cannot launch");controller.Execute("startupResume",idOnly);controller.RunStartupPresentation("launch",yielded,()=>{controller.CommitStartup();return new object();});Reject(()=>controller.RunStartupPresentation("launch",yielded,()=>new object()),"consumed attempt cannot replay");
    Reject(()=>controller.Execute("startupBegin",new Dictionary<string,object>{{"targetKind","normal"},{"targetId",""},{"path","untrusted"}}),"startup rejects extra path payload");
    var previous=Begin(controller);controller.CancelStartup((string)previous["startupId"]);var current=Begin(controller);controller.CancelStartup((string)previous["startupId"]);controller.RunStartupPresentation("launch",current,()=>{controller.CommitStartup();return new object();});Assert(true,"old cancellation cannot cancel a newer launch attempt");
    var changed=Begin(controller);typeof(ModernController).GetField("engine",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(controller,new Engine(Path.Combine(root,"other"),Path.Combine(root,"user"),Path.Combine(root,"data"),true));Reject(()=>controller.RunStartupPresentation("launch",changed,()=>new object()),"engine identity change invalidates preflight attempt");controller.CancelStartup((string)changed["startupId"]);

   }}finally{Directory.Delete(root,true);}
  }
  [STAThread] public static int Main(){try{Run();return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
