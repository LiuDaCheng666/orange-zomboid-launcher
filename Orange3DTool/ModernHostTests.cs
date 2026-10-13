using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace PZ3D {
 static class ModernHostTests {
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  public static void Run(){
   var policy=typeof(Engine).Assembly.GetType("PZ3D.ModernHostPolicy");Check(policy!=null,"modern bridge origin and command policy exists");var accept=policy.GetMethod("Validate");
   Func<string,string,bool> valid=(source,json)=>{try{accept.Invoke(null,new object[]{source,json});return true;}catch{return false;}};
   Check(valid("https://orange.local/index.html","{\"id\":\"1\",\"action\":\"memoryGet\",\"payload\":{}}"),"local trusted memory read accepted");
   Check(!valid("https://evil.test/index.html","{\"id\":\"1\",\"action\":\"maintenance\",\"payload\":{\"kind\":\"cache\",\"confirmed\":true}}"),"external origin cannot drive destructive backend");
   Check(!valid("https://orange.local.evil.test/","{\"id\":\"1\",\"action\":\"status\",\"payload\":{}}"),"lookalike origin rejected");
   Check(!valid("https://orange.local/index.html","{\"id\":\"1\",\"action\":\"shell\",\"payload\":{}}"),"unknown host execution action rejected");
   Check(!valid("https://orange.local/index.html","{\"id\":\"1\",\"action\":\"setGame\",\"payload\":{\"path\":\"C:\\\"}}"),"frontend cannot forge native selected folder action");
   Check(!valid("https://orange.local/index.html","{\"id\":\"1\",\"action\":\"importShader\",\"payload\":{\"path\":\"C:\\\"}}"),"frontend cannot forge native file selection");
   Check(!valid("https://orange.local/index.html",new string('x',300000)),"oversized native message refused");
   var assets=typeof(Engine).Assembly.GetType("PZ3D.ModernAssets");Check(assets!=null,"embedded asset confinement policy exists");var target=assets.GetMethod("EntryTarget");string root=Path.Combine(Path.GetTempPath(),"orange-host-policy-"+Guid.NewGuid().ToString("N"));
   string allowed=(string)target.Invoke(null,new object[]{root,"author-covers/3809306528.jpg"});Check(allowed.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar),"author cover maps under own asset root");
   foreach(string bad in new[]{"../outside.dll","C:/outside.dll","/rooted.dll","assets/../../escape.dll"}){bool refused=false;try{target.Invoke(null,new object[]{root,bad});}catch{refused=true;}Check(refused,"path traversal or absolute embedded entry refused: "+bad);}
   var queueType=typeof(Engine).Assembly.GetType("PZ3D.ModernDispatchQueue");Check(queueType!=null,"native bridge serializes simultaneous initial configuration reads");var queue=Activator.CreateInstance(queueType);var execute=queueType.GetMethod("Execute");int enteredCount=0;using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){
    var first=(Task<object>)execute.Invoke(queue,new object[]{new Func<object>(()=>{Interlocked.Increment(ref enteredCount);entered.Set();release.Wait(5000);return "first";}),false});Check(entered.Wait(5000),"first native read started");var second=(Task<object>)execute.Invoke(queue,new object[]{new Func<object>(()=>{Interlocked.Increment(ref enteredCount);return "second";}),false});Check(enteredCount==1&&!second.IsCompleted,"second read queued instead of transient busy failure");var cancel=(Task<object>)execute.Invoke(queue,new object[]{new Func<object>(()=>"cancelled"),true});Check(cancel.Wait(5000)&&cancel.Result.ToString()=="cancelled","cancel can bypass active native operation queue");release.Set();Task.WaitAll(first,second);Check(second.Result.ToString()=="second"&&enteredCount==2,"queued read completes after first read");}
  }
  public static void QueuedCancel(){var q=new ModernDispatchQueue();var method=typeof(ModernDispatchQueue).GetMethod("ExecuteMutation");Check(method!=null,"queued mutations have cancellation generation");int installed=0;using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim()){var read=q.Execute(()=>{entered.Set();release.Wait(5000);return null;});Check(entered.Wait(5000),"read occupies queue before mutation");var mutation=(Task<object>)method.Invoke(q,new object[]{new Func<object>(()=>{installed++;return null;})});q.Execute(()=>null,true).Wait();release.Set();read.Wait();bool cancelled=false;try{mutation.Wait();}catch(AggregateException ex){cancelled=ex.InnerException is OperationCanceledException;}Check(cancelled&&installed==0,"cancelled queued install never starts");var fresh=(Task<object>)method.Invoke(q,new object[]{new Func<object>(()=>{installed++;return null;})});fresh.Wait();Check(installed==1,"fresh mutation after cancellation remains usable");}}
  public static int Main(){try{Run();QueuedCancel();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
