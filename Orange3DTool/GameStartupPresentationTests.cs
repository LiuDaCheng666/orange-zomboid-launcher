using System;
using System.Collections.Generic;
using System.IO;
namespace PZ3D {
 static class GameStartupPresentationTests {
  static void Assert(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  public static void Run(){
   string root=Path.Combine(Path.GetTempPath(),"ozl-startup-presentation-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(root);
   try{using(var controller=new ModernController(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true)){
    var events=new List<GameStartupLaunch>();controller.StartupPresentation=value=>events.Add(value);
    foreach(string kind in new[]{"normal","save","server"}){
     events.Clear();string action=kind=="normal"?"launch":"profileLaunch";
     var payload=new Dictionary<string,object>{{"kind",kind},{"password","must-not-appear"},{"id","private-target"}};
     object expected=new object();DateTime before=DateTime.UtcNow;
     object result=controller.RunStartupPresentation(action,payload,()=>{Assert(events.Count==1&&events[0].Stage=="preparing","window request precedes original "+kind+" launch");return expected;});
     Assert(ReferenceEquals(result,expected),"original "+kind+" launch result preserved");
     Assert(events.Count==2&&events[1].Stage=="submitted","successful "+kind+" launch submits observer");
     Assert(events[0].SessionId==events[1].SessionId&&events[0].SessionId.Length==32,"one identity for "+kind+" lifecycle");
     Assert(events[0].TargetKind==kind&&events[0].StartedUtc>=before&&events[0].StartedUtc.Kind==DateTimeKind.Utc,"target and launch birth recorded for "+kind);
     Assert(!ReferenceEquals(events[0],events[1])&&events[0].Stage=="preparing","previous lifecycle event remains immutable");
     Assert(events[0].Message.IndexOf("must-not-appear",StringComparison.Ordinal)<0&&events[0].Message.IndexOf("private-target",StringComparison.Ordinal)<0,"startup display excludes private target credentials");
    }
    events.Clear();var failure=new IOException("Failed access_token=private-token");Exception actual=null;
    try{controller.RunStartupPresentation("launch",null,()=>{throw failure;});}catch(Exception error){actual=error;}
    Assert(ReferenceEquals(actual,failure),"original startup exception preserved");
    Assert(events.Count==2&&events[1].Stage=="failed"&&!events[1].Message.Contains("private-token"),"failed launch keeps safe diagnostic in same startup session");
    events.Clear();controller.RunStartupPresentation("status",null,()=>new object());Assert(events.Count==0,"nonlaunch reads never create startup windows");
    controller.StartupPresentation=value=>{throw new InvalidOperationException("observer unavailable");};int launched=0;
    controller.RunStartupPresentation("launch",null,()=>{launched++;return new object();});Assert(launched==1,"presentation failure cannot prevent game launch");
   }}finally{Directory.Delete(root,true);}
  }
  [STAThread] public static int Main(){try{Run();return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
