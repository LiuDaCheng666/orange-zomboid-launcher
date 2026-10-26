using System;
using System.Threading;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.ComponentModel;
namespace PZ3D {
 internal sealed class BuddyTakeoverPreview {
  public string stamp,message,recoveryState,scope,steamState,scanState;
  public BuddyRecoveryState recovery;
  public bool supported,knownIdentity,requiresSteamExit,hasConflicts;
  public string[] locations;
 }
 internal sealed class BuddyTakeoverResult {
  public string status,phase,message,scope;
  public bool cleanupCompleted,rollbackCompleted,steamVerified,steamExitRequested,steamReopenRequested,recoveryPending,componentInstallCompleted;
 }
 internal sealed class BuddyRecoveryState {
  public bool pending,supported,requiresSteamExit;
  public string stamp,state,message;
  public string[] locations;
 }
 internal enum BuddySteamState { Running, Exited, Changed }
 // Native evidence only; never serialize to the bridge.
 internal sealed class BuddySteamIdentity {
  internal SteamLaunchOptionsSnapshot Options;
  internal bool Native; internal int Pid; internal long Birth; internal string Executable;
  internal BuddySteamIdentity(SteamLaunchOptionsSnapshot options,int pid=0,long birth=0,string executable=null){Options=options;Pid=pid;Birth=birth;Executable=executable;}
 }
 internal abstract class BuddyTakeoverEnvironment {
  internal abstract BuddySteamIdentity Capture();
  internal abstract BuddySteamState Inspect(BuddySteamIdentity bound);
  internal abstract bool GameRunning(string game);
  internal abstract void Shutdown(BuddySteamIdentity bound);
  internal abstract bool Reopen(BuddySteamIdentity bound);
  internal abstract long Milliseconds {get;}
  internal abstract void Wait(int milliseconds);
  internal virtual void Replace(string temporary,string target,string displaced){System.IO.File.Replace(temporary,target,displaced);}
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
  struct RenameInformation {
   internal uint ReplaceIfExists;
   internal IntPtr RootDirectory;
   internal uint FileNameLength;
   internal char FileName;
  }
  [DllImport("kernel32.dll",SetLastError=true)]
  [return:MarshalAs(UnmanagedType.Bool)]
  static extern bool SetFileInformationByHandle(SafeFileHandle file,int informationClass,IntPtr information,uint size);
  internal virtual void MoveAbsent(string source,string target){
   LauncherMaintenance.NoReparse(source);
   LauncherMaintenance.NoReparse(target);
   File.Move(source,target); // The two-argument overload never overwrites an existing target.
  }
  internal virtual bool RestoreOwned(string candidate,string target,string retired,byte[] expected){
   foreach(string path in new[]{candidate,target,retired})LauncherMaintenance.NoReparse(path);
   // Deny writes and deletion for the entire ownership check and handle rename.
   // Rename this exact verified file; a pathname check followed by Replace is not conditional.
   using(var owned=new FileStream(target,FileMode.Open,FileSystemRights.Read|FileSystemRights.Delete,FileShare.None,4096,FileOptions.None)){
    if(owned.Length!=expected.Length)return false;
    var block=new byte[4096];
    int at=0,count;
    while((count=owned.Read(block,0,block.Length))>0){
     for(int i=0;i<count;i++)if(block[i]!=expected[at+i])return false;
     at+=count;
    }
    if(at!=expected.Length)return false;
    byte[] name=Encoding.Unicode.GetBytes(Path.GetFullPath(retired));
    int offset=Marshal.OffsetOf(typeof(RenameInformation),"FileName").ToInt32();
    int length=checked(offset+name.Length+2);
    IntPtr information=Marshal.AllocHGlobal(length);
    try{
     Marshal.Copy(new byte[length],0,information,length);
     Marshal.WriteInt32(information,Marshal.OffsetOf(typeof(RenameInformation),"FileNameLength").ToInt32(),name.Length);
     Marshal.Copy(name,0,IntPtr.Add(information,offset),name.Length);
     if(!SetFileInformationByHandle(owned.SafeFileHandle,3,information,(uint)length))throw new Win32Exception(Marshal.GetLastWin32Error());
    }finally{Marshal.FreeHGlobal(information);}
    // A writer can fill the now-vacant name. Refuse in that case; retain both private files.
    MoveAbsent(candidate,target);
   }
   return true;
  }
 }
 // This boundary is never selected in Engine.TestMode. No process termination API is used.
 internal sealed class NativeBuddyTakeoverEnvironment:BuddyTakeoverEnvironment {
  readonly Engine engine;
  internal NativeBuddyTakeoverEnvironment(Engine e){engine=e;}
  readonly Stopwatch clock=Stopwatch.StartNew();
  static uint Number(object value){return value is int?unchecked((uint)(int)value):value is uint?(uint)value:0;}
  static Tuple<string,uint,uint> RegistryIdentity(){using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(k==null)return null;using(var a=k.OpenSubKey("ActiveProcess"))return Tuple.Create(k.GetValue("SteamPath") as string,a==null?0:Number(a.GetValue("ActiveUser")),a==null?0:Number(a.GetValue("pid")));}}
  static bool SamePath(string a,string b){return !string.IsNullOrEmpty(a)&&!string.IsNullOrEmpty(b)&&string.Equals(Path.GetFullPath(a).TrimEnd('\\','/'),Path.GetFullPath(b).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);}
  internal override BuddySteamIdentity Capture(){
   var options=SteamLaunchOptions.Current();if(options.State==SteamLaunchOptionsState.UnknownIdentity)return SteamLaunchOptions.NativeCurrent(engine);if(!options.KnownIdentity||options.State!=SteamLaunchOptionsState.Ready)return new BuddySteamIdentity(options);
   try{var r=RegistryIdentity();if(r==null||r.Item2!=options.Account||r.Item3==0||!SamePath(r.Item1,options.Root))throw new InvalidOperationException();using(var p=Process.GetProcessById(checked((int)r.Item3))){string exe=Path.Combine(options.Root,"steam.exe");LauncherMaintenance.NoReparse(exe);if(p.HasExited||!SamePath(p.MainModule.FileName,exe))throw new InvalidOperationException();var bound=new BuddySteamIdentity(options,p.Id,p.StartTime.ToUniversalTime().Ticks,exe);if(Inspect(bound)!=BuddySteamState.Running)throw new InvalidOperationException();return bound;}}
   catch{return new BuddySteamIdentity(new SteamLaunchOptionsSnapshot(null,0,null,SteamLaunchOptionsState.UnknownIdentity));}
  }
  internal override BuddySteamState Inspect(BuddySteamIdentity bound){
   try{LauncherMaintenance.NoReparse(bound.Executable);if(!File.Exists(bound.Executable)||!SamePath(bound.Executable,Path.Combine(bound.Options.Root,"steam.exe")))return BuddySteamState.Changed;
    var r=RegistryIdentity();if(r==null||!SamePath(r.Item1,bound.Options.Root)||(r.Item2!=0&&r.Item2!=bound.Options.Account)||(r.Item3!=0&&r.Item3!=(uint)bound.Pid))return BuddySteamState.Changed;
    bool alive=false;try{using(var p=Process.GetProcessById(bound.Pid)){if(!p.HasExited){if(p.StartTime.ToUniversalTime().Ticks!=bound.Birth||!SamePath(p.MainModule.FileName,bound.Executable))return BuddySteamState.Changed;alive=true;}}}catch(ArgumentException){}
    // Steam clears ActiveProcess before its bound process finishes exiting. Keep waiting;
    // nonzero account/PID changes and reused process identities have already been rejected.
    if(alive)return BuddySteamState.Running;
    // A relaunched same client must not be treated as the old process's safe exit.
    foreach(var p in Process.GetProcessesByName("steam")){using(p){if(!p.HasExited&&SamePath(p.MainModule.FileName,bound.Executable))return BuddySteamState.Changed;}}
    return BuddySteamState.Exited;
   }catch{return BuddySteamState.Changed;}
  }
  internal override bool GameRunning(string game){return GameProcessGuard.IsRunning(game);}
  internal override void Shutdown(BuddySteamIdentity bound){if(bound.Native){var fresh=Capture();if(!fresh.Native||fresh.Pid!=bound.Pid||fresh.Birth!=bound.Birth||fresh.Options.ScopedStamp!=bound.Options.ScopedStamp)throw new InvalidOperationException();}if(Inspect(bound)!=BuddySteamState.Running)throw new InvalidOperationException();using(var p=Process.Start(new ProcessStartInfo(bound.Executable,"-shutdown"){UseShellExecute=false,WorkingDirectory=bound.Options.Root,CreateNoWindow=true})){if(p==null)throw new InvalidOperationException();}}
  internal override bool Reopen(BuddySteamIdentity bound){LauncherMaintenance.NoReparse(bound.Executable);if(!SamePath(bound.Executable,Path.Combine(bound.Options.Root,"steam.exe"))||!File.Exists(bound.Executable))return false;using(var p=Process.Start(new ProcessStartInfo(bound.Executable){UseShellExecute=false,WorkingDirectory=bound.Options.Root})){return p!=null;}}
  internal override long Milliseconds{get{return clock.ElapsedMilliseconds;}}
  internal override void Wait(int milliseconds){Thread.Sleep(milliseconds);}
 }
 internal sealed class DisabledBuddyTakeoverEnvironment:BuddyTakeoverEnvironment {
  internal override BuddySteamIdentity Capture(){return new BuddySteamIdentity(SteamLaunchOptions.Unavailable());}
  internal override BuddySteamState Inspect(BuddySteamIdentity bound){throw new InvalidOperationException();}
  internal override bool GameRunning(string game){return false;}
  internal override void Shutdown(BuddySteamIdentity bound){throw new InvalidOperationException();}
  internal override bool Reopen(BuddySteamIdentity bound){throw new InvalidOperationException();}
  internal override long Milliseconds{get{return 0;}}
  internal override void Wait(int milliseconds){throw new InvalidOperationException();}
 }
 internal static class BuddyTakeover {
  static readonly string[] Names={"ProjectZomboid64.json","ProjectZomboid32.json","ProjectZomboid64.bat","ProjectZomboid32.bat"};
  const string PendingMessage="旧 Buddy 参数接管尚未恢复；请在维护页检查恢复状态。";
  const string FailureMessage="接管未完成：配置、身份或文件写入条件已变化，请刷新检查。";
  const string ConflictMessage="部分文件已被其他程序改变，未覆盖；请核对私人备份后人工恢复。";
  internal sealed class Record {public int version=1;public string game,key,id,stage,root,exe;public uint account;public int pid;public long birth;public bool exitRequested;public List<Entry> entries=new List<Entry>();}
  internal sealed class Entry {public string target,before,after;public bool attempted;}
  sealed class Target {internal string Name,Path;internal byte[] Before,After;}
  sealed class Plan {internal BuddySteamIdentity Identity;internal List<Target> Targets=new List<Target>();internal Dictionary<string,byte[]> GameFiles=new Dictionary<string,byte[]>();internal string Stamp,Game,User,Data;internal bool Exit;}
  sealed class Refusal:Exception {internal Refusal(string message):base(message){}}
  static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=32768,RecursionLimit=12};}
  static BuddyTakeoverEnvironment Env(Engine e,BuddyTakeoverEnvironment env){return env??(e.TestMode?(BuddyTakeoverEnvironment)new DisabledBuddyTakeoverEnvironment():new NativeBuddyTakeoverEnvironment(e));}
  static Engine Bind(Engine e){return new Engine(e.Game,e.User,e.Data,e.TestMode);}
  static string Hash(byte[] bytes){return DlssInstaller.Hash(bytes);}
  static string Full(string path){return Path.GetFullPath(path).TrimEnd('\\','/');}
  static bool Same(string a,string b){return string.Equals(Full(a),Full(b),StringComparison.OrdinalIgnoreCase);}
  static string Key(Engine e){return Hash(Encoding.UTF8.GetBytes(Full(e.Game).ToUpperInvariant()));}
  static string Folder(Engine e){return Path.Combine(e.Data,"backups","buddy-"+Key(e).Substring(0,24));}
  static string JournalPath(Engine e){return Path.Combine(Folder(e),"pending.json");}
  static string Transaction(Engine e,Record r){return Path.Combine(Folder(e),r.id);}
  static string Backup(Engine e,Record r,int index,bool after){return Path.Combine(Transaction(e,r),index+(after?".after":".before"));}
  static string Label(string target){return target=="steam"?"Steam · 当前账号 · App108600":target;}
  static byte[] Read(string path,int limit=16777216){LauncherMaintenance.NoReparse(path);using(var s=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(s.Length>limit)throw new Refusal("启动配置过大，无法安全处理。");var bytes=new byte[(int)s.Length];int n=0,part;while(n<bytes.Length&&(part=s.Read(bytes,n,bytes.Length-n))>0)n+=part;if(n!=bytes.Length)throw new IOException();return bytes;}}
  static string Text(byte[] bytes,out Encoding encoding,out byte[] bom){int skip=0;encoding=new UTF8Encoding(false,true);if(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191)skip=3;else if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254){skip=2;encoding=new UnicodeEncoding(false,false,true);}else if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255){skip=2;encoding=new UnicodeEncoding(true,false,true);}bom=bytes.Take(skip).ToArray();return encoding.GetString(bytes,skip,bytes.Length-skip);}
  static void Preflight(Engine e,BuddyTakeoverEnvironment env,bool recovery){foreach(var p in new[]{e.Game,e.Config,e.User,e.Data,Folder(e),JournalPath(e),Path.Combine(e.Data,"workshop-rebuild.pending")})LauncherMaintenance.NoReparse(p);if(!File.Exists(e.Config))throw new Refusal("游戏启动配置不存在。");if(PlayProfileTransaction.Pending(e))throw new Refusal(PlayProfileTransaction.PendingMessage);if(File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending")))throw new Refusal("工坊维护尚未完成，请先完成工坊维护。");if(!recovery&&Pending(e))throw new Refusal(PendingMessage);}
  static string IdentityKey(BuddySteamIdentity id){return id.Options.ScopedStamp+"|"+id.Pid+"|"+id.Birth+"|"+(id.Executable??"").ToUpperInvariant();}
  static void VerifyIdentity(BuddySteamIdentity id){if(id==null||id.Options==null)throw new Refusal("无法核实 Steam 身份。");if(id.Options.State!=SteamLaunchOptionsState.UnknownIdentity)id.Options.RequireReadable();if(id.Options.KnownIdentity){if(id.Options.State!=SteamLaunchOptionsState.Ready||id.Pid<=0||id.Birth<=0||!Same(id.Executable,Path.Combine(id.Options.Root,"steam.exe")))throw new Refusal("无法核实 Steam 客户端进程身份。");LauncherMaintenance.NoReparse(id.Executable);}}
  static Plan Scan(Engine e,BuddyTakeoverEnvironment env){Preflight(e,env,false);var plan=new Plan{Identity=env.Capture(),Game=Full(e.Game),User=Full(e.User),Data=Full(e.Data)};VerifyIdentity(plan.Identity);var stamps=new List<string>{Full(e.Game).ToUpperInvariant(),Full(e.Data).ToUpperInvariant(),Full(e.User).ToUpperInvariant(),IdentityKey(plan.Identity)};
   foreach(string name in Names){string path=Path.Combine(e.Game,name);LauncherMaintenance.NoReparse(path);if(!File.Exists(path)){plan.GameFiles[name]=null;stamps.Add(name+":absent");continue;}byte[] before=Read(path,2097152),bom;plan.GameFiles[name]=before;Encoding encoding;string source=Text(before,out encoding,out bom);var clean=name.EndsWith(".json",StringComparison.Ordinal)?BuddyLaunchArguments.CleanJson(source):BuddyLaunchArguments.CleanBat(source);if(!clean.Safe)throw new Refusal("启动配置包含未知 Buddy 参数或复杂 BAT 语法，请人工检查。");stamps.Add(name+":"+Hash(before));if(clean.Changed)plan.Targets.Add(new Target{Name=name,Path=path,Before=before,After=bom.Concat(encoding.GetBytes(clean.Text)).ToArray()});}
   var options=plan.Identity.Options;if(options.HasField){var clean=BuddyLaunchArguments.CleanCommandLine(options.LaunchOptions);if(!clean.Safe)throw new Refusal("Steam 启动项包含未知参数语法，请人工检查。");if(clean.Changed){plan.Exit=true;plan.Targets.Add(new Target{Name="steam",Path=options.ConfigPath,Before=options.RawBytes,After=SteamLaunchOptions.Replace(options,clean.Text)});}}
   plan.Stamp=Hash(Encoding.UTF8.GetBytes(string.Join("\n",stamps)));return plan;
  }
  static void CheckGame(Engine e,Plan plan){if(!Same(e.Game,plan.Game)||!Same(e.User,plan.User)||!Same(e.Data,plan.Data))throw new Refusal("选定游戏或数据目录已改变，请刷新。");foreach(var pair in plan.GameFiles){string path=Path.Combine(e.Game,pair.Key);LauncherMaintenance.NoReparse(path);if(pair.Value==null?File.Exists(path):!File.Exists(path)||!Read(path).SequenceEqual(pair.Value))throw new Refusal("游戏启动文件已改变，请刷新预览。");}}
  internal static BuddyTakeoverPreview Preview(Engine e,BuddyTakeoverEnvironment environment=null){var p=new BuddyTakeoverPreview{stamp="",locations=new string[0],recoveryState="none",scope="localOnly",steamState="unverified",scanState="unsupported"};try{e=Bind(e);if(Pending(e)){var r=Recovery(e,environment);p.recovery=r;p.recoveryState=r.state;p.message=r.message;return p;}var plan=Scan(e,Env(e,environment));p.stamp=plan.Stamp;p.supported=true;p.knownIdentity=plan.Identity.Options.KnownIdentity;p.scope=p.knownIdentity?"localAndSteam":"localOnly";p.steamState=p.knownIdentity?"verified":"unverified";p.scanState=plan.Targets.Count>0?"conflicts":"clean";p.requiresSteamExit=plan.Exit;p.hasConflicts=plan.Targets.Count>0;p.locations=plan.Targets.Select(t=>Label(t.Name)).ToArray();p.message=p.hasConflicts?"发现可清理的旧 Buddy 启动参数。":p.knownIdentity?"启动参数正常。":"本机游戏启动参数正常。";if(!p.knownIdentity)p.message+=" Steam 启动参数未完成检查。";}
   catch(Refusal ex){p.message=ex.Message;}catch{p.message="无法安全预览，请检查当前账号与启动配置。";}return p;}
  internal static bool Pending(Engine e){try{LauncherMaintenance.NoReparse(JournalPath(e));return File.Exists(JournalPath(e));}catch{return true;}}
  internal static void Guard(Engine e){if(Pending(e))throw new Exception(PendingMessage);}
  static DirectorySecurity DirectorySecurity(){var s=new DirectorySecurity();s.SetAccessRuleProtection(true,false);s.SetOwner(WindowsIdentity.GetCurrent().User);s.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));return s;}
  static FileSecurity FileSecurity(){var s=new FileSecurity();s.SetAccessRuleProtection(true,false);s.SetOwner(WindowsIdentity.GetCurrent().User);s.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.FullControl,AccessControlType.Allow));return s;}
  static void PrivateDirectory(string path){LauncherMaintenance.NoReparse(path);if(!Directory.Exists(path))Directory.CreateDirectory(path,DirectorySecurity());else Directory.SetAccessControl(path,DirectorySecurity());}
  static void WriteNew(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);using(var file=new FileStream(path,FileMode.CreateNew,FileSystemRights.Write,FileShare.None,4096,FileOptions.WriteThrough,FileSecurity())){file.Write(bytes,0,bytes.Length);file.Flush(true);}}
  static void Durable(string path,byte[] bytes){LauncherMaintenance.NoReparse(path);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{WriteNew(temp,bytes);LauncherMaintenance.NoReparse(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);File.SetAccessControl(path,FileSecurity());}finally{if(File.Exists(temp))File.Delete(temp);}}
  static void Save(Engine e,Record r,string stage){r.stage=stage;Durable(JournalPath(e),Encoding.UTF8.GetBytes(Serializer().Serialize(r)));}
  static Record Prepare(Engine e,Plan plan){PrivateDirectory(Folder(e));var id=plan.Identity;var r=new Record{game=Full(e.Game),key=Key(e),id=Guid.NewGuid().ToString("N"),root=id.Options.Root,account=id.Options.Account,exe=id.Executable,pid=id.Pid,birth=id.Birth};PrivateDirectory(Transaction(e,r));for(int i=0;i<plan.Targets.Count;i++){var t=plan.Targets[i];Durable(Backup(e,r,i,false),t.Before);Durable(Backup(e,r,i,true),t.After);r.entries.Add(new Entry{target=t.Name,before=Hash(t.Before),after=Hash(t.After)});}Save(e,r,"prepared");return r;}
  static void Finish(Engine e,Record r,string stage){Save(e,r,stage);Durable(Path.Combine(Transaction(e,r),"result.json"),Read(JournalPath(e),32768));LauncherMaintenance.NoReparse(JournalPath(e));File.Delete(JournalPath(e));}
  static string StageDirectory(string target,Record r){return Path.Combine(Path.GetDirectoryName(target),".ob-"+r.id.Substring(0,12)+"-"+Hash(Encoding.UTF8.GetBytes(target.ToUpperInvariant())).Substring(0,8));}
  static void CleanStage(string stage,byte[] before,byte[] after){LauncherMaintenance.NoReparse(stage);if(!Directory.Exists(stage))return;foreach(string name in new[]{"new","old","newer"}){string file=Path.Combine(stage,name);LauncherMaintenance.NoReparse(file);if(File.Exists(file)){byte[] bytes=Read(file);if(!bytes.SequenceEqual(before)&&!bytes.SequenceEqual(after))throw new Refusal(ConflictMessage);File.Delete(file);}}if(!Directory.EnumerateFileSystemEntries(stage).Any())Directory.Delete(stage);else throw new Refusal(ConflictMessage);}
  static void Replace(BuddyTakeoverEnvironment env,string target,byte[] before,byte[] after,Record r){LauncherMaintenance.NoReparse(target);if(!Read(target).SequenceEqual(before))throw new Refusal("目标文件发生并发变化，未覆盖。");string stage=StageDirectory(target,r);CleanStage(stage,before,after);PrivateDirectory(stage);string temp=Path.Combine(stage,"new"),displaced=Path.Combine(stage,"old");bool safe=false;try{WriteNew(temp,after);LauncherMaintenance.NoReparse(target);if(!Read(target).SequenceEqual(before))throw new Refusal("目标文件发生并发变化，未覆盖。");env.Replace(temp,target,displaced);
    // Verify what the atomic operation actually displaced, not merely an earlier read.
    if(!Read(displaced).SequenceEqual(before)){
     // The corrective boundary must prove ownership under an exclusive handle as well.
     // Never swap an older foreign file over a second writer's newer update.
     string newer=Path.Combine(stage,"newer");
     if(env.RestoreOwned(displaced,target,newer,after))safe=true;
     throw new Refusal(ConflictMessage);
    }
    if(!Read(target).SequenceEqual(after))throw new IOException();safe=true;
   }finally{if(safe)CleanStage(stage,before,after);}}
  static string TargetPath(Engine e,Record r,Entry entry){if(Names.Contains(entry.target))return Path.Combine(e.Game,entry.target);if(entry.target!="steam"||r.account==0||string.IsNullOrWhiteSpace(r.root)||!Path.IsPathRooted(r.root)||!Same(r.exe,Path.Combine(r.root,"steam.exe")))throw new InvalidDataException();return Path.Combine(r.root,"userdata",r.account.ToString(System.Globalization.CultureInfo.InvariantCulture),"config","localconfig.vdf");}
  static Record Load(Engine e){var r=Serializer().Deserialize<Record>(Encoding.UTF8.GetString(Read(JournalPath(e),32768)));if(r==null||r.version!=1||!Same(r.game,e.Game)||r.key!=Key(e)||!Regex.IsMatch(r.id??"","^[a-f0-9]{32}$")||r.entries==null||r.entries.Count>5||r.entries.Select(x=>x.target).Distinct().Count()!=r.entries.Count)throw new InvalidDataException();LauncherMaintenance.NoReparse(Transaction(e,r));for(int i=0;i<r.entries.Count;i++){var entry=r.entries[i];LauncherMaintenance.NoReparse(TargetPath(e,r,entry));if(!Regex.IsMatch(entry.before??"","^[A-F0-9]{64}$")||!Regex.IsMatch(entry.after??"","^[A-F0-9]{64}$")||Hash(Read(Backup(e,r,i,false)))!=entry.before||Hash(Read(Backup(e,r,i,true)))!=entry.after)throw new InvalidDataException();}return r;}
  static bool Rollback(Engine e,Record r,BuddyTakeoverEnvironment env,BuddySteamIdentity id,bool exited){bool okay=true;Save(e,r,"rollingBack");for(int i=r.entries.Count-1;i>=0;i--){var entry=r.entries[i];if(!entry.attempted)continue;try{string path=TargetPath(e,r,entry);string hash=Hash(Read(path));if(hash==entry.before){CleanStage(StageDirectory(path,r),Read(Backup(e,r,i,false)),Read(Backup(e,r,i,true)));continue;}if(hash!=entry.after){okay=false;continue;}Preflight(e,env,true);if(entry.target=="steam"&&(!exited||env.Inspect(id)!=BuddySteamState.Exited)){okay=false;continue;}Replace(env,path,Read(Backup(e,r,i,true)),Read(Backup(e,r,i,false)),r);}catch{okay=false;}}if(okay)Finish(e,r,"rolledBack");else Save(e,r,"recoveryConflict");return okay;}
  static void Exit(Engine e,Record r,BuddySteamIdentity id,BuddyTakeoverEnvironment env,Action<int,string> progress,CancellationToken cancel,BuddyTakeoverResult result){cancel.ThrowIfCancellationRequested();Preflight(e,env,true);if(env.Inspect(id)!=BuddySteamState.Running)throw new Refusal("Steam 身份已改变，请重新预览。");Save(e,r,"exiting");result.phase="exiting";cancel.ThrowIfCancellationRequested();env.Shutdown(id);result.steamExitRequested=true;r.exitRequested=true;Save(e,r,"exiting");if(progress!=null)progress(-1,"正在等待已确认的 Steam 客户端正常退出");long begin=env.Milliseconds;for(;;){cancel.ThrowIfCancellationRequested();var state=env.Inspect(id);if(state==BuddySteamState.Exited){result.phase="revalidating";return;}if(state==BuddySteamState.Changed)throw new Refusal("Steam 账号或进程已改变，未修改配置。");long elapsed=env.Milliseconds-begin;if(elapsed<0||elapsed>=30000)throw new Refusal("Steam 在 30 秒内未退出，未修改配置。");env.Wait((int)Math.Min(100,30000-elapsed));}}
  static BuddyTakeoverResult Result(string status,string message){return new BuddyTakeoverResult{status=status,phase="preflight",message=message,componentInstallCompleted=false};}
  static void LogFinish(LauncherJournal log,BuddyTakeoverResult result){try{if(log!=null)log.Finish(result.status=="completed"||result.status=="recovered"?"completed":result.status=="cancelled"?"cancelled":result.recoveryPending||result.cleanupCompleted?"partial":"failed",result.message);}catch{}}
  static Mutex Lock(Engine e){var mutex=new Mutex(false,"Local\\OrangeBuddyTakeover-"+Key(e));bool held=false;try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new Refusal("另一项接管正在处理，请等待完成。");return mutex;}catch{mutex.Dispose();throw;}}
  internal static BuddyTakeoverResult Apply(Engine e,string stamp,bool confirmed,bool steamExitConfirmed,Action<int,string> progress,CancellationToken cancel,BuddyTakeoverEnvironment environment=null){
   e=Bind(e);if(!confirmed||cancel.IsCancellationRequested){var cancelled=Result("cancelled","已取消，未开始接管。");cancelled.recoveryPending=Pending(e);return cancelled;}var env=Env(e,environment);var result=Result("failed",FailureMessage);Record record=null;Plan plan=null;Mutex mutex=null;LauncherJournal log=null;bool exited=false;
   try{mutex=Lock(e);plan=Scan(e,env);result.scope=plan.Identity.Options.KnownIdentity?"localAndSteam":"localOnly";if(string.IsNullOrEmpty(stamp)||stamp!=plan.Stamp)throw new Refusal("预览已过期，请重新扫描后确认。");if(plan.Exit&&!steamExitConfirmed)return Result("confirmationRequired","需要单独确认一次性正常退出 Steam。");cancel.ThrowIfCancellationRequested();if(plan.Targets.Count==0){result.status="unchanged";result.message=plan.Identity.Options.KnownIdentity?"启动参数正常。":"本机游戏启动参数正常；Steam 启动参数未完成检查。";result.steamVerified=plan.Identity.Options.KnownIdentity;return result;}
    record=Prepare(e,plan);log=new LauncherJournal(e.Data);log.Begin("buddyTakeover","接管旧 Buddy 参数");log.Update(-1,"已保留私人备份和接管恢复记录");result.phase="prepared";
    if(plan.Exit){Exit(e,record,plan.Identity,env,progress,cancel,result);exited=true;Preflight(e,env,true);var fresh=SteamLaunchOptions.Read(plan.Identity.Options.Root,plan.Identity.Options.Account);if(fresh.State!=SteamLaunchOptionsState.Ready||fresh.ScopedStamp!=plan.Identity.Options.ScopedStamp||env.Inspect(plan.Identity)!=BuddySteamState.Exited)throw new Refusal("Steam 退出时目标启动项已变化，请重新扫描。");int n=plan.Targets.FindIndex(t=>t.Name=="steam");var target=plan.Targets[n];target.Before=fresh.RawBytes;target.After=SteamLaunchOptions.Replace(fresh,BuddyLaunchArguments.CleanCommandLine(fresh.LaunchOptions).Text);Durable(Backup(e,record,n,false),target.Before);Durable(Backup(e,record,n,true),target.After);record.entries[n].before=Hash(target.Before);record.entries[n].after=Hash(target.After);Save(e,record,"preparedAfterExit");}
    cancel.ThrowIfCancellationRequested();Preflight(e,env,true);foreach(var t in plan.Targets)if(!Read(t.Path).SequenceEqual(t.Before))throw new Refusal("配置已改变，请刷新预览。");if(!plan.Exit&&IdentityKey(env.Capture())!=IdentityKey(plan.Identity))throw new Refusal("Steam 身份已改变，请刷新预览。");
    Save(e,record,"writing");result.phase="writing";log.Update(-1,"正在原子清理已识别的旧 Buddy 参数");for(int i=0;i<plan.Targets.Count;i++){if(progress!=null)progress(i*90/plan.Targets.Count,"正在清理已识别的旧 Buddy 参数");cancel.ThrowIfCancellationRequested();Preflight(e,env,true);CheckGame(e,plan);if(plan.Exit?env.Inspect(plan.Identity)!=BuddySteamState.Exited:IdentityKey(env.Capture())!=IdentityKey(plan.Identity))throw new Refusal("Steam 身份已改变，停止写入。");var t=plan.Targets[i];record.entries[i].attempted=true;Save(e,record,"writing");Replace(env,t.Path,t.Before,t.After,record);if(t.Name!="steam")plan.GameFiles[t.Name]=t.After;Save(e,record,"writing");}
    cancel.ThrowIfCancellationRequested();foreach(var t in plan.Targets)if(!Read(t.Path).SequenceEqual(t.After))throw new IOException();Finish(e,record,"completed");record=null;result.cleanupCompleted=true;result.steamVerified=plan.Identity.Options.KnownIdentity;result.status="completed";result.phase="cleaned";result.message=result.steamVerified?"旧启动参数已清理。":"本机旧启动参数已清理；Steam 启动参数未完成检查。";
   }catch(Exception ex){result.status=ex is OperationCanceledException?"cancelled":"failed";result.message=ex is Refusal?ex.Message:ex is OperationCanceledException?"接管已取消；已完成安全回滚检查。":FailureMessage;if(record!=null){try{result.phase="rollingBack";result.rollbackCompleted=Rollback(e,record,env,plan.Identity,exited);if(!result.rollbackCompleted){result.status="recoveryRequired";result.message=ConflictMessage;}else{result.phase="rolledBack";result.message+=" 本轮自有改动已恢复。";}}catch{result.status="recoveryRequired";result.message=ConflictMessage;}}}
   finally{if(result.steamExitRequested&&plan!=null){try{result.steamReopenRequested=env.Reopen(plan.Identity);}catch{}if(!result.steamReopenRequested){if(result.cleanupCompleted)result.status="partial";result.message+=" Steam 重新打开请求失败，请手动打开客户端。";}else result.message+=" 已请求重新打开 Steam，登录状态尚待客户端确认。";}result.recoveryPending=Pending(e);LogFinish(log,result);if(mutex!=null){mutex.ReleaseMutex();mutex.Dispose();}}
   return result;
  }
  static string RecoveryStamp(Engine e,Record r,BuddySteamIdentity id){var parts=new List<string>{Hash(Read(JournalPath(e),32768)),IdentityKey(id)};foreach(var entry in r.entries)parts.Add(Hash(Read(TargetPath(e,r,entry))));return Hash(Encoding.UTF8.GetBytes(string.Join("\n",parts)));}
  internal static BuddyRecoveryState RecoveryStatus(Engine e){bool pending=Pending(e);return new BuddyRecoveryState{pending=pending,supported=false,stamp="",state=pending?"needsScan":"none",message=pending?"存在待恢复的启动参数，请扫描后检查恢复。":"没有待恢复的接管。",locations=new string[0]};}
  internal static BuddyRecoveryState Recovery(Engine e,BuddyTakeoverEnvironment environment=null){e=Bind(e);var state=new BuddyRecoveryState{pending=Pending(e),stamp="",state="none",message="没有待恢复的接管。",locations=new string[0]};if(!state.pending)return state;state.state="manual";state.message=ConflictMessage;try{var env=Env(e,environment);Preflight(e,env,true);var r=Load(e);var id=env.Capture();VerifyIdentity(id);state.locations=r.entries.Select(x=>Label(x.target)).ToArray();foreach(var entry in r.entries.Where(x=>x.attempted)){string hash=Hash(Read(TargetPath(e,r,entry)));if(hash!=entry.before&&hash!=entry.after)return state;}
    bool steam=r.entries.Any(x=>x.attempted&&x.target=="steam"&&Hash(Read(TargetPath(e,r,x)))!=x.before);if(steam&&(!id.Options.KnownIdentity||id.Options.Account!=r.account||!Same(id.Options.Root,r.root)))return state;state.requiresSteamExit=steam;state.stamp=RecoveryStamp(e,r,id);state.supported=true;state.state="recoverable";state.message="发现中断的接管；确认后仅恢复仍匹配本轮写入的文件。";}
   catch{}return state;}
  internal static BuddyTakeoverResult Recover(Engine e,string stamp,bool confirmed,bool steamExitConfirmed,Action<int,string> progress,CancellationToken cancel,BuddyTakeoverEnvironment environment=null){e=Bind(e);if(!confirmed||cancel.IsCancellationRequested){var cancelled=Result("cancelled","已取消，未开始恢复。");cancelled.recoveryPending=Pending(e);return cancelled;}var env=Env(e,environment);var result=Result("failed",FailureMessage);Mutex mutex=null;Record r=null;BuddySteamIdentity id=null;bool exited=false;LauncherJournal log=null;try{mutex=Lock(e);var state=Recovery(e,env);if(!state.pending||!state.supported||string.IsNullOrEmpty(stamp)||state.stamp!=stamp)throw new Refusal("恢复预览已过期或文件存在冲突，请重新检查。");if(state.requiresSteamExit&&!steamExitConfirmed){result=Result("confirmationRequired","恢复 Steam 启动项需要单独确认正常退出 Steam。");return result;}cancel.ThrowIfCancellationRequested();r=Load(e);id=env.Capture();if(RecoveryStamp(e,r,id)!=stamp)throw new Refusal("恢复身份或文件已改变。");log=new LauncherJournal(e.Data);log.Begin("buddyRecover","恢复旧 Buddy 参数接管");if(state.requiresSteamExit){Exit(e,r,id,env,progress,cancel,result);exited=true;}cancel.ThrowIfCancellationRequested();result.phase="rollingBack";log.Update(-1,"正在检查当前字节并恢复本轮自有改动");result.rollbackCompleted=Rollback(e,r,env,id,exited);if(result.rollbackCompleted){result.status=cancel.IsCancellationRequested?"cancelled":"recovered";result.phase="recovered";result.message=cancel.IsCancellationRequested?"已完成安全恢复后取消；文件已恢复。":"已恢复接管前的文件；可重新预览旧参数。";}else{result.status="recoveryRequired";result.message=ConflictMessage;}}
   catch(Exception ex){result.status=ex is OperationCanceledException?"cancelled":"recoveryRequired";result.message=ex is Refusal?ex.Message:ex is OperationCanceledException?"恢复已取消；记录仍保留。":ConflictMessage;}
   finally{if(result.steamExitRequested&&id!=null){try{result.steamReopenRequested=env.Reopen(id);}catch{}result.message+=result.steamReopenRequested?" 已请求重新打开 Steam，登录状态尚待确认。":" 请手动重新打开 Steam。";}result.recoveryPending=Pending(e);LogFinish(log,result);if(mutex!=null){mutex.ReleaseMutex();mutex.Dispose();}}return result;}
 }
}
