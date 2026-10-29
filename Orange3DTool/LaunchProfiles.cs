using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class GameLaunchOutcome { public string message; public string stage; public string warning; public LaunchPreflightReport preflight; public GameStartupPending startup; }
 sealed class ProfileLaunchRequest {
  public string Kind,Id,Mode,Save,Host,Username,UserPassword,ServerPassword,ServerComponents;
  public int Port; public bool UseSteamRelay;
  internal string StartupSessionId;
  internal CancellationToken Cancellation;
  internal Action ValidateBeforeStage;
  internal Action<string,long> Timing;
 }
 sealed class ProfileSessionRecord {
  public string Game,User,Original,AppliedHash,CreatedUtc,State,Token,Nonce;
  public string CaptureEndpoint,CaptureRevision,Kind,SnapshotId,SnapshotManifestHash;
  public Dictionary<string,string> Files=new Dictionary<string,string>();
  public int Pid;public long Birth;
  public string ConfigurationId,ConfigurationMode,ConfigurationOwner,ConfigurationRoot,SessionDirectory,BridgeDirectory,SessionAppliedHash;
  public string[] SessionVmArguments,ManagedIds;public string ServerComponents,ServerRuntimeRoot;
 }
 static partial class LaunchProfiles {
  const string ModId="OrangeLauncherSession";const string RequestName="OrangeLauncherSession.request.txt";const string StatusName="OrangeLauncherSession.status.txt";
  const int RequestLifetimeSeconds=600;
  static string LuaFile(Engine e,string name){return Path.Combine(e.User,"Lua",name);}
  static string[] RequestFiles(Engine e){return new[]{LuaFile(e,RequestName),LuaFile(e,"OrangeLauncherSession.request"),Path.Combine(e.User,"OrangeLauncherSession.request")};}
  const int MaxJournalBytes=8388608;
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=MaxJournalBytes};
  public static string Key(Engine e){return DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(e.Game).ToUpperInvariant()+"|"+Path.GetFullPath(e.User).ToUpperInvariant()));}
  internal static string Journal(Engine e){return Path.Combine(e.Data,"launch-sessions",Key(e)+".json");}
  static string MutexName(Engine e){return "Local\\OrangeLaunch-"+DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(e.User).ToUpperInvariant()));}
  public static string Recover(Engine e,bool force=false,string expectedNonce=null){using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;try{try{acquired=mutex.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}if(!acquired)return "另一个启动任务正在准备，请稍后恢复。";if(PlayProfileTransaction.Pending(e))return PlayProfileTransaction.PendingMessage;if(!Pending(e))return "";if(expectedNonce!=null&&Load(e).Nonce!=expectedNonce)return "旧会话已结束；未改动新会话。";return RecoverUnlocked(e,force);}finally{if(acquired)mutex.ReleaseMutex();}}}
  public static bool Pending(Engine e){return File.Exists(Journal(e));}
  internal static string RecoveryNonce(Engine e){return Pending(e)?Load(e).Nonce:null;}
  // A new UI/controller can reconnect only to a validated, still-fresh Steam
  // submission. A recovery journal on its own is not a pending launch forever.
  internal static GameStartupLaunch PendingStartup(Engine e,DateTime now){try{if(!Pending(e))return null;var record=Load(e);DateTime created;if(record.ConfigurationMode!=null||record.SnapshotId!=null||record.Kind!="normal"||record.State!="launched"||record.Pid!=0||!DateTime.TryParse(record.CreatedUtc,out created))return null;created=created.ToUniversalTime();if(created>now||now-created>=TimeSpan.FromSeconds(180))return null;return new GameStartupLaunch{SessionId=record.Nonce,GamePath=e.Game,UserPath=e.User,DataPath=e.Data,StartedUtc=created,TargetKind="normal",Stage="submitted"};}catch{return null;}}
  // Only identities verified and terminated by the confirmed close preview can
  // end Steam's no-PID grace early. Bind that proof to the session seen before
  // closing; a delayed close must never force recovery of a replacement session.
  internal static string RecoverStopped(Engine e,string nonce,IEnumerable<ClientIdentity> stopped){
   if(nonce==null)return "";
   using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;try{
    try{acquired=mutex.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}
    if(!acquired)return "另一个启动任务正在准备，请稍后恢复。";
    if(!Pending(e))return "";var record=Load(e);if(record.Nonce!=nonce)return "旧会话已结束；未改动新会话。";
    DateTime created;bool ended=record.State=="launched"&&stopped.Any(client=>record.Pid>0?client.Pid==record.Pid&&client.Birth==record.Birth:record.Kind=="normal"&&DateTime.TryParse(record.CreatedUtc,out created)&&client.Birth>=created.ToUniversalTime().Ticks);
    return Recover(e,ended,nonce);
   }finally{if(acquired)mutex.ReleaseMutex();}}
  }
  internal static string[] PersistentMods(Engine e,IEnumerable<string> mods){var ids=mods.Where(id=>id!=PresentationBridge.Id).ToArray();if(!Pending(e))return ids;try{var record=Load(e);var translations=ServerLaunchTranslations.OwnedIds(e,record.Files.Keys);return ids.Where(id=>id!=ModId&&!translations.Contains(id)).ToArray();}catch{return ids;}}
  // The caller holds the default-writer lease. Only a valid owned applied snapshot
  // is rebased; a player's current edits must keep their existing recovery behavior.
  internal static byte[] RetirePresentationRecord(Engine e,byte[] before,byte[] after){if(!Pending(e))return null;var record=Load(e);bool changed=false;if(record.Original!=null){byte[] original=Convert.FromBase64String(record.Original),clean=PresentationBridge.RemoveRow(original);if(!original.SequenceEqual(clean)){record.Original=Convert.ToBase64String(clean);changed=true;}}if(before!=null&&DlssInstaller.Hash(before)==record.AppliedHash&&!PlayProfileTransaction.Same(before,after)){record.AppliedHash=DlssInstaller.Hash(after);changed=true;}return changed?Encoding.UTF8.GetBytes(Json.Serialize(record)):null;}
  public static void ExpireRequest(Engine e,string expectedNonce){using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired||!Pending(e))return;var record=Load(e);DateTime created;if(record.Nonce!=expectedNonce||!DateTime.TryParse(record.CreatedUtc,out created)||DateTime.UtcNow-created.ToUniversalTime()<TimeSpan.FromSeconds(RequestLifetimeSeconds))return;bool hasRequest=false;foreach(string request in RequestFiles(e)){LauncherMaintenance.NoReparse(request);if(File.Exists(request)&&new FileInfo(request).Length>0)hasRequest=true;}if(hasRequest){ClearRequest(e);Atomic(LuaFile(e,StatusName),Encoding.UTF8.GetBytes("expired-or-invalid-request\n"));}}finally{if(acquired)mutex.ReleaseMutex();}}}
  static bool Alive(Process process){if(process==null)return false;try{return !process.HasExited;}catch{return false;}}
  static void Timed(Action<string,long> timing,string phase,Stopwatch watch){if(timing!=null)try{timing(phase,watch.ElapsedMilliseconds);}catch{ /* Timing is diagnostic and cannot change the launch transaction. */ }}
  static bool RecordedAlive(ProfileSessionRecord record){if(record.Pid<=0||record.Birth<=0)return false;try{using(var process=Process.GetProcessById(record.Pid))return !process.HasExited&&process.StartTime.ToUniversalTime().Ticks==record.Birth;}catch(ArgumentException){return false;}catch(InvalidOperationException){return false;}catch{return true;}}
  static string Clean(string s,int maximum=256){s=s??"";if(s.Length>maximum||s.Any(c=>c<' '||c=='\u007f'))throw new Exception("启动配置包含无效长度或控制字符。");return s;}
  public static string BuildDefault(string original,string kind,string[] retained){
   if(kind!="save"&&kind!="server"&&kind!="normal")throw new Exception("未知启动类型。");var mods=Regex.Matches(original,@"(?m)^\s*mod\s*=\s*([^,\r\n]+)\s*,").Cast<Match>().Select(m=>m.Groups[1].Value.Trim()).ToList();
   var keep=kind=="server"?mods.Where(id=>retained.Contains(id.TrimStart('\\'))).ToList():mods;keep.RemoveAll(x=>x==ModId);keep.Insert(0,ModId);
   var block=Regex.Match(original,@"\bmods\s*\{(?<body>[^}]*)\}");if(!block.Success||Regex.Matches(original,@"\bmods\s*\{").Count!=1)throw new Exception("无法安全识别本地模组清单。");
   string body="\r\n"+string.Join("",keep.Distinct().Select(id=>"    mod = "+id+",\r\n"));string result=original.Substring(0,block.Groups["body"].Index)+body+original.Substring(block.Groups["body"].Index+block.Groups["body"].Length);
   if(kind=="server"){var maps=Regex.Match(result,@"\bmaps\s*\{(?<body>[^}]*)\}");if(!maps.Success)throw new Exception("本地地图清单无法识别。");result=result.Substring(0,maps.Groups["body"].Index)+"\r\n"+result.Substring(maps.Groups["body"].Index+maps.Groups["body"].Length);}
   return result;
  }
  internal static string[] Retained(Engine e){return Retained(e,null);}
  internal static string[] Retained(Engine e,LaunchReadBudget budget){if(LegacyBuild(e))return new string[0];var list=new List<string>(ServerLaunchTranslations.Ids);var active=new HashSet<string>(SaveProfiles.DefaultMods(SaveProfiles.DefaultBytes(e)).Select(id=>id.TrimStart('\\')),StringComparer.Ordinal);if(!CoreComponents.FrameworkReady(e))return list.ToArray();list.Add("ZombieBuddy");if(active.Contains(ChinesePatch.Id))list.Add(ChinesePatch.Id);if(!active.Contains("Viewpoint"))return list.ToArray();list.Add("Viewpoint");if(active.Contains("PZVoxelStudioViewpoint")&&e.HasModels)list.Add("PZVoxelStudioViewpoint");if(active.Contains(ShootClient.Id))list.Add(ShootClient.Id);foreach(var addon in ViewpointAddons.All.Where(a=>active.Contains(a.Id))){try{var files=ViewpointAddons.Find(e,addon,budget);if(addon.Key=="weather"&&files.Jar!=null||ViewpointAddons.Preload(files,budget))list.Add(addon.Id);}catch{if(budget!=null)budget.Unknown();}}return list.ToArray();}
  static void Atomic(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".orange-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temporary,bytes);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  static byte[] Script(){return BundledAssets.ReadOwnAsset("OrangeLaunchBridge.lua");}
  internal static bool LegacyBuild(Engine e){var version=GameVersionProbe.Read(e.Game);return version!=null&&version.Major==41;}
  static Dictionary<string,byte[]> BridgeFiles(string folder,bool legacy){
   var info=Encoding.UTF8.GetBytes("name=Orange Launcher Session\nid="+ModId+"\ndescription=一次性客户端启动桥接，不修改服务器。\n");
   if(legacy)return new Dictionary<string,byte[]>{{Path.Combine(folder,"mod.info"),info},{Path.Combine(folder,"media","lua","client","OrangeLaunchBridge.lua"),Script()}};
   return new Dictionary<string,byte[]>{{Path.Combine(folder,"common","mod.info"),info},{Path.Combine(folder,"42","mod.info"),Encoding.UTF8.GetBytes("name=Orange Launcher Session\nid="+ModId+"\n")},{Path.Combine(folder,"42","media","lua","client","OrangeLaunchBridge.lua"),Script()}};
  }
  static string QuoteArgument(string value){var result=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"'){result.Append('\\',slashes*2+1);result.Append(c);}else{result.Append('\\',slashes);result.Append(c);}slashes=0;}return result.Append('\\',slashes*2).Append('"').ToString();}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct WindowsVersionInfo {public int Size,Major,Minor,Build,Platform;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string ServicePack;}
  [DllImport("ntdll.dll",CharSet=CharSet.Unicode)]static extern int RtlGetVersion(ref WindowsVersionInfo version);
  internal static Version CurrentWindowsVersion(){try{var version=new WindowsVersionInfo{Size=Marshal.SizeOf(typeof(WindowsVersionInfo))};if(RtlGetVersion(ref version)==0&&version.Major>0)return new Version(version.Major,version.Minor,version.Build);}catch{}return Environment.OSVersion.Version;}
  internal static string[] FrozenJavaArguments(Dictionary<string,object> config,Version windowsVersion){
   Func<object,string[]> strings=node=>{var sequence=node as System.Collections.IEnumerable;if(node is string||node is System.Collections.IDictionary||sequence==null)throw new IOException("快照启动参数必须为文本数组。");var items=sequence.Cast<object>().ToArray();if(items.Any(item=>!(item is string)))throw new IOException("快照启动参数必须为文本。");return items.Cast<string>().ToArray();};object value;string main=config!=null&&config.TryGetValue("mainClass",out value)?value as string:null;if(string.IsNullOrWhiteSpace(main)||!config.ContainsKey("classpath")||!config.ContainsKey("vmArgs"))throw new IOException("快照启动配置缺少主类或参数。");var arguments=new List<string>(strings(config["vmArgs"]));
   if(config.TryGetValue("windows",out value)){var windows=value as Dictionary<string,object>;if(windows==null)throw new IOException("快照 Windows 启动配置无效。");string branch=windows.Keys.Where(key=>{Version version;return Version.TryParse(key,out version)&&version<=windowsVersion;}).OrderByDescending(key=>new Version(key)).FirstOrDefault();if(branch!=null){var selected=windows[branch] as Dictionary<string,object>;if(selected==null)throw new IOException("快照 Windows 启动分支无效。");if(selected.TryGetValue("vmArgs",out value))arguments.AddRange(strings(value));}}
   arguments.Add("-cp");arguments.Add(string.Join(";",strings(config["classpath"])));arguments.Add(main.Replace('/','.'));return arguments.ToArray();
  }
  internal static ProcessStartInfo BuildStartInfo(Engine e){
   if(e.LiveSession!=null&&e.LiveSession.ServerComponents!=null)return ServerStartInfo(e);
   if(e.OwnedLaunch){int major=(GameVersionProbe.Read(e.Game)??new Version(42,0)).Major;var own=e.OwnedConfiguration.BuildStartInfo(major);if(e.LiveSession!=null){var arguments=e.OwnedConfiguration.StockArguments(major).ToList();arguments.InsertRange(arguments.IndexOf("-cp"),e.LiveSession.VmArguments);own.Arguments=string.Join(" ",arguments.Select(LauncherConfiguration.QuoteArgument));}return own;}

   foreach(string path in new[]{e.Game,e.User,Path.Combine(e.Game,"ProjectZomboid64.exe")})LauncherMaintenance.NoReparse(path);
   var start=new ProcessStartInfo(Path.Combine(Path.GetFullPath(e.Game),"ProjectZomboid64.exe")){UseShellExecute=false,WorkingDirectory=Path.GetFullPath(e.Game)};
   // The native launcher embeds jvm.dll; its instrument.dll dependencies must
   // resolve beside this game's JRE instead of a different JDK inherited in PATH.
   start.EnvironmentVariables["PATH"]=Path.Combine(Path.GetFullPath(e.Game),"jre64","bin")+";"+(start.EnvironmentVariables["PATH"]??"");
   if(e.FrozenEnvironment!=null){
    // The native JSON launcher passes UTF-8 option bytes through the system code
    // page. Java's Unicode command line preserves the frozen paths verbatim.
    try{LauncherMaintenance.NoReparse(e.Config);Dictionary<string,object> config;using(var input=new SnapshotFileSystem.Stream(e.Config,FileMode.Open,FileAccess.Read,FileShare.Read)){if(input.Length>4194304)throw new IOException("快照启动配置超过读取上限。");using(var reader=new StreamReader(input,new UTF8Encoding(false,true),true))config=new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());}
     string[] arguments=FrozenJavaArguments(config,CurrentWindowsVersion());start.FileName=Path.Combine(Path.GetFullPath(e.Game),"jre64","bin","java.exe");LauncherMaintenance.NoReparse(start.FileName);start.CreateNoWindow=true;start.Arguments=string.Join(" ",arguments.Select(QuoteArgument));
    }catch(Exception error){throw new IOException("无法读取已验证快照的 Java 启动配置。",error);}
   }
   if(e.ManagedVersion||e.FrozenEnvironment!=null){string cache=Path.GetFullPath(e.User);if(cache.Any(char.IsControl))throw new IOException("独立存档目录包含无效字符。");start.Arguments=(string.IsNullOrEmpty(start.Arguments)?"":start.Arguments+" ")+QuoteArgument("-cachedir="+cache);start.EnvironmentVariables["SteamAppId"]="108600";start.EnvironmentVariables["SteamGameId"]="108600";}return start;
  }
  static void PrivateRequest(string path,string text){LauncherMaintenance.NoReparse(path);if(File.Exists(path))throw new Exception("存在尚未清理的启动请求，拒绝覆盖。");var security=new FileSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));using(var file=new FileStream(path,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.None,security)){byte[] bytes=new UTF8Encoding(false).GetBytes(text);file.Write(bytes,0,bytes.Length);}}
  static string Payload(ProfileLaunchRequest r){Clean(r.Kind);Clean(r.Id);if(r.Kind=="save"){if(string.IsNullOrWhiteSpace(r.Mode)||string.IsNullOrWhiteSpace(r.Save)||new[]{r.Mode,r.Save}.Any(s=>s=="."||s==".."||s.IndexOfAny(new[]{'\\','/',':'})>=0)||r.Mode=="Multiplayer")throw new Exception("存档标识无效。");}else if(r.Kind=="server"){if(string.IsNullOrWhiteSpace(r.Host)||r.Port<1||r.Port>65535||string.IsNullOrWhiteSpace(r.Username))throw new Exception("服务器连接配置不完整。");}else if(r.Kind!="normal")throw new Exception("未知启动目标。");
   string nonce=r.StartupSessionId;if(string.IsNullOrEmpty(nonce))nonce=Guid.NewGuid().ToString("N");else if(!Regex.IsMatch(nonce,@"^[a-f0-9]{32}$"))throw new Exception("启动会话标识无效。");
   return string.Join("\n",new[]{"ORANGE1",nonce,DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),r.Kind,Clean(r.Mode),Clean(r.Save),Clean(r.Host),r.Port.ToString(),Clean(r.Username),Clean(r.UserPassword,1024),Clean(r.ServerPassword,1024),r.UseSteamRelay?"1":"0"})+"\n";
  }
  public static void Stage(Engine e,ProfileLaunchRequest r){using(SaveEnvironmentRuntime.AcquireRevision(e))StageCore(e,r);}
  static void StageCore(Engine e,ProfileLaunchRequest r){
   if(r.Kind=="server"&&!LegacyBuild(e)){StageServer(e,r);return;}if(r.Kind!="server"&&r.ServerComponents!=null)throw new IOException("服务器组件模式仅用于服务器连接。");
   if(e.OwnedLaunch){StageOwned(e,r);return;}
   var watch=Stopwatch.StartNew();
   e.GuardLaunch();foreach(string path in new[]{e.User,e.Mods,e.Data,Journal(e)})LauncherMaintenance.NoReparse(path);if(Pending(e))throw new Exception("上一次启动配置尚未恢复，请查看启动状态。");string payload=Payload(r);
   byte[] original=File.Exists(e.Mods)?File.ReadAllBytes(e.Mods):null;if(original!=null&&original.Length>1048576)throw new Exception("模组清单异常过大。");string text=original==null?"VERSION = 1,\r\nmods\r\n{\r\n}\r\nmaps\r\n{\r\n}\r\n":new UTF8Encoding(false,true).GetString(original).TrimStart('\uFEFF');string next=BuildDefault(text,r.Kind,Retained(e));var translations=r.Kind=="server"&&!LegacyBuild(e)?ServerLaunchTranslations.Build(e,SaveProfiles.DefaultMods(original)):new Dictionary<string,byte[]>();if(translations.Count>0){foreach(string id in ServerLaunchTranslations.Ids.Where(id=>translations.ContainsKey(Path.Combine(ServerLaunchTranslations.Root(e,id),"42","mod.info")))){string root=ServerLaunchTranslations.Root(e,id);LauncherMaintenance.NoReparse(root);if(Directory.Exists(root))throw new Exception("发现已有同名联机汉化目录，未覆盖。");}byte[] nextBytes=Encoding.UTF8.GetBytes(next);next=Encoding.UTF8.GetString(SaveProfiles.ReplaceDefaultMods(nextBytes,SaveProfiles.DefaultMods(nextBytes).Concat(ServerLaunchTranslations.GeneratedIds(e,SaveProfiles.DefaultMods(original),translations)).Distinct().ToArray()));}byte[] applied=new UTF8Encoding(original!=null&&original.Length>=3&&original[0]==239&&original[1]==187&&original[2]==191).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(next)).ToArray();
   string folder=Path.Combine(e.User,"mods",ModId);LauncherMaintenance.NoReparse(folder);if(Directory.Exists(folder))throw new Exception("发现已有同名启动桥接目录，未覆盖。");string request=LuaFile(e,RequestName),status=LuaFile(e,StatusName);LauncherMaintenance.NoReparse(status);foreach(string oldRequest in RequestFiles(e)){LauncherMaintenance.NoReparse(oldRequest);if(File.Exists(oldRequest))throw new Exception("存在上次启动请求，未覆盖。");}
   var record=new ProfileSessionRecord{ConfigurationId=e.LaunchConfiguration==null?null:e.LaunchConfiguration.Id,SnapshotId=e.FrozenEnvironment==null?null:e.FrozenEnvironment.Id,SnapshotManifestHash=e.FrozenEnvironment==null?null:e.FrozenEnvironment.ManifestHash,Game=e.Game,User=e.User,Original=original==null?null:Convert.ToBase64String(original),AppliedHash=DlssInstaller.Hash(applied),CreatedUtc=DateTime.UtcNow.ToString("o"),State="prepared",Kind=r.Kind,Token=Key(e),Nonce=payload.Split('\n')[1],CaptureEndpoint=r.Kind=="server"?r.Host+":"+r.Port:null};
   if(record.CaptureEndpoint!=null){try{record.CaptureRevision=ServerWorkshopCatalog.Revision(e.Data,record.CaptureEndpoint);}catch{record.CaptureRevision=null;}}
   var files=BridgeFiles(folder,LegacyBuild(e));
   foreach(var file in translations)files.Add(file.Key,file.Value);foreach(var file in files)record.Files[file.Key]=DlssInstaller.Hash(file.Value);
   ValidateRecord(e,record);byte[] journalBytes=Encoding.UTF8.GetBytes(Json.Serialize(record));if(journalBytes.Length>MaxJournalBytes)throw new Exception("联机汉化文件记录过大，未改动配置。");Timed(r.Timing,"stage-plan",watch);if(r.ValidateBeforeStage!=null)r.ValidateBeforeStage();watch.Restart();Atomic(Journal(e),journalBytes);
   try{foreach(var file in files)Atomic(file.Key,file.Value);Directory.CreateDirectory(Path.GetDirectoryName(request));PrivateRequest(request,payload);Atomic(status,Encoding.UTF8.GetBytes("prepared\n"));if(!SameOriginal(e.Mods,original))throw new Exception("准备期间本地模组清单改变，拒绝覆盖。");if(!e.TestMode&&e.RuntimeRunning(true))throw new Exception("客户端或无法核实用途的游戏 Java 已运行，取消重复启动。");Atomic(e.Mods,applied);}catch{Recover(e,true);throw;}finally{Timed(r.Timing,"stage-write",watch);}
  }
  static bool SameOriginal(string file,byte[] original){return original==null?!File.Exists(file):File.Exists(file)&&File.ReadAllBytes(file).SequenceEqual(original);}
  // Cached monitor images validate exact owned paths without loading UI assets.
  static void ValidateRecord(Engine e,ProfileSessionRecord record){if(record.ServerRuntimeRoot!=null){ValidateServerRecord(e,record);return;}if(record.SnapshotId!=null){var snapshot=SaveEnvironments.FindOwned(e.Data,e.Game,e.User);if(snapshot==null||snapshot.Id!=record.SnapshotId||snapshot.ManifestHash!=record.SnapshotManifestHash)throw new IOException("启动记录的快照版本已变化，未覆盖快照文件。");}if(record.ConfigurationMode!=null){ValidateOwnedRecord(e,record);return;}if(record.Files==null||record.Files.Count<2||record.Files.Count>4096||!Regex.IsMatch(record.Nonce??"",@"^[a-f0-9]{32}$")||!Regex.IsMatch(record.AppliedHash??"",@"^[A-F0-9]{64}$"))throw new Exception("启动恢复记录缺少有效会话及文件校验。");string folder=Path.Combine(e.User,"mods",ModId);bool legacy=record.Files.ContainsKey(Path.Combine(folder,"mod.info"));var owned=legacy?new[]{Path.Combine(folder,"mod.info"),Path.Combine(folder,"media","lua","client","OrangeLaunchBridge.lua")}:new[]{Path.Combine(folder,"common","mod.info"),Path.Combine(folder,"42","mod.info"),Path.Combine(folder,"42","media","lua","client","OrangeLaunchBridge.lua")};var allowed=new HashSet<string>(owned.Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(var file in record.Files){string path=Path.GetFullPath(file.Key);if((!allowed.Contains(path)&&!ServerLaunchTranslations.OwnedPath(e,path))||!seen.Add(path)||!Regex.IsMatch(file.Value??"",@"^[A-F0-9]{64}$"))throw new Exception("启动恢复记录文件越界或校验无效。");LauncherMaintenance.NoReparse(path);}if(!allowed.IsSubsetOf(seen))throw new Exception("启动恢复记录缺少启动脚本。");if(record.Original!=null&&Convert.FromBase64String(record.Original).Length>1048576)throw new Exception("启动恢复原文件异常过大。");}
  static ProfileSessionRecord Load(Engine e){LauncherMaintenance.NoReparse(Journal(e));if(new FileInfo(Journal(e)).Length>MaxJournalBytes)throw new Exception("启动恢复记录异常过大。");var r=Json.Deserialize<ProfileSessionRecord>(File.ReadAllText(Journal(e)));if(r==null||!string.Equals(Path.GetFullPath(r.Game),Path.GetFullPath(e.Game),StringComparison.OrdinalIgnoreCase)||!string.Equals(Path.GetFullPath(r.User),Path.GetFullPath(e.User),StringComparison.OrdinalIgnoreCase)||r.Token!=Key(e))throw new Exception("启动恢复记录与游戏目录不一致。");ValidateRecord(r.ConfigurationMode==null?e:OwnedRecoveryEngine(e,r),r);return r;}
  static void ClearRequest(Engine e){foreach(string file in RequestFiles(e)){LauncherMaintenance.NoReparse(file);if(File.Exists(file)){Atomic(file,new byte[0]);File.Delete(file);}}}
  static string StageText(string value){if(value=="menu-ready")return "游戏主菜单已就绪。";if(value=="prepared")return "正在准备一次性启动目标。";if(value=="submitted-save")return "读档请求已交给游戏；授权、版本转换等提示仍由游戏处理。";if(value=="submitted-server")return "服务器连接请求已提交。";if(value=="connection-failed")return "服务器连接失败；请查看游戏连接提示和日志后重试。";if(value.StartsWith("failed")||value.StartsWith("invalid")||value.StartsWith("expired"))return "自动直达未完成，请在游戏原入口继续；退出后恢复本地清单。";return value==""?"":"启动状态需检查。";}
  static string RecoverUnlocked(Engine e,bool force=false){if(!Pending(e))return "";if(!e.TestMode&&GameProcessGuard.IsSessionRunning(e.Game,e.ProcessInventory))return "客户端或无法核实用途的游戏 Java 仍在运行；退出后清理启动会话，保留其间编辑的配置。";var r=Load(e);if(!e.TestMode&&RecordedAlive(r))return "本次启动记录的进程仍在运行，暂不清理启动会话。";DateTime created;if(!force&&r.State=="prepared"&&DateTime.TryParse(r.CreatedUtc,out created)&&DateTime.UtcNow-created.ToUniversalTime()<TimeSpan.FromSeconds(90))return "启动请求准备中，暂不覆盖临时配置。";if(!force&&r.ConfigurationMode==null&&r.SnapshotId==null&&r.Kind=="normal"&&r.State=="launched"&&r.Pid==0&&DateTime.TryParse(r.CreatedUtc,out created)&&DateTime.UtcNow-created.ToUniversalTime()<TimeSpan.FromSeconds(180))return "正在等待 Steam 启动本次客户端，暂不覆盖临时配置。";
   if(!string.IsNullOrEmpty(r.CaptureEndpoint))try{if(!ServerWorkshopCatalog.Capture(e.Data,r.CaptureEndpoint,r.Nonce,LuaFile(e,"OrangeLauncherWorkshop.txt"),r.CaptureRevision))new LauncherJournal(e.Data).Diagnostic("服务器工坊连接记录缺失或不完整，保留已有清单。");}catch(Exception ex){try{new LauncherJournal(e.Data).Diagnostic("服务器工坊连接记录读取失败："+ex.Message);}catch{}}
   if(r.ServerRuntimeRoot!=null)return RecoverServer(e,r);
   if(r.ConfigurationMode!=null)return RecoverOwned(OwnedRecoveryEngine(e,r),r);
   LauncherMaintenance.NoReparse(e.Mods);byte[] original=r.Original==null?null:Convert.FromBase64String(r.Original);bool applied=File.Exists(e.Mods)&&DlssInstaller.HashFile(e.Mods)==r.AppliedHash;bool edited=!applied&&!SameOriginal(e.Mods,original);
   if(applied){if(original==null)File.Delete(e.Mods);else Atomic(e.Mods,original);}else if(edited&&File.Exists(e.Mods)){byte[] current=File.ReadAllBytes(e.Mods);string text=new UTF8Encoding(false,true).GetString(current);string pattern="(?:"+string.Join("|",new[]{ModId}.Concat(ServerLaunchTranslations.OwnedIds(e,r.Files.Keys)).Select(Regex.Escape))+")";string clean=Regex.Replace(text,@"(?m)^[ \t]*mod[ \t]*=[ \t]*"+pattern+@"[ \t]*,[^\r\n]*(?:\r?\n|$)","");if(clean!=text){if(!SameOriginal(e.Mods,current))throw new IOException("清理期间本地模组清单已改变，请重试；已保留玩家编辑。");Atomic(e.Mods,new UTF8Encoding(false).GetBytes(clean));}}ClearRequest(e);string folder=Path.Combine(e.User,"mods",ModId);foreach(var file in r.Files){LauncherMaintenance.NoReparse(file.Key);if(File.Exists(file.Key)&&DlssInstaller.HashFile(file.Key)==file.Value)File.Delete(file.Key);}ServerLaunchTranslations.RemoveEmptyParents(e,r.Files.Keys);
   foreach(string directory in new[]{Path.Combine(folder,"42","media","lua","client"),Path.Combine(folder,"42","media","lua"),Path.Combine(folder,"42","media"),Path.Combine(folder,"42"),Path.Combine(folder,"common"),Path.Combine(folder,"media","lua","client"),Path.Combine(folder,"media","lua"),Path.Combine(folder,"media"),folder}){LauncherMaintenance.NoReparse(directory);if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);}
   File.Delete(Journal(e));return edited?"已保留会话期间修改的本地模组清单，并清理启动会话。":"已恢复启动前的本地模组清单；未修改存档或服务器配置。";
  }
  public static object State(Engine e){string warning="";try{if(Pending(e))warning="存在一次性启动会话；可继续编辑配置，退出后清理会话并保留期间的编辑。";string stage="";foreach(string status in new[]{LuaFile(e,StatusName),LuaFile(e,"OrangeLauncherSession.status"),Path.Combine(e.User,"OrangeLauncherSession.status")}){LauncherMaintenance.NoReparse(status);if(File.Exists(status)){if(new FileInfo(status).Length<=1024)stage=File.ReadAllText(status).Trim();break;}}return new{pending=Pending(e),stage=StageText(stage),warning=warning};}catch(Exception ex){return new{pending=Pending(e),stage="恢复需检查",warning=ex.Message};}}
  internal static string RecoverWatched(Engine e,string nonce){using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;try{while(!acquired){try{acquired=mutex.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}if(!acquired){if(!Pending(e))return "";if(Load(e).Nonce!=nonce)return "旧会话已结束；未改动新会话。";}}return Recover(e,true,nonce);}finally{if(acquired)mutex.ReleaseMutex();}}}
  // Steam keeps its original URI and arguments; the owned one-shot mod only observes menu readiness.
  internal static GameLaunchOutcome LaunchNormal(Engine e,string sessionId,Action openSteam,Action validateBeforeStage=null,Action<string,long> timing=null,CancellationToken cancellation=default(CancellationToken)){
   if(e.TestMode)throw new Exception("测试模式禁止真实游戏启动。");
   if(e.OwnedLaunch||e.ManagedVersion)return Launch(e,new ProfileLaunchRequest{Kind="normal",StartupSessionId=sessionId,ValidateBeforeStage=validateBeforeStage,Timing=timing,Cancellation=cancellation});
   if(openSteam==null)throw new ArgumentNullException("openSteam");
   var watch=Stopwatch.StartNew();string reason=LauncherLaunch.Reason(e,false,e.RuntimeRunning(true));Timed(timing,"launch-check",watch);if(reason!="")throw new Exception(reason);
   using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;
    try{
     try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
     if(!acquired)throw new Exception("另一个启动器正在准备游戏。");
     watch.Restart();Recover(e);Timed(timing,"session-recover",watch);Stage(e,new ProfileLaunchRequest{Kind="normal",StartupSessionId=sessionId,ValidateBeforeStage=validateBeforeStage,Timing=timing,Cancellation=cancellation});
     watch.Restart();try{openSteam();}catch{Recover(e,true);throw;}finally{Timed(timing,"steam-submit",watch);}
     watch.Restart();
     var record=Load(e);record.State="launched";Atomic(Journal(e),Encoding.UTF8.GetBytes(Json.Serialize(record)));
     Timed(timing,"session-record",watch);watch.Restart();try{using(var worker=ManagedBackgroundWorker.Start("--session-watch",Key(e),record.Nonce)){} }
     catch{return new GameLaunchOutcome{message="游戏启动请求已发送，但退出监控未启动；请保留启动器，退出后刷新状态恢复配置。",stage="游戏启动请求已发送",warning="启动监控未能启动，请在退出游戏后打开“修复”处理启动状态。"};}
     finally{Timed(timing,"session-worker",watch);}
     return new GameLaunchOutcome{message="已通过 Steam 请求启动游戏。",stage="游戏启动请求已发送"};
    }finally{if(acquired)mutex.ReleaseMutex();}
   }
  }
  // A Steam URI does not supply a PID. Only a new client with a verified image/role can bind this session.
  internal static Process FindNormalClient(string game,long after){
   foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32","java","javaw"}){
    Process found=null;
    foreach(var p in Process.GetProcessesByName(name)){
     try{
      long birth=p.StartTime.ToUniversalTime().Ticks;string image=p.MainModule.FileName;
      bool matches=name.StartsWith("ProjectZomboid",StringComparison.Ordinal)
       ?string.Equals(Path.GetFullPath(image),Path.Combine(Path.GetFullPath(game),name+".exe"),StringComparison.OrdinalIgnoreCase)
       :GameProcessGuard.ReadJavaRole(game,name,image,p)==JavaProcessRole.Client;
      if(found==null&&matches&&!p.HasExited&&birth>=after){
       bool verified;
       using(var current=Process.GetProcessById(p.Id))verified=!current.HasExited&&current.StartTime.ToUniversalTime().Ticks==birth&&string.Equals(current.MainModule.FileName,image,StringComparison.OrdinalIgnoreCase);
       if(verified){found=p;continue;}
      }
     }catch{}
     p.Dispose();
    }
    if(found!=null)return found;
   }
   return null;
  }
  internal static int WatchNormal(Engine e,string nonce,TimeSpan maximumWait,int pollMilliseconds){
   if(maximumWait<TimeSpan.Zero||maximumWait>TimeSpan.FromSeconds(180)||pollMilliseconds<1)return 1;
   Process client=null;
   try{
    if(!Pending(e))return 0;
    var record=Load(e);if(record.Nonce!=nonce)return 0;DateTime created;
    if(record.Kind!="normal"||record.State!="launched"||!DateTime.TryParse(record.CreatedUtc,out created))return 1;
    long after=created.ToUniversalTime().Ticks;if(DateTime.UtcNow.Ticks<after)return 1;
    var wait=Stopwatch.StartNew();
    while(client==null&&DateTime.UtcNow.Ticks-after<=TimeSpan.FromSeconds(180).Ticks&&wait.Elapsed<maximumWait){
     if(!Pending(e)||Load(e).Nonce!=nonce)return 0;
     client=FindNormalClient(e.Game,after);if(client==null)Thread.Sleep(pollMilliseconds);
    }
    if(client!=null){
     using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;
      try{
       try{acquired=mutex.WaitOne(5000);}catch(AbandonedMutexException){acquired=true;}
       if(!acquired)return 1;if(!Pending(e))return 0;
       record=Load(e);if(record.Nonce!=nonce)return 0;
       record.Pid=client.Id;record.Birth=client.StartTime.ToUniversalTime().Ticks;
       Atomic(Journal(e),Encoding.UTF8.GetBytes(Json.Serialize(record)));
      }finally{if(acquired)mutex.ReleaseMutex();}
     }
     while(Alive(client)||e.RuntimeRunning(true)){ExpireRequest(e,nonce);Thread.Sleep(pollMilliseconds);}
    }
    RecoverWatched(e,nonce);return 0;
   }finally{if(client!=null)client.Dispose();}
  }
  internal static ProfileSessionRecord RecordStartedProcess(Engine e,Process process){var record=Load(e);record.Pid=process.Id;record.Birth=process.StartTime.ToUniversalTime().Ticks;record.State="launched";Atomic(Journal(e),Encoding.UTF8.GetBytes(Json.Serialize(record)));return record;}
  public static GameLaunchOutcome Launch(Engine e,ProfileLaunchRequest r){
   if(e.TestMode)throw new Exception("测试模式禁止真实游戏启动。");
   var watch=Stopwatch.StartNew();string reason=r.Kind=="server"?ServerReason(e,false,e.RuntimeRunning(true)):LauncherLaunch.Reason(e,false,e.RuntimeRunning(true));Timed(r.Timing,"launch-check",watch);if(reason!="")throw new Exception(reason);
   using(var mutex=new Mutex(false,MutexName(e))){bool acquired=false;try{
    try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired)throw new Exception("另一个启动器正在准备游戏。");
    watch.Restart();Recover(e);Timed(r.Timing,"session-recover",watch);Stage(e,r);
    Process game;
    watch.Restart();try{r.Cancellation.ThrowIfCancellationRequested();var start=BuildStartInfo(e);game=Process.Start(start);}catch{Recover(e,true);throw;}finally{Timed(r.Timing,"process-submit",watch);}
    watch.Restart();var record=RecordStartedProcess(e,game);game.Dispose();Timed(r.Timing,"session-record",watch);
    watch.Restart();try{using(var worker=ManagedBackgroundWorker.Start("--session-watch",Key(e),record.Nonce)){} }
    catch{return new GameLaunchOutcome{message="游戏已启动，但退出监控未启动；请保留启动器，退出后刷新状态恢复配置。",stage="游戏启动请求已发送",warning="启动监控未能启动，请在退出游戏后打开“修复”处理启动状态。"};}
    finally{Timed(r.Timing,"session-worker",watch);}
    return new GameLaunchOutcome{message="游戏已启动，正在准备所选目标。",stage="游戏启动请求已发送"};
   }finally{if(acquired)mutex.ReleaseMutex();}}
  }
  public static int Watch(string token,string nonce){try{if(!Regex.IsMatch(token??"",@"^[A-F0-9]{64}$")||!Regex.IsMatch(nonce??"",@"^[a-f0-9]{32}$"))return 1;string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PZ3DManager"),journal=Path.Combine(data,"launch-sessions",token+".json");LauncherMaintenance.NoReparse(journal);if(!File.Exists(journal))return 0;var r=Json.Deserialize<ProfileSessionRecord>(File.ReadAllText(journal));string user=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Zomboid");if(!VersionLibrary.OwnsUser(data,r.Game,r.User)||r.Token!=token||r.Nonce!=nonce||!GameDiscovery.IsGameFolder(r.Game))return 1;var e=new Engine(r.Game,r.User,data){ManagedVersion=!VersionLibrary.Same(r.User,user)};Load(e);if(r.ConfigurationMode!=null)RestoreOwnedContext(e,r);if(r.SnapshotId!=null)e.FrozenEnvironment=SaveEnvironments.FindOwned(e.Data,e.Game,e.User);if(r.Kind=="normal"&&r.ConfigurationMode==null&&e.FrozenEnvironment==null)return WatchNormal(e,nonce,TimeSpan.FromSeconds(180),1000);Process original=null;try{original=Process.GetProcessById(r.Pid);if(original.StartTime.ToUniversalTime().Ticks!=r.Birth){original.Dispose();original=null;}}catch{}try{while(Alive(original)||e.RuntimeRunning(true)){ExpireRequest(e,nonce);Thread.Sleep(2000);}}finally{if(original!=null)original.Dispose();}Thread.Sleep(2000);RecoverWatched(e,nonce);return 0;}catch{return 1;}}
 }
}
