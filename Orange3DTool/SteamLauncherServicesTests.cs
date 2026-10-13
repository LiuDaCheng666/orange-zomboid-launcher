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
  static object Call(string name,params object[] args){try{return Service.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static Dictionary<string,object> Map(object value){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new JavaScriptSerializer().Serialize(value));}
  static bool Refuses(Action call,string fragment){try{call();return false;}catch(Exception x){return x.Message.IndexOf(fragment,StringComparison.OrdinalIgnoreCase)>=0;}}
  static void Put(byte[] bytes,int offset,byte[] value){Buffer.BlockCopy(value,0,bytes,offset,value.Length);}
  static byte[] Ugc(uint creator,uint consumer,ulong id){var bytes=new byte[9776];Put(bytes,0,BitConverter.GetBytes(id));Put(bytes,8,BitConverter.GetBytes(1));Put(bytes,16,BitConverter.GetBytes(creator));Put(bytes,20,BitConverter.GetBytes(consumer));Put(bytes,24,Encoding.UTF8.GetBytes("橙子合成模组"));Put(bytes,153,Encoding.UTF8.GetBytes("Synthetic description <script>text</script>"));Put(bytes,8160,BitConverter.GetBytes(76561197960265728UL));Put(bytes,8172,BitConverter.GetBytes(1700000000U));Put(bytes,9492,BitConverter.GetBytes(123456));Put(bytes,9768,BitConverter.GetBytes(2U));return bytes;}
  static byte[] Server(){var bytes=new byte[376];Put(bytes,0,BitConverter.GetBytes((ushort)16261));Put(bytes,2,BitConverter.GetBytes((ushort)27015));Put(bytes,4,BitConverter.GetBytes(0xCB007109U));Put(bytes,8,BitConverter.GetBytes(38));bytes[12]=1;Put(bytes,78,Encoding.UTF8.GetBytes("Synthetic PZ"));Put(bytes,144,BitConverter.GetBytes(108600U));Put(bytes,148,BitConverter.GetBytes(7));Put(bytes,152,BitConverter.GetBytes(32));bytes[160]=1;Put(bytes,172,Encoding.UTF8.GetBytes("合成服务器 <b>"));Put(bytes,368,BitConverter.GetBytes(90123456789012345UL));return bytes;}
  public static void Run(){RunUnit();TransportTests();}
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

   Console.WriteLine("PASS: Steam services synthetic tests completed without any Steam connection or subscription");
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
   if(query=="hang"){Thread.Sleep(30000);return 0;}
   Console.WriteLine(json.Serialize(new{kind="progress",value=37,text="synthetic measured progress"}));
   File.WriteAllText(args[3],json.Serialize(new{nonce=query=="wrong-nonce"?"invalid":Convert.ToString(request["nonce"]),ok=query!="error",data=new{items=new object[0],message="synthetic worker result"},message="synthetic failure"}),new UTF8Encoding(false));return query=="error"?1:0;
  }
  public static int Main(string[] args){int fixture=FixtureWorker(args);if(fixture>=0)return fixture;try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
 }
}
