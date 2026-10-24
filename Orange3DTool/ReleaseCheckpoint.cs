using System;
using System.IO;
using System.Threading;
namespace PZ3D {
 // The exclusive sidecar is a filesystem identity, so long/short aliases cannot
 // create independent locks for the same checkpoint. It also denies deletion.
 // All read/compare/replace operations and the native consumer share this lease.
 internal static class ReleaseCheckpoint {
  internal static IDisposable Acquire(string path){
   if(path==null)return new Empty();string file=Path.GetFullPath(path)+".lock";LauncherMaintenance.NoReparse(file);Directory.CreateDirectory(Path.GetDirectoryName(file));var deadline=DateTime.UtcNow.AddSeconds(10);
   while(true){try{return new FileStream(file,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(DateTime.UtcNow>=deadline)throw new IOException("另一个启动器正在检查或应用更新，请稍后重试。");Thread.Sleep(25);LauncherMaintenance.NoReparse(file);}}
  }
  internal static string Floor(string path,string key,string current){
   if(path==null)return current;LauncherMaintenance.NoReparse(path);if(!File.Exists(path))return current;
   using(var reader=new BinaryReader(new MemoryStream(CoreDeployment.ReadBytes(path,1050000)))){
    int a=reader.ReadInt32(),b=reader.ReadInt32();if(a<=0||a>1048576||b<=0||b>1024||reader.BaseStream.Length!=8L+a+b)throw new InvalidDataException("Invalid release checkpoint.");
    var old=VelopackFeed.Verify(reader.ReadBytes(a),reader.ReadBytes(b),key,"0.0.0");return CoreReleaseProtocol.IsNewer(old.Version,current)?old.Version:current;
   }
  }
  internal static void Write(string path,byte[] raw,byte[] signature){if(path==null)return;LauncherMaintenance.NoReparse(path);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N");try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new BinaryWriter(stream)){writer.Write(raw.Length);writer.Write(signature.Length);writer.Write(raw);writer.Write(signature);writer.Flush();stream.Flush(true);}LauncherMaintenance.NoReparse(path);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  sealed class Empty:IDisposable{public void Dispose(){}}
 }
}
