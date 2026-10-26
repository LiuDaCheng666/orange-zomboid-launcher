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
  sealed class Ownership {public string version,jar,session,issues;internal bool PreparationPending,RemovalPending;}
  sealed class PendingOwnership {public string version,kind,jar,session,issues,nextJar,nextSession,nextIssues;}
  static string Folder(string game){return Path.Combine(Path.GetFullPath(game),"OrangeLauncherRuntime","startup");}
  static string Encode(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value??""));}
  static string Decode(string value){return new UTF8Encoding(false,true).GetString(Convert.FromBase64String(value));}
  static bool Nonce(string value){return Regex.IsMatch(value??"",@"\A[a-f0-9]{32}\z",RegexOptions.CultureInvariant);}
  static string Hash(byte[] bytes){return DlssInstaller.Hash(bytes);}
  static byte[] Read(string path,int maximum){LauncherMaintenance.NoReparse(path);if(Directory.Exists(path))throw new IOException("启动组件文件路径被目录占用。");if(!File.Exists(path))return null;using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(input.Length>maximum)throw new IOException("启动组件文件超过安全大小。");byte[] data=new byte[(int)input.Length];int n=0,count;while(n<data.Length&&(count=input.Read(data,n,data.Length-n))>0)n+=count;if(n!=data.Length)throw new IOException("启动组件文件读取期间改变。");return data;}}
  static byte[] LoadPayload(){byte[] data=BundledAssets.ReadOwnAsset("OrangeStartup.jar");if(data==null||data.Length==0||data.Length>JarLimit)throw new IOException("内置启动组件缺失或大小异常。");return data;}
  static byte[] Payload(){return BundledPayload.Value;}
  static byte[] Payload(Engine e){return e.FrozenEnvironment==null?Payload():SaveEnvironmentRuntime.PinnedFile(e,"game/OrangeLauncherRuntime/startup/OrangeStartup.jar",JarLimit);}
  static void Paths(string game,string user){LauncherMaintenance.NoReparse(Path.GetFullPath(game));LauncherMaintenance.NoReparse(Path.GetFullPath(user));LauncherMaintenance.NoReparse(Folder(game));}
  static Mutex Lease(string game){return new Mutex(false,"Local\\OrangeStartup-"+Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToUpperInvariant())));}
  static bool Acquire(Mutex lease){try{return lease.WaitOne(5000);}catch(AbandonedMutexException){return true;}}
  static void Atomic(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".new-"+Guid.NewGuid().ToString("N");try{using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}LauncherMaintenance.NoReparse(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  // Issue-only commits retain the same JAR buffer. Distinct reads still compare
  // every byte, including when a replacement preserves size and timestamps.
  static bool Same(byte[] a,byte[] b){if(ReferenceEquals(a,b))return true;if(a==null||b==null||a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
  static string OptionalHash(byte[] bytes){return bytes==null?null:Hash(bytes);}
  static bool HashValue(string value,bool nullable){return nullable&&value==null||Regex.IsMatch(value??"",@"\A[A-F0-9]{64}\z");}
  static void RequireHash(string path,byte[] bytes,string hash){if(bytes==null||!Regex.IsMatch(hash??"",@"\A[A-F0-9]{64}\z")||Hash(bytes)!=hash)throw new IOException("启动组件文件缺失或被修改，未覆盖："+Path.GetFileName(path));}
  static void RequireTransition(string name,byte[] bytes,string before,string after,bool removal=false){if(!HashValue(before,!removal)||!HashValue(after,removal)||removal&&after!=null)throw new IOException("启动组件恢复记录校验无效。");string actual=OptionalHash(bytes);if(actual!=before&&actual!=after)throw new IOException("启动组件恢复遇到外部修改，未覆盖："+name);}
  static Ownership Existing(string folder,byte[] jar,byte[] session,byte[] issues,byte[] record){
   if(record==null){if(jar!=null||session!=null||issues!=null)throw new IOException("已有同名启动组件文件缺少所有权记录，未覆盖。");return null;}
   Ownership owner;try{string text=new UTF8Encoding(false,true).GetString(record);var header=Json.Deserialize<Dictionary<string,object>>(text);object version;if(header==null||!header.TryGetValue("version",out version))throw new IOException();
    if(Equals(version,"OZL1-PENDING")){
     CoreReleaseProtocol.ReadStoredObject(record,"version","kind","jar","session","issues","nextJar","nextSession","nextIssues");var pending=Json.Deserialize<PendingOwnership>(text);if(pending.kind!="prepare"&&pending.kind!="issues"&&pending.kind!="remove")throw new IOException("未知的启动组件事务。");bool removal=pending.kind=="remove";
     RequireTransition("OrangeStartup.jar",jar,pending.jar,pending.nextJar,removal);RequireTransition("session.properties",session,pending.session,pending.nextSession,removal);RequireTransition("issues.txt",issues,pending.issues,pending.nextIssues,removal);
     return new Ownership{version="OZL1",jar=OptionalHash(jar),session=OptionalHash(session),issues=OptionalHash(issues),PreparationPending=pending.kind=="prepare"||removal,RemovalPending=removal};
    }
    CoreReleaseProtocol.ReadStoredObject(record,"version","jar","session","issues");owner=Json.Deserialize<Ownership>(text);
   }catch{throw new IOException("启动组件所有权或恢复记录无效，未覆盖。");}
   if(owner==null||owner.version!="OZL1")throw new IOException("启动组件所有权记录无效，未覆盖。");
   RequireHash(Path.Combine(folder,"OrangeStartup.jar"),jar,owner.jar);
   RequireHash(Path.Combine(folder,"session.properties"),session,owner.session);RequireHash(Path.Combine(folder,"issues.txt"),issues,owner.issues);return owner;
  }
  static int FileLimit(string path,string config){return path==config?ConfigLimit:Path.GetFileName(path)=="OrangeStartup.jar"?JarLimit:Path.GetFileName(path)=="ownership.json"?4096:Path.GetFileName(path)=="session.properties"?SessionLimit:IssueLimit;}
  // The write-ahead record authorizes only exact old/new file states, never code
  // execution. A later Prepare can renew a partially written session in place.
  // Persisting this record before every mutation makes abrupt process exit safe.
  static void CommitOwned(string folder,Dictionary<string,byte[]> before,Dictionary<string,byte[]> after,string config=null){
   string jar=Path.Combine(folder,"OrangeStartup.jar"),session=Path.Combine(folder,"session.properties"),issues=Path.Combine(folder,"issues.txt"),record=Path.Combine(folder,"ownership.json");
   if(after.All(item=>Same(item.Value,before[item.Key])))return;
   string[] targets=(config==null?new[]{jar,session,issues}:new[]{jar,session,issues,config});
   var pending=new PendingOwnership{version="OZL1-PENDING",kind=config==null?"issues":"prepare",jar=OptionalHash(before[jar]),session=OptionalHash(before[session]),issues=OptionalHash(before[issues]),nextJar=Hash(after[jar]),nextSession=Hash(after[session]),nextIssues=Hash(after[issues])};byte[] journal=Encoding.UTF8.GetBytes(Json.Serialize(pending));
   foreach(var item in before)if(!Same(Read(item.Key,FileLimit(item.Key,config)),item.Value))throw new IOException("启动写入前文件已改变，未覆盖。");
   Atomic(record,journal);var written=new List<string>();
   try{
    foreach(string path in targets){if(Same(after[path],before[path]))continue;if(!Same(Read(path,FileLimit(path,config)),before[path]))throw new IOException("启动准备期间文件改变，未覆盖。");Atomic(path,after[path]);written.Add(path);}
    if(!Same(Read(record,4096),journal))throw new IOException("启动组件恢复记录在提交前改变，未覆盖。");Atomic(record,after[record]);
   }catch{
    foreach(string path in written.AsEnumerable().Reverse()){try{if(Same(Read(path,FileLimit(path,config)),after[path])){if(before[path]==null)File.Delete(path);else Atomic(path,before[path]);}}catch{ /* The durable journal remains recoverable if rollback is interrupted. */ }}
    try{if(targets.All(path=>Same(Read(path,FileLimit(path,config)),before[path]))&&Same(Read(record,4096),journal)){if(before[record]==null)File.Delete(record);else Atomic(record,before[record]);}}catch{ /* Keep the exact old/new recovery record; never overwrite foreign edits. */ }
    throw;
   }
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
  static string Preference(Engine e){return Path.Combine(Path.GetFullPath(e.Data),"game-startup-enabled.txt");}
  static bool PreferenceValue(byte[] bytes){if(bytes==null)return true;string value=Encoding.ASCII.GetString(bytes);if(value=="enabled\n")return true;if(value=="disabled\n")return false;throw new IOException("加载页设置格式无效，未覆盖。");}
  internal static bool Enabled(Engine e){if(e==null)throw new ArgumentNullException("e");return PreferenceValue(Read(Preference(e),64));}
  static byte[] Detached(byte[] original,out bool installed){
   if(original==null)throw new IOException("游戏启动配置不存在。");string text=new UTF8Encoding(false,true).GetString(original).TrimStart('\uFEFF');if(!BuddyLaunchArguments.CleanJson(text).Safe)throw new IOException("游戏启动配置存在重复或不明确字段。");var config=Json.Deserialize<Dictionary<string,object>>(text);if(config==null||!config.ContainsKey("vmArgs"))throw new IOException("游戏启动配置缺少 vmArgs。");var branches=Branches(config).ToArray();installed=branches.Any(branch=>((IEnumerable)branch["vmArgs"]).Cast<string>().Contains(Argument));if(!installed)return original;
   foreach(var branch in branches)branch["vmArgs"]=((IEnumerable)branch["vmArgs"]).Cast<string>().Where(arg=>arg!=Argument).ToArray();byte[] result=new UTF8Encoding(false).GetBytes(Json.Serialize(config));if(result.Length>ConfigLimit)throw new IOException("游戏启动配置超过安全大小。");return result;
  }
  internal static object State(Engine e){
   bool enabled=true,installed=false,removable=false;string warning="";if(e==null)return new{enabled=enabled,installed=installed,removable=removable,warning="未选择游戏目录。"};
   try{enabled=Enabled(e);}catch(Exception error){warning=error.Message;}
   try{bool detected;Detached(Read(e.Config,ConfigLimit),out detected);installed=detected;}catch(Exception error){warning=warning==""?error.Message:warning+" "+error.Message;}
   try{Paths(e.Game,e.User);string folder=Folder(e.Game);byte[] session=Read(Path.Combine(folder,"session.properties"),SessionLimit);var owner=Existing(folder,Read(Path.Combine(folder,"OrangeStartup.jar"),JarLimit),session,Read(Path.Combine(folder,"issues.txt"),IssueLimit),Read(Path.Combine(folder,"ownership.json"),4096));if(owner!=null&&session!=null)Session(e.Game,e.User,session,false);removable=owner!=null&&(!owner.PreparationPending||owner.RemovalPending);if(owner!=null&&owner.RemovalPending)warning=warning==""?"加载页卸载未完成，请重试卸载。":warning;else if(owner!=null&&owner.PreparationPending)warning=warning==""?"加载页准备未完成，请先恢复启动。":warning;}catch(Exception error){warning=warning==""?error.Message:warning+" "+error.Message;}
   return new{enabled=enabled,installed=installed,removable=removable,warning=warning};
  }
  static void ControlGuard(Engine e){if(e==null)throw new ArgumentNullException("e");Paths(e.Game,e.User);LauncherMaintenance.NoReparse(e.Config);LauncherMaintenance.NoReparse(Path.GetFullPath(e.Data));LauncherMaintenance.NoReparse(Preference(e));LauncherMaintenance.NoReparse(LaunchProfiles.Journal(e));if(LaunchProfiles.Pending(e))throw new IOException("启动会话尚未恢复，未改动加载页。");e.GuardLaunch();if(!e.TestMode&&e.RuntimeRunning())throw new IOException("游戏客户端或同目录服务器仍在运行，请退出后再修改加载页。");}
  static void SaveEnabled(Engine e,bool enabled,byte[] preference,byte[] config,byte[] detached){
   byte[] next=Encoding.ASCII.GetBytes(enabled?"enabled\n":"disabled\n");PreferenceValue(preference);if(!Same(Read(Preference(e),64),preference)||!Same(Read(e.Config,ConfigLimit),config))throw new IOException("加载页设置或游戏配置已改变，请刷新后重试。");bool wroteConfig=false;
   try{if(!enabled&&!Same(config,detached)){Atomic(e.Config,detached);wroteConfig=true;}if(!Same(preference,next))Atomic(Preference(e),next);}
   catch{if(wroteConfig){try{if(Same(Read(e.Config,ConfigLimit),detached))Atomic(e.Config,config);}catch{ /* Exact detachment remains safe; never overwrite a later external edit. */ }}throw;}
  }
  static void UpdatePreference(Engine e,bool enabled,bool onlyIfDisabled){ControlGuard(e);using(var lease=Lease(e.Game)){if(!Acquire(lease))throw new IOException("另一个加载页操作正在进行。");try{byte[] preference=Read(Preference(e),64);if(onlyIfDisabled&&PreferenceValue(preference))return;byte[] config=Read(e.Config,ConfigLimit);bool installed;byte[] detached=Detached(config,out installed);SaveEnabled(e,enabled,preference,config,detached);}finally{lease.ReleaseMutex();}}}
  internal static void SetEnabled(Engine e,bool enabled){UpdatePreference(e,enabled,false);}
  internal static void Remove(Engine e){ControlGuard(e);using(var lease=Lease(e.Game)){if(!Acquire(lease))throw new IOException("另一个加载页操作正在进行。");try{
   string folder=Folder(e.Game),recordPath=Path.Combine(folder,"ownership.json");string[] targets={Path.Combine(folder,"OrangeStartup.jar"),Path.Combine(folder,"session.properties"),Path.Combine(folder,"issues.txt")};byte[][] bytes={Read(targets[0],JarLimit),Read(targets[1],SessionLimit),Read(targets[2],IssueLimit)};byte[] record=Read(recordPath,4096),preference=Read(Preference(e),64),config=Read(e.Config,ConfigLimit);bool installed;byte[] detached=Detached(config,out installed);var owner=Existing(folder,bytes[0],bytes[1],bytes[2],record);PreferenceValue(preference);
   if(owner!=null&&owner.PreparationPending&&!owner.RemovalPending)throw new IOException("启动组件准备尚未恢复，请先完成启动恢复后再卸载。");if(owner!=null&&bytes[1]!=null)Session(e.Game,e.User,bytes[1],false);
   SaveEnabled(e,false,preference,config,detached);if(owner==null)return;
   byte[] journal=owner.RemovalPending?record:Encoding.UTF8.GetBytes(Json.Serialize(new PendingOwnership{version="OZL1-PENDING",kind="remove",jar=owner.jar,session=owner.session,issues=owner.issues,nextJar=null,nextSession=null,nextIssues=null}));
   for(int i=0;i<targets.Length;i++)if(!Same(Read(targets[i],i==0?JarLimit:i==1?SessionLimit:IssueLimit),bytes[i]))throw new IOException("加载页文件已改变，未删除。");if(!Same(Read(recordPath,4096),record))throw new IOException("加载页所有权记录已改变，未删除。");if(!owner.RemovalPending)Atomic(recordPath,journal);
   for(int i=0;i<targets.Length;i++){if(!Same(Read(targets[i],i==0?JarLimit:i==1?SessionLimit:IssueLimit),bytes[i]))throw new IOException("加载页卸载遇到外部修改，未删除。");if(bytes[i]!=null)File.Delete(targets[i]);}
   if(!Same(Read(recordPath,4096),journal))throw new IOException("加载页所有权记录已改变，未删除。");File.Delete(recordPath);foreach(string directory in new[]{folder,Path.GetDirectoryName(folder)}){LauncherMaintenance.NoReparse(directory);if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory,false);}
  }finally{lease.ReleaseMutex();}}}
  internal static bool IsOwnedArgument(Engine e,string argument){if(argument!=Argument||e==null)return false;try{Paths(e.Game,e.User);return Same(Read(Path.Combine(Folder(e.Game),"OrangeStartup.jar"),JarLimit),Payload(e));}catch{return false;}}
  // Read-only preflight may offer to replace an intact recorded prior version.
  // Launch validation still requires IsOwnedArgument after preparation succeeds.
  internal static bool CanPrepareOwnedArgument(Engine e,string argument){if(argument!=Argument||e==null)return false;try{
   Paths(e.Game,e.User);string folder=Folder(e.Game);byte[] session=Read(Path.Combine(folder,"session.properties"),SessionLimit);var owner=Existing(folder,Read(Path.Combine(folder,"OrangeStartup.jar"),JarLimit),session,Read(Path.Combine(folder,"issues.txt"),IssueLimit),Read(Path.Combine(folder,"ownership.json"),4096));return owner!=null&&(session==null||Session(e.Game,e.User,session,false)!=null);
  }catch{return false;}}
  internal static void Prepare(Engine e,string sessionId){
   if(e==null)throw new ArgumentNullException("e");if(!Nonce(sessionId))throw new IOException("启动组件会话标识无效。");bool frozen=e.FrozenEnvironment!=null,enabled=Enabled(e);if(frozen&&!File.ReadAllText(e.Config).Contains(Argument))return;if(!enabled&&!frozen){UpdatePreference(e,false,true);return;}Paths(e.Game,e.User);LauncherMaintenance.NoReparse(e.Config);LauncherMaintenance.NoReparse(LaunchProfiles.Journal(e));if(LaunchProfiles.Pending(e))throw new IOException("启动会话尚未恢复，未改动启动组件。");e.GuardLaunch();byte[] payload=Payload(e);
   using(var lease=Lease(e.Game)){bool acquired=Acquire(lease);if(!acquired)throw new IOException("另一个启动组件准备正在进行。");try{
    string folder=Folder(e.Game),jarPath=Path.Combine(folder,"OrangeStartup.jar"),sessionPath=Path.Combine(folder,"session.properties"),issuesPath=Path.Combine(folder,"issues.txt"),recordPath=Path.Combine(folder,"ownership.json");
    var before=new Dictionary<string,byte[]>{{jarPath,Read(jarPath,JarLimit)},{sessionPath,Read(sessionPath,SessionLimit)},{issuesPath,Read(issuesPath,IssueLimit)},{recordPath,Read(recordPath,4096)},{e.Config,Read(e.Config,ConfigLimit)}};
    bool coldFrozen=frozen&&before[recordPath]==null&&before[sessionPath]==null&&before[issuesPath]==null&&Same(before[jarPath],payload);
    var owner=coldFrozen?null:Existing(folder,before[jarPath],before[sessionPath],before[issuesPath],before[recordPath]);if(owner!=null&&owner.RemovalPending)throw new IOException("加载页卸载未完成，请先重试卸载。");Dictionary<string,string> previous=null;if(owner!=null&&before[sessionPath]!=null)previous=Session(e.Game,e.User,before[sessionPath],false);
    byte[] config=frozen?before[e.Config]:Configuration(before[e.Config]);long created=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-(frozen&&!enabled?601:0);bool reuse=enabled&&previous!=null&&previous["session"]==sessionId&&long.Parse(previous["expires"],CultureInfo.InvariantCulture)>created&&before[issuesPath]!=null&&new UTF8Encoding(false,true).GetString(before[issuesPath]).StartsWith("OZL1\n"+sessionId+"\n",StringComparison.Ordinal);
    byte[] session=reuse?before[sessionPath]:Encoding.ASCII.GetBytes(string.Join("\n",new[]{"version=OZL1","session="+sessionId,"created="+created.ToString(CultureInfo.InvariantCulture),"expires="+(created+600).ToString(CultureInfo.InvariantCulture),"game="+Encode(Path.GetFullPath(e.Game)),"user="+Encode(Path.GetFullPath(e.User)),"issues="+Encode(issuesPath),"theme="+(LauncherTheme.Load(e.Data).Light?"light":"dark"),"reducedMotion="+(ReducedMotion()?"true":"false")})+"\n");
    Session(e.Game,e.User,session,enabled);byte[] issues=reuse?before[issuesPath]:IssueBytes(sessionId,null);byte[] record=Encoding.UTF8.GetBytes(Json.Serialize(new Ownership{version="OZL1",jar=Hash(payload),session=Hash(session),issues=Hash(issues)}));
    var after=new Dictionary<string,byte[]>{{jarPath,payload},{sessionPath,session},{issuesPath,issues},{recordPath,record},{e.Config,config}};CommitOwned(folder,before,after,e.Config);
   }finally{lease.ReleaseMutex();}}
  }
  static string Safe(string text,int maximum){text=GameDiagnostics.Sanitize(text??"");return text.Length>maximum?text.Substring(0,maximum):text;}
  static bool LiveClient(GameStartupLaunch launch,GameStartupSnapshot snapshot){if(!snapshot.ProcessId.HasValue||snapshot.ProcessBirth<launch.StartedUtc.Ticks)return false;try{using(var process=Process.GetProcessById(snapshot.ProcessId.Value)){
   if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=snapshot.ProcessBirth)return false;string name=process.ProcessName,image=process.MainModule.FileName;bool java=name=="java"||name=="javaw";if(java&&GameProcessGuard.ReadJavaRole(launch.GamePath,name,image,process)!=JavaProcessRole.Client)return false;return GameStartupMonitor.MatchesClientIdentity(launch.GamePath,launch.StartedUtc,name,image,new DateTime(snapshot.ProcessBirth,DateTimeKind.Utc),java);
  }}catch{return false;}}
  static byte[] IssueBytes(string session,GameLogIssue issue,long? timestamp=null){return new UTF8Encoding(false).GetBytes(string.Join("\n",new[]{"OZL1",session,(timestamp??DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString(CultureInfo.InvariantCulture),Encode(issue==null?"":Safe(issue.title,160)),Encode(issue==null?"":Safe(issue.meaning,768)),Encode(issue==null?"":string.Join("、",(issue.mods??new string[0]).Take(8).Select(m=>Safe(m,96)))),Encode(issue==null?"":string.Join("\n",(issue.evidence??new GameLogEvidence[0]).Where(v=>v!=null).Take(3).Select(v=>Safe(v.text,384)))),Encode(issue==null?"":Safe(issue.advice,768))})+"\n");}
  internal static bool Eligible(GameStartupSnapshot snapshot,GameLogIssue issue){
   if(issue==null||issue.attention!="primary"||string.IsNullOrWhiteSpace(issue.title)||string.IsNullOrWhiteSpace(issue.meaning))return false;
   // History is diagnostic only after the verified menu handoff or the packet TTL.
   // These bounds use original occurrence data, never the most recent reparse.
   if(snapshot.ObservationFloor>0&&issue.ObservationLine<=snapshot.ObservationFloor)return false;
   return snapshot.ObservationUtc==DateTime.MinValue||issue.ObservationUtc!=DateTime.MinValue&&(snapshot.ObservationUtc-issue.ObservationUtc).TotalSeconds<=30;
  }
  static GameLogIssue Primary(GameStartupSnapshot snapshot){return (snapshot.Issues??new GameLogIssue[0]).Where(i=>Eligible(snapshot,i)).OrderByDescending(GameLogAnalyzer.Priority).ThenByDescending(i=>i.ObservationLine).FirstOrDefault();}
  internal static string IssueIdentity(GameStartupSnapshot snapshot){var issue=Primary(snapshot);return Convert.ToBase64String(IssueBytes("",issue,0))+"|"+(issue==null?0:issue.ObservationLine).ToString(CultureInfo.InvariantCulture);}
  internal static bool WriteIssues(GameStartupLaunch launch,GameStartupSnapshot snapshot){
   if(launch==null||snapshot==null||!Nonce(launch.SessionId)||launch.SessionId!=snapshot.SessionId)return false;
   try{Paths(launch.GamePath,launch.UserPath);using(var lease=Lease(launch.GamePath)){bool acquired=Acquire(lease);if(!acquired)return false;try{
    string folder=Folder(launch.GamePath),jarPath=Path.Combine(folder,"OrangeStartup.jar"),sessionPath=Path.Combine(folder,"session.properties"),path=Path.Combine(folder,"issues.txt"),recordPath=Path.Combine(folder,"ownership.json");byte[] jar=Read(jarPath,JarLimit),session=Read(sessionPath,SessionLimit),old=Read(path,IssueLimit),record=Read(recordPath,4096);var owner=Existing(folder,jar,session,old,record);if(owner==null||owner.PreparationPending||jar==null||session==null)return false;var binding=Session(launch.GamePath,launch.UserPath,session,false);if(binding["session"]!=launch.SessionId||(long.Parse(binding["expires"],CultureInfo.InvariantCulture)<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()&&!LiveClient(launch,snapshot)))return false;
    var issue=Primary(snapshot);byte[] next=IssueBytes(launch.SessionId,issue);if(next.Length>IssueLimit)return false;
    owner.issues=Hash(next);var before=new Dictionary<string,byte[]>{{jarPath,jar},{sessionPath,session},{path,old},{recordPath,record}};var after=new Dictionary<string,byte[]>{{jarPath,jar},{sessionPath,session},{path,next},{recordPath,Encoding.UTF8.GetBytes(Json.Serialize(owner))}};CommitOwned(folder,before,after);return true;
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
