using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace PZ3D {
 public sealed class RecommendedServer {
  public string id,name,host,version,description,cover;
  public string extendedDescription="",descriptionHtml="";
  public string[] screenshots=new string[0];
  public int port,queryPort,order;
  public string[] tags;
  public bool useSteamRelay;
 }
 public sealed class RecommendedServerSnapshot {
  public int schema=1;
  public long revision;
  public string updatedUtc="",warning="";
  public List<RecommendedServer> servers=new List<RecommendedServer>();
  public bool cached;
 }
 public sealed class RecommendedServers {
  const int MaxBytes=1048576;
#if OZL_LOCAL_PREVIEW
  static readonly Uri endpoint=new Uri("http://127.0.0.1:18083/api/recommended-servers");
#else
  static readonly Uri endpoint=new Uri("https://orangezl.com/api/recommended-servers");
#endif
  readonly string directory,file;
  readonly Func<Uri,CancellationToken,string> reader;
  readonly object gate=new object();
  RecommendedServerSnapshot last;
  DateTime freshUntil;
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=MaxBytes,RecursionLimit=16};}
  public RecommendedServers(string data,Func<Uri,CancellationToken,string> reader=null){if(string.IsNullOrWhiteSpace(data))throw new ArgumentException("推荐列表缓存目录无效。");directory=Path.GetFullPath(data);file=Path.Combine(directory,"recommended-servers.json");this.reader=reader??Download;}
  static string Download(Uri uri,CancellationToken token){
   var request=(HttpWebRequest)WebRequest.Create(uri);request.Method="GET";request.AllowAutoRedirect=false;request.Timeout=5000;request.ReadWriteTimeout=5000;request.Accept="application/json";request.UserAgent="OrangeLauncher/RecommendedServers";request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;request.CookieContainer=null;
   using(token.Register(request.Abort))try{using(var response=(HttpWebResponse)request.GetResponse()){
    if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>MaxBytes)throw new IOException("推荐响应无效。");
    using(var input=response.GetResponseStream())using(var output=new MemoryStream()){byte[] buffer=new byte[8192];int count;while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();if(output.Length+count>MaxBytes)throw new IOException("推荐响应超过大小限制。");output.Write(buffer,0,count);}token.ThrowIfCancellationRequested();return new UTF8Encoding(false,true).GetString(output.ToArray());}
   }}catch(WebException){token.ThrowIfCancellationRequested();throw;}
  }
  static object Required(Dictionary<string,object> value,string key){object item;if(value==null||!value.TryGetValue(key,out item))throw new FormatException();return item;}
  static string Text(Dictionary<string,object> value,string key,int maximum,bool required=false,bool multiline=false){object item=Required(value,key);if(!(item is string))throw new FormatException();string text=(string)item;if(text.Length>maximum||(required&&string.IsNullOrWhiteSpace(text))||text.Any(c=>char.IsControl(c)&&!(multiline&&(c=='\n'||c=='\r'||c=='\t'))))throw new FormatException();return text;}
  static int Number(Dictionary<string,object> value,string key,int minimum,int maximum){object item=Required(value,key);if(!(item is int)||(int)item<minimum||(int)item>maximum)throw new FormatException();return (int)item;}
  static bool Boolean(Dictionary<string,object> value,string key){object item=Required(value,key);if(!(item is bool))throw new FormatException();return (bool)item;}
  static string Host(string host){IPAddress ip;if(host.Length==0||host.Length>253||host.Contains("%"))throw new FormatException();if(IPAddress.TryParse(host,out ip))return ip.ToString();if(host.All(c=>char.IsDigit(c)||c=='.')||host.Split('.').Any(part=>part.Length==0||part.Length>63||!Regex.IsMatch(part,@"^[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?$")))throw new FormatException();return host.ToLowerInvariant();}
  static RecommendedServer Record(Dictionary<string,object> value){
   string id=Text(value,"id",128,true);if(!Regex.IsMatch(id,@"^[a-zA-Z0-9_-]+$"))throw new FormatException();
   string cover=Text(value,"cover",2048);Uri image;if(cover.Length>0&&(!Uri.TryCreate(cover,UriKind.Absolute,out image)||image.Scheme!="https"||image.Host.Length==0||image.UserInfo.Length>0))throw new FormatException();
   object[] rawTags=Required(value,"tags") as object[];if(rawTags==null||rawTags.Length>12||rawTags.Any(tag=>!(tag is string)||((string)tag).Length>32||string.IsNullOrWhiteSpace((string)tag)||((string)tag).Any(char.IsControl)))throw new FormatException();
   string extended=value.ContainsKey("extendedDescription")?Text(value,"extendedDescription",8000,false,true):"";object[] screenshots=value.ContainsKey("screenshots")?Required(value,"screenshots") as object[]:new object[0];if(screenshots==null||screenshots.Length>8||screenshots.Any(item=>!(item is string)||((string)item).Length>2048||!ModernHostPolicy.IsWorkshopImage((string)item)))throw new FormatException();
   string html=value.ContainsKey("descriptionHtml")?Text(value,"descriptionHtml",32768,false,true):"";
   return new RecommendedServer{id=id,name=Text(value,"name",120,true),host=Host(Text(value,"host",253,true)),port=Number(value,"port",1,65535),queryPort=Number(value,"queryPort",0,65535),version=Text(value,"version",80),description=Text(value,"description",2000,false,true),cover=cover,extendedDescription=extended,descriptionHtml=html,screenshots=screenshots.Cast<string>().Distinct().ToArray(),tags=rawTags.Cast<string>().Distinct().ToArray(),order=Number(value,"order",0,1000000),useSteamRelay=Boolean(value,"useSteamRelay")};
  }
  static RecommendedServerSnapshot Parse(string content){
   if(content==null||Encoding.UTF8.GetByteCount(content)>MaxBytes)throw new FormatException();
   var value=Json().DeserializeObject(content) as Dictionary<string,object>;if(Number(value,"schema",1,1)!=1)throw new FormatException();
   object revision=Required(value,"revision");if(!(revision is int)&&!(revision is long))throw new FormatException();long rev=Convert.ToInt64(revision);if(rev<0)throw new FormatException();
   string updated=Text(value,"updatedUtc",80,true);DateTimeOffset stamp;if(!DateTimeOffset.TryParse(updated,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp)||!updated.EndsWith("Z",StringComparison.Ordinal)&&!updated.EndsWith("+00:00",StringComparison.Ordinal))throw new FormatException();
   var rows=Required(value,"servers") as object[];if(rows==null||rows.Length>200)throw new FormatException();
   var result=new RecommendedServerSnapshot{revision=rev,updatedUtc=updated};var ids=new HashSet<string>(StringComparer.Ordinal);int invalid=0;
   foreach(object raw in rows)try{var row=Record(raw as Dictionary<string,object>);if(!ids.Add(row.id))throw new FormatException();result.servers.Add(row);}catch(FormatException){invalid++;}
   if(rows.Length>0&&result.servers.Count==0)throw new FormatException();
   result.servers=result.servers.OrderBy(row=>row.order).ThenBy(row=>row.id,StringComparer.Ordinal).ToList();
   if(invalid>0)result.warning="部分推荐服务器信息无效，已跳过。";
   return result;
  }
  static void NoReparse(string path){for(string cursor=Path.GetFullPath(path);!string.IsNullOrEmpty(cursor);cursor=Path.GetDirectoryName(cursor)){if((File.Exists(cursor)||Directory.Exists(cursor))&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new IOException("缓存目录包含重解析点。");}}
  void LoadCache(){if(last!=null)return;try{NoReparse(file);if(File.Exists(file)&&new FileInfo(file).Length<=MaxBytes){last=Parse(File.ReadAllText(file,new UTF8Encoding(false,true)));last.cached=true;}}catch{}}
  void SaveCache(RecommendedServerSnapshot snapshot){string temp=null;try{NoReparse(directory);Directory.CreateDirectory(directory);NoReparse(file);temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";byte[] bytes=Encoding.UTF8.GetBytes(Json().Serialize(snapshot));using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}NoReparse(file);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}catch{}finally{if(temp!=null)try{if(File.Exists(temp))File.Delete(temp);}catch{}}}
  static RecommendedServerSnapshot Copy(RecommendedServerSnapshot snapshot){return Json().Deserialize<RecommendedServerSnapshot>(Json().Serialize(snapshot));}
  public RecommendedServerSnapshot Read(bool refresh,CancellationToken token){
   token.ThrowIfCancellationRequested();lock(gate){token.ThrowIfCancellationRequested();LoadCache();if(!refresh&&last!=null&&DateTime.UtcNow<freshUntil)return Copy(last);
    try{var result=Parse(reader(endpoint,token));token.ThrowIfCancellationRequested();last=result;freshUntil=DateTime.UtcNow.AddMinutes(5);SaveCache(last);return Copy(last);}
    catch(OperationCanceledException){throw;}
    catch{token.ThrowIfCancellationRequested();var result=last==null?new RecommendedServerSnapshot():Copy(last);result.cached=last!=null;result.warning=last==null?"暂时无法获取推荐服务器，请稍后刷新。":"暂时无法刷新推荐服务器，正在显示上次保存的列表。";return result;}
   }
  }
  static object Unknown(string id,string description,string error){return new{id=id,status="unknown",name=(string)null,players=(int?)null,maxPlayers=(int?)null,latencyMs=(int?)null,description=description,version=(string)null,checkedUtc=DateTime.UtcNow.ToString("o"),error=error};}
  static object CopyObject(object value){return Json().DeserializeObject(Json().Serialize(value));}
  public object Query(string id,CancellationToken token){
   token.ThrowIfCancellationRequested();RecommendedServer row;string stamp;
   lock(gate){LoadCache();row=last==null?null:last.servers.FirstOrDefault(item=>item.id==id);if(row==null)throw new InvalidOperationException("推荐列表已改变，请刷新后重试。");stamp=Json().Serialize(row);}
   object result=ServerPublicQuery.Query(row.id,row.host,row.port,row.queryPort,row.description,"admin",token);
   lock(gate){var current=last==null?null:last.servers.FirstOrDefault(item=>item.id==id);if(current==null||Json().Serialize(current)!=stamp)throw new InvalidOperationException("推荐列表已改变，请刷新后重试。");}
   return CopyObject(result);
  }
 }
}
