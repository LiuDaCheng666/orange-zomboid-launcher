using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PZ3D {
 internal sealed class InstallerShortcutResult {
  internal bool Created{get;private set;}internal string Path{get;private set;}internal string Message{get;private set;}
  internal InstallerShortcutResult(bool created,string path,string message){Created=created;Path=path??"";Message=message??"";}
 }
 internal sealed class InstallerShortcutLink {internal string Target,WorkingDirectory,Arguments,Icon;}
 internal sealed class InstallerShortcut {
  internal const string Receipt="OrangeLauncherRuntime/desktop-shortcut.json";
  const int MaxLink=1048576,MaxReceipt=16384;
  readonly Func<string> desktop;readonly string key;
  readonly Func<string,InstallerShortcutLink,string> write;
  internal InstallerShortcut(Func<string> knownDesktop,string publicKey,Func<string,InstallerShortcutLink,string> writeLink=null){desktop=knownDesktop;key=publicKey;write=writeLink??WriteLink;}
  internal static InstallerShortcutResult Create(InstallerInstallResult result){try{return new InstallerShortcut(()=>Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),CoreUpdateTrust.PublicKeyXml).CreateOwned(result);}catch(Exception error){return Warning(error);}}
  internal static string RemoveOwned(string root){try{return new InstallerShortcut(()=>Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),CoreUpdateTrust.PublicKeyXml).Remove(root);}catch(Exception error){return "桌面快捷方式保留："+error.Message;}}
  static InstallerShortcutResult Warning(Exception error){return new InstallerShortcutResult(false,"","桌面快捷方式未创建："+error.Message);}
  // COM work uses a dedicated STA when called by the install worker. No Resolve,
  // shell execution, scripts or registry operations are used.
  static T Sta<T>(Func<T> action){if(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA)return action();T result=default(T);Exception failure=null;var thread=new Thread(()=>{try{result=action();}catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return result;}
  internal InstallerShortcutResult CreateOwned(InstallerInstallResult result){return Sta(()=>CreateCore(result));}
  InstallerShortcutResult CreateCore(InstallerInstallResult result){
   string root=null;bool authenticated=false;InstallerShortcutResult outcome;
   try{
    if(result==null)throw new InvalidDataException("安装结果不存在。");root=CoreDeployment.Root(result.Root);
    using(new DirectoryLease(root))using(new DirectoryLease(CoreDeployment.Safe(root,CoreDeployment.Runtime)))using(CoreDeployment.Acquire(root)){
     var record=CoreDeployment.ReadAuthenticated(root,key);RequireResult(result,root,record);foreach(var entry in record.files)CoreDeployment.RequireFile(root,entry.path,entry);authenticated=true;
     if(File.Exists(CoreDeployment.Safe(root,Receipt))||Directory.Exists(CoreDeployment.Safe(root,Receipt)))throw new IOException("已有快捷方式所有权记录，未重复创建。");
     string directory=Desktop();using(var lease=new DirectoryLease(directory)){
      var link=Expected(root);string path=null,writtenHash=null;
      for(int n=1;n<=100;n++){
       string candidate=Child(directory,n==1?"OZL.lnk":"OZL ("+n+").lnk");
       if(File.Exists(candidate)||Directory.Exists(candidate))continue;
       try{writtenHash=write(candidate,link);path=candidate;break;}catch(IOException error){int code=error.HResult&65535;if((code==80||code==183)&&(File.Exists(candidate)||Directory.Exists(candidate)))continue;throw;}
      }
      if(path==null)throw new IOException("桌面上 100 个快捷方式名称均已占用。");
      using(var file=OpenFile(path,false)){
       byte[] bytes=Bytes(file,MaxLink);if(CoreReleaseProtocol.Hash(bytes)!=writtenHash)throw new IOException("创建后的快捷方式字节已变化，未记录所有权。");RequireLink(Parse(bytes),link);
       // Recheck identity and source bytes immediately before publishing ownership.
       var current=CoreDeployment.ReadAuthenticated(root,key);RequireResult(result,root,current);foreach(var entry in current.files)CoreDeployment.RequireFile(root,entry.path,entry);
       var receipt=new ShortcutReceipt{schema=1,installationId=record.installationId,path=path,target=link.Target,workingDirectory=link.WorkingDirectory,arguments="",icon=link.Icon,sha256=CoreReleaseProtocol.Hash(bytes)};
       CoreDeployment.WriteNew(root,Receipt,CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(receipt)));
      }
      outcome=new InstallerShortcutResult(true,path,"桌面快捷方式已创建："+path);
     }
    }
   }catch(Exception error){outcome=Warning(error);}
   if(authenticated)Evidence(root,"desktop-shortcut-result.log",outcome.Message);return outcome;
  }
  internal string Remove(string root){return Sta(()=>RemoveCore(root));}
  string RemoveCore(string root){
   bool authenticated=false;string outcome;
   try{
    root=CoreDeployment.Root(root);using(new DirectoryLease(root))using(new DirectoryLease(CoreDeployment.Safe(root,CoreDeployment.Runtime)))using(CoreDeployment.Acquire(root)){
     var record=CoreDeployment.ReadAuthenticated(root,key);authenticated=true;string receiptPath=CoreDeployment.Safe(root,Receipt);
     if(!File.Exists(receiptPath)&&!Directory.Exists(receiptPath))return "";
     byte[] receiptBytes;using(var receiptFile=OpenFile(receiptPath,false))receiptBytes=Bytes(receiptFile,MaxReceipt);
     CoreReleaseProtocol.ReadStoredObject(receiptBytes,"schema","installationId","path","target","workingDirectory","arguments","icon","sha256");
     var receipt=CoreDeployment.Json.Deserialize<ShortcutReceipt>(CoreDeployment.Utf8.GetString(receiptBytes));var expected=Expected(root);
     string directory=Desktop();using(var lease=new DirectoryLease(directory)){
      if(receipt==null||receipt.schema!=1||receipt.installationId!=record.installationId||string.IsNullOrEmpty(receipt.path)||receipt.path!=Child(directory,Path.GetFileName(receipt.path))||!OwnedName(Path.GetFileName(receipt.path))||receipt.sha256==null||!System.Text.RegularExpressions.Regex.IsMatch(receipt.sha256,"\\A[0-9A-Fa-f]{64}\\z"))throw new InvalidDataException("所有权记录与当前安装或当前用户桌面不匹配。");
      RequireLink(new InstallerShortcutLink{Target=receipt.target,WorkingDirectory=receipt.workingDirectory,Arguments=receipt.arguments,Icon=receipt.icon},expected);
      if(!File.Exists(receipt.path)&&!Directory.Exists(receipt.path))return "桌面快捷方式已不存在。";
      // Hold the exact file against write/rename/replacement while hashing and
      // parsing. Delete by handle, never by a path that could be swapped later.
      using(var file=OpenFile(receipt.path,true)){
       byte[] bytes=Bytes(file,MaxLink);if(CoreReleaseProtocol.Hash(bytes)!=receipt.sha256)throw new IOException("快捷方式已被修改或替换。");RequireLink(Parse(bytes),expected);
       if(CoreDeployment.ReadAuthenticated(root,key).installationId!=receipt.installationId)throw new IOException("安装身份已变化。");
       var disposition=new FileDisposition{Delete=true};if(!SetFileInformationByHandle(file.SafeFileHandle,4,ref disposition,Marshal.SizeOf(typeof(FileDisposition))))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
      }
      outcome="已删除本次安装的桌面快捷方式："+receipt.path;
     }
    }
   }catch(Exception error){outcome="桌面快捷方式保留："+error.Message;}
   if(authenticated)Evidence(root,"desktop-shortcut-uninstall.log",outcome);return outcome;
  }
  static void Evidence(string root,string name,string text){try{CoreDeployment.WriteNew(root,CoreDeployment.Runtime+"/"+name,CoreDeployment.Utf8.GetBytes(text));}catch{ /* Copyable UI/outcome remains available if diagnostics are unwritable. */ }}
  static void RequireResult(InstallerInstallResult result,string root,CoreInstallRecord record){if(result.InstallationId!=record.installationId||result.Version!=record.version||!CoreDeployment.SamePath(result.LauncherPath,Expected(root).Target))throw new InvalidDataException("安装结果与认证安装身份不一致。");}
  static InstallerShortcutLink Expected(string root){string exe=CoreReleaseProtocol.Target(root,"橙子启动器.exe");return new InstallerShortcutLink{Target=exe,WorkingDirectory=root,Arguments="",Icon=exe+",0"};}
  static void RequireLink(InstallerShortcutLink actual,InstallerShortcutLink expected){if(actual==null||actual.Target!=expected.Target||actual.WorkingDirectory!=expected.WorkingDirectory||actual.Arguments!=expected.Arguments||actual.Icon!=expected.Icon)throw new InvalidDataException("快捷方式目标、工作目录、参数或图标不匹配。");}
  string Desktop(){string value=desktop();if(string.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value)||Path.GetFullPath(value).TrimEnd('\\','/')!=value.TrimEnd('\\','/'))throw new IOException("当前用户桌面目录无效。");value=Path.GetFullPath(value).TrimEnd('\\','/');if(!Directory.Exists(value))throw new IOException("当前用户桌面目录不可用。");return value;}
  static string Child(string directory,string name){if(string.IsNullOrEmpty(name)||Path.GetFileName(name)!=name||name.IndexOfAny(new[]{'/', '\\',':','\0'})>=0)throw new IOException("快捷方式必须是桌面直接子文件。");string result=Path.Combine(directory,name);if(Path.GetDirectoryName(result)!=directory)throw new IOException("快捷方式路径越界。");return result;}
  static bool OwnedName(string name){foreach(string display in new[]{"OZL","橙子启动器"}){if(name==display+".lnk")return true;for(int n=2;n<=100;n++)if(name==display+" ("+n+").lnk")return true;}return false;}
  internal sealed class ShortcutReceipt {public int schema;public string installationId,path,target,workingDirectory,arguments,icon,sha256;}

  // Opening reparse points themselves (rather than their targets) also catches a
  // link swapped in between path checks. Directory handles pin every ancestor.
  const uint ReadAccess=0x80000000,DeleteAccess=0x10000,ShareRead=1,ShareWrite=2,OpenExisting=3,OpenReparse=0x200000,BackupSemantics=0x2000000;
  [StructLayout(LayoutKind.Sequential)] struct FileInfo {internal uint Attributes;internal System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;internal uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
  [StructLayout(LayoutKind.Sequential)] struct FileDisposition {[MarshalAs(UnmanagedType.Bool)]internal bool Delete;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle file,out FileInfo info);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileInformationByHandle(SafeFileHandle file,int type,ref FileDisposition value,int size);
  static SafeFileHandle Handle(string path,uint access,uint share,uint flags,bool directory){var handle=CreateFile(path,access,share,IntPtr.Zero,OpenExisting,flags|OpenReparse,IntPtr.Zero);if(handle.IsInvalid){handle.Dispose();throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}FileInfo info;if(!GetFileInformationByHandle(handle,out info)||(info.Attributes&(uint)FileAttributes.ReparsePoint)!=0||((info.Attributes&(uint)FileAttributes.Directory)!=0)!=directory){handle.Dispose();throw new IOException("拒绝重解析点或不匹配的文件类型。");}return handle;}
  static FileStream OpenFile(string path,bool delete){return new FileStream(Handle(path,ReadAccess|(delete?DeleteAccess:0),ShareRead,0,false),FileAccess.Read);}
  sealed class DirectoryLease:IDisposable {
   readonly List<SafeFileHandle> handles=new List<SafeFileHandle>();
   internal DirectoryLease(string directory){try{var paths=new Stack<string>();for(string path=directory;path!=null;path=Path.GetDirectoryName(path))paths.Push(path);foreach(string path in paths)handles.Add(Handle(path,0,ShareRead|ShareWrite,BackupSemantics,true));}catch{Dispose();throw;}}
   public void Dispose(){foreach(var handle in handles)handle.Dispose();handles.Clear();}
  }
  static byte[] Bytes(FileStream file,int max){if(file.Length<1||file.Length>max)throw new InvalidDataException("快捷方式或所有权记录大小无效。");byte[] bytes=new byte[(int)file.Length];int at=0,n;while(at<bytes.Length&&(n=file.Read(bytes,at,bytes.Length-at))>0)at+=n;if(at!=bytes.Length)throw new EndOfStreamException();return bytes;}

  // IShellLink persisted in memory avoids COM Save overwriting a desktop path.
  [ComImport,Guid("00021401-0000-0000-C000-000000000046")]class ShellLink {}
  [ComImport,InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("000214F9-0000-0000-C000-000000000046")]
  interface IShellLink {
   void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int max,IntPtr find,uint flags);void GetIDList(out IntPtr id);void SetIDList(IntPtr id);
   void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder text,int max);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string text);
   void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int max);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string path);
   void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder args,int max);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string args);
   void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int command);void SetShowCmd(int command);
   void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int max,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string path,int index);
   void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr window,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
  }
  [ComImport,InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("00000109-0000-0000-C000-000000000046")]
  interface IPersistStream {void GetClassID(out Guid id);[PreserveSig]int IsDirty();void Load(IStream input);void Save(IStream output,[MarshalAs(UnmanagedType.Bool)]bool clear);void GetSizeMax(out long size);}
  sealed class ComStream:IStream {
   internal readonly MemoryStream Data;internal ComStream(byte[] bytes=null){Data=bytes==null?new MemoryStream():new MemoryStream(bytes);}
   public void Read(byte[] buffer,int count,IntPtr read){int n=Data.Read(buffer,0,count);if(read!=IntPtr.Zero)Marshal.WriteInt32(read,n);}
   public void Write(byte[] buffer,int count,IntPtr written){Data.Write(buffer,0,count);if(written!=IntPtr.Zero)Marshal.WriteInt32(written,count);}
   public void Seek(long offset,int origin,IntPtr position){long p=Data.Seek(offset,(SeekOrigin)origin);if(position!=IntPtr.Zero)Marshal.WriteInt64(position,p);}
   public void SetSize(long size){Data.SetLength(size);}public void Commit(int flags){}public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG stat,int flags){stat=new System.Runtime.InteropServices.ComTypes.STATSTG{type=2,cbSize=Data.Length};}
   public void CopyTo(IStream target,long count,IntPtr read,IntPtr written){throw new NotSupportedException();}public void Revert(){throw new NotSupportedException();}public void LockRegion(long offset,long count,int type){throw new NotSupportedException();}public void UnlockRegion(long offset,long count,int type){throw new NotSupportedException();}public void Clone(out IStream clone){throw new NotSupportedException();}
  }
  internal static string WriteLink(string path,InstallerShortcutLink value){object instance=new ShellLink();try{var link=(IShellLink)instance;link.SetPath(value.Target);link.SetWorkingDirectory(value.WorkingDirectory);link.SetArguments(value.Arguments);int comma=value.Icon.LastIndexOf(',');link.SetIconLocation(value.Icon.Substring(0,comma),int.Parse(value.Icon.Substring(comma+1)));var output=new ComStream();((IPersistStream)instance).Save(output,true);byte[] bytes=output.Data.ToArray();if(bytes.Length<1||bytes.Length>MaxLink)throw new IOException("快捷方式大小无效。");using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}return CoreReleaseProtocol.Hash(bytes);}finally{Marshal.FinalReleaseComObject(instance);}}
  internal static InstallerShortcutLink ReadLink(string path){using(var file=OpenFile(path,false))return Parse(Bytes(file,MaxLink));}
  static InstallerShortcutLink Parse(byte[] bytes){object instance=new ShellLink();try{((IPersistStream)instance).Load(new ComStream(bytes));var link=(IShellLink)instance;var target=new StringBuilder(32768);var work=new StringBuilder(32768);var args=new StringBuilder(32768);var icon=new StringBuilder(32768);int index;link.GetPath(target,target.Capacity,IntPtr.Zero,4);link.GetWorkingDirectory(work,work.Capacity);link.GetArguments(args,args.Capacity);link.GetIconLocation(icon,icon.Capacity,out index);return new InstallerShortcutLink{Target=target.ToString(),WorkingDirectory=work.ToString(),Arguments=args.ToString(),Icon=icon+","+index};}finally{Marshal.FinalReleaseComObject(instance);}}
 }
}
