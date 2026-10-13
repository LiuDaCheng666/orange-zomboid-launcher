using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

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
  internal const string RecordName="orange-installation.json";
  internal const string Runtime="OrangeLauncherRuntime";
  internal const string Owner=Runtime+"/.orange-core-owner";
  internal const string Tasks=Runtime+"/CoreUpdates";
  internal static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  internal static JavaScriptSerializer Json {get{return new JavaScriptSerializer {MaxJsonLength=CoreReleaseProtocol.MaxManifestBytes};}}

  public static bool Managed(string root) {try{Read(root);return true;}catch{return false;}}
  public static CoreInstallRecord Read(string root) {root=Root(root);return ReadRecord(root,ReadBytes(Safe(root,RecordName),CoreReleaseProtocol.MaxManifestBytes));}
  internal static CoreInstallRecord ReadRecord(string root,byte[] bytes) {
   CoreReleaseProtocol.ReadStoredObject(bytes,"schema","root","installationId","version","files");
   var record=Json.Deserialize<CoreInstallRecord>(Utf8.GetString(bytes));
   if(record==null||record.schema!=2||!string.Equals(root,record.root,StringComparison.OrdinalIgnoreCase)||!Id(record.installationId))throw new InvalidDataException("Invalid installation ownership record.");
   CoreReleaseProtocol.Validate(new CoreManifest {schema=1,version=record.version,platform="win-x64",minimumUpdaterVersion="1.0.0",notes="",files=record.files},"0.0.0");
   foreach(var file in record.files)CoreReleaseProtocol.Target(root,file.path);
   if(ReadOwner(root)!=record.installationId)throw new InvalidDataException("Installation ownership marker does not match.");
   return record;
  }
  public static CoreInstallRecord Install(string root,CoreManifest manifest,IDictionary<string,byte[]> files) {return Install(root,manifest,files,CoreUpdateTrust.PublicKeyXml);}
  public static CoreInstallRecord Install(string root,CoreManifest manifest,IDictionary<string,byte[]> files,string publicKeyXml) {
   root=Root(root);
   using(Acquire(root)) {
    var verified=Authenticate(manifest,publicKeyXml,"0.0.0");
    if(Directory.Exists(root)&&Directory.EnumerateFileSystemEntries(root).Any())throw new IOException("Installation destination is not empty.");
    CoreUpdateTransaction.ValidateDownloads(verified.files,files);
    Directory.CreateDirectory(root);Directory.CreateDirectory(Safe(root,Runtime));
    string installationId=Guid.NewGuid().ToString("N");WriteNew(root,Owner,Utf8.GetBytes(installationId));
    string task=CoreUpdateTransaction.StageLocked(root,verified,files,null,installationId);
    CoreUpdateTransaction.ApplyLocked(root,task,exe=>true,null,publicKeyXml);
    return Read(root);
   }
  }
  public static string Uninstall(string root,string runningExe=null) {
   root=Root(root);
   using(Acquire(root)) {
    CoreUpdateTransaction.EnsureNoPending(root);
    var record=Read(root);ValidateBaseline(root,record);
    // Preflight every owned file before deleting any. Modified files fail closed.
    foreach(var entry in record.files) {
     string file=CoreReleaseProtocol.Target(root,entry.path);
     if(File.Exists(file)&&!SamePath(file,runningExe))using(new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {}
    }
    bool skipped=false;
    foreach(var entry in record.files) {
     string file=CoreReleaseProtocol.Target(root,entry.path);if(!File.Exists(file))continue;
     if(SamePath(file,runningExe)){skipped=true;continue;}
     RequireFile(root,entry.path,entry);File.Delete(CoreReleaseProtocol.Target(root,entry.path));
    }
    // Runtime diagnostics and unrelated files are intentionally retained. A running
    // uninstaller keeps the ownership record until a subsequent safe removal.
    if(!skipped)File.Delete(Safe(root,RecordName));
    return "卸载完成。保留个人文件和更新诊断记录。";
   }
  }
  internal static bool SamePath(string a,string b) {return b!=null&&string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}
  internal static CoreManifest Authenticate(CoreManifest manifest,string key,string current) {
   if(manifest==null||manifest.VerifiedEnvelope==null)throw new CryptographicException("A signed release envelope is required.");
   return CoreReleaseProtocol.Verify(manifest.VerifiedEnvelope.Raw,manifest.VerifiedEnvelope.Signature,key,current);
  }
  internal static string Root(string root) {CoreReleaseProtocol.Target(root,"VERSION");return PortableDeployment.ValidateRoot(root);}
  internal static bool Id(string value) {return value!=null&&Regex.IsMatch(value,@"\A[0-9a-f]{32}\z");}
  internal static string Safe(string root,string relative) {
   if(string.IsNullOrEmpty(relative)||relative.Split('/').Any(p=>p==""||p=="."||p==".."||p.IndexOfAny(new[]{'\\',':','\0'})>=0))throw new InvalidDataException("Invalid runtime path.");
   string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
   if(!path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Runtime path escapes root.");
   for(string item=path;item!=null;item=Path.GetDirectoryName(item)) {
    try {if((File.GetAttributes(item)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Reparse points are not permitted.");}
    catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
   }
   return path;
  }
  internal static byte[] ReadBytes(string path,int max) {
   using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
    if(input.Length<1||input.Length>max)throw new InvalidDataException("Invalid stored document size.");
    byte[] bytes=new byte[(int)input.Length];int at=0,n;while(at<bytes.Length&&(n=input.Read(bytes,at,bytes.Length-at))>0)at+=n;
    if(at!=bytes.Length)throw new EndOfStreamException();return bytes;
   }
  }
  internal static string ReadOwner(string root) {string id=Utf8.GetString(ReadBytes(Safe(root,Owner),64));if(!Id(id))throw new InvalidDataException("Invalid runtime ownership marker.");return id;}
  internal static void WriteNew(string root,string relative,byte[] bytes) {
   string path=Safe(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));path=Safe(root,relative);
   using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.WriteThrough)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
  }
  internal static void Atomic(string root,string relative,byte[] bytes,Action prepared=null) {
   string temp=relative+"."+Guid.NewGuid().ToString("N")+".tmp";WriteNew(root,temp,bytes);
   if(prepared!=null)prepared();
   string target=Safe(root,relative),source=Safe(root,temp);
   if(Directory.Exists(target))throw new IOException("A directory occupies a file target.");
   if(File.Exists(target))File.Replace(source,target,null);else File.Move(source,target);
  }
  internal static bool Matches(string path,CoreFile file) {return File.Exists(path)&&new FileInfo(path).Length==file.length&&string.Equals(CoreReleaseProtocol.HashFile(path),file.sha256,StringComparison.OrdinalIgnoreCase);}
  internal static void RequireFile(string root,string relative,CoreFile file) {if(!Matches(CoreReleaseProtocol.Target(root,relative),file))throw new IOException("Owned file conflict: "+relative);}
  internal static void ValidateBaseline(string root,CoreInstallRecord record) {
   foreach(var file in record.files) {string path=CoreReleaseProtocol.Target(root,file.path);if(Directory.Exists(path)||(File.Exists(path)&&!Matches(path,file)))throw new IOException("Owned baseline conflict: "+file.path);}
  }
  internal static IDisposable Acquire(string root) {return new RootLock(root);}
  sealed class RootLock:IDisposable {
   readonly Mutex mutex;bool held;
   internal RootLock(string root) {
    mutex=new Mutex(false,@"Global\OrangeLauncher.Core."+CoreReleaseProtocol.Hash(Utf8.GetBytes(root.ToUpperInvariant())));
    try {try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("Another process owns this installation transaction.");}
    catch{mutex.Dispose();throw;}
   }
   public void Dispose(){if(held){held=false;mutex.ReleaseMutex();}mutex.Dispose();}
  }
 }
}
