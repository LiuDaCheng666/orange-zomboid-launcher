using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Text;using System.Windows.Forms;
namespace PZ3D {
 internal sealed class LauncherWindowCandidate {
  internal readonly int Pid;internal readonly string Image,Title;internal readonly IntPtr Handle;internal readonly bool Visible,Minimized;
  internal LauncherWindowCandidate(int pid,string image,IntPtr handle,bool visible,bool minimized,string title){Pid=pid;Image=image;Handle=handle;Visible=visible;Minimized=minimized;Title=title;}
 }
 internal static class LauncherWindowActivation {
  internal static int Run(string root,Func<int> start){return Run(root,start,()=>TryActivate(root),message=>MessageBox.Show(message,"OZL",MessageBoxButtons.OK,MessageBoxIcon.Information));}
  internal static int Run(string root,Func<int> start,Func<bool> activate,Action<string> notice){
   IDisposable lease;
   try {
    if(!NativeApplyGuard.TryInstance(root,out lease)){
     if(activate())return 0;
     // An updater can still own the lease after restarting us. Keep its original
     // bounded handoff wait when there is no actual launcher UI to activate.
     lease=NativeApplyGuard.Instance(root);
    }
   }catch(IOException){notice("启动器已在运行或正在完成更新，暂时无法打开或切换窗口。请从任务栏切换，或稍后重试。");return 0;}
   using(lease)return start();
  }
  internal static bool TryActivate(string root){try{return SelectAndActivate(root,Windows(),Verify,handle=>ShowWindowAsync(handle,9),handle=>SetForegroundWindow(handle));}catch{return false;}}
  internal static bool SelectAndActivate(string root,IEnumerable<LauncherWindowCandidate> windows,Func<LauncherWindowCandidate,bool> verify,Func<IntPtr,bool> restore,Func<IntPtr,bool> foreground){
   string expected=NativeApplyGuard.Canonical(Path.Combine(root,"current","橙子启动器.exe"));int own=Process.GetCurrentProcess().Id;
   foreach(var window in windows){try{
    if(window.Pid<=0||window.Pid==own||window.Handle==IntPtr.Zero||!window.Visible||window.Title!="OZL"||!string.Equals(NativeApplyGuard.Canonical(window.Image),expected,StringComparison.OrdinalIgnoreCase)||!verify(window))continue;
    // Preserve a maximized or normal window; only a minimized UI needs restoring.
    if(window.Minimized&&!restore(window.Handle))return false;return foreground(window.Handle);
   }catch{}}
   return false;
  }
  internal static List<LauncherWindowCandidate> Windows(){var images=NativeApplyGuard.Inventory().ToDictionary(image=>image.Pid,image=>image.Path);var result=new List<LauncherWindowCandidate>();EnumWindows((handle,unused)=>{uint pid;GetWindowThreadProcessId(handle,out pid);string image;if(pid<=int.MaxValue&&images.TryGetValue((int)pid,out image)){var title=new StringBuilder(256);GetWindowText(handle,title,title.Capacity);result.Add(new LauncherWindowCandidate((int)pid,image,handle,IsWindowVisible(handle),IsIconic(handle),title.ToString()));}return true;},IntPtr.Zero);return result;}
  static bool Verify(LauncherWindowCandidate window){uint pid;if(GetWindowThreadProcessId(window.Handle,out pid)==0||pid!=window.Pid||!IsWindowVisible(window.Handle))return false;try{using(var process=Process.GetProcessById(window.Pid)){if(process.HasExited||!string.Equals(NativeApplyGuard.Canonical(process.MainModule.FileName),NativeApplyGuard.Canonical(window.Image),StringComparison.OrdinalIgnoreCase))return false;}uint current;return GetWindowThreadProcessId(window.Handle,out current)!=0&&current==window.Pid;}catch{return false;}}
  delegate bool EnumWindow(IntPtr handle,IntPtr parameter);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumWindow callback,IntPtr parameter);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr handle,out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr handle,StringBuilder title,int size);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr handle);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr handle);
  [DllImport("user32.dll")]static extern bool ShowWindowAsync(IntPtr handle,int command);
  [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
 }
}