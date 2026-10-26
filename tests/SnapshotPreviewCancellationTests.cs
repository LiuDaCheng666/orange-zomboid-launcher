using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace PZ3D {
 static class SnapshotPreviewCancellationTests {
  static int checks,failures;
  static Dictionary<string,object> P(params string[] pairs){var p=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)p.Add(pairs[i],pairs[i+1]);return p;}
  static void Check(bool condition,string label){checks++;if(!condition)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  static Dictionary<string,object> D(object value){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}
  static Exception Capture(Action action){try{action();return null;}catch(Exception error){return error;}}
  sealed class Fixture:IDisposable {
   internal readonly string Root;internal readonly ModernController Controller;
   internal Fixture(Func<Engine,string,Dictionary<string,object>,Action<int,string>,CancellationToken,Action<object>,object> network=null){
    Root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"ozl-preview-cancel-"+Guid.NewGuid().ToString("N")));LauncherMaintenance.NoReparse(Root);
    string game=Path.Combine(Root,"steamapps","common","ProjectZomboid"),user=Path.Combine(Root,"user"),data=Path.Combine(Root,"data");
    Put(Path.Combine(game,"ProjectZomboid64.exe"),"owned fixture, never executed");Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");Put(Path.Combine(game,"version.txt"),"42.21.0");
    Put(Path.Combine(user,"mods","default.txt"),"mods\n{\n}\nmaps\n{\n}\n");Put(Path.Combine(user,"Saves","Sandbox","Owned","mods.txt"),"mods\n{\n}\nmaps\n{\n}\n");
    Controller=new ModernController(game,user,data,true,null,network);
   }
   void Put(string path,string text){string full=Path.GetFullPath(path);if(!full.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Fixture write escaped owned root.");LauncherMaintenance.NoReparse(full);Directory.CreateDirectory(Path.GetDirectoryName(full));File.WriteAllText(full,text,new UTF8Encoding(false));}
   internal object Gate{get{return typeof(ModernController).GetField("gate",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Controller);}}
   internal object Preview(string id){return Controller.Execute("environmentPreview",P("saveId","Sandbox/Owned","jobId",id));}
   internal object Cancel(string id){return Controller.Execute("environmentPreviewCancel",P("jobId",id));}
   public void Dispose(){Controller.Dispose();}
  }
  static void Run(string name,Action test){try{test();}catch(Exception error){failures++;Console.WriteLine(name+": "+error);}}
  public static int Main(){Console.OutputEncoding=new UTF8Encoding(false);Run("gate cancellation",GateCancellation);Run("queued cancellation",CancelBeforeInvocation);Run("different job",CancelDifferentJob);Run("retry and legacy",CancelThenRetry);Run("context reset",ContextReset);Run("dispose",DisposedCancellation);Run("phase progress",StageProgress);Console.WriteLine("PREVIEW CANCELLATION: "+checks+" checks, "+failures+" failures");return failures==0?0:1;}

  // Break caught: cancel reaches the parallel dispatcher before the preview can
  // register behind controller.gate. Cancelling that job must never cancel the
  // independently running network query or permit later scan progress.
  static void GateCancellation(){
   using(var networkEntered=new ManualResetEventSlim())using(var networkRelease=new ManualResetEventSlim()){
    bool networkCancelled=false;using(var f=new Fixture((e,a,p,progress,token,batch)=>{networkEntered.Set();try{networkRelease.Wait(token);return new{state="owned network fixture"};}catch(OperationCanceledException){networkCancelled=true;throw;}})){
     var network=Task.Run(()=>f.Controller.Execute("steamConnection",P()));Check(networkEntered.Wait(5000),"unrelated network query is active before preview cancellation");
     string id=Guid.NewGuid().ToString("N");int events=0;f.Controller.Event=(kind,value)=>{if(kind=="environmentProgress")Interlocked.Increment(ref events);};Exception previewError=null;object cancelResult=null;
     using(var entered=new ManualResetEventSlim()){
      var worker=new Thread(()=>{entered.Set();previewError=Capture(()=>f.Preview(id));}){IsBackground=true};Monitor.Enter(f.Gate);
      try{worker.Start();Check(entered.Wait(5000)&&SpinWait.SpinUntil(()=>(worker.ThreadState&ThreadState.WaitSleepJoin)!=0,5000),"preview is deterministically waiting for controller gate");cancelResult=f.Cancel(id);}finally{Monitor.Exit(f.Gate);}
      Check(worker.Join(5000),"early-cancelled preview exits promptly after gate releases");
     }
     try{Check((bool)D(cancelResult)["cancelled"],"matching cancellation is accepted before scanning starts");Check(previewError is OperationCanceledException&&events==0,"early cancellation emits no scan progress and returns cancellation");Check(!networkCancelled&&!network.IsCompleted,"preview cancellation leaves unrelated network query active");}
     finally{networkRelease.Set();Check(network.Wait(5000)&&!networkCancelled,"unrelated network query finishes normally");}
    }
   }
  }
  static void CancelBeforeInvocation(){using(var f=new Fixture()){
   string id=Guid.NewGuid().ToString("N");int events=0;f.Controller.Event=(kind,value)=>{if(kind=="environmentProgress")events++;};object result=f.Cancel(id);Exception error=Capture(()=>f.Preview(id));
   Check((bool)D(result)["cancelled"]&&error is OperationCanceledException&&events==0,"cancel delivered before the queued preview invocation is retained by exact job ID");
  }}
  static void CancelDifferentJob(){using(var f=new Fixture())using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
   string running=Guid.NewGuid().ToString("N"),queued=Guid.NewGuid().ToString("N");int events=0;Exception error=null;
   f.Controller.Event=(kind,value)=>{if(kind=="environmentProgress"&&Interlocked.Increment(ref events)==1){entered.Set();release.Wait(5000);}};
   var worker=new Thread(()=>error=Capture(()=>f.Preview(running))){IsBackground=true};worker.Start();try{Check(entered.Wait(5000),"first preview reaches its actual progress callback");f.Cancel(queued);}finally{release.Set();}
   Check(worker.Join(5000)&&error==null,"cancelling a different queued job preserves the running preview");int before=events;error=Capture(()=>f.Preview(queued));Check(error is OperationCanceledException&&events==before,"the other job remains cancelled when it eventually starts");
  }}
  static void CancelThenRetry(){using(var f=new Fixture()){
   string first=Guid.NewGuid().ToString("N");int count=0;f.Controller.Event=(kind,value)=>{if(kind=="environmentProgress"&&count++==0)f.Cancel(first);};Exception error=Capture(()=>f.Preview(first));Check(error is OperationCanceledException,"normal in-progress preview cancellation remains cancellation");
   f.Controller.Event=null;var retry=(SaveEnvironmentPlan)f.Preview(Guid.NewGuid().ToString("N"));Check(retry.ready,"new-job preview succeeds after cancellation and releases occupied state");var legacy=(SaveEnvironmentPlan)f.Controller.Execute("environmentPreview",P("saveId","Sandbox/Owned"));Check(legacy.ready,"legacy preview without a job ID remains supported");
  }}
  static void ContextReset(){using(var f=new Fixture()){
   string id=Guid.NewGuid().ToString("N");f.Cancel(id);typeof(ModernController).GetMethod("CancelEnvironmentPreviews",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f.Controller,null);var result=(SaveEnvironmentPlan)f.Preview(id);Check(result.ready,"context reset retires prior pending cancellation state");
  }}
  static void DisposedCancellation(){using(var f=new Fixture()){
   f.Cancel(Guid.NewGuid().ToString("N"));f.Controller.Dispose();Exception error=Capture(()=>f.Cancel(Guid.NewGuid().ToString("N")));Check(error is ObjectDisposedException,"disposed controller refuses new preview cancellation state");
  }}
  static void StageProgress(){using(var f=new Fixture()){
   int global=-1,scoped=-1;f.Controller.Progress=(value,text)=>global=value;f.Controller.Event=(kind,value)=>{if(kind=="environmentProgress")scoped=Convert.ToInt32(D(value)["value"]);};
   f.Controller.EnvironmentReport("Sandbox/Owned",null,new SaveEnvironmentProgress{stage="copy",text="copy stage finished",value=100});Check(global==99&&scoped==100,"phase completion retains true scoped percentage without reporting an atomic global commit");
   f.Controller.EnvironmentReport("Sandbox/Owned",null,new SaveEnvironmentProgress{stage="complete",text="atomic commit finished",value=100});Check(global==100&&scoped==100,"only committed completion forwards global 100 percent");
  }}
 }
}
