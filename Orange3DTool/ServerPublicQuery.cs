using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;using System.Net;using System.Net.Sockets;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;
namespace PZ3D {
 // All server cards share public endpoint work; caller identities, private notes,
 // credentials, and cancellation remain separate from that shared socket.
 static class ServerPublicQuery {
  sealed class Work {internal readonly ManualResetEventSlim Done=new ManualResetEventSlim();internal readonly CancellationTokenSource Cancel=new CancellationTokenSource();internal int Readers;internal bool Retiring;internal readonly DateTime Started=DateTime.UtcNow;internal DateTime Until;internal object Result;}
  static readonly object gate=new object();static readonly Dictionary<string,Work> work=new Dictionary<string,Work>(StringComparer.Ordinal);static readonly Dictionary<string,Task<IPAddress[]>> dns=new Dictionary<string,Task<IPAddress[]>>(StringComparer.Ordinal);static readonly SemaphoreSlim slots=new SemaphoreSlim(4,4);
  static readonly byte[] request={255,255,255,255,84,83,111,117,114,99,101,32,69,110,103,105,110,101,32,81,117,101,114,121,0};
  static Dictionary<string,object> Copy(object value){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}
  static object Unknown(string status,string error){return new{id="",status=status,name=(string)null,players=(int?)null,maxPlayers=(int?)null,latencyMs=(int?)null,description=(string)null,version=(string)null,checkedUtc=DateTime.UtcNow.ToString("o"),error=error};}
  static object Project(object raw,string id,string note,string fallbackSource){var result=Copy(raw);result["id"]=id;if(result["description"]==null){result["description"]=note;if(result.ContainsKey("descriptionSource"))result["descriptionSource"]=fallbackSource;}return result;}
  internal static object Query(string id,string host,int port,int queryPort,string note,string fallbackSource,CancellationToken token){
   token.ThrowIfCancellationRequested();int target=queryPort==0?port:queryPort;string key=host+"\n"+target;Work selected;bool start;
   while(true){start=false;bool retiring=false;lock(gate){foreach(string expired in work.Where(pair=>pair.Value.Done.IsSet&&pair.Value.Until<=DateTime.UtcNow).Select(pair=>pair.Key).ToArray())work.Remove(expired);if(!work.TryGetValue(key,out selected)){selected=new Work{Readers=1};work[key]=selected;start=true;}else if(!selected.Done.IsSet&&(selected.Retiring||selected.Cancel.IsCancellationRequested))retiring=true;else selected.Readers++;}
    if(!retiring)break;selected.Done.Wait(token);
   }
   if(start){var current=selected;Task.Factory.StartNew(()=>Run(current,host,target),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);}
   try{selected.Done.Wait(token);token.ThrowIfCancellationRequested();return Project(selected.Result,id,note,fallbackSource);}
   finally{bool cancel;lock(gate){selected.Readers--;cancel=selected.Readers==0&&!selected.Done.IsSet;if(cancel)selected.Retiring=true;}if(cancel)selected.Cancel.Cancel();}
  }
  static IPAddress Resolve(string host,CancellationToken token){IPAddress address;if(IPAddress.TryParse(host,out address))return address;Task<IPAddress[]> lookup;lock(gate){if(!dns.TryGetValue(host,out lookup)){lookup=Task.Factory.StartNew(()=>Dns.GetHostAddresses(host));dns[host]=lookup;}}try{while(!lookup.IsCompleted){token.ThrowIfCancellationRequested();Thread.Sleep(25);}token.ThrowIfCancellationRequested();address=lookup.Result.FirstOrDefault(item=>item.AddressFamily==AddressFamily.InterNetwork||item.AddressFamily==AddressFamily.InterNetworkV6);if(address==null)throw new InvalidOperationException("无法解析服务器地址。");return address;}finally{if(lookup.IsCompleted)lock(gate){Task<IPAddress[]> current;if(dns.TryGetValue(host,out current)&&current==lookup)dns.Remove(host);}}}
  static void Run(Work current,string host,int port){object result=null;bool acquired=false,cache=true;try{
   if(!slots.Wait(0,current.Cancel.Token)){cache=false;result=Unknown("querying","查询通道正在使用，请等待下一次刷新。");return;}acquired=true;var watch=Stopwatch.StartNew();using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(current.Cancel.Token)){deadline.CancelAfter(2500);IPAddress address=Resolve(host,deadline.Token);using(var udp=new UdpClient(address.AddressFamily))using(deadline.Token.Register(()=>udp.Close())){
    udp.Connect(address,port);udp.Send(request,request.Length);bool challenged=false;
    while(true){deadline.Token.ThrowIfCancellationRequested();if(!udp.Client.Poll(50000,SelectMode.SelectRead))continue;IPEndPoint peer=null;byte[] packet=udp.Receive(ref peer);if(packet.Length==9&&packet.Take(5).SequenceEqual(new byte[]{255,255,255,255,65})&&!challenged){challenged=true;var retry=request.Concat(packet.Skip(5)).ToArray();udp.Send(retry,retry.Length);continue;}
     var info=Copy(ServerProfiles.ParseInfo(packet,"",(int)watch.ElapsedMilliseconds,""));if(Convert.ToString(info["status"])!="online"){info["status"]="partial";info["description"]=null;result=info;break;}
     string warning="",description=null;try{description=ServerPublicRules.Query(udp,deadline.Token,out warning);}catch(OperationCanceledException){current.Cancel.Token.ThrowIfCancellationRequested();warning="人数已读取，公开介绍查询超时。";}catch(ObjectDisposedException){current.Cancel.Token.ThrowIfCancellationRequested();warning="人数已读取，公开介绍查询超时。";}
     info["description"]=description;info["descriptionSource"]=description==null?null:"server";info["descriptionWarning"]=warning;result=info;break;
    }
   }}
  }catch(OperationCanceledException){cache=!current.Cancel.IsCancellationRequested;result=Unknown("unknown",cache?"公开查询无响应或失败；服务器状态未知。":"查询已取消。");}catch{cache=!current.Cancel.IsCancellationRequested;result=Unknown("unknown",cache?"公开查询无响应或失败；服务器状态未知。":"查询已取消。");}
   finally{if(acquired)slots.Release();lock(gate){current.Result=result??Unknown("unknown","公开查询未返回完整结果。");current.Until=cache?current.Started.AddSeconds(4):DateTime.MinValue;current.Done.Set();}}
  }
 }
}
