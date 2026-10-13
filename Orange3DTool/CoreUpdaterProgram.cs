using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
namespace PZ3D {
 public static class CoreUpdaterProgram {
  sealed class Handoff {public string task,installationId,nonce,parentPath,helperHash;public int parentPid;public long parentStartTicks;}
  static string Relative(string task,string name){if(!CoreDeployment.Id(task))throw new InvalidDataException("Invalid task identity.");return CoreDeployment.Tasks+"/"+task+"/"+name;}
  static bool SameProcess(Process process,int pid,long ticks,string path){try{return !process.HasExited&&process.Id==pid&&process.StartTime.ToUniversalTime().Ticks==ticks&&CoreDeployment.SamePath(process.MainModule.FileName,path);}catch{return false;}}
  internal static Process Start(string root,string task,Process original,string key){
   root=CoreDeployment.Root(root);string worker;
   using(CoreDeployment.Acquire(root)) {
    var info=CoreUpdateTransaction.GetTaskInfo(root,task,key);if(info.State!="Staged")throw new IOException("Update is no longer prepared.");
    var record=CoreDeployment.ReadAuthenticated(root,key);var helper=record.files.Single(f=>f.path=="橙子更新助手.exe");CoreDeployment.RequireFile(root,helper.path,helper);
    string parent=CoreReleaseProtocol.Target(root,"橙子启动器.exe");long ticks=original.StartTime.ToUniversalTime().Ticks;if(!SameProcess(original,original.Id,ticks,parent))throw new IOException("Original launcher identity mismatch.");
    worker=CoreDeployment.Safe(root,Relative(task,"helper.exe"));byte[] bytes=CoreDeployment.ReadBytes(CoreReleaseProtocol.Target(root,helper.path),(int)CoreReleaseProtocol.MaxFileBytes);CoreDeployment.Atomic(root,Relative(task,"helper.exe"),bytes);if(!CoreDeployment.Matches(worker,helper))throw new IOException("Copied helper hash mismatch.");
    var handoff=new Handoff{task=task,installationId=record.installationId,nonce=info.Nonce,parentPath=parent,parentPid=original.Id,parentStartTicks=ticks,helperHash=helper.sha256};CoreDeployment.Atomic(root,Relative(task,"handoff.json"),CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(handoff)));
   }
   var process=Process.Start(new ProcessStartInfo(worker,"--core-apply \""+root+"\" "+task){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root});
   try{string ready=CoreDeployment.Safe(root,Relative(task,"helper-ready"));var deadline=DateTime.UtcNow.AddSeconds(10);while(DateTime.UtcNow<deadline){if(process.HasExited)throw new IOException("Update helper refused startup.");if(File.Exists(ready)&&CoreDeployment.Utf8.GetString(CoreDeployment.ReadBytes(ready,128))==process.Id+":"+process.StartTime.ToUniversalTime().Ticks)return process;Thread.Sleep(25);}throw new IOException("Update helper did not acknowledge handoff.");}
   catch{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}process.Dispose();throw;}
  }
  [STAThread] public static int Main(string[] args){try{return Run(args,CoreUpdateTrust.PublicKeyXml);}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
  internal static int Run(string[] args,string key){
   if(args.Length!=3||args[0]!="--core-apply")throw new InvalidDataException("Invalid helper arguments.");string root=CoreDeployment.Root(args[1]),task=args[2];
   var info=CoreUpdateTransaction.GetTaskInfo(root,task,key);byte[] raw=CoreDeployment.ReadBytes(CoreDeployment.Safe(root,Relative(task,"handoff.json")),8192);CoreReleaseProtocol.ReadStoredObject(raw,"task","installationId","nonce","parentPath","helperHash","parentPid","parentStartTicks");var handoff=CoreDeployment.Json.Deserialize<Handoff>(CoreDeployment.Utf8.GetString(raw));
   var baseline=CoreDeployment.ReadAuthenticated(root,key);var helper=baseline.files.Single(f=>f.path=="橙子更新助手.exe");
   using(var self=Process.GetCurrentProcess()) {
    if(info.State!="Staged"||handoff.task!=task||handoff.installationId!=baseline.installationId||handoff.nonce!=info.Nonce||handoff.parentPid<=0||handoff.parentStartTicks<=0||!CoreDeployment.SamePath(handoff.parentPath,CoreReleaseProtocol.Target(root,"橙子启动器.exe"))||handoff.helperHash!=helper.sha256||!CoreDeployment.SamePath(self.MainModule.FileName,CoreDeployment.Safe(root,Relative(task,"helper.exe")))||!CoreDeployment.Matches(self.MainModule.FileName,helper))throw new IOException("Helper handoff identity mismatch.");
    // Do not accept an already absent/reused parent. The parent waits for our
    // acknowledgement before closing, so this handle must identify it now.
    using(var parent=Process.GetProcessById(handoff.parentPid)){if(!SameProcess(parent,handoff.parentPid,handoff.parentStartTicks,handoff.parentPath))throw new IOException("Launcher process identity changed.");CoreDeployment.Atomic(root,Relative(task,"helper-ready"),CoreDeployment.Utf8.GetBytes(self.Id+":"+self.StartTime.ToUniversalTime().Ticks));if(!parent.WaitForExit(60000))throw new IOException("Launcher did not exit; installed files unchanged.");}
   }
   CoreUpdateDiagnostics.Write(root,task,"apply","Original launcher exited");
   using(var candidate=new Candidate(root,task,key))try {CoreUpdateTransaction.ApplyWithRelease(root,task,candidate.Healthy,candidate.Release,key);CoreUpdateDiagnostics.Write(root,task,"success","Healthy UI acknowledged");return 0;}
   catch(CoreUpdateException e){CoreUpdateDiagnostics.Write(root,task,e.State,e.Message);if(e.State=="RolledBack"){Process.Start(new ProcessStartInfo(CoreReleaseProtocol.Target(root,"橙子启动器.exe")){UseShellExecute=false,WorkingDirectory=root}).Dispose();CoreUpdateDiagnostics.Write(root,task,"restart","Previous launcher restarted once");return 2;}throw;}
  }
  sealed class Candidate:IDisposable {
   readonly string root,task,key;Process process;int pid;long ticks;string path;
   internal Candidate(string root,string task,string key){this.root=root;this.task=task;this.key=key;}
   internal bool Healthy(string exe){path=exe;CoreUpdateTransaction.BeginCandidate(root,task,key);process=Process.Start(new ProcessStartInfo(exe,CoreUpdateTransaction.HealthArguments(root,task,key)){UseShellExecute=false,WorkingDirectory=root});pid=process.Id;ticks=process.StartTime.ToUniversalTime().Ticks;CoreUpdateTransaction.RecordCandidate(root,task,process,key);
    CoreUpdateDiagnostics.Write(root,task,"restart","Candidate "+pid);var deadline=DateTime.UtcNow.AddSeconds(45);while(DateTime.UtcNow<deadline){if(process.HasExited)return false;var info=CoreUpdateTransaction.GetTaskInfo(root,task,key);if(info.HealthConfirmed&&info.HealthPid==pid&&info.HealthStartTicks==ticks&&CoreDeployment.SamePath(info.HealthPath,exe)&&SameProcess(process,pid,ticks,exe))return true;Thread.Sleep(50);}return false;
   }
   internal void Release(){if(process==null||process.HasExited)return;if(!SameProcess(process,pid,ticks,path))throw new IOException("Cannot release candidate: identity changed.");process.CloseMainWindow();if(!process.WaitForExit(2000)){process.Kill();if(!process.WaitForExit(5000))throw new IOException("Failed candidate remains alive; recovery requires manual inspection.");}CoreUpdateDiagnostics.Write(root,task,"candidate-exit","Released only the spawned failed candidate");}
   public void Dispose(){if(process!=null)process.Dispose();}
  }
 }
 // Startup recognizes this path before normal recovery. A candidate cannot
 // recover its own PendingHealth transaction while its helper owns the mutex.
 internal sealed class CoreHealthStartup {
  readonly string root,task,nonce;bool page,bridge,confirmed;
  internal CoreHealthStartup(string[] args){if(args.Length!=4||args[0]!="--core-update-health")throw new InvalidDataException("Invalid health startup arguments.");root=CoreDeployment.Root(args[1]);task=args[2];nonce=args[3];if(!CoreDeployment.SamePath(Process.GetCurrentProcess().MainModule.FileName,CoreReleaseProtocol.Target(root,"橙子启动器.exe")))throw new InvalidDataException("Health executable path differs from installation.");var info=CoreUpdateTransaction.GetTaskInfo(root,task);if(info.State!="PendingHealth"||info.Nonce!=nonce)throw new InvalidDataException("Health task is not pending.");var until=DateTime.UtcNow.AddSeconds(10);while(true){try{CoreUpdateTransaction.AssertCandidateStartup(root,task,CoreUpdateTrust.PublicKeyXml);break;}catch(IOException){if(DateTime.UtcNow>=until)throw;Thread.Sleep(25);}}}
  internal void PageLoaded(){page=true;TryConfirm();}
  internal void BridgeReady(){bridge=true;TryConfirm();}
  internal void AssertAction(string action){ValidateAction(action,CoreUpdateTransaction.GetTaskInfo(root,task));}
  internal static void ValidateAction(string action,CoreUpdateTaskInfo info){if(ModernController.NativeReads.Contains(action)||ModernController.NativeNetworkReads.Contains(action)||action=="cancel"||action=="coreUpdateStatus"||action=="coreUpdateCheck"||action=="coreUpdateCancel")return;if(info.State!="Succeeded")throw new InvalidOperationException("新版界面正在完成健康验证，完成后才能启动游戏或修改文件。");}
  void TryConfirm(){if(!confirmed&&page&&bridge){CoreUpdateTransaction.ConfirmHealth(root,task,nonce);confirmed=true;}}
 }
}
