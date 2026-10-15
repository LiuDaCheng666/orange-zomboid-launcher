using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
namespace PZ3D {
 // The only injectable OS boundary is recycling. Resolution, stamps and guards stay real.
 static class SaveDeletion {
  const int MaxEntries=100000,MaxDepth=64,MaxMetadata=16777216;
  internal static bool ValidId(string id){if(string.IsNullOrWhiteSpace(id)||id.Length>260||id.Contains('\\')||id.Contains(':'))return false;string[] parts=id.Split('/');return parts.Length==2&&parts.All(p=>p.Length>0&&p!="."&&p!=".."&&!p.EndsWith(".")&&!p.EndsWith(" ")&&p.IndexOfAny(Path.GetInvalidFileNameChars())<0&&!p.Any(char.IsControl));}
  internal static bool ValidStamp(string stamp){return stamp!=null&&Regex.IsMatch(stamp,@"\AsaveDelete:[a-f0-9]{64}\z");}
  static string Resolve(Engine e,string id){if(!ValidId(id))throw new InvalidOperationException("无效存档标识。");return SaveProfiles.ResolveSave(e,id);}
  static void Guard(Engine e,Func<bool> running){
   string launchJournal=Path.Combine(e.Data,"launch-sessions",LaunchProfiles.Key(e)+".json"),workshopJournal=Path.Combine(e.Data,"workshop-rebuild.pending");
   foreach(string p in new[]{e.Game,e.Config,e.User,e.Mods,e.Data,launchJournal,workshopJournal})LauncherMaintenance.NoReparse(p);
   e.Guard();if(running())throw new InvalidOperationException("客户端、同目录专用服务器或 Java 仍占用文件，请结束占用后删除存档。");
   if(LaunchProfiles.Pending(e)||Directory.Exists(launchJournal))throw new InvalidOperationException("启动会话尚未恢复，请先恢复后再删除存档。");
   if(File.Exists(workshopJournal)||Directory.Exists(workshopJournal))throw new InvalidOperationException("工坊维护尚未恢复，请先完成恢复。");
  }
  sealed class Snapshot {
   readonly StringBuilder text=new StringBuilder();int entries;
   internal string Read(Engine e,string path){Add(Path.GetFullPath(e.User).ToUpperInvariant());Add(Path.GetFullPath(Path.Combine(e.User,"Saves")).ToUpperInvariant());Add(Path.GetFullPath(path).ToUpperInvariant());Add(Identity(Path.Combine(e.User,"Saves")));Add(Identity(path));Walk(path,"",0);return "saveDelete:"+DlssInstaller.Hash(Encoding.UTF8.GetBytes(text.ToString())).ToLowerInvariant();}
   void Add(string line){if(text.Length+line.Length+1>MaxMetadata)throw new IOException("存档元数据超过安全核对限制。");text.Append(line).Append('\n');}
   void Walk(string path,string relative,int depth){if(depth>MaxDepth||++entries>MaxEntries)throw new IOException("存档层级或条目超过安全核对限制。");LauncherMaintenance.NoReparse(path);FileAttributes attributes=File.GetAttributes(path);bool directory=(attributes&FileAttributes.Directory)!=0;FileSystemInfo info=directory?(FileSystemInfo)new DirectoryInfo(path):new FileInfo(path);info.Refresh();if(!info.Exists)throw new IOException("存档核对期间已变化。");Add((directory?"D|":"F|")+relative+"|"+(int)attributes+"|"+info.CreationTimeUtc.Ticks+"|"+info.LastWriteTimeUtc.Ticks+(directory?"":"|"+((FileInfo)info).Length));if(!directory)return;
    // Do not ask AllDirectories to recurse before each child has passed NoReparse.
    string[] children=Directory.EnumerateFileSystemEntries(path).Take(MaxEntries-entries+1).ToArray();if(children.Length>MaxEntries-entries)throw new IOException("存档条目超过安全核对限制。");Array.Sort(children,StringComparer.Ordinal);foreach(string child in children)Walk(child,relative.Length==0?Path.GetFileName(child):relative+"/"+Path.GetFileName(child),depth+1);
   }
  }
  static string Stamp(Engine e,string path){return new Snapshot().Read(e,path);}
  public static object Preview(Engine e,string id){string path=Resolve(e,id);return new{id=id,name=Path.GetFileName(path),mode=id.Split('/')[0],path=path,stamp=Stamp(e,path)};}
  public static object Delete(Engine e,string id,string stamp,bool confirmed){return Delete(e,id,stamp,confirmed,CancellationToken.None);}
  internal static object Delete(Engine e,string id,string stamp,bool confirmed,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();if(e.TestMode)throw new InvalidOperationException("测试模式禁止调用真实回收站。");Func<bool> running=()=>{cancellation.ThrowIfCancellationRequested();return GameProcessGuard.IsRunning(e.Game);};return Delete(e,id,stamp,confirmed,p=>Recycle(p,()=>Verify(e,id,p,stamp,running)),running,cancellation);}
  internal static object Delete(Engine e,string id,string stamp,bool confirmed,Action<string> recycle,Func<bool> running){return Delete(e,id,stamp,confirmed,recycle,running,CancellationToken.None);}
  internal static object Delete(Engine e,string id,string stamp,bool confirmed,Action<string> recycle,Func<bool> running,CancellationToken cancellation){
   cancellation.ThrowIfCancellationRequested();Func<bool> checkRunning=()=>{cancellation.ThrowIfCancellationRequested();return running();};
   if(!confirmed)throw new InvalidOperationException("删除存档需要明确确认。");if(!ValidStamp(stamp))throw new InvalidOperationException("删除预览凭据无效，请重新核对。");if(recycle==null||running==null)throw new ArgumentNullException("recycle/running");Guard(e,checkRunning);string path=Resolve(e,id);
   // Same naming as SaveProfiles.Save, plus the shared launch/profile lease to close launch races.
   string name="Local\\OrangeSaveProfiles-"+DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(Path.Combine(path,"mods.txt")).ToUpperInvariant())).ToLowerInvariant();
   using(PlayProfileTransaction.Lock(e))using(var mutex=new Mutex(false,name)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("存档配置正被其他窗口修改，请稍后重试。");Verify(e,id,path,stamp,checkRunning);cancellation.ThrowIfCancellationRequested();recycle(path);if(Directory.Exists(path)||File.Exists(path))throw new IOException("回收站操作未完成；存档仍在原位置。");return new{status="completed",id=id,message=id.Split('/')[0].Equals("Multiplayer",StringComparison.OrdinalIgnoreCase)?"本地多人缓存已移到 Windows 回收站；服务器世界和远程角色未修改。":"存档已移到 Windows 回收站。"};}finally{if(held)mutex.ReleaseMutex();}}
  }
  static void Verify(Engine e,string id,string expectedPath,string stamp,Func<bool> running){Guard(e,running);string path=Resolve(e,id);if(!string.Equals(path,expectedPath,StringComparison.OrdinalIgnoreCase)||Stamp(e,path)!=stamp)throw new InvalidOperationException("存档已变化，请重新核对后删除。");Guard(e,running);LauncherMaintenance.NoReparse(path);}
  [StructLayout(LayoutKind.Sequential)]struct FileInfoNative {internal uint attributes;internal System.Runtime.InteropServices.ComTypes.FILETIME creation,access,write;internal uint volume,sizeHigh,sizeLow,links,indexHigh,indexLow;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,out FileInfoNative info);
  static string Identity(string path){LauncherMaintenance.NoReparse(path);using(var handle=CreateFile(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){FileInfoNative info;if(handle.IsInvalid||!GetFileInformationByHandle(handle,out info))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法核实存档目录身份。");if((info.attributes&0x400)!=0)throw new IOException("拒绝重解析存档目录。");return info.volume+":"+info.indexHigh+":"+info.indexLow;}}
  // IFileOperation runs as the current user on an STA; no elevation, shell command, or delete fallback.
  static void Recycle(string path,Action verify){
   Exception error=null;
   var worker=new Thread(()=>{
    IFileOperation operation=null;object item=null;
    try{
     verify();operation=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3ad05575-8857-4850-9277-11b85bdb8e09")));
     // RECYCLEONDELETE | EARLYFAILURE | NOERRORUI | NOCONFIRMATION | SILENT.
     // PreDeleteItem separately refuses any transfer without recycle capability.
     operation.SetOperationFlags(0x00080000|0x00100000|0x00000400|0x00000010|0x00000004);
     Guid iid=new Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe");Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out item));
     var sink=new RecycleSink(verify);operation.DeleteItem(item,sink);int result=operation.PerformOperations();bool aborted;operation.GetAnyOperationsAborted(out aborted);CheckRecycleResult(result,aborted,sink);
    }catch(Exception x){error=x;}
    finally{if(item!=null&&Marshal.IsComObject(item))Marshal.ReleaseComObject(item);if(operation!=null)Marshal.ReleaseComObject(operation);}
   });
   worker.SetApartmentState(ApartmentState.STA);worker.Start();worker.Join();if(error!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
  }
  static bool Cancellation(int result){return result==unchecked((int)0x800704c7)||result==unchecked((int)0x80004004);}
  static void CheckRecycleResult(int result,bool aborted,RecycleSink sink){if(sink.Refused)throw new IOException("该存档无法移到回收站；已拒绝永久删除。");if(sink.Error!=null)throw sink.Error;if(result<0&&!Cancellation(result))Marshal.ThrowExceptionForHR(result);if(sink.Result<0&&!Cancellation(sink.Result))Marshal.ThrowExceptionForHR(sink.Result);if(aborted||Cancellation(result)||Cancellation(sink.Result))throw new OperationCanceledException("回收站操作已取消。");if(!sink.Recycled)throw new IOException("Windows 未确认存档已移入回收站。");}
  [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)]static extern int SHCreateItemFromParsingName(string path,IntPtr bind,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object item);
  [ComImport,Guid("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IFileOperation {
   void Advise(IntPtr sink,out uint cookie);void Unadvise(uint cookie);void SetOperationFlags(uint flags);void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string message);void SetProgressDialog(IntPtr dialog);void SetProperties(IntPtr properties);void SetOwnerWindow(IntPtr owner);void ApplyPropertiesToItem([MarshalAs(UnmanagedType.Interface)]object item);void ApplyPropertiesToItems([MarshalAs(UnmanagedType.Interface)]object items);void RenameItem([MarshalAs(UnmanagedType.Interface)]object item,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void RenameItems([MarshalAs(UnmanagedType.Interface)]object items,[MarshalAs(UnmanagedType.LPWStr)]string name);void MoveItem([MarshalAs(UnmanagedType.Interface)]object item,[MarshalAs(UnmanagedType.Interface)]object destination,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void MoveItems([MarshalAs(UnmanagedType.Interface)]object items,[MarshalAs(UnmanagedType.Interface)]object destination);void CopyItem([MarshalAs(UnmanagedType.Interface)]object item,[MarshalAs(UnmanagedType.Interface)]object destination,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);void CopyItems([MarshalAs(UnmanagedType.Interface)]object items,[MarshalAs(UnmanagedType.Interface)]object destination);void DeleteItem([MarshalAs(UnmanagedType.Interface)]object item,IFileOperationProgressSink sink);void DeleteItems([MarshalAs(UnmanagedType.Interface)]object items);void NewItem([MarshalAs(UnmanagedType.Interface)]object destination,uint attributes,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,IntPtr sink);[PreserveSig]int PerformOperations();void GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
  }
 }
  [ComVisible(true),Guid("04b0f1a7-9490-44bc-96e1-4296a31252e2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]public interface IFileOperationProgressSink {
   [PreserveSig]int StartOperations();[PreserveSig]int FinishOperations(int result);[PreserveSig]int PreRenameItem(uint flags,IntPtr item,IntPtr name);[PreserveSig]int PostRenameItem(uint flags,IntPtr item,IntPtr name,int result,IntPtr created);[PreserveSig]int PreMoveItem(uint flags,IntPtr item,IntPtr destination,IntPtr name);[PreserveSig]int PostMoveItem(uint flags,IntPtr item,IntPtr destination,IntPtr name,int result,IntPtr created);[PreserveSig]int PreCopyItem(uint flags,IntPtr item,IntPtr destination,IntPtr name);[PreserveSig]int PostCopyItem(uint flags,IntPtr item,IntPtr destination,IntPtr name,int result,IntPtr created);[PreserveSig]int PreDeleteItem(uint flags,IntPtr item);[PreserveSig]int PostDeleteItem(uint flags,IntPtr item,int result,IntPtr created);[PreserveSig]int PreNewItem(uint flags,IntPtr destination,IntPtr name);[PreserveSig]int PostNewItem(uint flags,IntPtr destination,IntPtr name,IntPtr template,uint attributes,int result,IntPtr created);[PreserveSig]int UpdateProgress(uint total,uint done);[PreserveSig]int ResetTimer();[PreserveSig]int PauseTimer();[PreserveSig]int ResumeTimer();
  }
  [ComVisible(true),ClassInterface(ClassInterfaceType.None)]public sealed class RecycleSink:IFileOperationProgressSink {
   readonly Action verify;internal bool Refused,Recycled;internal int Result;internal Exception Error;internal RecycleSink(Action verify){this.verify=verify;}
   public int PreDeleteItem(uint flags,IntPtr item){if((flags&0x80)==0){Refused=true;return unchecked((int)0x80004004);}try{verify();return 0;}catch(Exception x){Error=x;return unchecked((int)0x80004004);}}
   public int PostDeleteItem(uint flags,IntPtr item,int result,IntPtr created){Result=result;Recycled=result>=0&&created!=IntPtr.Zero;return 0;}
   public int StartOperations(){return 0;}public int FinishOperations(int result){if(result<0)Result=result;return 0;}public int PreRenameItem(uint f,IntPtr i,IntPtr n){return 0;}public int PostRenameItem(uint f,IntPtr i,IntPtr n,int r,IntPtr c){return 0;}public int PreMoveItem(uint f,IntPtr i,IntPtr d,IntPtr n){return 0;}public int PostMoveItem(uint f,IntPtr i,IntPtr d,IntPtr n,int r,IntPtr c){return 0;}public int PreCopyItem(uint f,IntPtr i,IntPtr d,IntPtr n){return 0;}public int PostCopyItem(uint f,IntPtr i,IntPtr d,IntPtr n,int r,IntPtr c){return 0;}public int PreNewItem(uint f,IntPtr d,IntPtr n){return 0;}public int PostNewItem(uint f,IntPtr d,IntPtr n,IntPtr t,uint a,int r,IntPtr c){return 0;}public int UpdateProgress(uint t,uint d){return 0;}public int ResetTimer(){return 0;}public int PauseTimer(){return 0;}public int ResumeTimer(){return 0;}
  }
}
