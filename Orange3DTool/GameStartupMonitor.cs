using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PZ3D {
 // Reduces observed evidence only. Phase changes never invent a percentage.
 sealed class GameStartupReducer {
  readonly string session;readonly DateTime started;readonly Queue<string> lines=new Queue<string>();
  internal string Stage,Message;internal bool Complete,Failed,WaitingForInput;
  internal int? ProcessId;internal long? WorkingSetBytes,TotalPhysicalBytes,HeapLimitBytes;
  static readonly Regex Record=new Regex(@"^(?<severity>LOG\s*:|DEBUG:)\s*(?<category>[A-Za-z]+)\s+f:[0-9]+(?:\s+(?:t|st):[0-9.]+)*(?:\s+at\s+(?<caller>[A-Za-z0-9_.$]+)(?:\([^\r\n)]{1,100}\))?)?\s*>\s?(?<body>.*)$",RegexOptions.CultureInvariant);
  internal GameStartupReducer(GameStartupLaunch launch){session=launch.SessionId;started=launch.StartedUtc;Stage=launch.Stage??"preparing";Message=GameDiagnostics.Sanitize(launch.Message??"正在准备游戏启动");}
  internal void Submitted(){if(Failed||Complete)return;Stage="submitted";Message="启动请求已提交，等待本次游戏进程与加载日志";}
  internal void Fail(string message){if(Complete||Failed)return;Failed=true;WaitingForInput=false;Stage="failed";Message=GameDiagnostics.Sanitize(message??"游戏启动失败");}
  internal void Line(string line){
   string clean=GameDiagnostics.Sanitize(line??"");if(clean.Length>4096)clean=clean.Substring(0,4096);lines.Enqueue(clean);while(lines.Count>80)lines.Dequeue();
   if(Complete||Failed)return;
   Match record=Record.Match(line??"");if(!record.Success)return;string body=record.Groups["body"].Value,category=record.Groups["category"].Value;
   // The existing one-shot Lua bridge prints only after verifying its ORANGE1
   // nonce/TTL and the visible, delayed-finished main menu with no TOS dialog.
   if(IsMenuReady(line,session)){Complete=true;WaitingForInput=false;Stage="ready";Message="游戏主菜单已就绪";return;}
   if(category!="General"||record.Groups["severity"].Value=="DEBUG:"||record.Groups["caller"].Success)return;
   // Verified in ZombieBuddy Loader immediately before blocking approvePendingMods;
   // GameWindow.DoLoadingText forwards the exact text to DebugLog.log.
   if(body=="Waiting for Java mods approval…"||body=="Waiting for Java mods approval..."){WaitingForInput=true;Stage="authorization";Message="请在游戏的 Java 模组授权窗口中确认；此窗口不会抢占焦点";return;}
   string phase=null,message=null;
   if(body=="Loading Mods"){phase="mods";message="正在加载模组";}
   else if(body=="Loading Scripts"){phase="scripts";message="正在加载游戏脚本";}
   else if(body=="Loading Lua"||body=="Reloading Lua"){phase="lua";message="正在加载 Lua 与游戏界面";}
   else if(body=="Loading Clothing"){phase="clothing";message="正在加载服装资源";}
   else if(body=="Loading models and animations"){phase="models";message="正在加载模型与动画";}
   else if(Regex.IsMatch(body,@"^Loading [A-Za-z0-9_.-]{1,128} texture pack$",RegexOptions.CultureInvariant)){phase="textures";message="正在加载贴图资源";}
   else if(body=="Game booting"){phase="booting";message="游戏正在完成初始化；等待主菜单就绪证据";}
   if(phase!=null){Stage=phase;Message=message;WaitingForInput=false;}
  }
  internal static bool IsMenuReady(string line,string session){if(session==null||!Regex.IsMatch(session,@"\A[a-f0-9]{32}\z",RegexOptions.CultureInvariant))return false;Match record=Record.Match(line??"");return record.Success&&record.Groups["severity"].Value!="DEBUG:"&&record.Groups["category"].Value=="Lua"&&!record.Groups["caller"].Success&&record.Groups["body"].Value=="[OZL-STARTUP] menu-ready "+session;}
  internal GameStartupSnapshot Snapshot(DateTime now){return new GameStartupSnapshot{SessionId=session,Stage=Stage,Message=Message,Lines=lines.ToArray(),ElapsedSeconds=Math.Max(0,(now-started).TotalSeconds),ProcessId=ProcessId,WorkingSetBytes=WorkingSetBytes,TotalPhysicalBytes=TotalPhysicalBytes,HeapLimitBytes=HeapLimitBytes,Complete=Complete,Failed=Failed,WaitingForInput=WaitingForInput,CompletedItems=null,TotalItems=null};}
 }

 // A byte boundary captured before launch excludes old logs and old half-lines.
 // Each read opens read-only with delete sharing, detects file identity and checks
 // the preceding bytes so same-file truncate/rewrite also resets the decoder.
 sealed class GameStartupLogTail {
  const int TickBytes=262144,LineBytes=4096;
  readonly string path;long offset,initialBoundary;string identity,initialIdentity;byte[] anchor=new byte[0];
  readonly List<byte> pending=new List<byte>(LineBytes);bool discard;
  [StructLayout(LayoutKind.Sequential)]struct FileInformation {public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInformation information);
  static string Identity(FileStream input){FileInformation info;if(!GetFileInformationByHandle(input.SafeFileHandle,out info))throw new IOException("无法确认日志文件身份");return info.Volume+":"+info.IndexHigh+":"+info.IndexLow;}
  FileStream Open(){LauncherMaintenance.NoReparse(path);return new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);}
  internal GameStartupLogTail(string path){this.path=path;try{using(var input=Open()){identity=Identity(input);offset=input.Length;initialBoundary=offset;initialIdentity=identity;anchor=Anchor(input,offset);discard=offset>0&&(anchor.Length==0||anchor[anchor.Length-1]!=10);}}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}}
  static byte[] Anchor(FileStream input,long at){int count=(int)Math.Min(64,at);byte[] bytes=new byte[count];input.Position=at-count;int n=0,read;while(n<count&&(read=input.Read(bytes,n,count-n))>0)n+=read;if(n!=count)throw new IOException("日志读取期间改变");return bytes;}
  // UI dispatch can create this observer a few milliseconds after launch. The
  // unguessable session nonce permits recovery of exactly its verified ready
  // record before the captured boundary without replaying any previous logs.
  // Called on a separate first-client tick, so aggregate reads remain <=256 KiB.
  internal string[] ReadStartupEvidence(string session){if(initialIdentity==null||initialBoundary==0||!File.Exists(path))return new string[0];using(var input=Open()){
   if(Identity(input)!=initialIdentity||input.Length<initialBoundary)return new string[0];long start=Math.Max(0,initialBoundary-(TickBytes-64));input.Position=start;byte[] bytes=new byte[(int)(initialBoundary-start)];int count=0,n;while(count<bytes.Length&&(n=input.Read(bytes,count,bytes.Length-count))>0)count+=n;
   var line=new List<byte>(LineBytes);bool skip=start>0;for(int i=0;i<count;i++){byte b=bytes[i];if(b==10){if(!skip&&line.Count>0){string value=Encoding.UTF8.GetString(line.ToArray()).TrimEnd('\r').TrimStart('\uFEFF');if(GameStartupReducer.IsMenuReady(value,session))return new[]{value};}line.Clear();skip=false;}else if(!skip){if(line.Count>=LineBytes){line.Clear();skip=true;}else line.Add(b);}}
   return new string[0];
  }}
  internal string[] Read(){
   if(!File.Exists(path))return new string[0];using(var input=Open()){
    string current=Identity(input);int overhead=0;
    bool reset=current!=identity||input.Length<offset;
    if(!reset&&offset>0){byte[] check=Anchor(input,offset);overhead=check.Length;reset=!check.SequenceEqual(anchor);}
    if(reset){identity=current;offset=0;pending.Clear();discard=false;anchor=new byte[0];}
    input.Position=offset;int wanted=(int)Math.Min(TickBytes-overhead-64,Math.Max(0,input.Length-offset));byte[] bytes=new byte[wanted];int count=input.Read(bytes,0,wanted);offset+=count;
    var result=new List<string>();for(int i=0;i<count;i++){byte b=bytes[i];if(b==10){if(!discard&&pending.Count>0){string line=Encoding.UTF8.GetString(pending.ToArray()).TrimEnd('\r').TrimStart('\uFEFF');result.Add(line);}pending.Clear();discard=false;}else if(!discard){if(pending.Count>=LineBytes){pending.Clear();discard=true;}else pending.Add(b);}}
    anchor=Anchor(input,offset);return result.ToArray();
   }
  }
 }

 // Observes owned launch identity and files. Disposing never signals the game.
 sealed class GameStartupMonitor:IGameStartupMonitor {
  readonly object gate=new object();readonly GameStartupLaunch launch;readonly Action<GameStartupSnapshot> changed;readonly GameStartupReducer state;readonly string consolePath;GameStartupLogTail tail;
  readonly ManualResetEvent stop=new ManualResetEvent(false);bool started,disposed;Process client;long clientBirth;
  internal GameStartupMonitor(GameStartupLaunch launch,Action<GameStartupSnapshot> changed){if(launch==null)throw new ArgumentNullException("launch");this.launch=new GameStartupLaunch{SessionId=launch.SessionId,StartedUtc=launch.StartedUtc,GamePath=launch.GamePath,UserPath=launch.UserPath,DataPath=launch.DataPath,TargetKind=launch.TargetKind,Stage=launch.Stage,Message=launch.Message};this.changed=changed??delegate{};state=new GameStartupReducer(this.launch);try{consolePath=Path.Combine(launch.UserPath,"console.txt");tail=new GameStartupLogTail(consolePath);}catch{state.Message="日志暂不可读取；游戏启动可以继续";}}
  public void Start(){lock(gate){if(disposed||started)return;started=true;var worker=new Thread(Observe){IsBackground=true,Name="OZL game startup observer"};worker.Start();}}
  public void Submitted(){lock(gate){if(disposed)return;state.Submitted();Publish();}}
  public void Fail(string message){lock(gate){if(disposed)return;state.Fail(message);Publish();}}
  void Publish(){if(disposed)return;try{changed(state.Snapshot(DateTime.UtcNow));}catch{ /* UI observers cannot affect launch or observation. */ }}
  internal static bool MatchesClientIdentity(string game,DateTime startedUtc,string name,string image,DateTime birthUtc,bool javaClient){
   if(birthUtc<startedUtc||birthUtc>DateTime.UtcNow.AddSeconds(5)||string.IsNullOrWhiteSpace(game)||string.IsNullOrWhiteSpace(image))return false;
   try{string root=Path.GetFullPath(game),full=Path.GetFullPath(image);if(name=="ProjectZomboid64"||name=="ProjectZomboid32")return string.Equals(full,Path.Combine(root,name+".exe"),StringComparison.OrdinalIgnoreCase);return javaClient&&GameProcessGuard.MatchesJava(root,name,full);}catch{return false;}
  }
  void FindClient(){
   foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32","java","javaw"}){Process[] processes=Process.GetProcessesByName(name);foreach(Process process in processes){bool keep=false;try{if(process.HasExited)continue;DateTime birth=process.StartTime.ToUniversalTime();if(birth<launch.StartedUtc)continue;string image=process.MainModule.FileName;bool java=name=="java"||name=="javaw";if(!MatchesClientIdentity(launch.GamePath,launch.StartedUtc,name,image,birth,!java)){
      if(!java||!GameProcessGuard.MatchesJava(launch.GamePath,name,image)||GameProcessGuard.ReadJavaRole(launch.GamePath,name,image,process)!=JavaProcessRole.Client)continue;
      if(!MatchesClientIdentity(launch.GamePath,launch.StartedUtc,name,image,birth,true))continue;
     }
     if(client==null){client=process;clientBirth=birth.Ticks;keep=true;}
    }catch{}finally{if(!keep)process.Dispose();}}if(client!=null)return;}
  }
  long? ReadHeap(){try{string path=Path.Combine(launch.GamePath,"ProjectZomboid64.json");LauncherMaintenance.NoReparse(path);using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){if(input.Length>2097152)return null;byte[] bytes=new byte[(int)input.Length];int offset=0,n;while(offset<bytes.Length&&(n=input.Read(bytes,offset,bytes.Length-offset))>0)offset+=n;if(offset!=bytes.Length)return null;long? heap=LauncherMaintenance.ProfileHeapBytes(bytes);return heap.HasValue&&heap.Value>0?heap:null;}}catch{return null;}}
  void Observe(){
   try{long? heap=ReadHeap(),total=null;try{long physical=LauncherMaintenance.TotalPhysicalMemoryBytes();if(physical>0)total=physical;}catch{}
    lock(gate){if(disposed)return;state.HeapLimitBytes=heap;state.TotalPhysicalBytes=total;Publish();}
    while(!stop.WaitOne(0)){
     try{bool firstClient=client==null;if(firstClient)FindClient();long? effectiveHeap=firstClient&&client!=null?ReadHeap():null;long? working=null;int? pid=null;bool exited=false,unreadable=false;string[] logs=new string[0];
      if(client!=null){try{client.Refresh();exited=client.HasExited||client.StartTime.ToUniversalTime().Ticks!=clientBirth;if(!exited){pid=client.Id;working=client.WorkingSet64;}}catch{exited=true;}try{bool retryBoundary=tail==null;if(retryBoundary&&consolePath!=null)tail=new GameStartupLogTail(consolePath);if(tail!=null)logs=firstClient||retryBoundary?tail.ReadStartupEvidence(launch.SessionId):tail.Read();}catch{unreadable=true;}}
      lock(gate){if(disposed)return;if(firstClient&&client!=null)state.HeapLimitBytes=effectiveHeap;state.ProcessId=pid;state.WorkingSetBytes=working;if(exited&&!state.Complete)state.Fail("游戏进程在确认就绪前退出；请查看最近日志或游戏诊断");foreach(string line in logs)state.Line(line);if(client==null&&!state.Failed)state.Message="等待本次启动的游戏进程；观察窗口可随时关闭";else if(unreadable&&!state.Complete&&!state.Failed)state.Message="游戏进程正在运行；日志暂不可读取，正在继续观察";Publish();if(state.Complete||state.Failed)return;}
     }catch{lock(gate){if(disposed)return;state.Message="暂时无法读取游戏状态；正在继续观察，游戏启动不受影响";Publish();}}
     if(stop.WaitOne(500))return;
    }
   }finally{if(client!=null)client.Dispose();lock(gate){stop.Dispose();}}
  }
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;if(started){try{stop.Set();}catch(ObjectDisposedException){}}else stop.Dispose();}}
 }
}
