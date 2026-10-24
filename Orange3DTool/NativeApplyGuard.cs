using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace PZ3D {
 internal sealed class RunningImage {internal int Pid;internal string Path;internal RunningImage(int pid,string path){Pid=pid;Path=path;}}
 internal static class NativeApplyGuard {
  internal static bool Within(string root,string path){return System.IO.Path.GetFullPath(path).StartsWith(System.IO.Path.GetFullPath(root).TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  internal static void Layout(string root,string current){
   LauncherMaintenance.NoReparse(root);if(!CoreDeployment.SamePath(Path.Combine(root,"current"),current)||CoreDeployment.SamePath(root,Path.GetPathRoot(root)))throw new IOException("更新需要独立的启动器目录；请将便携包完整解压到专用文件夹。");
   foreach(string dir in Directory.GetDirectories(root)){LauncherMaintenance.NoReparse(dir);if(!new[]{"current","packages","OrangeLauncherRuntime"}.Contains(Path.GetFileName(dir),StringComparer.OrdinalIgnoreCase))throw new IOException("启动器目录包含其他目录，已阻止更新以保护游戏和其他程序："+dir);}
   var pending=new Stack<string>();pending.Push(root);int entries=0;
   while(pending.Count>0){string dir=pending.Pop();foreach(string p in Directory.EnumerateFileSystemEntries(dir)){if(++entries>100000)throw new IOException("启动器目录内容过多，无法确认安全更新范围。");LauncherMaintenance.NoReparse(p);if(Directory.Exists(p)){pending.Push(p);continue;}if(Path.GetExtension(p).Equals(".exe",StringComparison.OrdinalIgnoreCase)&&!new[]{Path.Combine(root,"Update.exe"),Path.Combine(root,"橙子启动器.exe"),Path.Combine(current,"橙子启动器.exe"),Path.Combine(current,"Squirrel.exe")}.Any(x=>CoreDeployment.SamePath(x,p)))throw new IOException("更新目录包含其他可执行程序，请先移出专用启动器目录："+p);}}
  }
  // QueryFullProcessImageName returns an absolute OS path, including paths above MAX_PATH.
  internal static bool ImageWithin(string root,string image){string p=image.StartsWith(@"\\?\",StringComparison.Ordinal)?image.Substring(4):image;return p.StartsWith(Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  internal static void CheckInventory(string root,int allowedPid,IEnumerable<RunningImage> inventory){foreach(var p in inventory)if(p.Pid!=allowedPid&&ImageWithin(root,p.Path))throw new IOException("请先关闭启动器目录内的其他进程，更新不会结束它们："+p.Path+" (PID "+p.Pid+")");}
  internal static void Check(string root,string current,int parentPid){Layout(root,current);CheckInventory(root,parentPid,Inventory());}
  internal static List<RunningImage> Inventory(){var result=new List<RunningImage>();foreach(var p in Process.GetProcesses())using(p){IntPtr h=OpenProcess(0x1000,false,p.Id);if(h==IntPtr.Zero)continue;try{var b=new StringBuilder(32768);int size=b.Capacity;if(QueryFullProcessImageName(h,0,b,ref size))result.Add(new RunningImage(p.Id,b.ToString()));}finally{CloseHandle(h);}}return result;}
  // Unreadable protected/exited process images are also outside the pinned
  // native updater's observable kill set (it uses QueryFullProcessImageName).
  [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(int access,bool inherit,int pid);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder text,ref int size);
  [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
  internal static IDisposable Instance(string root){return new InstanceLock(root);}
  sealed class InstanceLock:IDisposable {readonly Mutex mutex;bool held;internal InstanceLock(string root){mutex=new Mutex(false,"Local\\OZL-Instance-"+CoreReleaseProtocol.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(root).ToUpperInvariant())));try{try{held=mutex.WaitOne(10000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("另一个启动器窗口或更新正在使用此目录，请关闭后重试。");}catch{mutex.Dispose();throw;}}public void Dispose(){if(held){held=false;mutex.ReleaseMutex();mutex.Dispose();}}}
 }
}
