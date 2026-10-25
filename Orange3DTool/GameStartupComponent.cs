using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 // This allowlist names one bundled component. A disk ownership record never
 // authorizes executable bytes: the JAR must match the authenticated resource.
 static class GameStartupComponent {
  internal const string Argument="-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar";
  const int SessionLimit=8192,IssueLimit=16384,JarLimit=33554432,ConfigLimit=2097152;
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=ConfigLimit};
  static readonly Lazy<byte[]> BundledPayload=new Lazy<byte[]>(LoadPayload);
  [DllImport("user32.dll")]static extern bool SystemParametersInfo(uint action,uint parameter,out bool enabled,uint flags);
  static bool ReducedMotion(){try{bool enabled;return SystemParametersInfo(0x1042,0,out enabled,0)&&!enabled;}catch{return false;}}
  sealed class Ownership {public string version,jar,session,issues;}
  static string Folder(string game){return Path.Combine(Path.GetFullPath(game),"OrangeLauncherRuntime","startup");}
  static string Encode(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value??""));}
  static string Decode(string value){return new UTF8Encoding(false,true).GetString(Convert.FromBase64String(value));}
  static bool Nonce(string value){return Regex.IsMatch(value??"",@"\A[a-f0-9]{32}\z",RegexOptions.CultureInvariant);}
  static string Hash(byte[] bytes){return DlssInstaller.Hash(bytes);}
  static byte[] Read(string path,int maximum){LauncherMaintenance.NoReparse(path);if(Directory.Exists(path))throw new IOException("启动组件文件路径被目录占用。");if(!File.Exists(path))return null;using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(input.Length>maximum)throw new IOException("启动组件文件超过安全大小。");byte[] data=new byte[(int)input.Length];int n=0,count;while(n<data.Length&&(count=input.Read(data,n,data.Length-n))>0)n+=count;if(n!=data.Length)throw new IOException("启动组件文件读取期间改变。");return data;}}
  static byte[] LoadPayload(){byte[] data=BundledAssets.ReadOwnAsset("OrangeStartup.jar");if(data==null||data.Length==0||data.Length>JarLimit)throw new IOException("内置启动组件缺失或大小异常。");return data;}
  static byte[] Payload(){return BundledPayload.Value;}
  static void Paths(string game,string user){LauncherMaintenance.NoReparse(Path.GetFullPath(game));LauncherMaintenance.NoReparse(Path.GetFullPath(user));LauncherMaintenance.NoReparse(Folder(game));}
  static Mutex Lease(string game){return new Mutex(false,"Local\\OrangeStartup-"+Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToUpperInvariant())));}
  static bool Acquire(Mutex lease){try{return lease.WaitOne(5000);}catch(AbandonedMutexException){return true;}}
  static void Atomic(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".new-"+Guid.NewGuid().ToString("N");try{using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}LauncherMaintenance.NoReparse(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  static bool Same(byte[] a,byte[] b){return a==null?b==null:b!=null&&a.SequenceEqual(b);}
  static void RequireHash(string path,byte[] bytes,string hash){if(bytes==null||!Regex.IsMatch(hash??"",@"\A[A-F0-9]{64}\z")||Hash(bytes)!=hash)throw new IOException("启动组件文件缺失或被修改，未覆盖："+Path.GetFileName(path));}
  static Ownership Existing(string folder,byte[] jar,byte[] session,byte[] issues,byte[] record){
   if(record==null){if(jar!=null||session!=null||issues!=null)throw new IOException("已有同名启动组件文件缺少所有权记录，未覆盖。");return null;}
   Ownership owner;try{CoreReleaseProtocol.ReadStoredObject(record,"version","jar","session","issues");owner=Json.Deserialize<Ownership>(new UTF8Encoding(false,true).GetString(record));}catch{throw new IOException("启动组件所有权记录无效，未覆盖。");}
   if(owner==null||owner.version!="OZL1")throw new IOException("启动组件所有权记录无效，未覆盖。");
   RequireHash(Path.Combine(folder,"OrangeStartup.jar"),jar,owner.jar);
   RequireHash(Path.Combine(folder,"session.properties"),session,owner.session);RequireHash(Path.Combine(folder,"issues.txt"),issues,owner.issues);return owner;
  }
  static Dictionary<string,string> Session(string game,string user,byte[] bytes,bool fresh){
   if(bytes==null||bytes.Length>SessionLimit||bytes.Any(b=>b>127||b==13||b==0))throw new IOException("启动组件会话格式无效。");var values=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(string line in Encoding.ASCII.GetString(bytes).Split('\n')){if(line.Length==0)continue;int at=line.IndexOf('=');if(at<=0||values.ContainsKey(line.Substring(0,at)))throw new IOException("启动组件会话字段无效。");values.Add(line.Substring(0,at),line.Substring(at+1));}
   long created,expires,now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();string[] keys={"version","session","created","expires","game","user","issues","theme","reducedMotion"};
   if(values.Count!=keys.Length||keys.Any(k=>!values.ContainsKey(k))||values["version"]!="OZL1"||!Nonce(values["session"])||!long.TryParse(values["created"],NumberStyles.None,CultureInfo.InvariantCulture,out created)||!long.TryParse(values["expires"],NumberStyles.None,CultureInfo.InvariantCulture,out expires)||created<0||expires<=created||expires-created>600||created>now+5||(fresh&&expires<=now)||(values["theme"]!="dark"&&values["theme"]!="light")||(values["reducedMotion"]!="true"&&values["reducedMotion"]!="false"))throw new IOException("启动组件会话已过期或无效。");
   string[] paths={Decode(values["game"]),Decode(values["user"]),Decode(values["issues"])},expected={Path.GetFullPath(game),Path.GetFullPath(user),Path.Combine(Folder(game),"issues.txt")};
   for(int i=0;i<paths.Length;i++){if(!Path.IsPathRooted(paths[i])||!string.Equals(paths[i],Path.GetFullPath(paths[i]),StringComparison.OrdinalIgnoreCase)||!string.Equals(paths[i],expected[i],StringComparison.OrdinalIgnoreCase))throw new IOException("启动组件会话路径不匹配。");LauncherMaintenance.NoReparse(paths[i]);}return values;
  }
  static IEnumerable<Dictionary<string,object>> Branches(Dictionary<string,object> branch){
   object raw;if(branch.TryGetValue("vmArgs",out raw)){if(raw is string||!(raw is IEnumerable)||((IEnumerable)raw).Cast<object>().Any(a=>!(a is string)))throw new IOException("vmArgs 必须是字符串数组。");yield return branch;}
   if(branch.TryGetValue("windows",out raw)){var windows=raw as Dictionary<string,object>;if(windows==null)throw new IOException("windows 启动分支格式无效。");foreach(object value in windows.Values){var child=value as Dictionary<string,object>;if(child==null)throw new IOException("windows 启动分支格式无效。");foreach(var item in Branches(child))yield return item;}}
  }
  static byte[] Configuration(byte[] original){
   if(original==null)throw new IOException("游戏启动配置不存在。");string text=new UTF8Encoding(false,true).GetString(original).TrimStart('\uFEFF');if(!BuddyLaunchArguments.CleanJson(text).Safe)throw new IOException("游戏启动配置存在重复或不明确字段。");var config=Json.Deserialize<Dictionary<string,object>>(text);if(config==null||!config.ContainsKey("vmArgs"))throw new IOException("游戏启动配置缺少 vmArgs。");var branches=Branches(config).ToArray();
   int count=branches.Sum(b=>((IEnumerable)b["vmArgs"]).Cast<string>().Count(a=>a==Argument));bool atRoot=((IEnumerable)config["vmArgs"]).Cast<string>().Contains(Argument);
   if(count==1&&atRoot)return original;
   foreach(var branch in branches)branch["vmArgs"]=((IEnumerable)branch["vmArgs"]).Cast<string>().Where(a=>a!=Argument).ToArray();config["vmArgs"]=((IEnumerable)config["vmArgs"]).Cast<string>().Concat(new[]{Argument}).ToArray();byte[] bytes=new UTF8Encoding(false).GetBytes(Json.Serialize(config));if(bytes.Length>ConfigLimit)throw new IOException("游戏启动配置超过安全大小。");return bytes;
  }
  internal static bool IsOwnedArgument(Engine e,string argument){if(argument!=Argument||e==null)return false;try{Paths(e.Game,e.User);return Same(Read(Path.Combine(Folder(e.Game),"OrangeStartup.jar"),JarLimit),Payload());}catch{return false;}}
  // Read-only preflight may offer to replace an intact recorded prior version.
  // Launch validation still requires IsOwnedArgument after preparation succeeds.
  internal static bool CanPrepareOwnedArgument(Engine e,string argument){if(argument!=Argument||e==null)return false;try{
   Paths(e.Game,e.User);string folder=Folder(e.Game);byte[] session=Read(Path.Combine(folder,"session.properties"),SessionLimit);var owner=Existing(folder,Read(Path.Combine(folder,"OrangeStartup.jar"),JarLimit),session,Read(Path.Combine(folder,"issues.txt"),IssueLimit),Read(Path.Combine(folder,"ownership.json"),4096));return owner!=null&&Session(e.Game,e.User,session,false)!=null;
  }catch{return false;}}
  internal static void Prepare(Engine e,string sessionId){
   if(e==null)throw new ArgumentNullException("e");if(!Nonce(sessionId))throw new IOException("启动组件会话标识无效。");Paths(e.Game,e.User);LauncherMaintenance.NoReparse(e.Config);LauncherMaintenance.NoReparse(LaunchProfiles.Journal(e));if(LaunchProfiles.Pending(e))throw new IOException("启动会话尚未恢复，未改动启动组件。");e.GuardLaunch();byte[] payload=Payload();
   using(var lease=Lease(e.Game)){bool acquired=Acquire(lease);if(!acquired)throw new IOException("另一个启动组件准备正在进行。");try{
    string folder=Folder(e.Game),jarPath=Path.Combine(folder,"OrangeStartup.jar"),sessionPath=Path.Combine(folder,"session.properties"),issuesPath=Path.Combine(folder,"issues.txt"),recordPath=Path.Combine(folder,"ownership.json");
    var before=new Dictionary<string,byte[]>{{jarPath,Read(jarPath,JarLimit)},{sessionPath,Read(sessionPath,SessionLimit)},{issuesPath,Read(issuesPath,IssueLimit)},{recordPath,Read(recordPath,4096)},{e.Config,Read(e.Config,ConfigLimit)}};
    var owner=Existing(folder,before[jarPath],before[sessionPath],before[issuesPath],before[recordPath]);Dictionary<string,string> previous=null;if(owner!=null)previous=Session(e.Game,e.User,before[sessionPath],false);
    byte[] config=Configuration(before[e.Config]);long created=DateTimeOffset.UtcNow.ToUnixTimeSeconds();bool reuse=previous!=null&&previous["session"]==sessionId&&long.Parse(previous["expires"],CultureInfo.InvariantCulture)>created;
    byte[] session=reuse?before[sessionPath]:Encoding.ASCII.GetBytes(string.Join("\n",new[]{"version=OZL1","session="+sessionId,"created="+created.ToString(CultureInfo.InvariantCulture),"expires="+(created+600).ToString(CultureInfo.InvariantCulture),"game="+Encode(Path.GetFullPath(e.Game)),"user="+Encode(Path.GetFullPath(e.User)),"issues="+Encode(issuesPath),"theme="+(LauncherTheme.Load(e.Data).Light?"light":"dark"),"reducedMotion="+(ReducedMotion()?"true":"false")})+"\n");
    Session(e.Game,e.User,session,true);byte[] issues=reuse?before[issuesPath]:IssueBytes(sessionId,null);byte[] record=Encoding.UTF8.GetBytes(Json.Serialize(new Ownership{version="OZL1",jar=Hash(payload),session=Hash(session),issues=Hash(issues)}));
    var after=new Dictionary<string,byte[]>{{jarPath,payload},{sessionPath,session},{issuesPath,issues},{recordPath,record},{e.Config,config}};var written=new List<string>();
    // Configuration commits last, after every owned runtime file is ready.
    try{foreach(string path in new[]{jarPath,sessionPath,issuesPath,recordPath,e.Config}){if(Same(after[path],before[path]))continue;if(!Same(Read(path,path==jarPath?JarLimit:path==e.Config?ConfigLimit:IssueLimit),before[path]))throw new IOException("启动准备期间文件改变，未覆盖。");Atomic(path,after[path]);written.Add(path);}}
    catch{foreach(string path in written.AsEnumerable().Reverse()){try{if(Same(Read(path,path==jarPath?JarLimit:path==e.Config?ConfigLimit:IssueLimit),after[path])){if(before[path]==null)File.Delete(path);else Atomic(path,before[path]);}}catch{ /* Preserve any concurrent edit rather than overwriting it. */ }}throw;}
   }finally{lease.ReleaseMutex();}}
  }
  static string Safe(string text,int maximum){text=GameDiagnostics.Sanitize(text??"");return text.Length>maximum?text.Substring(0,maximum):text;}
  static bool LiveClient(GameStartupLaunch launch,GameStartupSnapshot snapshot){if(!snapshot.ProcessId.HasValue||snapshot.ProcessBirth<launch.StartedUtc.Ticks)return false;try{using(var process=Process.GetProcessById(snapshot.ProcessId.Value)){
   if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=snapshot.ProcessBirth)return false;string name=process.ProcessName,image=process.MainModule.FileName;bool java=name=="java"||name=="javaw";if(java&&GameProcessGuard.ReadJavaRole(launch.GamePath,name,image,process)!=JavaProcessRole.Client)return false;return GameStartupMonitor.MatchesClientIdentity(launch.GamePath,launch.StartedUtc,name,image,new DateTime(snapshot.ProcessBirth,DateTimeKind.Utc),java);
  }}catch{return false;}}
  static byte[] IssueBytes(string session,GameLogIssue issue,long? timestamp=null){return new UTF8Encoding(false).GetBytes(string.Join("\n",new[]{"OZL1",session,(timestamp??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString(CultureInfo.InvariantCulture),Encode(issue==null?"":Safe(issue.title,160)),Encode(issue==null?"":Safe(issue.meaning,768)),Encode(issue==null?"":string.Join("、",(issue.mods??new string[0]).Take(8).Select(m=>Safe(m,96)))),Encode(issue==null?"":string.Join("\n",(issue.evidence??new GameLogEvidence[0]).Where(v=>v!=null).Take(3).Select(v=>Safe(v.text,384)))),Encode(issue==null?"":Safe(issue.advice,768))})+"\n");}
  static GameLogIssue Primary(GameStartupSnapshot snapshot){return (snapshot.Issues??new GameLogIssue[0]).FirstOrDefault(i=>i!=null&&i.attention=="primary"&&!string.IsNullOrWhiteSpace(i.title)&&!string.IsNullOrWhiteSpace(i.meaning));}
  internal static string IssueIdentity(GameStartupSnapshot snapshot){var issue=Primary(snapshot);return Convert.ToBase64String(IssueBytes("",issue,0))+"|"+(issue==null?0:issue.count).ToString(CultureInfo.InvariantCulture);}
  internal static bool WriteIssues(GameStartupLaunch launch,GameStartupSnapshot snapshot){
   if(launch==null||snapshot==null||!Nonce(launch.SessionId)||launch.SessionId!=snapshot.SessionId)return false;
   try{Paths(launch.GamePath,launch.UserPath);using(var lease=Lease(launch.GamePath)){bool acquired=Acquire(lease);if(!acquired)return false;try{
    string folder=Folder(launch.GamePath),jarPath=Path.Combine(folder,"OrangeStartup.jar"),sessionPath=Path.Combine(folder,"session.properties"),path=Path.Combine(folder,"issues.txt"),recordPath=Path.Combine(folder,"ownership.json");byte[] session=Read(sessionPath,SessionLimit),old=Read(path,IssueLimit),record=Read(recordPath,4096);var owner=Existing(folder,Read(jarPath,JarLimit),session,old,record);if(owner==null)return false;var binding=Session(launch.GamePath,launch.UserPath,session,false);if(binding["session"]!=launch.SessionId||(long.Parse(binding["expires"],CultureInfo.InvariantCulture)<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()&&!LiveClient(launch,snapshot)))return false;
    var issue=Primary(snapshot);byte[] next=IssueBytes(launch.SessionId,issue);if(next.Length>IssueLimit)return false;
    // The lease serializes our writers; compare again before replacing user-visible data.
    if(!Same(Read(sessionPath,SessionLimit),session)||!Same(Read(path,IssueLimit),old)||!Same(Read(recordPath,4096),record))return false;
    owner.issues=Hash(next);Atomic(path,next);try{Atomic(recordPath,Encoding.UTF8.GetBytes(Json.Serialize(owner)));}catch{if(Same(Read(path,IssueLimit),next))Atomic(path,old);throw;}return true;
   }finally{lease.ReleaseMutex();}}}catch{return false;}
  }
 }
 // Production host observes logs only. Rendering and input belong to the game.
 sealed class GameStartupObserver:IDisposable {
  readonly object gate=new object();readonly Func<GameStartupLaunch,Action<GameStartupSnapshot>,IGameStartupMonitor> factory;IGameStartupMonitor monitor;volatile string session;volatile bool disposed;
  internal GameStartupObserver():this((launch,changed)=>new GameStartupMonitor(launch,changed,false)){}
  internal GameStartupObserver(Func<GameStartupLaunch,Action<GameStartupSnapshot>,IGameStartupMonitor> factory){this.factory=factory;}
  internal void Handle(GameStartupLaunch launch){if(launch==null)return;lock(gate){if(disposed)return;if(launch.Stage=="preparing"){
    if(session==launch.SessionId)return;Clear();session=launch.SessionId;var active=new GameStartupLaunch{SessionId=launch.SessionId,StartedUtc=launch.StartedUtc,GamePath=launch.GamePath,UserPath=launch.UserPath,DataPath=launch.DataPath,TargetKind=launch.TargetKind,Stage=launch.Stage,Message=launch.Message};
    string lastIssue=null;
    monitor=factory(active,snapshot=>{if(snapshot==null||disposed||session!=active.SessionId||snapshot.SessionId!=active.SessionId)return;string identity=GameStartupComponent.IssueIdentity(snapshot);if(identity==lastIssue)return;if(GameStartupComponent.WriteIssues(active,snapshot))lastIssue=identity;});monitor.Start();
   }else if(session==launch.SessionId&&monitor!=null){if(launch.Stage=="submitted")monitor.Submitted();else if(launch.Stage=="failed"){monitor.Fail(launch.Message);Clear();}else if(launch.Stage=="cancelled")Clear();}}}
  void Clear(){session=null;var previous=monitor;monitor=null;if(previous!=null)previous.Dispose();}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;Clear();}}
 }
}
