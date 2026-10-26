// Installer-only shell-link reader; no legacy creation, ownership or uninstall flow.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace PZ3D {
 internal sealed class InstallerShortcutResult {
  internal bool Created{get;private set;}internal string Path{get;private set;}internal string Message{get;private set;}
  internal InstallerShortcutResult(bool created,string path,string message){Created=created;Path=path??"";Message=message??"";}
 }
 internal sealed class InstallerShortcutLink {internal string Target,WorkingDirectory,Arguments,Icon;}
 internal sealed class InstallerShortcut {
  const int MaxLink=1048576;
  const uint ReadAccess=0x80000000,DeleteAccess=0x10000,ShareRead=1,ShareWrite=2,OpenExisting=3,OpenReparse=0x200000,BackupSemantics=0x2000000;
  [StructLayout(LayoutKind.Sequential)] struct FileInfo {internal uint Attributes;internal System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;internal uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(SafeFileHandle file,out FileInfo info);
  static SafeFileHandle Handle(string path,uint access,uint share,uint flags,bool directory){var handle=CreateFile(path,access,share,IntPtr.Zero,OpenExisting,flags|OpenReparse,IntPtr.Zero);if(handle.IsInvalid){handle.Dispose();throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}FileInfo info;if(!GetFileInformationByHandle(handle,out info)||(info.Attributes&(uint)FileAttributes.ReparsePoint)!=0||((info.Attributes&(uint)FileAttributes.Directory)!=0)!=directory){handle.Dispose();throw new IOException("拒绝重解析点或不匹配的文件类型。");}return handle;}
  static FileStream OpenFile(string path,bool delete){return new FileStream(Handle(path,ReadAccess|(delete?DeleteAccess:0),ShareRead,0,false),FileAccess.Read);}
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
  internal static InstallerShortcutLink ReadLink(string path){using(var file=OpenFile(path,false))return Parse(Bytes(file,MaxLink));}
  static InstallerShortcutLink Parse(byte[] bytes){object instance=new ShellLink();try{((IPersistStream)instance).Load(new ComStream(bytes));var link=(IShellLink)instance;var target=new StringBuilder(32768);var work=new StringBuilder(32768);var args=new StringBuilder(32768);var icon=new StringBuilder(32768);int index;link.GetPath(target,target.Capacity,IntPtr.Zero,4);link.GetWorkingDirectory(work,work.Capacity);link.GetArguments(args,args.Capacity);link.GetIconLocation(icon,icon.Capacity,out index);return new InstallerShortcutLink{Target=target.ToString(),WorkingDirectory=work.ToString(),Arguments=args.ToString(),Icon=icon+","+index};}finally{Marshal.FinalReleaseComObject(instance);}}
 }
}
