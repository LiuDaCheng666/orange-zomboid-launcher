using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class CoreUpdateServiceTests {
  public static int Main(string[] args) {try {if(args.Length==2&&args[0]=="--lock"){try{using(CoreDeployment.Acquire(args[1]))return 4;}catch(IOException e){return e.Message.Contains("Another process owns")?0:5;}}CoreFixture.Initialize();CoreReleaseSourcesTests.Run();CoreReleaseNetworkTests.Run();Policy();Reservation();Service();Console.WriteLine("CORE SERVICE: "+CoreFixture.Passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  static object Invoke(object s,string name){try{return s.GetType().GetMethod(name).Invoke(s,null);}catch(TargetInvocationException e){throw e.InnerException;}}
  static Dictionary<string,object> Snapshot(object s){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new JavaScriptSerializer().Serialize(Invoke(s,"Status")));}
  static void Until(object s,string state){var end=DateTime.UtcNow.AddSeconds(10);while((string)Snapshot(s)["state"]!=state){if(DateTime.UtcNow>end)throw new Exception("FAIL: expected "+state+", got "+new JavaScriptSerializer().Serialize(Snapshot(s)));Thread.Sleep(10);}}
  static string NextVersion(){var current=new Version(AppVersion.Current);return current.Major+"."+current.Minor+"."+(current.Build+1);}
  static string InstallCurrent(){string root=CoreFixture.Root();CoreDeployment.Install(root,CoreFixture.Manifest(AppVersion.Current,CoreFixture.A),CoreFixture.A,CoreFixture.Key);return root;}
  static void CurrentRelease(){string root=InstallCurrent();var current=CoreFixture.Manifest(AppVersion.Current,CoreFixture.A);int downloads=0;using(var service=new CoreUpdateService(root,CoreFixture.Key,token=>new[]{current.VerifiedEnvelope.Raw,current.VerifiedEnvelope.Signature},(url,token)=>{downloads++;throw new Exception("Current release must not download");})){service.Check();Until(service,"Latest");CoreFixture.Check(service.Status().latestVersion==AppVersion.Current&&downloads==0,"signed current release is Latest without downloading");CoreFixture.Same(root,CoreFixture.A,"current release check preserves installation");}}
  static void RetryCache(){
   string root=InstallCurrent();var files=CoreFixture.B();files["Web/app.css"]=CoreFixture.Bytes("new-css");var target=CoreFixture.Manifest(NextVersion(),files);int js=0,css=0;bool fail=true;
   using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var controller=new ModernController(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true,null,(e,a,p,progress,token,batch)=>{entered.Set();release.Wait();return new{items=new object[0]};}))
   using(var service=new CoreUpdateService(root,CoreFixture.Key,token=>new[]{target.VerifiedEnvelope.Raw,target.VerifiedEnvelope.Signature},(url,token)=>{if(url.EndsWith("app.js")){js++;return new MemoryStream(CoreFixture.Bytes("new-js"));}css++;return new MemoryStream(CoreFixture.Bytes(fail?"bad-css":"new-css"));})) {
    var busy=Task.Run(()=>controller.Execute("updatesGet",new Dictionary<string,object>()));CoreFixture.Check(entered.Wait(5000)&&controller.Busy,"real controller network lane is occupied");service.Check();Until(service,"Available");CoreFixture.Check(!busy.IsCompleted,"core check finishes while real controller remains occupied");release.Set();busy.Wait();
    service.Download();Until(service,"Error");CoreFixture.Same(root,CoreFixture.A,"corrupt streamed file leaves installed baseline intact");fail=false;service.Download();Until(service,"Ready");CoreFixture.Check(js==1&&css==2,"retry reuses verified JS cache and refetches failed CSS only");
   }
  }
  static void Service() {
   CurrentRelease();
   RetryCache();
   CoreFixture.Check(CoreReleaseNetwork.Allowed(new Uri("https://release-assets.githubusercontent.com/github-production-release-asset/file?sig=x")),"official HTTPS release asset redirect accepted");
   foreach(string uri in new[]{"http://github.com/file","https://github.com.evil.invalid/file","https://github.com:444/file","https://user@github.com/file","https://example.invalid/file"})CoreFixture.Check(!CoreReleaseNetwork.Allowed(new Uri(uri)),"unsafe download redirect refused: "+uri);
   var type=typeof(CoreFixture).Assembly.GetType("PZ3D.CoreUpdateService");CoreFixture.Check(type!=null,"authenticated native update service exists");
   string root=InstallCurrent();var target=CoreFixture.Manifest(NextVersion(),CoreFixture.B());int opens=0,checks=0;bool corrupt=false,block=false;
   using(var entered=new ManualResetEventSlim())using(var proceed=new ManualResetEventSlim()) {
    Func<CancellationToken,byte[][]> lookup=t=>{Interlocked.Increment(ref checks);return new[]{target.VerifiedEnvelope.Raw,corrupt?new byte[256]:target.VerifiedEnvelope.Signature};};
    Func<string,CancellationToken,Stream> open=(url,t)=>{Interlocked.Increment(ref opens);CoreFixture.Check(url.EndsWith("/app.js"),"sparse download fetches only changed JS");entered.Set();if(block){t.WaitHandle.WaitOne(10000);t.ThrowIfCancellationRequested();}proceed.Wait(t);return new MemoryStream(CoreFixture.Bytes("new-js"));};
    var service=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{root,CoreFixture.Key,lookup,open},null);
    try {
     corrupt=true;Invoke(service,"Check");Until(service,"Error");CoreFixture.Check(string.IsNullOrEmpty((string)Snapshot(service)["latestVersion"]),"unauthenticated publication never offered");
     corrupt=false;Invoke(service,"Check");Until(service,"Available");CoreFixture.Check((string)Snapshot(service)["currentVersion"]==AppVersion.Current&&(string)Snapshot(service)["latestVersion"]==target.version,"native authority supplies both versions");
     block=true;Invoke(service,"Download");CoreFixture.Check(entered.Wait(5000),"download starts asynchronously");Invoke(service,"Download");CoreFixture.Check(opens==1,"repeated download starts one job");
     using(var child=Process.Start(new ProcessStartInfo(typeof(CoreFixture).Assembly.Location,"--lock \""+root+"\""){UseShellExecute=false,CreateNoWindow=true})){CoreFixture.Check(child.WaitForExit(5000)&&child.ExitCode==0,"real other process specifically refused by download mutex");}
     Invoke(service,"Cancel");Until(service,"Cancelled");using(CoreDeployment.Acquire(root))CoreFixture.Check(true,"cancel releases thread-affine mutex");CoreFixture.Same(root,CoreFixture.A,"cancel preserves current installation");
     block=false;proceed.Set();Invoke(service,"Download");Until(service,"Ready");CoreFixture.Check((long)Convert.ToInt64(Snapshot(service)["totalBytes"])==6,"download byte total excludes unchanged files");CoreFixture.Same(root,CoreFixture.A,"prepared update does not replace running files");
    }finally{((IDisposable)service).Dispose();}
   }
  }
  static void Policy() {
   foreach(string action in new[]{"coreUpdateStatus","coreUpdateCheck","coreUpdateDownload","coreUpdateCancel"}) {
    bool accepted=true;try{ModernHostPolicy.Validate("https://orange.local/index.html","{\"id\":\"u1\",\"action\":\""+action+"\",\"payload\":{\"protocol\":1}}");}catch{accepted=false;}
    CoreFixture.Check(accepted,"native update lane accepts compatible "+action);
    CoreFixture.Reject(()=>ModernHostPolicy.Validate("https://orange.local/index.html","{\"id\":\"u1\",\"action\":\""+action+"\",\"payload\":{\"protocol\":1,\"url\":\"https://evil.invalid\"}}"),"page cannot choose update URL: "+action);
   }
   CoreFixture.Reject(()=>ModernHostPolicy.Validate("https://orange.local/index.html","{\"id\":\"u1\",\"action\":\"coreUpdateCheck\",\"payload\":{\"protocol\":2}}"),"incompatible page protocol refused");
  }
  static void Reservation() {
   var q=new ModernDispatchQueue();var reserve=typeof(ModernDispatchQueue).GetMethod("TryReserveRestart");CoreFixture.Check(reserve!=null,"host can reserve restart against queued mutations");
   using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()) {
    var read=q.Execute(()=>{entered.Set();release.Wait(5000);return null;});CoreFixture.Check(entered.Wait(5000),"read occupies dispatch lane");
    var write=q.ExecuteMutation(()=>null);CoreFixture.Check(!(bool)reserve.Invoke(q,null),"queued write prevents restart before controller Busy");release.Set();Task.WaitAll(read,write);
   }
   CoreFixture.Check((bool)reserve.Invoke(q,null),"idle host reserves restart");
   CoreFixture.Reject(()=>q.ExecuteMutation(()=>null).GetAwaiter().GetResult(),"new write cannot enter reserved host");
   typeof(ModernDispatchQueue).GetMethod("ReleaseRestart").Invoke(q,null);
   q.ExecuteMutation(()=>null).Wait();CoreFixture.Check(true,"refused restart releases reservation");
   var guard=typeof(ModernController).GetMethod("AssertCanRestart");CoreFixture.Check(guard!=null,"restart gate checks fresh game and launch session state");
   string root=CoreFixture.Root();using(var controller=new ModernController(System.IO.Path.Combine(root,"game"),System.IO.Path.Combine(root,"user"),System.IO.Path.Combine(root,"data"),true)){guard.Invoke(controller,null);CoreFixture.Check(true,"idle synthetic controller permits restart");var engine=(Engine)typeof(ModernController).GetField("engine",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(controller);Directory.CreateDirectory(engine.Game);File.WriteAllText(engine.Config,"{\"vmArgs\":[]}");LaunchProfiles.Stage(engine,new ProfileLaunchRequest{Kind="save",Id="Sandbox/A",Mode="Sandbox",Save="A"});CoreFixture.Reject(()=>guard.Invoke(controller,null),"pending launch restoration prevents updater restart");CoreFixture.Check(LaunchProfiles.Pending(engine),"restart refusal never restores a game session itself");LaunchProfiles.Recover(engine,true);}
  }
 }
}
