using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Velopack;
using Velopack.Locators;
namespace PZ3D {
 internal sealed class VelopackUpdateBackend:IDisposable {
  readonly object gate=new object();readonly AuthenticatedVelopackSource source;readonly UpdateManager manager;readonly IVelopackLocator locator;readonly bool lease;
  CoreUpdateStatus status=new CoreUpdateStatus();UpdateInfo candidate;CancellationTokenSource cancel;Task worker;bool disposed;internal Action<CoreUpdateStatus> Progress;
  internal VelopackUpdateBackend():this(new AuthenticatedVelopackSource(CoreUpdateTrust.PublicKeyXml,AppVersion.Current,VelopackNetwork.Open,DistributionPaths.Child("accepted-release.bin")),VelopackLocator.Current,true){string error=DistributionPaths.Child("last-apply-error.txt");if(File.Exists(error)){status.state="Error";status.message=System.Text.Encoding.UTF8.GetString(CoreDeployment.ReadBytes(error,16384));File.Delete(error);}}
  // Only unmarked compiled fixtures may disable the OS lease for recording tests.
  internal VelopackUpdateBackend(AuthenticatedVelopackSource source,IVelopackLocator locator,bool lease=true){if(!lease&&DistributionPaths.Enabled)throw new InvalidOperationException("Production apply requires a process lease.");this.source=source;this.locator=locator;this.lease=lease;manager=new UpdateManager(source,new UpdateOptions{AllowVersionDowngrade=false,ExplicitChannel="win",MaximumDeltasBeforeFallback=0},locator);}
  CoreUpdateStatus Clone(){return new JavaScriptSerializer().Deserialize<CoreUpdateStatus>(new JavaScriptSerializer().Serialize(status));}
  internal CoreUpdateStatus Status(){lock(gate)return Clone();}
  void Change(Action<CoreUpdateStatus> change){CoreUpdateStatus s;lock(gate){change(status);status.revision++;s=Clone();}var p=Progress;if(p!=null)try{p(s);}catch{}}
  CoreUpdateStatus Start(string phase,Action<CancellationToken> action){lock(gate){if(disposed)throw new ObjectDisposedException("Updater");if(worker!=null&&!worker.IsCompleted||status.state=="Restarting")return Clone();if(cancel!=null)cancel.Dispose();cancel=new CancellationTokenSource();var token=cancel.Token;status.state=phase;status.revision++;worker=Task.Run(()=>{try{action(token);}catch(Exception e){Change(s=>{s.state=token.IsCancellationRequested?"Cancelled":"Error";s.message=token.IsCancellationRequested?"已取消更新。":e.Message;});}});return Clone();}}
  internal CoreUpdateStatus Check(){return Start("Checking",token=>{candidate=null;source.CheckToken=token;var update=manager.CheckForUpdatesAsync().GetAwaiter().GetResult();token.ThrowIfCancellationRequested();lock(gate){token.ThrowIfCancellationRequested();candidate=update;Change(s=>{s.checkedUtc=DateTime.UtcNow.ToString("o");s.latestVersion=update==null?AppVersion.Current:update.TargetFullRelease.Version.ToString();s.notes=update==null?"":update.TargetFullRelease.NotesMarkdown;s.state=update==null?"Latest":"Available";s.message=update==null?"已验证当前正式版本":"可更新至 "+s.latestVersion;s.taskId="";});}});}
  internal CoreUpdateStatus Download(){lock(gate){if(status.state=="Ready"){source.VerifyPackage(PackagePath(candidate.TargetFullRelease),candidate.TargetFullRelease);return Clone();}if(candidate==null||status.state!="Available")throw new InvalidOperationException("请先检查并验证正式发布。");}return Start("Downloading",token=>{var asset=candidate.TargetFullRelease;Change(s=>{s.totalBytes=asset.Size;s.receivedBytes=0;s.currentFile=asset.FileName;});manager.DownloadUpdatesAsync(new UpdateInfo(asset,false),p=>Change(s=>s.receivedBytes=asset.Size*p/100),token).GetAwaiter().GetResult();source.VerifyPackage(PackagePath(asset),asset);lock(gate){token.ThrowIfCancellationRequested();Change(s=>{s.state="Ready";s.taskId=Guid.NewGuid().ToString("N");s.message="下载已验证，等待确认重启";s.currentFile="";});}});}
  string PackagePath(VelopackAsset a){return Path.Combine(locator.PackagesDir,a.FileName);}
  internal CoreUpdateStatus Cancel(){lock(gate){if(status.state=="Restarting")return Clone();if(cancel!=null)cancel.Cancel();candidate=null;Change(s=>{s.state="Cancelled";s.taskId="";s.message="已取消更新。";});return Clone();}}
  internal string PreparedTask(){lock(gate){if(status.state!="Ready"||candidate==null||cancel==null||cancel.IsCancellationRequested||worker!=null&&!worker.IsCompleted)throw new InvalidOperationException("更新尚未准备完成。");return status.taskId;}}
  internal void Apply(){lock(gate){PreparedTask();var asset=candidate.TargetFullRelease;string path=PackagePath(asset);using(var bytes=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){
   source.VerifyPackage(path,asset);
   // The SDK extracts Squirrel.exe from the full package during download. Bind
   // the executable it will actually launch to those same authenticated bytes.
   var package=new Velopack.NuGet.ZipPackage(path,true);byte[] expected=package.UpdateExeBytes;
   if(expected==null||expected.Length==0)throw new InvalidDataException("Signed package contains no upstream updater.");
   LauncherMaintenance.NoReparse(locator.UpdateExePath);
   using(var updater=new FileStream(locator.UpdateExePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
    if(updater.Length!=expected.Length||!string.Equals(CoreReleaseProtocol.HashFile(locator.UpdateExePath),CoreReleaseProtocol.Hash(expected),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Upstream updater image differs from signed package.");
    cancel.Token.ThrowIfCancellationRequested();var apply=lease?new UpdateManager(source,new UpdateOptions{ExplicitChannel="win",AllowVersionDowngrade=false},new VelopackApplyLease(locator,source,cancel.Token)):manager;apply.WaitExitThenApplyUpdates(asset,true,true,new string[0]);Change(s=>{s.state="Restarting";s.message="正在安全重启启动器";});
   }
  }}}
  public void Dispose(){lock(gate){disposed=true;if(cancel!=null)cancel.Cancel();}}
 }
}
