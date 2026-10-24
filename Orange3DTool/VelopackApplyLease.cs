using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Velopack;
using Velopack.Locators;
namespace PZ3D {
 internal sealed class VelopackApplyLease:IVelopackLocator,IProcessImpl {
  readonly IVelopackLocator inner;readonly AuthenticatedVelopackSource source;readonly CancellationToken token;
  internal VelopackApplyLease(IVelopackLocator inner,AuthenticatedVelopackSource source,CancellationToken token){this.inner=inner;this.source=source;this.token=token;}
  public void StartProcess(string file,IEnumerable<string> args,string workingDirectory,bool x){
   if(!CoreDeployment.SamePath(file,UpdateExePath))throw new IOException("Unexpected SDK executable.");
   string[] command=args.ToArray();var request=source.LeaseRequest(RootAppDir,AppContentDir,PackagesDir,UpdateExePath,IsPortable);int pid=System.Diagnostics.Process.GetCurrentProcess().Id;
   ApplyLeaseWorker.ValidateCommand(request,pid,command);NativeApplyGuard.Check(RootAppDir,AppContentDir,pid);token.ThrowIfCancellationRequested();
   string original=typeof(VelopackApplyLease).Assembly.Location,image=ManagedBackgroundWorker.PrepareDistribution(original,CoreUpdateTrust.PublicKeyXml,DistributionPaths.ExecutingRoot(RootAppDir,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
   string name="OZL-Apply-"+Guid.NewGuid().ToString("N");using(var pipe=ApplyLeaseWorker.Server(name)){
    using(var current=new FileStream(original,FileMode.Open,FileAccess.Read,FileShare.Read))using(var cached=new FileStream(image,FileMode.Open,FileAccess.Read,FileShare.Read)){
     var record=DistributionBootstrap.Read(AppContentDir,CoreUpdateTrust.PublicKeyXml).Single(f=>f.path=="橙子启动器.exe");if(!CoreDeployment.Matches(image,record))throw new IOException("Lease worker changed before launch.");
     using(var child=ApplyLeaseWorker.Start(image,new[]{"--velopack-lease",name,pid.ToString()})){
      ApplyLeaseWorker.Wait(Task.Factory.FromAsync(pipe.BeginWaitForConnection,pipe.EndWaitForConnection,null));ApplyLeaseWorker.Peer(pipe,child.Id,true);
      using(var reader=new BinaryReader(pipe,Encoding.UTF8,true))using(var writer=new BinaryWriter(pipe,Encoding.UTF8,true)){
       ApplyLeaseWorker.Write(writer,new JavaScriptSerializer{MaxJsonLength=3000000}.Serialize(request));string response=null;ApplyLeaseWorker.Wait(Task.Run(()=>response=ApplyLeaseWorker.Read(reader)));if(response!="READY")throw new IOException("Update lease could not authenticate package: "+response);
       token.ThrowIfCancellationRequested();ApplyLeaseWorker.Write(writer,new JavaScriptSerializer().Serialize(command));ApplyLeaseWorker.Wait(Task.Run(()=>response=ApplyLeaseWorker.Read(reader)));if(response!="ARMED")throw new IOException("Update lease was not armed: "+response);token.ThrowIfCancellationRequested();ApplyLeaseWorker.Write(writer,"COMMIT");
      }
     }
    }
   }
  }
  public string AppId{get{return inner.AppId;}}public string RootAppDir{get{return inner.RootAppDir;}}public string PackagesDir{get{return inner.PackagesDir;}}public string AppContentDir{get{return inner.AppContentDir;}}public string AppTempDir{get{return inner.AppTempDir;}}public string UpdateExePath{get{return inner.UpdateExePath;}}public SemanticVersion CurrentlyInstalledVersion{get{return inner.CurrentlyInstalledVersion;}}public string ThisExeRelativePath{get{return inner.ThisExeRelativePath;}}public string Channel{get{return inner.Channel;}}public string AppUserModelId{get{return inner.AppUserModelId;}}public Velopack.Logging.IVelopackLogger Log{get{return inner.Log;}}public bool IsPortable{get{return inner.IsPortable;}}public IProcessImpl Process{get{return this;}}
  public void AddLogger(Velopack.Logging.IVelopackLogger logger){inner.AddLogger(logger);}public List<VelopackAsset> GetLocalPackages(){return inner.GetLocalPackages();}public VelopackAsset GetLatestLocalFullPackage(){return inner.GetLatestLocalFullPackage();}public Guid? GetOrCreateStagedUserId(){return inner.GetOrCreateStagedUserId();}public string GetCurrentProcessPath(){return inner.Process.GetCurrentProcessPath();}public uint GetCurrentProcessId(){return inner.Process.GetCurrentProcessId();}public void Exit(int code){throw new InvalidOperationException("Host owns guarded shutdown.");}
 }
}
