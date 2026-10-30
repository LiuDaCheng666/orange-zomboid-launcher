using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
namespace PZ3D {
 internal sealed class RunningImage {internal int Pid;internal string Path;internal RunningImage(int pid,string path){Pid=pid;Path=path;}}
 internal static class NativeApplyGuard {
  internal static bool Within(string root,string path){return System.IO.Path.GetFullPath(path).StartsWith(System.IO.Path.GetFullPath(root).TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  internal static void Layout(string root,string current){
   LauncherMaintenance.NoReparse(root);if(!CoreDeployment.SamePath(Path.Combine(root,"current"),current)||CoreDeployment.SamePath(root,Path.GetPathRoot(root)))throw new IOException("更新需要独立的启动器目录；请将便携包完整解压到专用文件夹。");
   foreach(string dir in Directory.GetDirectories(root)){LauncherMaintenance.NoReparse(dir);if(!new[]{"current","packages","OrangeLauncherRuntime"}.Contains(Path.GetFileName(dir),StringComparer.OrdinalIgnoreCase))throw new IOException("启动器目录包含其他目录，已阻止更新以保护游戏和其他程序："+dir);}
   var pending=new Stack<string>();pending.Push(root);int entries=0;
   while(pending.Count>0){string dir=pending.Pop();foreach(string p in Directory.EnumerateFileSystemEntries(dir)){if(++entries>100000)throw new IOException("启动器目录内容过多，无法确认安全更新范围。");LauncherMaintenance.NoReparse(p);if(Directory.Exists(p)){pending.Push(p);continue;}if(Path.GetExtension(p).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&!new[]{Path.Combine(root,"Update.exe"),Path.Combine(root,VelopackFeed.AppId+".exe"),Path.Combine(root,"橙子启动器.exe"),Path.Combine(current,"橙子启动器.exe"),Path.Combine(current,"Squirrel.exe"),Path.Combine(current,VelopackFeed.AppId+"_ExecutionStub.exe"),Path.Combine(current,"橙子启动器_ExecutionStub.exe")}.Any(x=>CoreDeployment.SamePath(x,p)))throw new IOException("更新目录包含其他可执行程序，请先移出专用启动器目录："+p);}}
  }
  // Generated metadata is checked against the embedded identity at handoff.
  // It is not a signature or a lease against hostile concurrent local edits.
  internal static void Activation(string current){
   string path=Path.Combine(current,"sq.version");LauncherMaintenance.NoReparse(path);var document=new XmlDocument{XmlResolver=null};
   using(var bytes=new MemoryStream(CoreDeployment.ReadBytes(path,1048576)))using(var reader=XmlReader.Create(bytes,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))document.Load(reader);
   string[] names={"id","version","channel","mainExe"},values={VelopackFeed.AppId,AppVersion.Current,"win","橙子启动器.exe"};
   for(int i=0;i<names.Length;i++){var nodes=document.SelectNodes("//*[local-name()='"+names[i]+"']");if(nodes.Count!=1||nodes[0].ParentNode.LocalName!="metadata"||nodes[0].ChildNodes.Count!=1||(nodes[0].FirstChild.NodeType!=XmlNodeType.Text&&nodes[0].FirstChild.NodeType!=XmlNodeType.CDATA)||nodes[0].InnerText!=values[i])throw new InvalidDataException("当前安装的框架启动信息不匹配，请重新完整安装："+names[i]);}
  }
  static string DosPath(string path){if(path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return @"\\"+path.Substring(8);return path.StartsWith(@"\\?\",StringComparison.Ordinal)?path.Substring(4):path;}
  // Match the pinned native normalization (environment/full/long path), without
  // managed MAX_PATH truncation. An observable but unclassifiable image blocks.
  internal static string Canonical(string path){
   string p=DosPath(Environment.ExpandEnvironmentVariables(path??"").Replace('/','\\'));
   if(p.IndexOf('\0')>=0||!(p.Length>=3&&char.IsLetter(p[0])&&p[1]==':'&&p[2]=='\\'||p.StartsWith(@"\\")&&!p.StartsWith(@"\\.\")))throw new IOException("无法确认更新进程的绝对路径："+path);
   var full=new StringBuilder(32768);uint size=GetFullPathName(p,(uint)full.Capacity,full,IntPtr.Zero);if(size==0||size>=full.Capacity)throw new IOException("无法规范更新进程路径："+path);
   p=DosPath(full.ToString());string extended=p.StartsWith(@"\\")?@"\\?\UNC\"+p.Substring(2):@"\\?\"+p;
   var longer=new StringBuilder(32768);size=GetLongPathName(extended,longer,(uint)longer.Capacity);if(size==0||size>=longer.Capacity)throw new IOException("无法确认更新进程路径别名，请稍后重试："+path);
   return DosPath(longer.ToString()).TrimEnd('\\');
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFullPathName(string path,uint size,StringBuilder result,IntPtr filePart);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetLongPathName(string path,StringBuilder result,uint size);
  internal static bool ImageWithin(string root,string image){return Canonical(image).StartsWith(Canonical(root)+"\\",StringComparison.OrdinalIgnoreCase);}
  internal static void CheckInventory(string root,int allowedPid,IEnumerable<RunningImage> inventory){foreach(var p in inventory)if(p.Pid!=allowedPid&&ImageWithin(root,p.Path))throw new IOException("请先关闭启动器目录内的其他进程，更新不会结束它们："+p.Path+" (PID "+p.Pid+")");}
  internal static void Check(string root,string current,int parentPid){Layout(root,current);CheckInventory(root,parentPid,Inventory());}
  internal static List<RunningImage> Inventory(){var result=new List<RunningImage>();foreach(var p in Process.GetProcesses())using(p){IntPtr h=OpenProcess(0x1000,false,p.Id);if(h==IntPtr.Zero)continue;try{var b=new StringBuilder(32768);int size=b.Capacity;if(QueryFullProcessImageName(h,0,b,ref size))result.Add(new RunningImage(p.Id,b.ToString()));}finally{CloseHandle(h);}}return result;}
  // Unreadable protected/exited process images are also outside the pinned
  // native updater's observable kill set (it uses QueryFullProcessImageName).
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(int access,bool inherit,int pid);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder text,ref int size);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
  internal static IDisposable Instance(string root){return new InstanceLock(root,10000,true);}
  internal static bool TryInstance(string root,out IDisposable lease){var candidate=new InstanceLock(root,0,false);if(candidate.Held){lease=candidate;return true;}candidate.Dispose();lease=null;return false;}
  sealed class InstanceLock:IDisposable {readonly Mutex mutex;bool held,disposed;internal bool Held{get{return held;}}internal InstanceLock(string root,int wait,bool required){mutex=new Mutex(false,"Local\\OZL-Instance-"+CoreReleaseProtocol.Hash(Encoding.UTF8.GetBytes(Canonical(root).ToUpperInvariant())));try{try{held=mutex.WaitOne(wait);}catch(AbandonedMutexException){held=true;}if(!held&&required)throw new IOException("另一个启动器窗口或更新正在使用此目录，请关闭后重试。");}catch{mutex.Dispose();throw;}}public void Dispose(){if(disposed)return;disposed=true;if(held){held=false;mutex.ReleaseMutex();}mutex.Dispose();}}
 }
}
