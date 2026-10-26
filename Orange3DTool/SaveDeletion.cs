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
 // The only injectable OS boundary is deletion. Resolution, stamps and guards stay real.
 static class SaveDeletion {
  const int MaxEntries=100000,MaxDepth=64,MaxMetadata=16777216;
  internal static bool ValidId(string id){if(string.IsNullOrWhiteSpace(id)||id.Length>260||id.Contains('\\')||id.Contains(':'))return false;string[] parts=id.Split('/');return parts.Length==2&&parts.All(p=>p.Length>0&&p!="."&&p!=".."&&!p.EndsWith(".")&&!p.EndsWith(" ")&&p.IndexOfAny(Path.GetInvalidFileNameChars())<0&&!p.Any(char.IsControl));}
  internal static bool ValidStamp(string stamp){return stamp!=null&&Regex.IsMatch(stamp,@"\AsaveDelete:[a-f0-9]{64}\z");}
  static string Resolve(Engine e,string id){if(!ValidId(id))throw new InvalidOperationException("无效存档标识。");return SaveProfiles.ResolveSave(e,id);}
  static void Guard(Engine e,string path){if(PendingSaveWrite(e,path))throw new IOException("此存档的模组清单尚有未完成写入，请先处理该存档的配置恢复："+Path.Combine(path,"mods.txt"));}
  static bool PendingSaveWrite(Engine e,string path){
   // Buddy, Workshop, normal-profile settings and other saves do not write this
   // directory. Only a bound journal proving an attempted saveMods write to
   // this exact target is relevant; unknown global metadata is not a conflict.
   try{
    string journal=PlayProfileTransaction.Journal(e);if(!File.Exists(journal))return false;byte[] bytes=PlayProfileTransaction.Read(journal,32768);if(bytes==null)return false;
    var record=PlayProfileJson.Object(Encoding.UTF8.GetString(bytes),32768);object version,kind,id,binding,stage,entries;
    if(!record.TryGetValue("version",out version)||!(version is int)||(int)version!=1||!record.TryGetValue("targetKind",out kind)||!object.Equals(kind,"save")||!record.TryGetValue("targetId",out id)||!(id is string)||!ValidId((string)id)||!record.TryGetValue("binding",out binding)||!(binding is string)||!record.TryGetValue("stage",out stage)||!(stage is string)||!new[]{"prepared","applying","rollingBack","recoveryRequired"}.Contains((string)stage)||!record.TryGetValue("entries",out entries)||!(entries is object[]))return false;
    string expected=PlayProfileTransaction.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(e.Game).ToUpperInvariant()+"|"+Path.GetFullPath(e.User).ToUpperInvariant()+"|"+Path.GetFullPath(e.Data).ToUpperInvariant()));if((string)binding!=expected)return false;
    string target=Path.GetFullPath(Path.Combine(e.User,"Saves",((string)id).Replace('/',Path.DirectorySeparatorChar)));if(!string.Equals(target,path,StringComparison.OrdinalIgnoreCase))return false;
    foreach(object raw in (object[])entries){var entry=raw as Dictionary<string,object>;object slot,attempted;if(entry!=null&&entry.TryGetValue("target",out slot)&&object.Equals(slot,"saveMods")&&entry.TryGetValue("attempted",out attempted)&&attempted is bool&&(bool)attempted)return true;}return false;
   }catch(Exception error){if(error is OutOfMemoryException||error is StackOverflowException)throw;return false;}
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
  internal static object Delete(Engine e,string id,string stamp,bool confirmed,CancellationToken cancellation){return Delete(e,id,stamp,confirmed,p=>Directory.Delete(p,true),cancellation);}
  internal static object Delete(Engine e,string id,string stamp,bool confirmed,Action<string> delete){return Delete(e,id,stamp,confirmed,delete,CancellationToken.None);}
  internal static object Delete(Engine e,string id,string stamp,bool confirmed,Action<string> delete,CancellationToken cancellation){
   cancellation.ThrowIfCancellationRequested();
   if(!confirmed)throw new InvalidOperationException("删除存档需要明确确认。");if(!ValidStamp(stamp))throw new InvalidOperationException("删除预览凭据无效，请重新核对。");if(delete==null)throw new ArgumentNullException("delete");string path=Resolve(e,id);Guard(e,path);
   // Same naming as SaveProfiles.Save, plus the shared launch/profile lease to close launch races.
   string name="Local\\OrangeSaveProfiles-"+DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(Path.Combine(path,"mods.txt")).ToUpperInvariant())).ToLowerInvariant();
   using(PlayProfileTransaction.Lock(e))using(var mutex=new Mutex(false,name)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("存档配置正被其他窗口修改，请稍后重试。");Verify(e,id,path,stamp,cancellation);cancellation.ThrowIfCancellationRequested();delete(path);if(Directory.Exists(path)||File.Exists(path))throw new IOException("删除未完成；存档仍在原位置。");return new{status="completed",id=id,message=id.Split('/')[0].Equals("Multiplayer",StringComparison.OrdinalIgnoreCase)?"本地多人缓存已永久删除；服务器世界和远程角色未修改。":"存档已永久删除。"};}finally{if(held)mutex.ReleaseMutex();}}
  }
  static void Verify(Engine e,string id,string expectedPath,string stamp,CancellationToken cancellation){cancellation.ThrowIfCancellationRequested();string path=Resolve(e,id);if(!string.Equals(path,expectedPath,StringComparison.OrdinalIgnoreCase)||Stamp(e,path)!=stamp)throw new InvalidOperationException("存档已变化，请重新核对后删除。");cancellation.ThrowIfCancellationRequested();Guard(e,path);LauncherMaintenance.NoReparse(path);}
  [StructLayout(LayoutKind.Sequential)]struct FileInfoNative {internal uint attributes;internal System.Runtime.InteropServices.ComTypes.FILETIME creation,access,write;internal uint volume,sizeHigh,sizeLow,links,indexHigh,indexLow;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,out FileInfoNative info);
  static string Identity(string path){LauncherMaintenance.NoReparse(path);using(var handle=CreateFile(path,0,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero)){FileInfoNative info;if(handle.IsInvalid||!GetFileInformationByHandle(handle,out info))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法核实存档目录身份。");if((info.attributes&0x400)!=0)throw new IOException("拒绝重解析存档目录。");return info.volume+":"+info.indexHigh+":"+info.indexLow;}}
 }
}
