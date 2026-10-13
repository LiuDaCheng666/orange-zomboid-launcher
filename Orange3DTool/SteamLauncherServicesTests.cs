using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PZ3D {
 static class SteamLauncherServicesTests {
  static Type Service { get { return typeof(Engine).Assembly.GetType("PZ3D.SteamLauncherServices"); } }
  static void Check(bool pass,string text){if(!pass)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);}
  static object Call(string name,params object[] args){try{var method=Service.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);var parameters=method.GetParameters();if(args.Length<parameters.Length){var supplied=args;args=new object[parameters.Length];Array.Copy(supplied,args,supplied.Length);for(int i=supplied.Length;i<args.Length;i++)args[i]=parameters[i].DefaultValue;}return method.Invoke(null,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static Dictionary<string,object> Map(object value){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new JavaScriptSerializer().Serialize(value));}
  static bool Refuses(Action call,string fragment){try{call();return false;}catch(Exception x){return x.Message.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0;}}
  static void Put(byte[] bytes,int offset,byte[] value){Buffer.BlockCopy(value,0,bytes,offset,value.Length);}
  static byte[] Ugc(uint creator,uint consumer,ulong id){var bytes=new byte[9776];Put(bytes,0,BitConverter.GetBytes(id));Put(bytes,8,BitConverter.GetBytes(1));Put(bytes,16,BitConverter.GetBytes(creator));Put(bytes,20,BitConverter.GetBytes(consumer));Put(bytes,24,Encoding.UTF8.GetBytes("橙子合成模组"));Put(bytes,153,Encoding.UTF8.GetBytes("Synthetic description <script>text</script>"));Put(bytes,8160,BitConverter.GetBytes(76561197960265728UL));Put(bytes,8172,BitConverter.GetBytes(1700000000U));Put(bytes,9492,BitConverter.GetBytes(123456));Put(bytes,9768,BitConverter.GetBytes(2U));return bytes;}
  static byte[] Server(){var bytes=new byte[376];Put(bytes,0,BitConverter.GetBytes((ushort)16261));Put(bytes,2,BitConverter.GetBytes((ushort)27015));Put(bytes,4,BitConverter.GetBytes(0xCB007109U));Put(bytes,8,BitConverter.GetBytes(38));bytes[12]=1;Put(bytes,78,Encoding.UTF8.GetBytes("Synthetic PZ"));Put(bytes,144,BitConverter.GetBytes(108600U));Put(bytes,148,BitConverter.GetBytes(7));Put(bytes,152,BitConverter.GetBytes(32));bytes[160]=1;Put(bytes,172,Encoding.UTF8.GetBytes("合成服务器 <b>"));Put(bytes,368,BitConverter.GetBytes(90123456789012345UL));return bytes;}
  public static void Run(){RunUnit();TransportTests();HallTransportTests();}
  public static void RunUnit(){
   Check(Service!=null,"Steam services run in an isolated request worker");
   // Removing allowlisting would persist credentials or allow unintended actions.
   var normalized=Map(Call("Normalize","search",new Dictionary<string,object>{{"query","  橙子  "},{"page",2},{"password","DO-NOT-PERSIST"},{"ticket","DO-NOT-PERSIST"}}));
   Check(Convert.ToString(normalized["query"])=="橙子"&&Convert.ToInt32(normalized["page"])==2&&!normalized.ContainsKey("password")&&!normalized.ContainsKey("ticket"),"worker request contains only scoped search fields");
   Check(Refuses(()=>Call("Normalize","download",new Dictionary<string,object>{{"id","42"},{"confirmed","true"}}),"确认"),"download requires a boolean explicit confirmation");
   Check(Refuses(()=>Call("Normalize","download",new Dictionary<string,object>{{"id","0"},{"confirmed",true}}),"ID"),"zero Workshop item cannot be downloaded");
   Check(Refuses(()=>Call("Normalize","search",new Dictionary<string,object>{{"page",0}}),"页"),"search rejects page zero");
   Check(Refuses(()=>Call("Normalize","unsubscribe",new Dictionary<string,object>()),"操作"),"unknown worker operation is refused");
   var item=Map(Call("ParseUgc",Ugc(108600,108600,42),5U,"https://example.invalid/cover.png",new ulong[]{81,82}));
   Check(Convert.ToString(item["id"])=="42"&&Convert.ToString(item["title"])=="橙子合成模组"&&Convert.ToString(item["author"])=="76561197960265728"&&Convert.ToInt64(item["sizeBytes"])==123456,"Workshop binary fields use Windows x64 offsets and UTF-8");
   Check(Convert.ToBoolean(item["subscribed"])&&Convert.ToBoolean(item["downloaded"])&&!Convert.ToBoolean(item["needsUpdate"])&&((System.Collections.IEnumerable)item["dependencies"]).Cast<object>().Count()==2,"Workshop status and dependencies preserve real source state");
   var stale=Map(Call("ParseUgc",Ugc(108600,108600,42),12U,"file:///private",new ulong[0]));
   Check(!Convert.ToBoolean(stale["subscribed"])&&Convert.ToBoolean(stale["downloaded"])&&Convert.ToBoolean(stale["needsUpdate"])&&Convert.ToString(stale["previewUrl"])=="","cached installed item is distinct from subscription and unsafe preview URLs are removed");
   Check(Refuses(()=>Call("ParseUgc",new byte[64],0U,"",new ulong[0]),"ABI"),"truncated Workshop data is refused");
   Check(Refuses(()=>Call("ParseUgc",Ugc(480,108600,42),0U,"",new ulong[0]),"108600"),"foreign creator app is refused");
   Check(Refuses(()=>Call("ParseUgc",Ugc(108600,480,42),0U,"",new ulong[0]),"108600"),"foreign consumer app is refused");
   int mutations=0;Action<ulong> mutation=id=>mutations++;
   Check(Refuses(()=>Call("DownloadVerified",42UL,false,new Func<ulong,byte[]>(id=>Ugc(108600,108600,id)),mutation),"确认")&&mutations==0,"missing confirmation prevents all Steam mutations");
   Check(Refuses(()=>Call("DownloadVerified",42UL,true,new Func<ulong,byte[]>(id=>Ugc(480,108600,id)),mutation),"108600")&&mutations==0,"creator app verification precedes subscription and download");
   Check(Refuses(()=>Call("DownloadVerified",42UL,true,new Func<ulong,byte[]>(id=>Ugc(108600,480,id)),mutation),"108600")&&mutations==0,"consumer app verification precedes subscription and download");
   Check(Refuses(()=>Call("DownloadVerified",42UL,true,new Func<ulong,byte[]>(id=>Ugc(108600,108600,43)),mutation),"ID")&&mutations==0,"details response ID must match the explicit download ID");
   var denied=Ugc(108600,108600,42);Put(denied,8,BitConverter.GetBytes(15));
   Check(Refuses(()=>Call("DownloadVerified",42UL,true,new Func<ulong,byte[]>(id=>denied),mutation),"EResult")&&mutations==0,"failed details lookup cannot authorize download");
   Call("DownloadVerified",42UL,true,new Func<ulong,byte[]>(id=>Ugc(108600,108600,id)),mutation);
   Check(mutations==1,"verified explicit Workshop item reaches one synthetic mutation boundary");
   var server=Map(Call("ParseServer",Server()));
   Check(Convert.ToString(server["host"])=="203.0.113.9"&&Convert.ToInt32(server["port"])==16261&&Convert.ToInt32(server["queryPort"])==27015&&Convert.ToInt32(server["players"])==7&&Convert.ToInt32(server["maxPlayers"])==32&&Convert.ToInt32(server["ping"])==38,"server hall decodes connection/query ports, host-order IPv4 and metrics");
   Check(Convert.ToString(server["name"])=="合成服务器 <b>"&&Convert.ToBoolean(server["passwordRequired"])&&Convert.ToBoolean(server["steam"]),"server text stays text and password is a flag only");
   var foreign=Server();Put(foreign,144,BitConverter.GetBytes(480U));Check(Call("ParseServer",foreign)==null,"server hall excludes foreign apps");
   var noResponse=Server();noResponse[12]=0;Check(Call("ParseServer",noResponse)==null,"unresponsive server rows do not fabricate online metrics");
   var hallRequest=Map(Call("Normalize","hall",new Dictionary<string,object>{{"query","  合成  "},{"requestId","client-owned"}}));
   Check(Convert.ToString(hallRequest["query"])=="合成"&&!hallRequest.ContainsKey("requestId"),"hall worker never owns the client request correlation");
   SteamLauncherNativeHallTests.Run();

   Console.WriteLine("PASS: Steam services synthetic tests completed without any Steam connection or subscription");
  }
  static Dictionary<string,object> HallRow(){return new Dictionary<string,object>{{"id","hall:203.0.113.9:16261"},{"name","First synthetic <b>"},{"host","203.0.113.9"},{"port",16261},{"queryPort",27015},{"players",7},{"maxPlayers",32},{"ping",38},{"description","Synthetic PZ"},{"passwordRequired",true},{"steam",true}};}
  static void HallTransportTests(){
   var transport=Service.GetMethod("Transport",BindingFlags.Static|BindingFlags.NonPublic).GetParameters();
   Check(transport.Length==8&&transport[7].ParameterType==typeof(Action<object>)&&transport[7].IsOptional,"hall transport accepts an optional batch callback");
   var execute=Service.GetMethod("Execute",BindingFlags.Static|BindingFlags.Public).GetParameters();
   Check(execute.Length==6&&execute[5].ParameterType==typeof(Action<object>)&&execute[5].IsOptional,"existing Execute callers retain the optional streaming contract");
   string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hall-stream-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string exe=Process.GetCurrentProcess().MainModule.FileName,ack=Path.Combine(root,"hall-ack");
   Func<string,Dictionary<string,object>> request=query=>new Dictionary<string,object>{{"action","hall"},{"args",new Dictionary<string,object>{{"query",query},{"requestId","client-owned"}}}};
   try{
    bool returned=false,beforeFinal=false;int batches=0;Dictionary<string,object> first=null;
    var result=Map(Call("Transport",root,"X:\\synthetic game\\",request("hall-first"),exe,5000,null,CancellationToken.None,new Action<object>(batch=>{var data=Map(batch);first=((System.Collections.IEnumerable)data["servers"]).Cast<object>().Select(Map).Single();beforeFinal=!returned&&Directory.GetFiles(root,"response.json",SearchOption.AllDirectories).Length==0;batches++;File.WriteAllText(ack,"continue");})));returned=true;
    Check(beforeFinal&&batches==1&&Convert.ToString(first["id"])=="hall:203.0.113.9:16261"&&Convert.ToInt32(first["players"])==7,"actual worker transport delivers its first server before final response exists");
    Check(!first.ContainsKey("requestId")&&!first.ContainsKey("ticket")&&Convert.ToString(result["message"])=="synthetic final hall result"&&((System.Collections.IEnumerable)result["servers"]).Cast<object>().Count()==1,"batch DTO is allowlisted and final hall result remains authoritative");
    int longest=0;Call("Transport",root,"X:\\synthetic game\\",request("hall-long-text"),exe,5000,null,CancellationToken.None,new Action<object>(batch=>longest=((System.Collections.IEnumerable)Map(batch)["servers"]).Cast<object>().Count()));
    Check(longest==16,"maximum native text escaping fits a bounded hall protocol batch");
    foreach(string bad in new[]{"hall-wrong-nonce","hall-bad-shape","hall-bad-id","hall-bad-number","hall-too-many"}){int leaked=0;Check(Refuses(()=>Call("Transport",root,"X:\\synthetic game\\",request(bad),exe,5000,null,CancellationToken.None,new Action<object>(batch=>leaked++)),"响应")&&leaked==0,"hall protocol rejects "+bad+" before emitting a row");}
    int outOfScope=0;var search=new Dictionary<string,object>{{"action","search"},{"args",new Dictionary<string,object>{{"query","hall-first"},{"page",1}}}};
    Check(Refuses(()=>Call("Transport",root,"X:\\synthetic game\\",search,exe,5000,null,CancellationToken.None,new Action<object>(batch=>outOfScope++)),"响应")&&outOfScope==0,"hall batches cannot escape a non-hall request");
    int partial=0;Check(Refuses(()=>Call("Transport",root,"X:\\synthetic game\\",request("hall-error"),exe,5000,null,CancellationToken.None,new Action<object>(batch=>{partial++;File.WriteAllText(ack,"continue");})),"synthetic hall failure")&&partial==1,"valid partial hall rows survive before the final error reaches the caller");
    using(var cancelled=new CancellationTokenSource()){int received=0;bool stopped=false;try{Call("Transport",root,"X:\\synthetic game\\",request("hall-cancel"),exe,5000,null,cancelled.Token,new Action<object>(batch=>{received++;cancelled.Cancel();}));}catch(OperationCanceledException){stopped=true;}Check(stopped&&received==1,"cancelling from the first hall batch stops the pending worker");}
    Check(Directory.GetDirectories(root).Length==0,"all streaming worker paths release their owned request directories");
   }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }
  static void TransportTests(){
   string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"steam-services-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string exe=Process.GetCurrentProcess().MainModule.FileName;
   Func<string,Dictionary<string,object>> request=query=>new Dictionary<string,object>{{"action","search"},{"args",new Dictionary<string,object>{{"query",query},{"page",1}}}};
   try{
    int seen=-2;var result=Map(Call("Transport",root,"X:\\synthetic game\\",request("success"),exe,5000,new Action<int,string>((v,t)=>seen=v),CancellationToken.None));
    Check(Convert.ToString(result["message"])=="synthetic worker result"&&seen==37,"parent consumes worker response and measured progress");
    Check(Directory.GetDirectories(root).Length==0,"successful worker request files are removed");
    Check(Refuses(()=>Call("Transport",root,"X:\\synthetic game\\",request("wrong-nonce"),exe,5000,null,CancellationToken.None),"响应"),"worker response is bound to its request nonce");
    Check(Refuses(()=>Call("Transport",root,"X:\\synthetic game\\",request("error"),exe,5000,null,CancellationToken.None),"synthetic failure"),"worker errors remain failures instead of empty success lists");
    Check(Refuses(()=>Call("Transport",root,"X:\\synthetic game\\",request("hang"),exe,350,null,CancellationToken.None),"超时"),"parent stops a timed-out Steam worker");
    using(var cancelled=new CancellationTokenSource()){cancelled.CancelAfter(350);bool cancelledProperly=false;try{Call("Transport",root,"X:\\synthetic game\\",request("hang"),exe,5000,null,cancelled.Token);}catch(OperationCanceledException){cancelledProperly=true;}Check(cancelledProperly,"parent cancellation stops a running worker");}
    Check(Directory.GetDirectories(root).Length==0,"timeout, failure and cancellation remove owned request data");
   }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }
  static int FixtureWorker(string[] args){
   if(args.Length!=4||args[0]!="--steam-service-worker")return -1;
   var json=new JavaScriptSerializer();var request=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[2]));var payload=(Dictionary<string,object>)request["args"];string query=Convert.ToString(payload["query"]);
   if(query.StartsWith("hall-",StringComparison.Ordinal)){
    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true});var row=HallRow();row["requestId"]="worker-must-not-correlate";row["ticket"]="must-not-leave-worker";
    if(query=="hall-bad-shape")row.Remove("port");if(query=="hall-bad-id")row["id"]="hall:203.0.113.9:9999";if(query=="hall-bad-number")row["players"]=7.5;
    if(query=="hall-long-text"){row["name"]=new string('<',64);row["description"]=new string('>',64);}
    object[] rows=query=="hall-too-many"||query=="hall-long-text"?Enumerable.Range(0,query=="hall-too-many"?17:16).Select(i=>(object)row).ToArray():new object[]{row};
    Console.WriteLine(json.Serialize(new{kind="hallBatch",nonce=query=="hall-wrong-nonce"?"wrong":Convert.ToString(request["nonce"]),servers=rows}));
    if(query=="hall-cancel"){Thread.Sleep(30000);return 0;}
    if(query=="hall-first"||query=="hall-error"){string ack=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(args[2])),"hall-ack");var wait=Stopwatch.StartNew();while(!File.Exists(ack)&&wait.ElapsedMilliseconds<1800)Thread.Sleep(10);if(File.Exists(ack))File.Delete(ack);}
    File.WriteAllText(args[3],json.Serialize(new{nonce=Convert.ToString(request["nonce"]),ok=query!="hall-error",data=new{servers=new object[]{HallRow()},message="synthetic final hall result"},message="synthetic hall failure"}),new UTF8Encoding(false));return query=="hall-error"?1:0;
   }
   if(query=="hang"){Thread.Sleep(30000);return 0;}
   Console.WriteLine(json.Serialize(new{kind="progress",value=37,text="synthetic measured progress"}));
   File.WriteAllText(args[3],json.Serialize(new{nonce=query=="wrong-nonce"?"invalid":Convert.ToString(request["nonce"]),ok=query!="error",data=new{items=new object[0],message="synthetic worker result"},message="synthetic failure"}),new UTF8Encoding(false));return query=="error"?1:0;
  }
  public static int Main(string[] args){int fixture=FixtureWorker(args);if(fixture>=0)return fixture;try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
 }
}
