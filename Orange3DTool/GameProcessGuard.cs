using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using System.Text.RegularExpressions;
namespace PZ3D {
 enum JavaProcessRole {Unknown,Client,Server}
 // Read-only process evidence shared by launch, status and every Engine mutation.
 static class GameProcessGuard {
  public static bool MatchesJava(string game,string processName,string executablePath){
   if(string.IsNullOrWhiteSpace(game)||string.IsNullOrWhiteSpace(processName)||string.IsNullOrWhiteSpace(executablePath))return false;
   string name=processName.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?processName.Substring(0,processName.Length-4):processName;
   if(!name.Equals("java",StringComparison.OrdinalIgnoreCase)&&!name.Equals("javaw",StringComparison.OrdinalIgnoreCase))return false;
   try{string root=Path.GetFullPath(game),image=Path.GetFullPath(executablePath);foreach(string runtime in new[]{"jre64","jre"})if(string.Equals(image,Path.Combine(root,runtime,"bin",name+".exe"),StringComparison.OrdinalIgnoreCase))return true;}catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}
   return false;
  }
  static bool Exited(Process process){try{return process.HasExited;}catch{return false;}}
  // Windows command-line quoting, bounded before allocating tokens. No command text is logged.
  static List<string> Arguments(string command){
   if(string.IsNullOrWhiteSpace(command)||command.Length>32768)return null;
   // Windows argument separators are space/tab. Refuse other whitespace instead of
   // splitting a JVM property and promoting its contents to the main class.
   foreach(char c in command)if(c=='\0'||char.IsWhiteSpace(c)&&c!=' '&&c!='\t')return null;
   var result=new List<string>();int i=0;
   while(i<command.Length){while(i<command.Length&&char.IsWhiteSpace(command[i]))i++;if(i==command.Length)break;if(result.Count>=1024)return null;
    var token=new StringBuilder();bool quoted=false;
    while(i<command.Length&&(quoted||!char.IsWhiteSpace(command[i]))){int slashes=0;while(i<command.Length&&command[i]=='\\'){slashes++;i++;}
     if(i<command.Length&&command[i]=='"'){token.Append('\\',slashes/2);if(slashes%2==1)token.Append('"');else quoted=!quoted;i++;}
     else {token.Append('\\',slashes);if(i<command.Length&&(quoted||!char.IsWhiteSpace(command[i])))token.Append(command[i++]);}
    }
    if(quoted)return null;result.Add(token.ToString());
   }return result;
  }
  public static JavaProcessRole ClassifyJavaCommandLine(string command){
   var args=Arguments(command);if(args==null||args.Count<2)return JavaProcessRole.Unknown;
   string exe;try{exe=Path.GetFileName(args[0]);}catch{return JavaProcessRole.Unknown;}
   if(!exe.Equals("java.exe",StringComparison.OrdinalIgnoreCase)&&!exe.Equals("javaw.exe",StringComparison.OrdinalIgnoreCase)&&!exe.Equals("java",StringComparison.OrdinalIgnoreCase)&&!exe.Equals("javaw",StringComparison.OrdinalIgnoreCase))return JavaProcessRole.Unknown;
   for(int i=1;i<args.Count;i++){
    string a=args[i];if(a.Length==0||a[0]=='@')return JavaProcessRole.Unknown;
    if(a=="-cp"||a=="-classpath"||a=="--class-path"||a=="-p"||a=="--module-path"||a=="--upgrade-module-path"||a=="--add-modules"||a=="--limit-modules"||a=="--add-opens"||a=="--add-exports"||a=="--add-reads"||a=="--patch-module"){if(++i>=args.Count||string.IsNullOrEmpty(args[i])||args[i][0]=='@')return JavaProcessRole.Unknown;continue;}
    if(a.StartsWith("--class-path=",StringComparison.Ordinal)||a.StartsWith("--module-path=",StringComparison.Ordinal)||a.StartsWith("--add-opens=",StringComparison.Ordinal)||a.StartsWith("--add-exports=",StringComparison.Ordinal)||a.StartsWith("--add-modules=",StringComparison.Ordinal)||a.StartsWith("--enable-native-access=",StringComparison.Ordinal))continue;
    if(a.StartsWith("-D",StringComparison.Ordinal)&&a.Length>2||a.StartsWith("-XX:",StringComparison.Ordinal)&&a.Length>4||Regex.IsMatch(a,@"^-X(?:m[sx]|ss)[0-9]+[kKmMgG]?$",RegexOptions.CultureInvariant)||a=="-Xlog"||a.StartsWith("-Xlog:",StringComparison.Ordinal)||a=="-Xint"||a=="-Xcomp"||a=="-Xmixed"||a=="-Xcheck:jni"||a.StartsWith("-agentlib:",StringComparison.Ordinal)||a.StartsWith("-agentpath:",StringComparison.Ordinal)||a.StartsWith("-javaagent:",StringComparison.Ordinal)||a=="-server"||a=="-client"||a=="-ea"||a=="-da"||a=="-esa"||a=="-dsa"||a=="-verbose"||a=="--enable-preview"||a.StartsWith("-ea:",StringComparison.Ordinal)||a.StartsWith("-da:",StringComparison.Ordinal))continue;
    if(a=="zombie.network.GameServer")return JavaProcessRole.Server;
    if(a=="zombie.gameStates.MainScreenState")return JavaProcessRole.Client;
    return JavaProcessRole.Unknown;
   }return JavaProcessRole.Unknown;
  }
  internal static JavaProcessRole ReadJavaRole(string game,string name,string image,Process process){
   if(!MatchesJava(game,name,image))return JavaProcessRole.Unknown;
   try{
    long started=process.StartTime.ToUniversalTime().Ticks;int pid=process.Id;string command=null;long created=0;
    using(var query=new ManagementObjectSearcher("SELECT CommandLine, CreationDate FROM Win32_Process WHERE ProcessId = "+pid))using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){command=row["CommandLine"] as string;string date=row["CreationDate"] as string;if(date!=null)created=ManagementDateTimeConverter.ToDateTime(date).ToUniversalTime().Ticks;}
    // WMI creation timestamps have microsecond precision; process start ticks can be finer.
    if(created==0||Math.Abs(created-started)>=TimeSpan.TicksPerMillisecond)return JavaProcessRole.Unknown;
    JavaProcessRole role=ClassifyJavaCommandLine(command);
    using(var current=Process.GetProcessById(pid)){
     if(current.HasExited||current.StartTime.ToUniversalTime().Ticks!=started||!MatchesJava(game,name,Image(current)))return JavaProcessRole.Unknown;
    }
    if(process.HasExited)return JavaProcessRole.Unknown;return role;
   }catch{return JavaProcessRole.Unknown;}
  }
  public static bool IsClientRunning(string game,Func<string,Process[]> inventory=null){return Running(game,true,inventory??Process.GetProcessesByName);}
  public static bool IsRunning(string game,Func<string,Process[]> inventory=null){
   return Running(game,false,inventory??Process.GetProcessesByName);
  }
  internal static bool MatchesNative(string game,string image){
   if(string.IsNullOrWhiteSpace(game)||string.IsNullOrWhiteSpace(image))return false;
   try{return new[]{"ProjectZomboid64.exe","ProjectZomboid32.exe"}.Any(n=>string.Equals(Path.GetFullPath(image),Path.Combine(Path.GetFullPath(game),n),StringComparison.OrdinalIgnoreCase));}catch{return false;}
  }
  internal static string Image(Process process){try{using(var handle=ClientProcessHandle.Open(process.Id,false))return handle.Image;}catch{return null;}}
  internal static List<ClientIdentity> Clients(string game,Func<string,Process[]> inventory=null){return Observe(game,true,inventory??Process.GetProcessesByName);}
  static bool Running(string game,bool clientOnly,Func<string,Process[]> inventory){return Observe(game,clientOnly,inventory).Count>0;}
  static List<ClientIdentity> Observe(string game,bool clientOnly,Func<string,Process[]> inventory){
   var found=new List<ClientIdentity>();
   foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32","java","javaw"}){
    Process[] processes=inventory(name);try{foreach(var process in processes){try{
     if(process.HasExited)continue;string image=Image(process);bool native=MatchesNative(game,image),java=MatchesJava(game,name,image);
     if(!native&&!java)continue;if(java&&clientOnly&&ReadJavaRole(game,name,image,process)!=JavaProcessRole.Client)continue;
     long birth=process.StartTime.ToUniversalTime().Ticks;if(process.HasExited)continue;
     if(!found.Any(x=>x.Pid==process.Id))found.Add(new ClientIdentity{Pid=process.Id,Birth=birth,Image=image,Java=java});
    }catch{}}}finally{foreach(var process in processes)process.Dispose();}
   }return found;
  }
 }
 internal sealed class ClientIdentity {internal int Pid;internal long Birth;internal string Image;internal bool Java;}
 internal interface IClientProcessHandle:IDisposable {long Birth{get;}string Image{get;}bool Exited{get;}bool ClientRole(string game,ClientIdentity identity);bool TerminateAndWait();}
 internal sealed class ClientProcessHandle:IClientProcessHandle {
  IntPtr handle;readonly int pid;
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref int size);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);
  [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint timeout);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(IntPtr process,uint code);
  ClientProcessHandle(IntPtr value,int id){handle=value;pid=id;}
  internal static ClientProcessHandle Open(int pid,bool terminate){IntPtr h=OpenProcess(0x1000|0x100000|(terminate?1u:0u),false,pid);if(h==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return new ClientProcessHandle(h,pid);}
  public long Birth{get{long birth,exit,kernel,user;if(!GetProcessTimes(handle,out birth,out exit,out kernel,out user))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return DateTime.FromFileTimeUtc(birth).Ticks;}}
  public string Image{get{var text=new StringBuilder(32768);int size=text.Capacity;if(!QueryFullProcessImageName(handle,0,text,ref size))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return Path.GetFullPath(text.ToString());}}
  public bool Exited{get{return WaitForSingleObject(handle,0)==0;}}
  public bool ClientRole(string game,ClientIdentity identity){if(!identity.Java)return GameProcessGuard.MatchesNative(game,Image);using(var p=Process.GetProcessById(pid))return p.StartTime.ToUniversalTime().Ticks==Birth&&GameProcessGuard.ReadJavaRole(game,p.ProcessName,Image,p)==JavaProcessRole.Client;}
  public bool TerminateAndWait(){if(!TerminateProcess(handle,1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return WaitForSingleObject(handle,5000)==0;}
  public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
 }
 internal sealed class ClientClosePreview {
  string stamp,game;DateTime expires;List<ClientIdentity> identities;
  internal Func<DateTime> Now=()=>DateTime.UtcNow;
  internal Func<string,List<ClientIdentity>> Discover=g=>GameProcessGuard.Clients(g);
  internal Func<int,IClientProcessHandle> Open=pid=>ClientProcessHandle.Open(pid,true);
  internal void Invalidate(){stamp=null;identities=null;}
  internal object Preview(string selected){game=Path.GetFullPath(selected);identities=Discover(game);stamp=identities.Count==0?"":Guid.NewGuid().ToString("N");expires=Now().AddSeconds(60);return new{clients=identities.Select(x=>new{pid=x.Pid,executable=Path.GetFileName(x.Image)}).ToArray(),stamp=stamp,message=identities.Count==0?"未找到当前游戏客户端进程":"请确认关闭这些游戏客户端；未保存进度会丢失。"};}
  internal object Close(string selected,Dictionary<string,object> payload,CancellationToken token){
   object consent,raw;if(payload==null||payload.Count!=2||!payload.TryGetValue("confirmed",out consent)||!(consent is bool)||!(bool)consent||!payload.TryGetValue("stamp",out raw)||!(raw is string))throw new InvalidOperationException("强制关闭需要预览和明确确认。");
   if(string.IsNullOrEmpty(stamp)||(string)raw!=stamp||Now()>expires||!string.Equals(Path.GetFullPath(selected),game,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("游戏进程预览已变化或过期，请重新确认。");
   stamp=null;var results=new List<object>();int closed=0,failed=0;
   foreach(var identity in identities){token.ThrowIfCancellationRequested();try{using(var handle=Open(identity.Pid)){
    if(handle.Exited){results.Add(new{pid=identity.Pid,status="alreadyExited",message="进程已退出"});continue;}
    if(handle.Birth!=identity.Birth||!string.Equals(handle.Image,identity.Image,StringComparison.OrdinalIgnoreCase)||!handle.ClientRole(game,identity))throw new InvalidOperationException("进程身份已变化，未关闭。");
    token.ThrowIfCancellationRequested();if(!handle.TerminateAndWait())throw new InvalidOperationException("进程未在等待时间内退出。");closed++;results.Add(new{pid=identity.Pid,status="closed",message="已关闭"});
   }}catch(OperationCanceledException){throw;}catch(Exception ex){failed++;results.Add(new{pid=identity.Pid,status="failed",message=ex.Message});}}
   return new{results=results.ToArray(),closed=closed,failed=failed,message="已关闭 "+closed+" 个游戏进程"+(failed>0?"；"+failed+" 个未能关闭，请查看结果。":"。")};
  }
 }
}
