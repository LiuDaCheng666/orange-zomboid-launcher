using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace PZ3D {
 // Native prefixes belong only at the Windows I/O boundary. Stored paths and
 // enumerated names stay ordinary absolute paths for manifests and game APIs.
 internal static class SnapshotFileSystem {
  internal static string DisplayPath(string path){if(path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return @"\\"+path.Substring(8);return path.StartsWith(@"\\?\",StringComparison.Ordinal)?path.Substring(4):path;}
  internal static string NativePath(string path){
   if(string.IsNullOrWhiteSpace(path))throw new IOException("快照文件路径不能为空。");string display=DisplayPath(path);
   bool drive=display.Length>=3&&char.IsLetter(display[0])&&display[1]==':'&&(display[2]=='\\'||display[2]=='/');
   if(!drive&&!display.StartsWith(@"\\",StringComparison.Ordinal)||display.StartsWith(@"\\.\",StringComparison.Ordinal))throw new IOException("快照文件路径必须是完整的绝对路径。");
   string full=System.IO.Path.GetFullPath(display);return full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;
  }
  [DllImport("kernel32.dll",EntryPoint="GetFileAttributesW",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint PathAttributes(string path);
  [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CreateDirectoryNative(string path,IntPtr security);
  [DllImport("kernel32.dll",EntryPoint="RemoveDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool RemoveDirectoryNative(string path);
  [DllImport("kernel32.dll",EntryPoint="DeleteFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool DeleteFileNative(string path);
  [DllImport("kernel32.dll",EntryPoint="MoveFileExW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool MoveNative(string source,string destination,uint flags);
  [DllImport("kernel32.dll",EntryPoint="ReplaceFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool ReplaceNative(string destination,string source,string backup,uint flags,IntPtr exclude,IntPtr reserved);
  [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern SafeFileHandle CreateFileNative(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetFileTime(SafeFileHandle file,IntPtr created,IntPtr accessed,ref long written);
  [StructLayout(LayoutKind.Sequential,Pack=4)]struct AttributeData {internal uint attributes;internal long created,accessed,written;internal uint sizeHigh,sizeLow;}
  [DllImport("kernel32.dll",EntryPoint="GetFileAttributesExW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool AttributesNative(string path,int level,out AttributeData data);
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct FindData {internal uint attributes;internal System.Runtime.InteropServices.ComTypes.FILETIME created,accessed,written;internal uint sizeHigh,sizeLow,reserved0,reserved1;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]internal string name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=14)]internal string alternate;}
  [DllImport("kernel32.dll",EntryPoint="FindFirstFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr FindFirst(string pattern,out FindData data);
  [DllImport("kernel32.dll",EntryPoint="FindNextFileW",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool FindNext(IntPtr find,out FindData data);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool FindClose(IntPtr find);
  static IOException Error(string action,string path,int error){return new IOException(action+"："+path,new Win32Exception(error));}
  static bool Exists(string path,bool directory){if(string.IsNullOrEmpty(path))return false;uint value=PathAttributes(NativePath(path));return value!=uint.MaxValue&&((value&(uint)FileAttributes.Directory)!=0)==directory;}
  static SafeFileHandle Open(string path,FileMode mode,FileAccess access,FileShare share){uint creation;switch(mode){case FileMode.CreateNew:creation=1;break;case FileMode.Create:creation=2;break;case FileMode.Open:creation=3;break;case FileMode.OpenOrCreate:creation=4;break;case FileMode.Truncate:creation=5;break;default:throw new NotSupportedException("快照不使用追加文件模式。");}uint rights=0;if((access&FileAccess.Read)!=0)rights|=0x80000000;if((access&FileAccess.Write)!=0)rights|=0x40000000;var handle=CreateFileNative(NativePath(path),rights,(uint)share&7,IntPtr.Zero,creation,0x80,IntPtr.Zero);if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();throw Error("无法打开快照文件",path,error);}return handle;}
  // Payloads remain independent files. Storage controls work on Windows 10 as
  // well as older NTFS versions; no Windows 11-only sparse-copy flag is needed.
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool DeviceIoControl(SafeFileHandle handle,uint code,byte[] input,uint inputLength,IntPtr output,uint outputLength,out uint returned,IntPtr overlapped);
  static bool StorageFlag(Stream output,uint code,byte[] input,string path){uint returned;if(DeviceIoControl(output.SafeFileHandle,code,input,input==null?0:(uint)input.Length,IntPtr.Zero,0,out returned,IntPtr.Zero))return true;int error=Marshal.GetLastWin32Error();if(error==1||error==50)return false;throw Error("无法保留文件存储属性",path,error);}
  internal static string CopyPayload(string source,string destination,long expectedLength,CancellationToken cancellation,Action<int> progress){
   cancellation.ThrowIfCancellationRequested();var attributes=Files.GetAttributes(source);
   using(var sha=SHA256.Create())using(var from=new Stream(source,FileMode.Open,FileAccess.Read,FileShare.Read))using(var to=new Stream(destination,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){
    if(from.Length!=expectedLength)throw new IOException("复制前来源文件已变化。");
    bool compressed=(attributes&FileAttributes.Compressed)!=0&&StorageFlag(to,0x0009c040,new byte[]{1,0},destination);
    bool sparse=(attributes&FileAttributes.SparseFile)!=0&&StorageFlag(to,0x000900c4,null,destination);
    byte[] buffer=new byte[(int)Math.Max(1,Math.Min(1048576L,expectedLength))];long copied=0;int count;
    while(true){cancellation.ThrowIfCancellationRequested();count=from.Read(buffer,0,buffer.Length);if(count==0)break;cancellation.ThrowIfCancellationRequested();
     if(sparse){for(int offset=0;offset<count;){int length=Math.Min(65536,count-offset),end=offset+length,index=offset;while(index<end&&buffer[index]==0)index++;if(index==end)to.Seek(length,SeekOrigin.Current);else to.Write(buffer,offset,length);offset=end;}}
     else to.Write(buffer,0,count);
     sha.TransformBlock(buffer,0,count,null,0);copied+=count;if(progress!=null)progress(count);cancellation.ThrowIfCancellationRequested();
    }
    if(copied!=expectedLength||from.Length!=expectedLength)throw new IOException("来源文件在复制期间变化。");
    if(sparse)to.SetLength(copied);if(compressed)to.Flush(true);sha.TransformFinalBlock(new byte[0],0,0);
    // Compressed files need a flush to realize compressed allocation before
    // publication; ordinary payloads only flush managed buffers on close.
    // Durable metadata barriers remain on the final
    // manifest/index publication, not every individual verified payload file.
    return BitConverter.ToString(sha.Hash).Replace("-","");
   }
  }
  internal static void NoReparse(string path){string current=System.IO.Path.GetFullPath(DisplayPath(path));while(current!=null){uint attributes=PathAttributes(NativePath(current));if(attributes==uint.MaxValue){int error=Marshal.GetLastWin32Error();if(error!=2&&error!=3)throw new IOException("无法核实快照路径属性："+current,new Win32Exception(error));}else if((attributes&(uint)FileAttributes.ReparsePoint)!=0)throw new IOException("拒绝重解析点 / 联接路径："+current);current=System.IO.Path.GetDirectoryName(current);}}
  internal static class Directories {
   public static void CreateDirectory(string path){string display=DisplayPath(NativePath(path));uint attributes=PathAttributes(NativePath(display));if(attributes!=uint.MaxValue){if((attributes&(uint)FileAttributes.Directory)==0)throw new IOException("快照目录位置已被文件占用："+display);return;}int error=Marshal.GetLastWin32Error();if(error!=2&&error!=3)throw Error("无法读取快照目录",display,error);string parent=System.IO.Path.GetDirectoryName(display.TrimEnd('\\'));if(parent!=null)CreateDirectory(parent);if(!CreateDirectoryNative(NativePath(display),IntPtr.Zero)){error=Marshal.GetLastWin32Error();if(error!=183||!Exists(display))throw Error("无法创建快照目录",display,error);}}
   public static bool Exists(string path){return SnapshotFileSystem.Exists(path,true);}
   public static IEnumerable<string> EnumerateFileSystemEntries(string path){string display=DisplayPath(NativePath(path));FindData data;IntPtr find=FindFirst(NativePath(display).TrimEnd('\\')+@"\*",out data);if(find==new IntPtr(-1)){int error=Marshal.GetLastWin32Error();if(error==2)yield break;throw Error("无法枚举快照目录",display,error);}try{while(true){if(data.name!="."&&data.name!="..")yield return System.IO.Path.Combine(display,data.name);if(!FindNext(find,out data)){int error=Marshal.GetLastWin32Error();if(error!=18)throw Error("快照目录枚举未完成",display,error);break;}}}finally{FindClose(find);}}
   public static void Delete(string path){if(!RemoveDirectoryNative(NativePath(path)))throw Error("无法删除快照目录",path,Marshal.GetLastWin32Error());}
   public static void Delete(string path,bool recursive){if(recursive)foreach(string item in EnumerateFileSystemEntries(path)){var attributes=Files.GetAttributes(item);if((attributes&FileAttributes.Directory)!=0){if((attributes&FileAttributes.ReparsePoint)!=0)Delete(item);else Delete(item,true);}else Files.Delete(item);}Delete(path);}
   public static void Move(string source,string destination){Files.Move(source,destination);}
  }
  internal static class Files {
   public static bool Exists(string path){return SnapshotFileSystem.Exists(path,false);}
   public static void Delete(string path){if(!DeleteFileNative(NativePath(path))){int error=Marshal.GetLastWin32Error();if(error!=2&&error!=3)throw Error("无法删除快照文件",path,error);}}
   public static FileAttributes GetAttributes(string path){uint value=PathAttributes(NativePath(path));if(value==uint.MaxValue)throw Error("无法读取快照文件属性",path,Marshal.GetLastWin32Error());return (FileAttributes)value;}
   public static void SetLastWriteTimeUtc(string path,DateTime value){using(var handle=CreateFileNative(NativePath(path),0x100,7,IntPtr.Zero,3,0x80,IntPtr.Zero)){if(handle.IsInvalid)throw Error("无法打开快照文件时间属性",path,Marshal.GetLastWin32Error());long written=value.ToFileTimeUtc();if(!SetFileTime(handle,IntPtr.Zero,IntPtr.Zero,ref written))throw Error("无法保存快照文件时间",path,Marshal.GetLastWin32Error());}}
   public static void WriteAllBytes(string path,byte[] bytes){using(var output=new Stream(path,FileMode.Create,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);}}
   public static void Move(string source,string destination){if(!MoveNative(NativePath(source),NativePath(destination),0))throw Error("无法移动快照文件或目录",destination,Marshal.GetLastWin32Error());}
   public static void Replace(string source,string destination,string backup){if(!ReplaceNative(NativePath(destination),NativePath(source),backup==null?null:NativePath(backup),0,IntPtr.Zero,IntPtr.Zero))throw Error("无法原子保存快照元数据",destination,Marshal.GetLastWin32Error());}
  }
  internal sealed class Stream:System.IO.FileStream {
   public Stream(string path,FileMode mode,FileAccess access,FileShare share):base(Open(path,mode,access,share),access){}
  }
  internal sealed class Info {
   readonly AttributeData value;
   public Info(string path){if(!AttributesNative(NativePath(path),0,out value))throw Error("无法读取快照文件元数据",path,Marshal.GetLastWin32Error());}
   public long Length {get{return ((long)value.sizeHigh<<32)|value.sizeLow;}}
   public DateTime LastWriteTimeUtc {get{return DateTime.FromFileTimeUtc(value.written);}}
  }
 }
}
