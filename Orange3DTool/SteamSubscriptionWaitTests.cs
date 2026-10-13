using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class SteamSubscriptionWaitTests {
  const ulong Item=3810637496UL;
  static void Check(bool pass,string text){if(!pass)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);}
  static void Wait(Func<ulong,uint> state,Action<ulong> subscribe,Action pump,CancellationToken cancel,Action<int,string> progress,Func<long> elapsed,Action<int> pause){
   SteamWorkshop.EnsureSubscribed(Item,state,subscribe,pump,cancel,progress,elapsed,pause);
  }
  static bool Refuses(Action action,string fragment){try{action();return false;}catch(Exception x){return x.Message.Contains(fragment);}}
  static void Capture(Dictionary<ulong,uint> results,uint app,ulong id,uint code){
   SteamWorkshop.CaptureDownload(results,new HashSet<ulong>{Item},app,id,code);
  }
  public static void Run(){
   int requests=0,pumps=0;long elapsed=0;var results=new Dictionary<ulong,uint>();
   Wait(id=>pumps>=2?5U:4U,id=>{ValidateSubscription(1,id,id);requests++;},()=>{pumps++;Capture(results,480,Item,1);Capture(results,108600,42,1);Capture(results,108600,Item,1);},CancellationToken.None,null,()=>elapsed,ms=>elapsed+=ms);
   Check(requests==1&&pumps==2&&results.Count==1&&results[Item]==1,"delayed state pumps matching completion and preserves it for later download validation");
   int downloads=0;RequestDownload(results,Item,id=>{downloads++;return true;});Check(DownloadCompleted(results,Item)&&downloads==0,"saved successful completion is consumed without starting another download");
   var empty=new Dictionary<ulong,uint>();RequestDownload(empty,Item,id=>{downloads++;return true;});Check(!DownloadCompleted(empty,Item)&&downloads==1,"starting a download alone does not fabricate completion");
   Check(Refuses(()=>RequestDownload(empty,Item,id=>false),"拒绝下载"),"download request refusal remains a failure");
   Capture(results,108600,Item,15);
   Check(!SteamWorkshop.SuccessResult(108600,Item,results[Item],Item),"selected rejected download completion cannot authorize installation");
   requests=0;pumps=0;Wait(id=>5,id=>requests++,()=>pumps++,CancellationToken.None,null,()=>0,ms=>{});
   Check(requests==0&&pumps==0,"already subscribed avoids another request and any wait");
   foreach(uint code in new[]{1U,15U}){ulong returned=code==1?42UL:Item;Check(Refuses(()=>Wait(id=>4,id=>ValidateSubscription(code,returned,id),()=>{throw new Exception("must not pump");},CancellationToken.None,null,()=>0,ms=>{}),"订阅被拒绝"),"native result validation rejects "+(code==1?"mismatched item":"EResult=15")+" before pumping");}
   Check(Refuses(()=>DownloadCompleted(results,Item),"EResult=15"),"retained rejected completion cannot authorize installation");
   using(var cancelled=new CancellationTokenSource()){pumps=0;bool stopped=false;try{Wait(id=>4,id=>{},()=>{pumps++;cancelled.Cancel();},cancelled.Token,null,()=>0,ms=>{throw new Exception("must not wait after cancellation");});}catch(OperationCanceledException){stopped=true;}Check(stopped&&pumps==1,"cancellation from pump stops before another state check or wait");}
   using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();requests=0;bool stopped=false;try{Wait(id=>{throw new Exception("must not read");},id=>requests++,()=>{},cancelled.Token,null,()=>0,ms=>{});}catch(OperationCanceledException){stopped=true;}Check(stopped&&requests==0,"pre-cancelled wait performs no read or subscription");}
   elapsed=0;int pauses=0,progress=0;long previous=-1000;bool bounded=true,throttled=true;
   Check(Refuses(()=>Wait(id=>4,id=>{},()=>{},CancellationToken.None,(v,t)=>{throttled&=t.Contains("等待 Steam 同步订阅状态")&&elapsed-previous>=1000;previous=elapsed;progress++;},()=>elapsed,ms=>{bounded&=ms>0&&ms<=100;pauses++;elapsed+=ms;}),"订阅状态同步超时"),"never-set subscription flag fails specifically instead of returning paths");
   Check(bounded&&throttled,"waits are at most 100ms and subscription progress is limited to once per second");
   Check(elapsed==15000&&pauses==150&&progress==15,"subscription synchronization deadline is 15000 monotonic milliseconds");
  }
  static void ValidateSubscription(uint code,ulong returned,ulong requested){SteamWorkshop.ValidateSubscription(code,returned,requested);}
  static bool DownloadCompleted(Dictionary<ulong,uint> results,ulong id){return SteamWorkshop.DownloadCompleted(results,id);}
  static void RequestDownload(Dictionary<ulong,uint> results,ulong id,Func<ulong,bool> request){SteamWorkshop.RequestDownload(results,id,request);}
  static Dictionary<ulong,string> FetchWorker(string fixture,Action<int,string> progress,CancellationToken cancel){
   return WorkshopWorker.Transport(fixture,new[]{Item},progress,cancel,false,Process.GetCurrentProcess().MainModule.FileName);
  }
  public static void WorkerTests(){
   string error="";try{FetchWorker("synthetic-error",null,CancellationToken.None);}catch(Exception x){error=x.Message;}
   Check(error.StartsWith("UTF8 安装目录验证失败")&&error.Contains("释放 Steam 游戏身份")&&!error.Contains("76561198012345678"),"structured UTF8 worker failure stays primary and raw stderr identity is discarded");
   string data=Path.Combine(LauncherTestPaths.Root,"subscription-worker-"+Guid.NewGuid().ToString("N"));var journal=new LauncherJournal(data);journal.Begin("workshop","synthetic download");journal.Finish("failed",error);
   Check(!File.ReadAllText(Path.Combine(data,"diagnostics","tasks.json")).Contains("76561198012345678")&&!File.ReadAllText(Path.Combine(data,"diagnostics","launcher.log")).Contains("76561198012345678"),"component worker failure does not persist native identity in diagnostics");
   Check(Refuses(()=>FetchWorker("synthetic-raw",null,CancellationToken.None),"原生工坊下载阶段失败"),"nonprotocol child failure uses a clear bounded generic phase");
   int seen=0;var paths=FetchWorker("synthetic-success",(v,t)=>seen=v,CancellationToken.None);Check(seen==37&&paths[Item]=="X:\\synthetic content","existing progress and paths protocol remains compatible");
   using(var cancelled=new CancellationTokenSource()){cancelled.CancelAfter(200);bool stopped=false;try{FetchWorker("synthetic-hang",null,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"cancellation stops only the owned component worker");}
   var capture=new StringWriter();TextWriter prior=Console.Out;try{Console.SetOut(capture);Check(WorkshopWorker.Run("synthetic-no-Steam","0",false)==1,"invalid worker request fails before native Steam access");}finally{Console.SetOut(prior);}
   var line=capture.ToString().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)[0];var protocol=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);Check(Convert.ToString(protocol["kind"])=="error"&&Convert.ToString(protocol["message"]).Contains("未知条目"),"worker emits explicit bounded JSON error on stdout");
  }
  static int Fixture(string[] args){if(args.Length<3||args[0]!="--workshop-worker")return -1;Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true});Console.Error.WriteLine("native SteamID=76561198012345678");Console.WriteLine("Breakpad native SteamID=76561198012345678");if(args[1]=="synthetic-hang"){Thread.Sleep(30000);return 1;}var json=new JavaScriptSerializer();if(args[1]=="synthetic-success"){Console.WriteLine(json.Serialize(new{kind="progress",value=37,text="synthetic progress"}));Console.WriteLine(json.Serialize(new{kind="paths",paths=new Dictionary<string,string>{{Item.ToString(),"X:\\synthetic content"}}}));return 0;}if(args[1]=="synthetic-error")Console.WriteLine(json.Serialize(new{kind="error",message="UTF8 安装目录验证失败 · item=3810637496 · SteamID=76561198012345678"}));return 1;}
  public static int Main(string[] args){int fixture=Fixture(args);if(fixture>=0)return fixture;try{if(args.Contains("--worker-tests"))WorkerTests();else Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
 }
}
