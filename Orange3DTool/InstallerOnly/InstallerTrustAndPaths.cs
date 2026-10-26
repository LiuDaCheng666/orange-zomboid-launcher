// Installer-only primitives copied from the existing production implementations.
// Keep path, reparse-point, bounded-read and embedded-key semantics in sync.
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace PZ3D {
 public static class CoreUpdateTrust {
  public static string PublicKeyXml {
   get {using(var input=typeof(CoreUpdateTrust).Assembly.GetManifestResourceStream("Launcher.UpdatePublicKey.xml")) {
    if(input==null)throw new InvalidDataException("Missing embedded update trust key.");
    using(var reader=new StreamReader(input,new UTF8Encoding(false,true)))return reader.ReadToEnd();
   }}
  }
 }
 public static class CoreDeployment {
  internal const string Runtime="OrangeLauncherRuntime";
  internal static bool SamePath(string a,string b) {return b!=null&&string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}
  internal static string Root(string root) {CoreReleaseProtocol.Target(root,"VERSION");return PortableDeployment.ValidateRoot(root);}
  internal static byte[] ReadBytes(string path,int max) {
   return SharingRetry(()=>{using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)) {
    if(input.Length<1||input.Length>max)throw new InvalidDataException("Invalid stored document size.");
    byte[] bytes=new byte[(int)input.Length];int at=0,n;while(at<bytes.Length&&(n=input.Read(bytes,at,bytes.Length-at))>0)at+=n;
    if(at!=bytes.Length)throw new EndOfStreamException();return bytes;
   }},true);
  }
  // Immutable snapshots allow atomic replacement, but never concurrent in-place
  // writes. Windows sharing/lock violations and a read during the replacement
  // name transition are bounded; parsing, bounds and authentication are not retried.
  static T SharingRetry<T>(Func<T> action,bool snapshot=false){for(int attempt=0;;attempt++){try{return action();}catch(IOException e){int code=e.HResult&65535;if(attempt>=20||(code!=32&&code!=33&&!(snapshot&&code==2)))throw;Thread.Sleep(25);}}}
 }
 static class PortableDeployment {
  public static string ValidateRoot(string root){string full=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);LauncherMaintenance.NoReparse(full);string drive=Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar);if(full==drive||full.Length<drive.Length+3||!string.IsNullOrEmpty(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))&&full.Equals(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),StringComparison.OrdinalIgnoreCase)||full.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows),StringComparison.OrdinalIgnoreCase)||File.Exists(Path.Combine(full,"ProjectZomboid64.exe")))throw new Exception("请选择独立的工具文件夹，不能安装到盘符根目录、用户根目录、Windows 或游戏目录。");return full;}
 }
 static class LauncherMaintenance {
  [DllImport("kernel32.dll",EntryPoint="GetFileAttributesW",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint PathAttributes(string path);
  internal static void NoReparse(string path){string current=Path.GetFullPath(path);while(current!=null){uint attributes=PathAttributes(current);if(attributes==uint.MaxValue){int error=Marshal.GetLastWin32Error();if(error!=2&&error!=3)throw new IOException("无法核实路径属性："+current,new System.ComponentModel.Win32Exception(error));}else if((attributes&(uint)FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝重解析点 / 联接路径："+current);current=Path.GetDirectoryName(current);}}
 }
}
