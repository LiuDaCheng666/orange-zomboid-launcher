using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PZ3D {
 // Only the short-lived worker loads Steam. The launcher itself holds no app identity.
 static class SteamLauncherServices {
  internal const uint App=108600;
  internal const int UgcBytes=9776,ServerBytes=376;
  const int MaxResponseBytes=16*1024*1024;
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=MaxResponseBytes,RecursionLimit=32};}
  static object Field(Dictionary<string,object> values,string name){object value;return values!=null&&values.TryGetValue(name,out value)?value:null;}
  static string Text(object value){return Convert.ToString(value,CultureInfo.InvariantCulture)??"";}
  static ulong ItemId(object value){ulong id;string text=Text(value);if(text.Length==0||text.Length>20||text.Any(c=>c<'0'||c>'9')||!ulong.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out id)||id==0)throw new Exception("工坊条目 ID 必须为有效正整数。");return id;}
  internal static Dictionary<string,object> Normalize(string action,Dictionary<string,object> payload){
   var clean=new Dictionary<string,object>();
   if(action=="connection"||action=="subscriptions")return clean;
   if(action=="search"||action=="hall"){
    string query=Text(Field(payload,"query")).Trim();if(query.Length>200||query.Any(c=>char.IsControl(c)))throw new Exception("搜索文本最多 200 字符且不能包含控制字符。");clean["query"]=query;
    if(action=="search"){object raw=Field(payload,"page");int page=1;if(raw!=null&&(!int.TryParse(Text(raw),NumberStyles.None,CultureInfo.InvariantCulture,out page)||page<1||page>1000))throw new Exception("工坊页码必须为 1 至 1000 的整数。");clean["page"]=page;}return clean;
   }
   if(action=="details"||action=="download"){
    clean["id"]=ItemId(Field(payload,"id")).ToString(CultureInfo.InvariantCulture);
    if(action=="download"){object confirm=Field(payload,"confirmed");if(!(confirm is bool)||!(bool)confirm)throw new Exception("请明确确认订阅并下载所选工坊条目。");clean["confirmed"]=true;}return clean;
   }
   throw new Exception("不支持的 Steam 操作。");
  }
  internal static string Utf8(byte[] bytes,int offset,int length){int end=offset;while(end<offset+length&&bytes[end]!=0)end++;return Encoding.UTF8.GetString(bytes,offset,end-offset);}
  static void RequireUgc(byte[] bytes,ulong expected){
   if(bytes==null||bytes.Length!=UgcBytes)throw new Exception("Steam 工坊 ABI 数据长度不完整。");
   ulong id=BitConverter.ToUInt64(bytes,0);if(id==0||(expected!=0&&id!=expected))throw new Exception("Steam 工坊详情 ID 与请求不一致。");
   int result=BitConverter.ToInt32(bytes,8);if(result!=1)throw new Exception("Steam 工坊详情查询失败，EResult="+result+"。");
   if(BitConverter.ToUInt32(bytes,16)!=App||BitConverter.ToUInt32(bytes,20)!=App)throw new Exception("工坊创作者及使用者应用必须同时为 App108600。");
  }
  internal static object ParseUgc(byte[] bytes,uint state,string preview,ulong[] children){
   RequireUgc(bytes,0);Uri uri;if(!Uri.TryCreate(preview,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||!string.IsNullOrEmpty(uri.UserInfo))preview="";
   int size=BitConverter.ToInt32(bytes,9492);return new{
    id=BitConverter.ToUInt64(bytes,0).ToString(CultureInfo.InvariantCulture),title=Utf8(bytes,24,129),description=Utf8(bytes,153,8000),author=BitConverter.ToUInt64(bytes,8160).ToString(CultureInfo.InvariantCulture),previewUrl=preview??"",
    updated=BitConverter.ToUInt32(bytes,8172),subscribed=(state&1)!=0,downloaded=(state&4)!=0,needsUpdate=(state&8)!=0,sizeBytes=Math.Max(0,size),dependencies=(children??new ulong[0]).Where(id=>id!=0).Distinct().Select(id=>id.ToString(CultureInfo.InvariantCulture)).ToArray()
   };
  }
  internal static object ParseSubscribedUgc(byte[] bytes,uint state,string preview,ulong[] children){if(bytes==null||bytes.Length!=UgcBytes)throw new Exception("订阅元数据长度异常。");ulong id=BitConverter.ToUInt64(bytes,0);if(id==0)throw new Exception("订阅元数据缺少条目ID。");int result=BitConverter.ToInt32(bytes,8);if(result==1){var row=Json().Deserialize<Dictionary<string,object>>(Json().Serialize(ParseUgc(bytes,state,preview,children)));string description=Convert.ToString(row["description"]);row["description"]=description.Length>500?description.Substring(0,500):description;return row;}return new{id=id.ToString(),title="工坊条目不可访问 · "+id,description="已订阅ID；Steam详情不可访问（私有、删除或权限限制），EResult="+result+"。不代表可以下载或启用。",author="",previewUrl="",updated=(uint?)null,subscribed=(state&1)!=0,downloaded=(state&4)!=0,needsUpdate=(state&8)!=0,sizeBytes=0,dependencies=new string[0],unavailable=true};}
  internal static void DownloadVerified(ulong id,bool confirmed,Func<ulong,byte[]> details,Action<ulong> mutate){
   if(!confirmed)throw new Exception("请明确确认订阅并下载所选工坊条目。");if(id==0)throw new Exception("无效工坊 ID。");
   byte[] bytes=details(id);RequireUgc(bytes,id);if(bytes[8184]!=0||BitConverter.ToInt32(bytes,12)!=0)throw new Exception("所选条目已被封禁或不是可下载的普通工坊模组。");mutate(id);
  }
  internal static object ParseServer(byte[] bytes){
   if(bytes==null||bytes.Length!=ServerBytes)throw new Exception("Steam 服务器 ABI 数据长度不完整。");
   uint app=BitConverter.ToUInt32(bytes,144),ip=BitConverter.ToUInt32(bytes,4);ushort port=BitConverter.ToUInt16(bytes,0),queryPort=BitConverter.ToUInt16(bytes,2);
   if(app!=App||bytes[12]==0||ip==0||port==0||queryPort==0)return null;
   int players=BitConverter.ToInt32(bytes,148),max=BitConverter.ToInt32(bytes,152),ping=BitConverter.ToInt32(bytes,8);if(players<0||max<0||players>100000||max>100000)return null;
   string host=((ip>>24)&255)+"."+((ip>>16)&255)+"."+((ip>>8)&255)+"."+(ip&255),name=Utf8(bytes,172,64);ulong steamId=BitConverter.ToUInt64(bytes,368);
   return new{id="hall:"+host+":"+port,name=string.IsNullOrEmpty(name)?host+":"+port:name,host=host,port=(int)port,queryPort=(int)queryPort,players=players,maxPlayers=max,ping=ping<0?-1:ping,description=Utf8(bytes,78,64),passwordRequired=bytes[160]!=0,steam=steamId!=0};
  }
  public static object Execute(Engine engine,string action,Dictionary<string,object> payload,Action<int,string> progress,CancellationToken cancel){
   var args=Normalize(action,payload);cancel.ThrowIfCancellationRequested();if(engine==null)throw new Exception("没有选择游戏安装。");
   if(action=="download"){engine.Guard();if(!engine.TestMode&&GameProcessGuard.IsRunning(engine.Game))throw new Exception("请退出游戏后下载工坊模组。");}
   string basePath=Path.Combine(Path.GetFullPath(engine.Data),"steam-services");
   return Transport(basePath,engine.Game,new Dictionary<string,object>{{"action",action},{"args",args}},Process.GetCurrentProcess().MainModule.FileName,action=="download"?510000:90000,progress,cancel);
  }
  static void NoReparse(string path){string full=Path.GetFullPath(path);for(var at=new DirectoryInfo(full);at!=null;at=at.Parent)if(at.Exists&&(at.Attributes&FileAttributes.ReparsePoint)!=0)throw new Exception("Steam 临时请求目录不能经过链接。");}
  static string Quote(string value){var quoted=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){quoted.Append('\\',slashes*2+1);quoted.Append(c);}else{quoted.Append('\\',slashes);quoted.Append(c);}slashes=0;}quoted.Append('\\',slashes*2);return quoted.Append('"').ToString();}
  static void PrivateDirectory(string directory){Directory.CreateDirectory(directory);var sid=WindowsIdentity.GetCurrent().User;var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));new DirectoryInfo(directory).SetAccessControl(security);}
  internal static object Transport(string basePath,string game,Dictionary<string,object> request,string executable,int timeoutMilliseconds,Action<int,string> progress,CancellationToken cancel){
   cancel.ThrowIfCancellationRequested();NoReparse(basePath);Directory.CreateDirectory(basePath);string owned=Path.Combine(basePath,Guid.NewGuid().ToString("N"));PrivateDirectory(owned);
   string input=Path.Combine(owned,"request.json"),output=Path.Combine(owned,"response.json"),nonce=Guid.NewGuid().ToString("N");
   string action=Text(Field(request,"action"));var args=Normalize(action,Field(request,"args") as Dictionary<string,object>);var json=Json();Process process=null;
   try{
    File.WriteAllText(input,json.Serialize(new{action=action,args=args,nonce=nonce}),new UTF8Encoding(false));
    var start=new ProcessStartInfo(executable,"--steam-service-worker "+Quote(Path.GetFullPath(game))+" "+Quote(input)+" "+Quote(output)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
    var gate=new object();string protocolError=null;process=new Process{StartInfo=start};
    process.OutputDataReceived+=(sender,line)=>{if(line.Data==null||line.Data.Length>8192||!line.Data.StartsWith("{"))return;try{var item=Json().Deserialize<Dictionary<string,object>>(line.Data);if(Text(Field(item,"kind"))=="progress"&&progress!=null){int value=Convert.ToInt32(Field(item,"value"),CultureInfo.InvariantCulture);if(value<-1||value>100)throw new Exception("Steam 进度超出范围。");progress(value,Text(Field(item,"text")));}}catch(Exception x){lock(gate)protocolError=x.Message;}};
    // Native Steam diagnostics can contain account/server information. Drain but never persist them.
    process.ErrorDataReceived+=(sender,line)=>{};
    if(progress!=null)progress(-1,"正在请求 Steam · "+(action=="hall"?"公共服务器列表":"工坊服务"));
    process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();var watch=Stopwatch.StartNew();
    while(!process.WaitForExit(100)){cancel.ThrowIfCancellationRequested();if(watch.ElapsedMilliseconds>timeoutMilliseconds)throw new Exception("Steam 服务子进程超时；已释放该请求的游戏身份。");}process.WaitForExit();cancel.ThrowIfCancellationRequested();
    if(protocolError!=null)throw new Exception("Steam 服务响应无效："+protocolError);
    if(!File.Exists(output)||new FileInfo(output).Length>MaxResponseBytes)throw new Exception("Steam 服务未返回有效响应。");
    var response=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(output,Encoding.UTF8));object ok=Field(response,"ok");
    if(Text(Field(response,"nonce"))!=nonce||!(ok is bool))throw new Exception("Steam 服务响应与请求不一致。");
    if(!(bool)ok||process.ExitCode!=0)throw new Exception(Text(Field(response,"message"))==""?"Steam 请求失败；请确认桌面 Steam 已登录。":Text(Field(response,"message")));
    object data=Field(response,"data");if(data==null)throw new Exception("Steam 服务响应缺少结果。");return data;
   }finally{
    if(process!=null){try{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}}catch(InvalidOperationException){}finally{process.Dispose();}}
    try{if(File.Exists(input))File.Delete(input);if(File.Exists(output))File.Delete(output);if(Directory.Exists(owned))Directory.Delete(owned,false);}catch(IOException){}catch(UnauthorizedAccessException){}
   }
  }
  static void WorkerPaths(string requestPath,string responsePath){
   string request=Path.GetFullPath(requestPath),response=Path.GetFullPath(responsePath),directory=Path.GetDirectoryName(request);Guid id;
   if(Path.GetFileName(request)!="request.json"||Path.GetFileName(response)!="response.json"||!string.Equals(directory,Path.GetDirectoryName(response),StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(directory),"N",out id))throw new Exception("无效 Steam 请求目录。");
   NoReparse(directory);if(!File.Exists(request)||new FileInfo(request).Length>32768||File.Exists(response)||(File.GetAttributes(request)&FileAttributes.ReparsePoint)!=0)throw new Exception("Steam 请求文件不存在、过大或响应已存在。");
  }
  public static int Worker(string game,string requestPath,string responsePath){
   Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true});
   Console.SetError(new StreamWriter(Console.OpenStandardError(),new UTF8Encoding(false)){AutoFlush=true});
   string nonce="";bool validPaths=false;var json=Json();
   try{
    WorkerPaths(requestPath,responsePath);validPaths=true;var request=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(requestPath,Encoding.UTF8));nonce=Text(Field(request,"nonce"));Guid parsed;if(!Guid.TryParseExact(nonce,"N",out parsed))throw new Exception("Steam 请求标识无效。");string action=Text(Field(request,"action"));var args=Normalize(action,Field(request,"args") as Dictionary<string,object>);
    using(var bounded=new CancellationTokenSource(action=="download"?480000:75000)){
     object result;
     try{using(var native=new SteamLauncherNative(game,bounded.Token)){result=native.Execute(action,args,(v,t)=>{Console.WriteLine(json.Serialize(new{kind="progress",value=v,text=t}));Console.Out.Flush();});}}
     catch(Exception x){if(action!="connection")throw;result=new{available=false,accountName="",steamId="",message=x.Message};}
     File.WriteAllText(responsePath,json.Serialize(new{nonce=nonce,ok=true,data=result}),new UTF8Encoding(false));return 0;
    }
   }catch(Exception x){if(validPaths)try{File.WriteAllText(responsePath,json.Serialize(new{nonce=nonce,ok=false,message=x is OperationCanceledException?"Steam 请求达到时限；请重试。":x.Message}),new UTF8Encoding(false));}catch{}return 1;}
  }
 }
}
