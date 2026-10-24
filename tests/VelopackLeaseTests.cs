using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Velopack;
using Velopack.Locators;
namespace PZ3D {
 internal static class VelopackLeaseTests {
  internal static bool Dispatch(string[] args){
   if(args.Length==3&&args[0]=="--velopack-lease"){Environment.Exit(ApplyLeaseWorker.Run(args[1],int.Parse(args[2])));return true;}
   if(args.Length==1&&(args[0]=="--lease-parent"||args[0]=="--lease-cancel")){Parent(args[0]=="--lease-cancel");return true;}
   if(args.Length==3&&args[0]=="--floor-source"){FloorSource(args[1],args[2]);return true;}
   if(args.Length==10&&args[0]=="--silent"&&args[1]=="apply"){string package=args[3];File.WriteAllText(package+".ready","consumer created after parent exit");Until(()=>File.Exists(package+".go"));File.WriteAllText(package+".consumed",CoreReleaseProtocol.HashFile(package));return true;}
   if(args.Length==4&&args[0]=="--floor-child"){string p=args[1];using(ReleaseCheckpoint.Acquire(p)){File.WriteAllText(args[2],"held");Until(()=>File.Exists(args[3]));}return true;}
   return false;
  }
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  static void Parent(bool abort){string current=Path.GetDirectoryName(typeof(VelopackLeaseTests).Assembly.Location),root=Path.GetDirectoryName(current);try{
   var source=new AuthenticatedVelopackSource(CoreUpdateTrust.PublicKeyXml,AppVersion.Current,(url,t)=>File.OpenRead(Path.Combine(root,url.EndsWith(".sig")?"feed.sig":url.EndsWith(".json")?"feed.json":"download.bin")),Path.Combine(root,"OrangeLauncherRuntime","accepted-release.bin"));
   using(NativeApplyGuard.Instance(root))using(var b=new VelopackUpdateBackend(source,new LeaseLocator(root),true)){b.Check();Until(()=>b.Status().state!="Checking");Check(b.Status().state=="Available",b.Status().message);b.Download();Until(()=>b.Status().state!="Downloading");Check(b.Status().state=="Ready",b.Status().message);if(abort){AbortHandshake(source,new LeaseLocator(root));return;}b.Apply();Check(b.Status().state=="Restarting",b.Status().message);File.WriteAllText(Path.Combine(root,"parent-armed"),"armed");}
  }catch(Exception e){Thread.Sleep(250);string err=Path.Combine(root,"OrangeLauncherRuntime","last-apply-error.txt");File.WriteAllText(Path.Combine(root,"parent-error"),e.ToString()+(File.Exists(err)?File.ReadAllText(err):""));Environment.ExitCode=1;}}
  static void AbortHandshake(AuthenticatedVelopackSource source,LeaseLocator locator){
   var r=source.LeaseRequest(locator.RootAppDir,locator.AppContentDir,locator.PackagesDir,locator.UpdateExePath,true);string image=ManagedBackgroundWorker.PrepareDistribution(typeof(VelopackLeaseTests).Assembly.Location,CoreUpdateTrust.PublicKeyXml,DistributionPaths.ExecutingRoot(r.Root,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));string name="OZL-Abort-"+Guid.NewGuid().ToString("N");int parent=Process.GetCurrentProcess().Id;
   using(var pipe=ApplyLeaseWorker.Server(name))using(var worker=ApplyLeaseWorker.Start(image,new[]{"--velopack-lease",name,parent.ToString()})){
    ApplyLeaseWorker.Wait(System.Threading.Tasks.Task.Factory.FromAsync(pipe.BeginWaitForConnection,pipe.EndWaitForConnection,null));ApplyLeaseWorker.Peer(pipe,worker.Id,true);using(var reader=new BinaryReader(pipe,Encoding.UTF8,true))using(var writer=new BinaryWriter(pipe,Encoding.UTF8,true)){
     ApplyLeaseWorker.Write(writer,new JavaScriptSerializer().Serialize(r));Check(ApplyLeaseWorker.Read(reader)=="READY","abort lease not ready");ApplyLeaseWorker.Write(writer,new JavaScriptSerializer().Serialize(new[]{"--silent","apply","--package",r.Package,"--waitPid",parent.ToString(),"--rootDir",r.Root,"--packageDir",locator.PackagesDir}));Check(ApplyLeaseWorker.Read(reader)=="ARMED","abort lease not armed");
    }pipe.Dispose();Check(worker.WaitForExit(5000)&&worker.ExitCode==1,"disconnected uncommitted lease was not stopped");
   }
  }
  [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
  static void FloorSource(string floor,string prefix){var source=new AuthenticatedVelopackSource(CoreUpdateTrust.PublicKeyXml,AppVersion.Current,(url,t)=>File.OpenRead(prefix+(url.EndsWith(".sig")?".sig":".json")),floor);File.WriteAllText(prefix+".constructed","");Until(()=>File.Exists(prefix+".go"));try{source.GetReleaseFeed(null,VelopackFeed.AppId,"win",null,null).GetAwaiter().GetResult();File.WriteAllText(prefix+".result","accepted");}catch(InvalidDataException){File.WriteAllText(prefix+".result","rejected");}}
  static void Check(bool b,string message){if(!b)throw new Exception(message);}
  static void Until(Func<bool> ready){DateTime limit=DateTime.UtcNow.AddSeconds(20);while(!ready()){if(DateTime.UtcNow>limit)throw new Exception("fixture timeout");Thread.Sleep(20);}}
  static void Denied(Action mutate){try{mutate();}catch(IOException){return;}throw new Exception("Authenticated bytes mutable after parent exit and before consumer open");}
  internal static void Run(string temp){
   AliasScope();
   string install=Path.Combine(temp,"lease"),current=Path.Combine(install,"current"),packages=Path.Combine(install,"packages"),source=typeof(VelopackLeaseTests).Assembly.Location;
   Directory.CreateDirectory(current);Directory.CreateDirectory(packages);File.WriteAllText(Path.Combine(install,".portable"),"");File.WriteAllText(Path.Combine(install,"Update.exe"),"download replaces with signed helper");
   using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(File.ReadAllText(Path.Combine(Path.GetDirectoryName(source),"fixture-private.xml")));
    var files=new List<CoreFile>();foreach(string name in DistributionBootstrap.Owned){string p=Path.Combine(current,name);Directory.CreateDirectory(Path.GetDirectoryName(p));string original=name=="橙子启动器.exe"?source:Path.Combine(Path.GetDirectoryName(source),name);if(File.Exists(original))File.Copy(original,p);else File.WriteAllText(p,"fixture");files.Add(new CoreFile{path=name,url="",length=new FileInfo(p).Length,sha256=CoreReleaseProtocol.HashFile(p)});}
    byte[] bootstrap=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{schema=1,version=AppVersion.Current,files=files}));File.WriteAllBytes(Path.Combine(current,"velopack-bootstrap.json"),bootstrap);File.WriteAllBytes(Path.Combine(current,"velopack-bootstrap.sig"),rsa.SignData(bootstrap,"SHA256"));
    using(var zip=ZipFile.Open(Path.Combine(install,"download.bin"),ZipArchiveMode.Create)){using(var w=new StreamWriter(zip.CreateEntry("OrangeZomboidLauncher.nuspec").Open()))w.Write("<package><metadata><id>OrangeZomboidLauncher</id><version>9.0.0</version><authors>fixture</authors><description>fixture</description></metadata></package>");zip.CreateEntryFromFile(source,"lib/net45/Squirrel.exe");}
    string download=Path.Combine(install,"download.bin");byte[] raw=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{schema=1,appId=VelopackFeed.AppId,channel="win",version="9.0.0",fileName=VelopackFeed.AppId+"-9.0.0-full.nupkg",length=new FileInfo(download).Length,sha256=CoreReleaseProtocol.HashFile(download),notes="lease fixture"}));File.WriteAllBytes(Path.Combine(install,"feed.json"),raw);File.WriteAllBytes(Path.Combine(install,"feed.sig"),rsa.SignData(raw,"SHA256"));
   }
   string package=Path.Combine(packages,VelopackFeed.AppId+"-9.0.0-full.nupkg");string hash=CoreReleaseProtocol.HashFile(Path.Combine(install,"download.bin"));
   string metadata=Path.Combine(current,"sq.version");string validMetadata="<package><metadata><id>OrangeZomboidLauncher</id><version>"+AppVersion.Current+"</version><channel>win</channel><mainExe>橙子启动器.exe</mainExe></metadata></package>";File.WriteAllText(metadata,validMetadata.Replace("橙子启动器.exe",@"C:\Outside\Unrelated.exe"));using(var foreign=ApplyLeaseWorker.Start(Path.Combine(current,"橙子启动器.exe"),new[]{"--lease-cancel"})){Check(foreign.WaitForExit(15000)&&foreign.ExitCode!=0,"foreign sq.version mainExe was accepted");}File.WriteAllText(metadata,validMetadata);File.Delete(Path.Combine(install,"parent-error"));
   using(var cancelled=ApplyLeaseWorker.Start(Path.Combine(current,"橙子启动器.exe"),new[]{"--lease-cancel"})){Check(cancelled.WaitForExit(15000)&&cancelled.ExitCode==0,"cancellation fixture failed");}Check(!File.Exists(package+".ready"),"uncommitted handoff launched consumer after parent exit");Console.WriteLine("PASS disconnected ARMED handoff cannot apply without final COMMIT");
   using(var parent=ApplyLeaseWorker.Start(Path.Combine(current,"橙子启动器.exe"),new[]{"--lease-parent"})){Check(parent.WaitForExit(30000),"parent stuck");Check(parent.ExitCode==0,File.Exists(Path.Combine(install,"parent-error"))?File.ReadAllText(Path.Combine(install,"parent-error")):"parent failed");}
   Until(()=>File.Exists(package+".ready"));Check(File.Exists(Path.Combine(install,"parent-armed")),"parent not armed");
   Denied(()=>File.WriteAllText(package,"replacement"));Denied(()=>File.Delete(package));Denied(()=>Directory.Move(packages,packages+"-moved"));Denied(()=>File.Move(package,package+".moved"));Denied(()=>File.WriteAllText(Path.Combine(install,"Update.exe"),"replacement updater"));
   File.WriteAllText(package+".go","");Until(()=>File.Exists(package+".consumed"));Check(File.ReadAllText(package+".consumed")==hash,"consumer read replaced bytes");
   Until(()=>{try{using(File.Open(package,FileMode.Open,FileAccess.ReadWrite,FileShare.None))return true;}catch(IOException){return false;}});
   Console.WriteLine("PASS real parent exit retains package/helper authority until SDK-command consumer exits (SDK-free worker)");
   // Actual cross-process mutex exclusion, using the very same production key.
   string floor=Path.Combine(install,"OrangeLauncherRuntime","accepted-release.bin"),held=Path.Combine(temp,"floor-held"),go=Path.Combine(temp,"floor-go");
   using(var child=ApplyLeaseWorker.Start(source,new[]{"--floor-child",floor,held,go})){Until(()=>File.Exists(held));bool acquired=false;var thread=new Thread(()=>{using(ReleaseCheckpoint.Acquire(floor))acquired=true;});thread.Start();Thread.Sleep(150);Check(!acquired,"checkpoint mutex is not cross-process");File.WriteAllText(go,"");Check(child.WaitForExit(5000),"floor child did not finish");Check(thread.Join(5000)&&acquired,"checkpoint lock did not release");}
   Console.WriteLine("PASS checkpoint lock excludes a second actual process");
   string shared=Path.Combine(temp,"shared-floor.bin"),older=Path.Combine(temp,"old-source"),newer=Path.Combine(temp,"new-source");using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(File.ReadAllText(Path.Combine(Path.GetDirectoryName(source),"fixture-private.xml")));foreach(string prefix in new[]{older,newer}){byte[] raw=File.ReadAllBytes(Path.Combine(install,"feed.json"));if(prefix==newer)raw=Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(raw).Replace("9.0.0","9.0.1"));File.WriteAllBytes(prefix+".json",raw);File.WriteAllBytes(prefix+".sig",rsa.SignData(raw,"SHA256"));}}
   using(var oldProcess=ApplyLeaseWorker.Start(source,new[]{"--floor-source",shared,older}))using(var newProcess=ApplyLeaseWorker.Start(source,new[]{"--floor-source",shared,newer})){Until(()=>File.Exists(older+".constructed")&&File.Exists(newer+".constructed"));File.WriteAllText(newer+".go","");Check(newProcess.WaitForExit(10000)&&File.ReadAllText(newer+".result")=="accepted","new release rejected");File.WriteAllText(older+".go","");Check(oldProcess.WaitForExit(10000)&&File.ReadAllText(older+".result")=="rejected","stale actual process lowered signed checkpoint");}using(ReleaseCheckpoint.Acquire(shared))Check(ReleaseCheckpoint.Floor(shared,CoreUpdateTrust.PublicKeyXml,AppVersion.Current)=="9.0.1","checkpoint regressed");Console.WriteLine("PASS overlapping actual source processes cannot lower persisted signed release");
   // Live, isolated images; never terminate a user/game process.
   string outside=Path.Combine(temp,"outside-monitor.exe"),inside=Path.Combine(current,"橙子启动器.exe"),ready=Path.Combine(temp,"monitor-ready"),ready2=Path.Combine(temp,"other-ready");File.Copy(source,outside);
   using(var monitor=ApplyLeaseWorker.Start(outside,new[]{"--guard-fixture",ready}))using(var other=ApplyLeaseWorker.Start(inside,new[]{"--guard-fixture",ready2})){try{Until(()=>File.Exists(ready)&&File.Exists(ready2));var inventory=NativeApplyGuard.Inventory();Check(inventory.Any(p=>p.Pid==monitor.Id)&&inventory.Any(p=>p.Pid==other.Id),"live images not enumerated");bool rejected=false;try{NativeApplyGuard.CheckInventory(install,-1,inventory);}catch(IOException){rejected=true;}Check(rejected,"live nested process was not blocked");Check(!monitor.HasExited&&!other.HasExited,"guard terminated a fixture");NativeApplyGuard.CheckInventory(install,other.Id,inventory);Console.WriteLine("PASS real process scope rejects nested image and excludes surviving outside monitor");}finally{monitor.Kill();other.Kill();monitor.WaitForExit();other.WaitForExit();}}
   Directory.Delete(DistributionPaths.ExecutingRoot(install,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)),true);
  }
  [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]static extern uint GetShortPathName(string path,StringBuilder output,uint size);
  static void AliasScope(){
   string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet"),image=Path.Combine(root,"dotnet.exe");var b=new StringBuilder(32768);Check(GetShortPathName(image,b,(uint)b.Capacity)>0,"alias fixture path unavailable");string alias=b.ToString();Check(alias.Contains("~"),"fixture needs an actual existing 8.3 alias");
   Check(NativeApplyGuard.ImageWithin(root,alias),"native would terminate alias image excluded by guard");Check(NativeApplyGuard.ImageWithin(Path.GetDirectoryName(alias),image),"alias root bypassed guard");Check(NativeApplyGuard.ImageWithin(@"\\?\"+root,@"\\?\"+alias),"extended alias bypassed guard");
   bool rejected=false;try{NativeApplyGuard.CheckInventory(root,-1,new[]{new RunningImage(123,alias)});}catch(IOException){rejected=true;}Check(rejected,"alias inventory not rejected");
   using(NativeApplyGuard.Instance(root)){bool acquired=false;var thread=new Thread(()=>{try{using(NativeApplyGuard.Instance(Path.GetDirectoryName(alias)))acquired=true;}catch(IOException){}});thread.Start();Check(thread.Join(12000)&&!acquired,"same installation alias acquired independent instance gate");}
   Console.WriteLine("PASS 8.3/extended process scope and same-installation alias instance exclusion");
  }
  sealed class LeaseLocator:IVelopackLocator,IProcessImpl {
   readonly TestVelopackLocator inner;internal LeaseLocator(string root){inner=new TestVelopackLocator(VelopackFeed.AppId,AppVersion.Current,Path.Combine(root,"packages"),Path.Combine(root,"current"),root,Path.Combine(root,"Update.exe"),"win",null,null,Path.Combine(root,"current","橙子启动器.exe"));}
   public string AppId{get{return inner.AppId;}}public string RootAppDir{get{return inner.RootAppDir;}}public string PackagesDir{get{return inner.PackagesDir;}}public string AppContentDir{get{return inner.AppContentDir;}}public string AppTempDir{get{return inner.AppTempDir;}}public string UpdateExePath{get{return inner.UpdateExePath;}}public SemanticVersion CurrentlyInstalledVersion{get{return inner.CurrentlyInstalledVersion;}}public string ThisExeRelativePath{get{return inner.ThisExeRelativePath;}}public string Channel{get{return "win";}}public string AppUserModelId{get{return inner.AppUserModelId;}}public Velopack.Logging.IVelopackLogger Log{get{return inner.Log;}}public bool IsPortable{get{return true;}}public IProcessImpl Process{get{return this;}}
   public void AddLogger(Velopack.Logging.IVelopackLogger logger){inner.AddLogger(logger);}public List<VelopackAsset> GetLocalPackages(){return inner.GetLocalPackages();}public VelopackAsset GetLatestLocalFullPackage(){return inner.GetLatestLocalFullPackage();}public Guid? GetOrCreateStagedUserId(){return inner.GetOrCreateStagedUserId();}public string GetCurrentProcessPath(){return typeof(VelopackLeaseTests).Assembly.Location;}public uint GetCurrentProcessId(){return (uint)System.Diagnostics.Process.GetCurrentProcess().Id;}public void StartProcess(string file,IEnumerable<string> args,string cwd,bool x){throw new Exception("Lease must intercept process start");}public void Exit(int code){throw new Exception("Host owns shutdown");}
  }
 }
}
