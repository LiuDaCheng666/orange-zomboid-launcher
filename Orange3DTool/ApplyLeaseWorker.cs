using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Same signed launcher image, dispatched before SDK/bootstrap/UI loading.
 // This worker only leases authenticated bytes and invokes official Update.exe.
 internal sealed class ApplyLeaseRequest {
  public string Root,Current,Package,Updater,Checkpoint,Raw,Signature;public bool Portable;
 }
 internal static class ApplyLeaseWorker {
  internal static string Quote(string s){return "\""+System.Text.RegularExpressions.Regex.Replace(s,@"(\\*)\""","$1$1\\\"")+new string('\\',s.Length-s.TrimEnd('\\').Length)+"\"";}
  internal static Process Start(string file,string[] args){return Process.Start(new ProcessStartInfo(file,string.Join(" ",args.Select(Quote))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(file)});}
  internal static string Read(BinaryReader reader){int n=reader.ReadInt32();if(n<0||n>3000000)throw new InvalidDataException("Invalid lease message.");var b=reader.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return Encoding.UTF8.GetString(b);}
  internal static void Write(BinaryWriter writer,string text){var b=Encoding.UTF8.GetBytes(text);writer.Write(b.Length);writer.Write(b);writer.Flush();}
  internal static NamedPipeServerStream Server(string name){var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));return new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);}
  internal static void Wait(Task task){if(!task.Wait(15000))throw new IOException("更新保护进程响应超时，未启动更新。");task.GetAwaiter().GetResult();}
  internal static void Peer(PipeStream pipe,int pid,bool server){uint actual;if(!(server?GetNamedPipeClientProcessId(pipe.SafePipeHandle.DangerousGetHandle(),out actual):GetNamedPipeServerProcessId(pipe.SafePipeHandle.DangerousGetHandle(),out actual))||actual!=pid)throw new IOException("Update lease process identity mismatch.");}
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(IntPtr pipe,out uint pid);
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(IntPtr pipe,out uint pid);
  internal static void ValidatePaths(ApplyLeaseRequest r,Process parent,VelopackFeed feed){
   string current=Path.GetDirectoryName(parent.MainModule.FileName),root=Path.GetDirectoryName(current),local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   if(Path.GetFileName(parent.MainModule.FileName)!="橙子启动器.exe"||!CoreDeployment.SamePath(r.Root,root)||!CoreDeployment.SamePath(r.Current,current))throw new IOException("Update parent is not the installed launcher.");
   string fallback=Path.Combine(local,VelopackFeed.AppId),packages=Path.GetDirectoryName(r.Package);
   if((!CoreDeployment.SamePath(packages,Path.Combine(root,"packages"))&&!CoreDeployment.SamePath(packages,Path.Combine(fallback,"packages")))||Path.GetFileName(r.Package)!=feed.FileName||(!CoreDeployment.SamePath(r.Updater,Path.Combine(root,"Update.exe"))&&!CoreDeployment.SamePath(r.Updater,Path.Combine(fallback,"Update.exe"))))throw new IOException("Invalid framework paths.");
   if(r.Portable!=File.Exists(Path.Combine(root,".portable"))||!CoreDeployment.SamePath(r.Checkpoint,Path.Combine(DistributionPaths.Resolve(root,current,r.Portable,local),"accepted-release.bin")))throw new IOException("Invalid release checkpoint location.");
   string self=typeof(ApplyLeaseWorker).Assembly.Location;var file=DistributionBootstrap.Read(current,CoreUpdateTrust.PublicKeyXml).Single(f=>f.path=="橙子启动器.exe");
   string expected=Path.Combine(DistributionPaths.ExecutingRoot(root,local),"BackgroundWorkers",file.sha256.ToLowerInvariant(),"橙子启动器.exe");
   if(!CoreDeployment.SamePath(expected,self)||!CoreDeployment.Matches(self,file))throw new IOException("Update lease image is not authenticated.");
   LauncherMaintenance.NoReparse(r.Package);LauncherMaintenance.NoReparse(r.Updater);NativeApplyGuard.Check(root,current,parent.Id);
  }
  internal static void ValidateCommand(ApplyLeaseRequest r,int parent,string[] args){
   var expected=new[]{"--silent","apply","--package",r.Package,"--waitPid",parent.ToString(),"--rootDir",r.Root,"--packageDir",Path.GetDirectoryName(r.Package)};
   if(args==null||!args.SequenceEqual(expected,StringComparer.Ordinal))throw new IOException("Unexpected SDK apply command; update stopped.");
  }
  internal static int Run(string pipeName,int parentPid){
   string error=null;try{using(var parent=Process.GetProcessById(parentPid))using(var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.None)){pipe.Connect(10000);Peer(pipe,parentPid,false);
    using(var reader=new BinaryReader(pipe,Encoding.UTF8,true))using(var writer=new BinaryWriter(pipe,Encoding.UTF8,true)){try{
     var r=new JavaScriptSerializer{MaxJsonLength=3000000}.Deserialize<ApplyLeaseRequest>(Read(reader));byte[] raw=Convert.FromBase64String(r.Raw),sig=Convert.FromBase64String(r.Signature);
     var feed=VelopackFeed.Verify(raw,sig,CoreUpdateTrust.PublicKeyXml,AppVersion.Current);ValidatePaths(r,parent,feed);error=Path.Combine(Path.GetDirectoryName(r.Checkpoint),"last-apply-error.txt");LauncherMaintenance.NoReparse(error);if(File.Exists(error))File.Delete(error);
     using(ReleaseCheckpoint.Acquire(r.Checkpoint)){
      VelopackFeed.Verify(raw,sig,CoreUpdateTrust.PublicKeyXml,ReleaseCheckpoint.Floor(r.Checkpoint,CoreUpdateTrust.PublicKeyXml,AppVersion.Current));
      using(var package=new FileStream(r.Package,FileMode.Open,FileAccess.Read,FileShare.Read))using(var updater=new FileStream(r.Updater,FileMode.Open,FileAccess.Read,FileShare.Read)){
       if(package.Length!=feed.Length||!string.Equals(CoreReleaseProtocol.HashFile(r.Package),feed.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Leased package differs from signed release.");
       using(var zip=new ZipArchive(package,ZipArchiveMode.Read,true)){var entries=zip.Entries.Where(e=>e.FullName.EndsWith("Squirrel.exe",StringComparison.Ordinal)).ToArray();if(entries.Length!=1||entries[0].Length!=updater.Length)throw new IOException("Invalid signed upstream updater.");using(var content=entries[0].Open())using(var sha=System.Security.Cryptography.SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(content)).Replace("-","");if(hash!=CoreReleaseProtocol.HashFile(r.Updater))throw new IOException("Updater differs from signed package.");}}
       Write(writer,"READY");string[] command=new JavaScriptSerializer().Deserialize<string[]>(Read(reader));ValidateCommand(r,parentPid,command);NativeApplyGuard.Check(r.Root,r.Current,parentPid);Write(writer,"ARMED");if(Read(reader)!="COMMIT")throw new IOException("Update handoff was not committed.");
       // A disconnected/cancelled parent cannot commit. After COMMIT, only actual
       // parent exit permits launch, and the lease outlives the consumer handle.
       if(!parent.WaitForExit(60000))throw new IOException("Launcher did not exit; update was not started.");
       using(NativeApplyGuard.Instance(r.Root)){NativeApplyGuard.Check(r.Root,r.Current,-1);using(var consumer=Start(r.Updater,command)){consumer.WaitForExit();if(consumer.ExitCode!=0)throw new IOException("官方更新程序未完成更新，退出码："+consumer.ExitCode);}}
      }
     }
    }catch(Exception e){try{Write(writer,"ERROR "+e.Message);}catch{}throw;}}
   }return 0;}catch(Exception e){if(error!=null)try{LauncherMaintenance.NoReparse(error);Directory.CreateDirectory(Path.GetDirectoryName(error));File.WriteAllText(error,e.Message);}catch{}return 1;}
  }
 }
}
