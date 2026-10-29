using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace PZ3D {
 public sealed class ServerLaunchTarget {
  public string Id,Host,Username,UserPassword,ServerPassword;
  public int Port;
  public bool UseSteamRelay;
 }
 public sealed class ServerProfiles {
  public sealed class Record {public string id,name,host,username,userCipher,serverCipher,description;public int port,queryPort;public bool useSteamRelay;}
  public sealed class Store {public int version=1;public List<Record> servers=new List<Record>();}
  readonly string data,file,mutexName; readonly object gate=new object();
  static readonly byte[] entropy=Encoding.UTF8.GetBytes("OrangeLauncher.ServerProfiles.v1");
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1048576,RecursionLimit=12};}
  public ServerProfiles(string data){if(string.IsNullOrWhiteSpace(data))throw new Exception("服务器配置目录无效。");this.data=Path.GetFullPath(data);file=Path.Combine(this.data,"server-profiles.json");mutexName="Local\\OrangeServerProfiles-"+Hash(Encoding.UTF8.GetBytes(file.ToUpperInvariant()));}
  static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
  byte[] Bytes(){LauncherMaintenance.NoReparse(file);if(!File.Exists(file))return new byte[0];if(new FileInfo(file).Length>1048576)throw new Exception("服务器配置超过大小限制。");return File.ReadAllBytes(file);}
  static string Id(string id){Guid g;if(id==null||id.Length!=32||!Guid.TryParseExact(id,"N",out g))throw new Exception("服务器标识无效。");return id.ToLowerInvariant();}
  static string Text(Dictionary<string,object> p,string key,int limit,bool optional){object v;if(!p.TryGetValue(key,out v)){if(optional)return "";throw new Exception("缺少服务器字段："+key);}if(!(v is string))throw new Exception("服务器字段类型无效："+key);string s=(string)v;if(s.Length>limit||s.Any(c=>char.IsControl(c)))throw new Exception("服务器字段长度或字符无效："+key);return s;}
  static int Number(Dictionary<string,object> p,string key,bool zero){object v;if(!p.TryGetValue(key,out v)||!(v is int))throw new Exception("端口必须是整数。");int n=(int)v;if(n<(zero?0:1)||n>65535)throw new Exception("端口范围无效。");return n;}
  static string Host(string s){s=s.Trim();IPAddress ip;if(s.Length==0||s.Length>253||s.Contains("%"))throw new Exception("服务器地址无效。");if(IPAddress.TryParse(s,out ip))return ip.ToString();if(s.All(c=>char.IsDigit(c)||c=='.'))throw new Exception("服务器地址无效。");if(s.Split('.').Any(label=>label.Length==0||label.Length>63||!Regex.IsMatch(label,@"^[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?$")))throw new Exception("请输入 IP 或域名。");return s.ToLowerInvariant();}
  Store Read(byte[] bytes){if(bytes.Length==0&&!File.Exists(file))return new Store();try{string text=new UTF8Encoding(false,true).GetString(bytes);var raw=Json().DeserializeObject(text) as Dictionary<string,object>;object version,servers;if(raw==null||!raw.TryGetValue("version",out version)||!(version is int)||(int)version!=1||!raw.TryGetValue("servers",out servers)||!(servers is object[]))throw new Exception();var s=Json().Deserialize<Store>(text);if(s==null||s.version!=1||s.servers==null||s.servers.Count>200)throw new Exception();var ids=new HashSet<string>();foreach(var r in s.servers){if(r==null||!ids.Add(Id(r.id))||r.id!=Id(r.id)||Host(r.host)!=r.host||r.port<1||r.port>65535||r.queryPort<0||r.queryPort>65535||r.name==null||r.name.Length>120||r.username==null||r.username.Length>128||r.description==null||r.description.Length>2000||r.userCipher==null||r.serverCipher==null||r.userCipher.Length>12000||r.serverCipher.Length>12000)throw new Exception();}return s;}catch{throw new Exception("服务器配置损坏或版本不支持；未修改文件。");}}
  static Record Find(Store s,string id){id=Id(id);var r=s.servers.FirstOrDefault(x=>x.id==id);if(r==null)throw new Exception("服务器配置不存在，请刷新。");return r;}
  static string Protect(string s){if(s=="")return "";byte[] plain=Encoding.UTF8.GetBytes(s);try{return Convert.ToBase64String(ProtectedData.Protect(plain,entropy,DataProtectionScope.CurrentUser));}finally{Array.Clear(plain,0,plain.Length);}}
  static string Unprotect(string s){if(s=="")return "";byte[] plain=null;try{plain=ProtectedData.Unprotect(Convert.FromBase64String(s),entropy,DataProtectionScope.CurrentUser);return new UTF8Encoding(false,true).GetString(plain);}catch{throw new Exception("无法解密密码；请使用保存配置的 Windows 用户或重新输入密码。");}finally{if(plain!=null)Array.Clear(plain,0,plain.Length);}}
  static object View(Record r){return new{id=r.id,name=r.name,host=r.host,port=r.port,queryPort=r.queryPort,username=r.username,hasUserPassword=r.userCipher.Length>0,hasServerPassword=r.serverCipher.Length>0,useSteamRelay=r.useSteamRelay,description=r.description};}
  public object List(){lock(gate){var b=Bytes();return new{stamp=Hash(b),servers=Read(b).servers.Select(View).ToArray()};}}
  object Mutate(string stamp,Action<Store> change){using(var mutex=new Mutex(false,mutexName)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new Exception("服务器配置正被其他窗口修改。");lock(gate){byte[] before=Bytes();if(stamp!=Hash(before))throw new Exception("服务器配置已改变，请刷新后重试。");var s=Read(before);change(s);LauncherMaintenance.NoReparse(data);Directory.CreateDirectory(data);string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{byte[] bytes=Encoding.UTF8.GetBytes(Json().Serialize(s));if(bytes.Length>1048576)throw new Exception("服务器配置超过大小限制。");using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}if(!before.SequenceEqual(Bytes()))throw new Exception("服务器配置已改变，请刷新后重试。");LauncherMaintenance.NoReparse(file);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}return List();}}finally{if(held)mutex.ReleaseMutex();}}}
  public object Upsert(Dictionary<string,object> p){if(p==null)throw new Exception("服务器配置无效。");string stamp=Text(p,"stamp",64,false),id=Text(p,"id",32,false),name=Text(p,"name",120,false).Trim(),host=Host(Text(p,"host",253,false)),username=Text(p,"username",128,false),description=Text(p,"description",2000,true);int port=Number(p,"port",false),queryPort=Number(p,"queryPort",true);object relay;if(name.Length==0||!p.TryGetValue("useSteamRelay",out relay)||!(relay is bool))throw new Exception("服务器标题或中继选项无效。");string user=p.ContainsKey("userPassword")?Protect(Text(p,"userPassword",1024,false)):null,server=p.ContainsKey("serverPassword")?Protect(Text(p,"serverPassword",1024,false)):null;
   return Mutate(stamp,s=>{Record r;if(id==""){if(s.servers.Count>=200)throw new Exception("最多保存 200 个服务器。");r=new Record{id=Guid.NewGuid().ToString("N"),userCipher="",serverCipher=""};s.servers.Add(r);}else r=Find(s,id);r.name=name;r.host=host;r.port=port;r.queryPort=queryPort;r.username=username;r.useSteamRelay=(bool)relay;r.description=description;if(user!=null)r.userCipher=user;if(server!=null)r.serverCipher=server;});}
  public object Delete(string id,string stamp){Id(id);return Mutate(stamp,s=>s.servers.Remove(Find(s,id)));}
  internal ServerLaunchTarget PublicTarget(string id){lock(gate){var r=Find(Read(Bytes()),id);return new ServerLaunchTarget{Id=r.id,Host=r.host,Port=r.port};}}
  public ServerLaunchTarget Target(string id){lock(gate){var r=Find(Read(Bytes()),id);return new ServerLaunchTarget{Id=r.id,Host=r.host,Port=r.port,Username=r.username,UserPassword=Unprotect(r.userCipher),ServerPassword=Unprotect(r.serverCipher),UseSteamRelay=r.useSteamRelay};}}
  internal string TargetStamp(string id){lock(gate){byte[] bytes=Bytes();Find(Read(bytes),id);return Hash(bytes);}}
  static object Unknown(string id,string note,string error){return new{id=id,status="unknown",name=(string)null,players=(int?)null,maxPlayers=(int?)null,latencyMs=(int?)null,description=note,version=(string)null,checkedUtc=DateTime.UtcNow.ToString("o"),error=error};}
  static string Clean(string s,int max){return new string(s.Select(c=>char.IsControl(c)?' ':c).Take(max).ToArray());}
  sealed class Packet {readonly byte[] b;int i;public Packet(byte[] b){this.b=b;i=5;}public byte Byte(){if(i>=b.Length)throw new Exception();return b[i++];}public void Skip(int count){if(i+count>b.Length)throw new Exception();i+=count;}public string Str(){int start=i;while(i<b.Length&&b[i]!=0&&i-start<=2048)i++;if(i>=b.Length||i-start>2048)throw new Exception();string v=new UTF8Encoding(false,true).GetString(b,start,i-start);i++;return v;}public bool More{get{return i<b.Length;}}}
  internal static object ParseInfo(byte[] bytes,string id,int latency,string note){try{if(bytes==null||bytes.Length<6||bytes.Length>65507||bytes[0]!=255||bytes[1]!=255||bytes[2]!=255||bytes[3]!=255||bytes[4]!=73)throw new Exception();var p=new Packet(bytes);p.Byte();string name=Clean(p.Str(),256);p.Str();p.Str();p.Str();p.Skip(2);int players=p.Byte(),max=p.Byte();p.Byte();p.Byte();p.Byte();p.Byte();p.Byte();string version=Clean(p.Str(),128);if(players>max)throw new Exception();if(p.More){byte flags=p.Byte();if((flags&128)!=0)p.Skip(2);if((flags&16)!=0)p.Skip(8);if((flags&64)!=0){p.Skip(2);p.Str();}if((flags&32)!=0)p.Str();if((flags&1)!=0)p.Skip(8);if((flags&14)!=0||p.More)throw new Exception();}return new{id=id,status="online",name=name,players=(int?)players,maxPlayers=(int?)max,latencyMs=(int?)latency,description=note,version=version,checkedUtc=DateTime.UtcNow.ToString("o"),error=(string)null};}catch{return Unknown(id,note,"查询响应无效或不支持该数据包格式。");}}
  internal static void ValidateQueryPayload(Dictionary<string,object> p){if(p==null)throw new ArgumentException("缺少服务器查询目标。");var fields=new HashSet<string>(p.ContainsKey("host")?new[]{"id","host","port","queryPort"}:new[]{"id"});if(p.ContainsKey("requestId"))fields.Add("requestId");if(p.Count!=fields.Count||p.Keys.Any(key=>!fields.Contains(key)))throw new ArgumentException("服务器查询只接受公开地址与端口。");string id=Text(p,"id",160,false);if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("服务器查询标识无效。");if(p.ContainsKey("host")){Host(Text(p,"host",253,false));Number(p,"port",false);Number(p,"queryPort",true);}else Id(id);}
  internal object QueryRequest(Dictionary<string,object> p,CancellationToken token){ValidateQueryPayload(p);if(!p.ContainsKey("host"))return Query((string)p["id"],token);return ServerPublicQuery.Query((string)p["id"],Host((string)p["host"]),(int)p["port"],(int)p["queryPort"],"","note",token);}
  public object Query(string id,CancellationToken token){Record r;lock(gate)r=Find(Read(Bytes()),id);try{return ServerPublicQuery.Query(r.id,r.host,r.port,r.queryPort,r.description,"note",token);}catch(OperationCanceledException){return Unknown(r.id,r.description,"查询已取消。");}}

 }
}
