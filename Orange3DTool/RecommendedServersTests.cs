using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class RecommendedServersTests {
  static int checks;
  static readonly JavaScriptSerializer json=new JavaScriptSerializer();
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);checks++;}
  static Dictionary<string,object> D(object value){return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}
  static object[] Rows(object value){return ((IEnumerable)D(value)["servers"]).Cast<object>().ToArray();}
  static void Cancelled(Action action,string label){try{action();throw new Exception("FAIL "+label);}catch(OperationCanceledException){checks++;}}
  static string Feed(params object[] rows){return json.Serialize(new{schema=1,revision=7,updatedUtc="2026-10-26T10:00:00Z",servers=rows});}
  static object Row(string id="fixture",object port=null,string host="127.0.0.1",int queryPort=16262){return new{id=id,name="<b>橙子合作服</b>",host=host,port=port??(object)16261,queryPort=queryPort,version="42.13",description="合作\n欢迎新手",cover="",tags=new[]{"合作"},order=1,useSteamRelay=false};}
  static byte[] Info(){var bytes=new List<byte>(new byte[]{255,255,255,255,73,17});foreach(string text in new[]{"Loopback Cooperation","map","folder","Project Zomboid"})bytes.AddRange(Encoding.UTF8.GetBytes(text+"\0"));bytes.AddRange(new byte[]{56,166,3,24,0,100,119,0,1});bytes.AddRange(Encoding.UTF8.GetBytes("42.0\0"));return bytes.ToArray();}
  public static int Main(){string root=Path.Combine(Path.GetTempPath(),"ozl-recommended-tests-"+Guid.NewGuid().ToString("N"));try{
   Type type=typeof(RecommendedServersTests).Assembly.GetType("PZ3D.RecommendedServers");Check(type!=null,"native recommendation reader exists");
   var ctor=type.GetConstructor(new[]{typeof(string),typeof(Func<Uri,CancellationToken,string>)});Check(ctor!=null,"reader supports isolated transport injection");var method=type.GetMethod("Read");Check(method!=null,"reader exposes Read");
   int calls=0;string payload=Feed(Row());bool fail=false;Uri requested=null;
   Func<Uri,CancellationToken,string> read=(uri,token)=>{calls++;requested=uri;token.ThrowIfCancellationRequested();if(fail)throw new IOException("fixture unavailable");return payload;};
   object client=ctor.Invoke(new object[]{root,read});Func<bool,CancellationToken,object> invoke=(refresh,token)=>{try{return method.Invoke(client,new object[]{refresh,token});}catch(TargetInvocationException error){throw error.InnerException;}};
   object first=invoke(true,CancellationToken.None);Check(Rows(first).Length==1,"fresh document returns published records");Check((string)((Dictionary<string,object>)Rows(first)[0])["name"]=="<b>橙子合作服</b>","plain text is not interpreted as markup");
#if OZL_LOCAL_PREVIEW
   Check(requested.AbsoluteUri=="http://127.0.0.1:18083/api/recommended-servers","preview stays on fixed loopback endpoint");
#else
   Check(requested.AbsoluteUri=="https://orangezl.com/api/recommended-servers","production uses fixed HTTPS endpoint");
#endif
   invoke(false,CancellationToken.None);Check(calls==1,"ordinary read reuses fresh memory snapshot");
   var gallery=D(Row("gallery"));gallery["extendedDescription"]="管理员长介绍\n<b>按普通文字显示</b>";gallery["screenshots"]=new[]{"https://orangezl.com/example-screen.png","https://images.steamusercontent.com/example-screen.jpg"};payload=Feed(gallery);var enriched=D(Rows(invoke(true,CancellationToken.None))[0]);Check((string)enriched["extendedDescription"]==(string)gallery["extendedDescription"]&&((IEnumerable)enriched["screenshots"]).Cast<object>().Count()==2,"optional administrator text and multiple screenshots survive native parsing");
   payload=Feed(Row("old-feed"));var oldFeed=D(Rows(invoke(true,CancellationToken.None))[0]);Check((string)oldFeed["extendedDescription"]==""&&((IEnumerable)oldFeed["screenshots"]).Cast<object>().Count()==0,"legacy feed without optional gallery fields remains compatible");
   gallery["screenshots"]=new[]{"javascript:alert(1)"};payload=Feed(gallery,Row("safe"));Check(Rows(invoke(true,CancellationToken.None)).Length==1,"unsafe optional screenshot record is rejected independently");
   payload=Feed(Row("good"),Row("bad",0),Row("bad-host",16261,"https://evil.invalid/"),Row("typed-port","16261"));object partial=invoke(true,CancellationToken.None);Check(Rows(partial).Length==1&&(string)((Dictionary<string,object>)Rows(partial)[0])["id"]=="good","bad records skipped independently");
   fail=true;object fallback=invoke(true,CancellationToken.None);Check(Rows(fallback).Length==1&&(bool)D(fallback)["cached"],"network failure retains last valid records");Check(!string.IsNullOrEmpty((string)D(fallback)["warning"]),"cached result explains refresh failure");
   client=ctor.Invoke(new object[]{root,read});object diskFallback=invoke(true,CancellationToken.None);Check(Rows(diskFallback).Length==1&&(bool)D(diskFallback)["cached"],"new instance reads persisted cache offline");
   fail=false;payload="{\"schema\":1,\"servers\":[]}";Check(Rows(invoke(true,CancellationToken.None)).Length==1,"malformed envelope cannot erase cache");
   payload=Feed(Row("bad",0));Check(Rows(invoke(true,CancellationToken.None)).Length==1,"all-invalid nonempty document cannot erase cache");
   payload=Feed();Check(Rows(invoke(true,CancellationToken.None)).Length==0,"valid empty published list clears cache");
   Cancelled(()=>invoke(true,new CancellationToken(true)),"cancelled request never returns stale success");
   string empty=Path.Combine(root,"new");fail=true;client=ctor.Invoke(new object[]{empty,read});object unavailable=invoke(true,CancellationToken.None);Check(Rows(unavailable).Length==0&&!string.IsNullOrEmpty((string)D(unavailable)["warning"]),"first outage returns usable empty list with message");
   Directory.CreateDirectory(empty);File.WriteAllText(Path.Combine(empty,"recommended-servers.json"),"{broken");client=ctor.Invoke(new object[]{empty,read});Check(Rows(invoke(true,CancellationToken.None)).Length==0,"damaged cache never throws through recommendation UI");
   MethodInfo query=type.GetMethod("Query",new[]{typeof(string),typeof(CancellationToken)});Check(query!=null,"recommended records expose controlled live query");
   Func<string,CancellationToken,object> queryCall=(id,token)=>{try{return query.Invoke(client,new object[]{id,token});}catch(TargetInvocationException error){throw error.InnerException;}};
   using(var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){udp.Client.ReceiveTimeout=4000;int port=((IPEndPoint)udp.Client.LocalEndPoint).Port;payload=Feed(Row("query",null,"127.0.0.1",port));fail=false;invoke(true,CancellationToken.None);Exception serverError=null;
    var thread=new Thread(()=>{try{IPEndPoint peer=null;byte[] request=udp.Receive(ref peer);Check(request.SequenceEqual(new byte[]{255,255,255,255,84,83,111,117,114,99,101,32,69,110,103,105,110,101,32,81,117,101,114,121,0}),"recommended query sends A2S public request");byte[] challenge={255,255,255,255,65,1,2,3,4};udp.Send(challenge,challenge.Length,peer);byte[] retry=udp.Receive(ref peer);Check(retry.Skip(retry.Length-4).SequenceEqual(new byte[]{1,2,3,4}),"recommended query echoes protocol challenge");byte[] info=Info();udp.Send(info,info.Length,peer);byte[] rulesRequest=udp.Receive(ref peer);Check(rulesRequest.Take(5).SequenceEqual(new byte[]{255,255,255,255,86}),"recommended query requests public description rules");var rules=new List<byte>(new byte[]{255,255,255,255,69,1,0});rules.AddRange(Encoding.UTF8.GetBytes("PublicDescription\0公开规则介绍\0"));udp.Send(rules.ToArray(),rules.Count,peer);}catch(Exception error){serverError=error;}});thread.Start();
    var live=D(queryCall("query",CancellationToken.None));thread.Join();if(serverError!=null)throw serverError;Check((string)live["status"]=="online"&&Convert.ToInt32(live["players"])==3&&Convert.ToInt32(live["maxPlayers"])==24,"real loopback live player count uses common parser");Check((string)live["name"]=="Loopback Cooperation"&&(string)live["description"]=="公开规则介绍"&&(string)live["descriptionSource"]=="server","recommendations expose actual queried name and public description");
    var watch=System.Diagnostics.Stopwatch.StartNew();Check((string)D(queryCall("query",CancellationToken.None))["status"]=="online"&&watch.ElapsedMilliseconds<400,"repeat query reuses ten-second result cache");
    bool rejected=false;try{queryCall("unknown",CancellationToken.None);}catch(InvalidOperationException){rejected=true;}Check(rejected,"query refuses target outside validated recommendation snapshot");
    Cancelled(()=>queryCall("query",new CancellationToken(true)),"cancelled live query stops before socket activity");
   }
   using(var delayed=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){delayed.Client.ReceiveTimeout=4000;payload=Feed(Row("delayed",null,"127.0.0.1",((IPEndPoint)delayed.Client.LocalEndPoint).Port));invoke(true,CancellationToken.None);var responder=new Thread(()=>{IPEndPoint peer=null;delayed.Receive(ref peer);Thread.Sleep(1800);byte[] info=Info();delayed.Send(info,info.Length,peer);delayed.Receive(ref peer);});responder.Start();var watch=System.Diagnostics.Stopwatch.StartNew();var live=D(queryCall("delayed",CancellationToken.None));responder.Join();Check((string)live["status"]=="online"&&(string)live["name"]=="Loopback Cooperation"&&(string)live["descriptionSource"]=="admin","rules child deadline retains valid name and players with administrator description");Check(watch.ElapsedMilliseconds<2900,"delayed INFO and silent rules share one 2.5 second budget");}
   using(var cancelled=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))using(var cancellation=new CancellationTokenSource()){cancelled.Client.ReceiveTimeout=4000;payload=Feed(Row("cancel-rules",null,"127.0.0.1",((IPEndPoint)cancelled.Client.LocalEndPoint).Port));invoke(true,CancellationToken.None);var responder=new Thread(()=>{IPEndPoint peer=null;cancelled.Receive(ref peer);byte[] info=Info();cancelled.Send(info,info.Length,peer);cancelled.Receive(ref peer);cancellation.Cancel();});responder.Start();Cancelled(()=>queryCall("cancel-rules",cancellation.Token),"parent cancellation during rules propagates instead of returning valid INFO");responder.Join();}
   using(var silent=new UdpClient(new IPEndPoint(IPAddress.Loopback,0))){payload=Feed(Row("query",null,"127.0.0.1",((IPEndPoint)silent.Client.LocalEndPoint).Port));invoke(true,CancellationToken.None);var watch=System.Diagnostics.Stopwatch.StartNew();var unknown=D(queryCall("query",CancellationToken.None));Check((string)unknown["status"]=="unknown"&&unknown["players"]==null,"changed published query address invalidates stale live count");Check(watch.ElapsedMilliseconds<3500,"silent live server respects bounded deadline");}
   Console.WriteLine("PASS RecommendedServersTests "+checks+" checks");return 0;
  }catch(Exception error){Console.WriteLine(error.Message);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
 }
}
