using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace PZ3D {
 public sealed class CoreUpdatePlan {
  public List<CoreFile> Changed,Removed;
  public long Bytes;
  public CoreInstallRecord Baseline;
 }
 public sealed class CoreUpdateTaskInfo {
  public string TaskId,Root,Directory,InstallationId,Version,State,Nonce,Error;
  public bool HealthConfirmed;
  public int HealthPid;
  public long HealthStartTicks;
  public string HealthPath;
 }
 public sealed class CoreUpdateResult {
  public string TaskId,State,Message;
 }
 public sealed class CoreUpdateException:IOException {
  public string State {get;private set;}
  internal CoreUpdateException(string state,string message,Exception inner):base(message,inner){State=state;}
 }
 // Journals describe intent before any installed bytes change. Every backup and
 // candidate is rechecked against its recorded digest before use during recovery.
 public static class CoreUpdateTransaction {
  sealed class HealthReceipt {public string nonce,path;public int pid;public long startTicks;}
  sealed class CandidateReceipt {public string nonce,path,state;public int pid;public long startTicks;}
  internal sealed class Journal {
   public string root,installationId,taskId,nonce,state,baselineRecord,targetRecord,error;
   public int started,candidateExpected;
   public List<string> existed;
  }
  static bool Terminal(string state) {return state=="Succeeded"||state=="RolledBack";}
  static string Rel(string task,string tail) {if(!CoreDeployment.Id(task))throw new InvalidDataException("Invalid update task ID.");return CoreDeployment.Tasks+"/"+task+"/"+tail;}
  static string PathAt(string root,string task,string tail) {return CoreDeployment.Safe(root,Rel(task,tail));}
  static void Save(string root,Journal journal) {CoreDeployment.Atomic(root,Rel(journal.taskId,"journal.json"),CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(journal)));}
  static CoreInstallRecord Baseline(string root,Journal j) {return j.baselineRecord==null?null:CoreDeployment.ReadRecord(root,Convert.FromBase64String(j.baselineRecord));}
  static CoreInstallRecord TargetRecord(string root,Journal j,CoreManifest manifest) {return new CoreInstallRecord{schema=2,root=root,installationId=j.installationId,version=manifest.version,files=manifest.files};}
  static byte[] RecordBytes(CoreInstallRecord record) {return CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(record));}
  static CoreManifest VerifyStored(string root,Journal j,string key) {
   var baseline=Baseline(root,j);
   return CoreReleaseProtocol.Verify(CoreDeployment.ReadBytes(PathAt(root,j.taskId,"manifest.json"),CoreReleaseProtocol.MaxManifestBytes),CoreDeployment.ReadBytes(PathAt(root,j.taskId,"manifest.sig"),4096),key,baseline==null?"0.0.0":baseline.version);
  }
  static Journal Load(string root,string task) {
   string owner=CoreDeployment.ReadOwner(root);
   if(CoreDeployment.Utf8.GetString(CoreDeployment.ReadBytes(PathAt(root,task,".owner"),100))!=owner+":"+task)throw new InvalidDataException("Task ownership marker mismatch.");
   byte[] raw=CoreDeployment.ReadBytes(PathAt(root,task,"journal.json"),CoreReleaseProtocol.MaxManifestBytes);
   CoreReleaseProtocol.ReadStoredObject(raw,"root","installationId","taskId","nonce","state","baselineRecord","targetRecord","error","existed","started","candidateExpected");
   var j=CoreDeployment.Json.Deserialize<Journal>(CoreDeployment.Utf8.GetString(raw));
   if(j==null||j.root!=root||j.taskId!=task||j.installationId!=owner||!CoreDeployment.Id(j.nonce)||j.existed==null||j.existed.Count>CoreReleaseProtocol.MaxFiles||j.existed.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=j.existed.Count||!new[]{"Staged","Applying","PendingHealth","Succeeded","RollingBack","RolledBack","Conflicted"}.Contains(j.state))throw new InvalidDataException("Invalid transaction journal.");
   var baseline=Baseline(root,j);
   if(j.started!=0&&j.started!=1)throw new InvalidDataException("Invalid apply phase.");
   if(j.candidateExpected!=0&&j.candidateExpected!=1)throw new InvalidDataException("Invalid candidate phase.");
   foreach(string path in j.existed)if(baseline==null||!baseline.files.Any(f=>f.path==path))throw new InvalidDataException("Invalid backup ownership.");
   return j;
  }
  internal static void EnsureNoPending(string root) {
   foreach(string task in TaskIds(root)){var j=Load(root,task);if(!Terminal(j.state))throw new CoreUpdateException(j.state,"An unfinished update requires recovery before another operation.",null);}
  }
  static IEnumerable<string> TaskIds(string root) {
   string folder=CoreDeployment.Safe(root,CoreDeployment.Tasks);if(!Directory.Exists(folder))return new string[0];
   var ids=Directory.GetDirectories(folder).Select(Path.GetFileName).ToArray();
   foreach(string id in ids){if(!CoreDeployment.Id(id))throw new InvalidDataException("Unknown folder in owned update tasks.");PathAt(root,id,".owner");}
   return ids;
  }
  public static CoreUpdatePlan Plan(string root,CoreManifest target) {return Plan(root,target,CoreUpdateTrust.PublicKeyXml);}
  public static CoreUpdatePlan Plan(string root,CoreManifest target,string publicKeyXml) {
   root=CoreDeployment.Root(root);using(CoreDeployment.Acquire(root)){EnsureNoPending(root);var baseline=CoreDeployment.Read(root);return PlanLocked(root,CoreDeployment.Authenticate(target,publicKeyXml,baseline.version),baseline);}
  }
  static CoreUpdatePlan PlanLocked(string root,CoreManifest target,CoreInstallRecord baseline) {
   if(baseline!=null)CoreDeployment.ValidateBaseline(root,baseline);
   var prior=baseline==null?new List<CoreFile>():baseline.files;var changed=new List<CoreFile>();
   foreach(var file in target.files) {
    string path=CoreReleaseProtocol.Target(root,file.path);var old=prior.SingleOrDefault(f=>f.path==file.path);
    if(Directory.Exists(path)||(old==null&&File.Exists(path)))throw new IOException("Unowned target is occupied: "+file.path);
    if(!CoreDeployment.Matches(path,file))changed.Add(file);
   }
   return new CoreUpdatePlan {Changed=changed,Removed=prior.Where(f=>!target.files.Any(t=>t.path==f.path)).ToList(),Bytes=changed.Sum(f=>f.length),Baseline=baseline};
  }
  internal static void ValidateDownloads(IEnumerable<CoreFile> required,IDictionary<string,byte[]> files) {
   var expected=required.ToList();if(files==null||files.Count!=expected.Count)throw new InvalidDataException("Downloaded file set does not match the update plan.");
   foreach(var file in expected){byte[] data;if(!files.TryGetValue(file.path,out data)||data==null||data.LongLength!=file.length||!string.Equals(CoreReleaseProtocol.Hash(data),file.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Downloaded bytes do not match signed metadata: "+file.path);}
  }
  public static string Stage(string root,CoreManifest target,IDictionary<string,byte[]> changed) {return Stage(root,target,changed,CoreUpdateTrust.PublicKeyXml);}
  public static string Stage(string root,CoreManifest target,IDictionary<string,byte[]> changed,string publicKeyXml) {
   return Stage(root,target,changed,null,publicKeyXml);
  }
  public static string Stage(string root,CoreManifest target,IDictionary<string,byte[]> changed,Action<string> checkpoint,string publicKeyXml) {
   root=CoreDeployment.Root(root);using(CoreDeployment.Acquire(root)){EnsureNoPending(root);var baseline=CoreDeployment.Read(root);var verified=CoreDeployment.Authenticate(target,publicKeyXml,baseline.version);return StageLocked(root,verified,changed,baseline,baseline.installationId,checkpoint);}
  }
  internal static string StageLocked(string root,CoreManifest target,IDictionary<string,byte[]> changed,CoreInstallRecord baseline,string installationId,Action<string> checkpoint=null) {
   var plan=PlanLocked(root,target,baseline);ValidateDownloads(plan.Changed,changed);
   // Clone caller bytes before persistence; signature and digest checks are repeated
   // on disk before apply, so mutation by a downloader cannot authorize other bytes.
   var bytes=changed.ToDictionary(x=>x.Key,x=>(byte[])x.Value.Clone());ValidateDownloads(plan.Changed,bytes);
   string task=Guid.NewGuid().ToString("N");
   string preparing=CoreDeployment.Runtime+"/CorePreparing/"+task;
   Func<string,string> pending=tail=>preparing+"/"+tail;
   var j=new Journal{root=root,installationId=installationId,taskId=task,nonce=Guid.NewGuid().ToString("N"),state="Staged",existed=new List<string>(),baselineRecord=baseline==null?null:Convert.ToBase64String(CoreDeployment.ReadBytes(CoreDeployment.Safe(root,CoreDeployment.RecordName),CoreReleaseProtocol.MaxManifestBytes))};
   CoreDeployment.WriteNew(root,pending(".owner"),CoreDeployment.Utf8.GetBytes(installationId+":"+task));
   Checkpoint(checkpoint,"staging-started");
   CoreDeployment.WriteNew(root,pending("manifest.json"),target.VerifiedEnvelope.Raw);
   CoreDeployment.WriteNew(root,pending("manifest.sig"),target.VerifiedEnvelope.Signature);
   foreach(var entry in bytes)CoreDeployment.WriteNew(root,pending("new/"+entry.Key),entry.Value);
   if(baseline!=null)foreach(var file in baseline.files) {
    string source=CoreReleaseProtocol.Target(root,file.path);if(!File.Exists(source))continue;
    CoreDeployment.RequireFile(root,file.path,file);byte[] old=CoreDeployment.ReadBytes(source,(int)CoreReleaseProtocol.MaxFileBytes);
    CoreDeployment.WriteNew(root,pending("old/"+file.path),old);
    if(!CoreDeployment.Matches(CoreDeployment.Safe(root,pending("old/"+file.path)),file))throw new IOException("Baseline changed during backup.");
    j.existed.Add(file.path);
   }
   j.targetRecord=Convert.ToBase64String(RecordBytes(TargetRecord(root,j,target)));
   CoreDeployment.Atomic(root,pending("journal.json"),CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(j)));
   Checkpoint(checkpoint,"staging-ready");
   // Only publish a fully flushed task. Failed/interrupted preparation stays in
   // its marked diagnostic folder and cannot block recovery or a later attempt.
   Directory.CreateDirectory(CoreDeployment.Safe(root,CoreDeployment.Tasks));
   Directory.Move(CoreDeployment.Safe(root,preparing),CoreDeployment.Safe(root,CoreDeployment.Tasks+"/"+task));return task;
  }
  public static CoreUpdateTaskInfo GetTaskInfo(string root,string taskId) {return GetTaskInfo(root,taskId,CoreUpdateTrust.PublicKeyXml);}
  public static CoreUpdateTaskInfo GetTaskInfo(string root,string taskId,string publicKeyXml) {
   root=CoreDeployment.Root(root);var j=Load(root,taskId);var target=VerifyStored(root,j,publicKeyXml);ValidateTargetRecord(root,j,target);
   string receipt=PathAt(root,taskId,"health-confirmed");
   HealthReceipt health=null;if((j.state=="PendingHealth"||j.state=="Succeeded")&&File.Exists(receipt)){byte[] raw=CoreDeployment.ReadBytes(receipt,8192);CoreReleaseProtocol.ReadStoredObject(raw,"nonce","path","pid","startTicks");health=CoreDeployment.Json.Deserialize<HealthReceipt>(CoreDeployment.Utf8.GetString(raw));}
   bool confirmed=health!=null&&health.nonce==j.nonce&&health.pid>0&&health.startTicks>0&&Path.IsPathRooted(health.path);
   return new CoreUpdateTaskInfo {TaskId=taskId,Root=root,Directory=CoreDeployment.Safe(root,CoreDeployment.Tasks+"/"+taskId),InstallationId=j.installationId,Version=target.version,State=j.state,Nonce=j.nonce,Error=j.error,HealthConfirmed=confirmed,HealthPid=confirmed?health.pid:0,HealthStartTicks=confirmed?health.startTicks:0,HealthPath=confirmed?health.path:null};
  }
  static void ValidateTargetRecord(string root,Journal j,CoreManifest target) {
   // This is derived metadata, not the signed envelope. Framework reflection
   // cache warmup can reorder serialized fields between processes; compare every
   // strictly parsed field instead. Installed/baseline byte-conflict checks stay exact.
   var record=CoreReleaseProtocol.ReadStoredObject(Convert.FromBase64String(j.targetRecord),"schema","root","installationId","version","files");var files=CoreReleaseProtocol.Files(record["files"]);
   if(!(record["schema"] is long)||(long)record["schema"]!=2||!string.Equals(record["root"] as string,root,StringComparison.OrdinalIgnoreCase)||(record["installationId"] as string)!=j.installationId||(record["version"] as string)!=target.version||files.Count!=target.files.Count||!files.Zip(target.files,(a,b)=>a.path==b.path&&a.sha256==b.sha256&&a.url==b.url&&a.length==b.length).All(equal=>equal))throw new InvalidDataException("Journal target record differs from signed release.");
  }
  public static string HealthArguments(string root,string taskId) {return HealthArguments(root,taskId,CoreUpdateTrust.PublicKeyXml);}
  public static string HealthArguments(string root,string taskId,string publicKeyXml) {var info=GetTaskInfo(root,taskId,publicKeyXml);if(info.State!="Staged"&&info.State!="Applying"&&info.State!="PendingHealth")throw new InvalidDataException("Task is not awaiting health startup.");return "--core-update-health \""+info.Root+"\" "+taskId+" "+info.Nonce;}
  public static void ConfirmHealth(string root,string taskId,string nonce) {ConfirmHealth(root,taskId,nonce,CoreUpdateTrust.PublicKeyXml);}
  public static void ConfirmHealth(string root,string taskId,string nonce,string publicKeyXml) {
   // The helper owns the root mutex while it waits for this child handshake.
   // Confirmation reads the pending journal and never invokes normal recovery.
   root=CoreDeployment.Root(root);var j=Load(root,taskId);var target=VerifyStored(root,j,publicKeyXml);ValidateTargetRecord(root,j,target);
   if(j.state!="PendingHealth"||j.nonce!=nonce)throw new InvalidDataException("Health task or nonce is not pending.");
   ValidateInstalled(root,j,target);using(var current=Process.GetCurrentProcess()){var receipt=new HealthReceipt{nonce=nonce,pid=current.Id,startTicks=current.StartTime.ToUniversalTime().Ticks,path=current.MainModule.FileName};CoreDeployment.Atomic(root,Rel(taskId,"health-confirmed"),CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(receipt)));}
  }
  public static CoreUpdateResult Apply(string root,string taskId,Func<string,bool> health,Action<string> checkpoint=null) {return Apply(root,taskId,health,checkpoint,CoreUpdateTrust.PublicKeyXml);}
  public static CoreUpdateResult Apply(string root,string taskId,Func<string,bool> health,Action<string> checkpoint,string publicKeyXml) {
   root=CoreDeployment.Root(root);using(CoreDeployment.Acquire(root))return ApplyLocked(root,taskId,health,checkpoint,publicKeyXml);
  }
  internal static CoreUpdateResult ApplyWithRelease(string root,string taskId,Func<string,bool> health,Action releaseCandidate,string key){root=CoreDeployment.Root(root);using(CoreDeployment.Acquire(root))return ApplyLocked(root,taskId,health,null,key,releaseCandidate);}
  // Called only by the mutex-owning helper. Persist intent before Process.Start:
  // an interrupted spawn with no recorded identity must never authorize rollback.
  internal static void BeginCandidate(string root,string task,string key){var j=Load(root,task);VerifyStored(root,j,key);if(j.state!="PendingHealth"||j.candidateExpected!=1)throw new IOException("Candidate is not pending.");CoreDeployment.WriteNew(root,Rel(task,"candidate.json"),CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(new CandidateReceipt{nonce=j.nonce,path=CoreReleaseProtocol.Target(root,"橙子启动器.exe"),state="Starting"})));}
  internal static void RecordCandidate(string root,string task,Process child,string key){var j=Load(root,task);VerifyStored(root,j,key);var receipt=ReadCandidate(root,j);if(receipt==null||receipt.state!="Starting")throw new IOException("Candidate spawn intent missing.");if(!CoreDeployment.SamePath(child.MainModule.FileName,receipt.path))throw new IOException("Candidate executable differs from intent.");receipt.pid=child.Id;receipt.startTicks=child.StartTime.ToUniversalTime().Ticks;receipt.state="Started";CoreDeployment.Atomic(root,Rel(task,"candidate.json"),CoreDeployment.Utf8.GetBytes(CoreDeployment.Json.Serialize(receipt)));}
  static CandidateReceipt ReadCandidate(string root,Journal j){string file=PathAt(root,j.taskId,"candidate.json");if(!File.Exists(file)){if(j.candidateExpected!=0)throw new IOException("Candidate spawn outcome is unresolved; recovery is blocked.");return null;}byte[] raw=CoreDeployment.ReadBytes(file,8192);CoreReleaseProtocol.ReadStoredObject(raw,"nonce","path","pid","startTicks","state");var receipt=CoreDeployment.Json.Deserialize<CandidateReceipt>(CoreDeployment.Utf8.GetString(raw));if(receipt==null||receipt.nonce!=j.nonce||!CoreDeployment.SamePath(receipt.path,CoreReleaseProtocol.Target(root,"橙子启动器.exe"))||!(receipt.state=="Starting"&&receipt.pid==0&&receipt.startTicks==0||receipt.state=="Started"&&receipt.pid>0&&receipt.startTicks>0))throw new IOException("Invalid durable candidate ownership.");return receipt;}
  internal static void AssertCandidateStartup(string root,string task,string key){var j=Load(root,task);VerifyStored(root,j,key);var receipt=ReadCandidate(root,j);using(var self=Process.GetCurrentProcess()){if(receipt==null||receipt.state!="Started"||receipt.pid!=self.Id||receipt.startTicks!=self.StartTime.ToUniversalTime().Ticks||!CoreDeployment.SamePath(receipt.path,self.MainModule.FileName))throw new IOException("Candidate identity is not durably recorded.");}}
  static void RequireCandidateAbsent(string root,Journal j){var receipt=ReadCandidate(root,j);if(receipt==null)return;if(receipt.state!="Started")throw new IOException("Candidate spawn outcome is unresolved; recovery is blocked.");Process candidate;try{candidate=Process.GetProcessById(receipt.pid);}catch(ArgumentException){return;}using(candidate){if(candidate.HasExited)return;if(candidate.StartTime.ToUniversalTime().Ticks!=receipt.startTicks)return;throw new IOException("Recorded candidate is still alive or cannot be safely released; recovery is blocked.");}}
  internal static CoreUpdateResult ApplyLocked(string root,string taskId,Func<string,bool> health,Action<string> checkpoint,string key,Action releaseCandidate=null) {
   if(health==null)throw new ArgumentNullException("health");var j=Load(root,taskId);
   if(j.state!="Staged")throw new CoreUpdateException(j.state,"This task cannot be applied again.",null);
   CoreManifest target;CoreInstallRecord baseline;CoreUpdatePlan plan;
   try {
    target=VerifyStored(root,j,key);ValidateTargetRecord(root,j,target);
    baseline=Baseline(root,j);ValidateOriginalRecord(root,j);
    foreach(string path in (baseline==null?new List<CoreFile>():baseline.files).Select(f=>f.path).Union(target.files.Select(f=>f.path)))RequireOriginal(root,j,baseline,path);
    plan=PlanLocked(root,target,baseline);ValidateBackup(root,j,baseline);
    foreach(var file in plan.Changed)if(!CoreDeployment.Matches(PathAt(root,taskId,"new/"+file.path),file))throw new InvalidDataException("Staged bytes changed: "+file.path);
   }catch(Exception refusal) {
    // No installed bytes have changed. Preserve that phase and the original
    // contents; a preflight conflict must never initiate rollback writes.
    j.state="Conflicted";j.error="Preflight: "+refusal.Message;
    try{Save(root,j);}catch(Exception persistence){throw new CoreUpdateException("Conflicted",j.error+"; diagnostic journal could not be saved: "+persistence.Message,new AggregateException(refusal,persistence));}
    throw new CoreUpdateException("Conflicted",j.error,refusal);
   }
   try {
    Checkpoint(checkpoint,"staged");j.state="Applying";j.started=1;Save(root,j);
    foreach(var file in plan.Changed) {
     RequireOriginal(root,j,baseline,file.path);
     ReplaceReleaseFile(root,j,"new",file,()=>{Checkpoint(checkpoint,"prepared:"+file.path);RequireOriginal(root,j,baseline,file.path);});Checkpoint(checkpoint,"replaced:"+file.path);
    }
    foreach(var file in plan.Removed) {
     RequireOriginal(root,j,baseline,file.path);string path=CoreReleaseProtocol.Target(root,file.path);if(File.Exists(path))File.Delete(path);Checkpoint(checkpoint,"replaced:"+file.path);
    }
    ValidateOriginalRecord(root,j);CoreDeployment.Atomic(root,CoreDeployment.RecordName,Convert.FromBase64String(j.targetRecord),()=>{Checkpoint(checkpoint,"record-prepared");ValidateOriginalRecord(root,j);});Checkpoint(checkpoint,"record-committed");
    j.state="PendingHealth";j.candidateExpected=releaseCandidate==null?0:1;Save(root,j);ValidateInstalled(root,j,target);
    if(!health(CoreReleaseProtocol.Target(root,"橙子启动器.exe")))throw new IOException("Updated launcher did not confirm healthy startup.");
    ValidateInstalled(root,j,target);j.state="Succeeded";Save(root,j);return Result(j);
   }catch(Exception failure) {
    j.error=failure.Message;
    if(releaseCandidate!=null)try{releaseCandidate();}catch(Exception release){j.state="Conflicted";j.error="Candidate release failed; installed files retained: "+release.Message;Save(root,j);throw new CoreUpdateException("Conflicted",j.error,release);}
    try{Rollback(root,j,target,checkpoint);}catch(Exception rollback) {j.state="Conflicted";j.error="Apply: "+failure.Message+"; recovery: "+rollback.Message;try{Save(root,j);}catch{}throw new CoreUpdateException("Conflicted",j.error,rollback);}
    throw new CoreUpdateException("RolledBack","Update failed; previous installation restored. "+failure.Message,failure);
   }
  }
  static void Checkpoint(Action<string> action,string name) {if(action!=null)action(name);}
  static CoreUpdateResult Result(Journal j) {return new CoreUpdateResult{TaskId=j.taskId,State=j.state,Message=j.error};}
  static void ValidateOriginalRecord(string root,Journal j) {
   string path=CoreDeployment.Safe(root,CoreDeployment.RecordName);
   if(j.baselineRecord==null){if(File.Exists(path)||Directory.Exists(path))throw new IOException("An independent installation record appeared.");}
   else if(!File.Exists(path)||!CoreDeployment.ReadBytes(path,CoreReleaseProtocol.MaxManifestBytes).SequenceEqual(Convert.FromBase64String(j.baselineRecord)))throw new IOException("Installation record changed independently.");
  }
  static void RequireOriginal(string root,Journal j,CoreInstallRecord baseline,string relative) {
   string path=CoreReleaseProtocol.Target(root,relative);var old=baseline==null?null:baseline.files.SingleOrDefault(f=>f.path==relative);
   if(j.existed.Contains(relative)){if(old==null||!CoreDeployment.Matches(path,old))throw new IOException("Baseline changed: "+relative);}
   else if(File.Exists(path)||Directory.Exists(path))throw new IOException("Previously absent file appeared: "+relative);
  }
  static void ValidateBackup(string root,Journal j,CoreInstallRecord baseline) {
   foreach(string path in j.existed)if(!CoreDeployment.Matches(PathAt(root,j.taskId,"old/"+path),baseline.files.Single(f=>f.path==path)))throw new IOException("Rollback backup changed: "+path);
  }
  static void ReplaceReleaseFile(string root,Journal j,string area,CoreFile file,Action prepared=null) {
   string source=PathAt(root,j.taskId,area+"/"+file.path);
   if(!CoreDeployment.Matches(source,file))throw new IOException("Stored file changed: "+file.path);
   byte[] data=CoreDeployment.ReadBytes(source,(int)CoreReleaseProtocol.MaxFileBytes);
   if(data.LongLength!=file.length||!string.Equals(CoreReleaseProtocol.Hash(data),file.sha256,StringComparison.OrdinalIgnoreCase))throw new IOException("Stored file changed while reading.");
   // Prepare on the same volume in the task's marked directory, then atomically
   // replace the destination; a power/process interruption cannot expose partial bytes.
   string temp=Rel(j.taskId,"move-"+Guid.NewGuid().ToString("N"));CoreDeployment.WriteNew(root,temp,data);
   if(prepared!=null)prepared();
   string target=CoreReleaseProtocol.Target(root,file.path);Directory.CreateDirectory(Path.GetDirectoryName(target));target=CoreReleaseProtocol.Target(root,file.path);
   if(Directory.Exists(target))throw new IOException("Directory occupies release target.");
   if(File.Exists(target))File.Replace(CoreDeployment.Safe(root,temp),target,null);else File.Move(CoreDeployment.Safe(root,temp),target);
  }
  static void ValidateInstalled(string root,Journal j,CoreManifest target) {
   foreach(var file in target.files)CoreDeployment.RequireFile(root,file.path,file);
   var baseline=Baseline(root,j);if(baseline!=null)foreach(var old in baseline.files.Where(f=>!target.files.Any(n=>n.path==f.path))){string path=CoreReleaseProtocol.Target(root,old.path);if(File.Exists(path)||Directory.Exists(path))throw new IOException("Removed file unexpectedly exists.");}
   if(!CoreDeployment.ReadBytes(CoreDeployment.Safe(root,CoreDeployment.RecordName),CoreReleaseProtocol.MaxManifestBytes).SequenceEqual(Convert.FromBase64String(j.targetRecord)))throw new IOException("Committed record changed.");
  }
  static void ValidateRollbackFile(string root,Journal j,string relative,CoreFile old,CoreFile next) {
   string path=CoreReleaseProtocol.Target(root,relative);
   if(Directory.Exists(path)||(File.Exists(path)&&!(old!=null&&j.existed.Contains(relative)&&CoreDeployment.Matches(path,old))&&!(next!=null&&CoreDeployment.Matches(path,next))))throw new IOException("Rollback conflict: "+relative);
  }
  static void ValidateRollbackRecord(string root,Journal j) {
   string record=CoreDeployment.Safe(root,CoreDeployment.RecordName);if(Directory.Exists(record))throw new IOException("Rollback record is a directory.");
   if(File.Exists(record)){string current=Convert.ToBase64String(CoreDeployment.ReadBytes(record,CoreReleaseProtocol.MaxManifestBytes));if(current!=j.baselineRecord&&current!=j.targetRecord)throw new IOException("Rollback installation record conflict.");}
  }
  static void Rollback(string root,Journal j,CoreManifest target,Action<string> checkpoint=null) {
   // Both immediate failure and later startup recovery share this gate. It never
   // kills a process; inaccessible identity and incomplete spawn intent fail closed.
   RequireCandidateAbsent(root,j);
   var baseline=Baseline(root,j);ValidateBackup(root,j,baseline);
   var oldFiles=baseline==null?new List<CoreFile>():baseline.files;
   string[] paths=oldFiles.Select(f=>f.path).Union(target.files.Select(f=>f.path)).ToArray();
   // A staged task has never owned target bytes, even when an independent file
   // happens to equal the signed candidate. Keep that distinction after conflicts.
   if(j.started==0) {
    ValidateOriginalRecord(root,j);foreach(string relative in paths)RequireOriginal(root,j,baseline,relative);
    j.state="RolledBack";Save(root,j);return;
   }
   // Preflight the entire candidate before any rollback write. Unknown bytes are
   // never overwritten, including an independently edited record or removed file.
   foreach(string relative in paths) {
    ValidateRollbackFile(root,j,relative,oldFiles.SingleOrDefault(f=>f.path==relative),target.files.SingleOrDefault(f=>f.path==relative));
   }
   ValidateRollbackRecord(root,j);
   j.state="RollingBack";Save(root,j);
   foreach(string relative in paths) {
    string path=CoreReleaseProtocol.Target(root,relative);var old=oldFiles.SingleOrDefault(f=>f.path==relative);var next=target.files.SingleOrDefault(f=>f.path==relative);
    ValidateRollbackFile(root,j,relative,old,next);
    if(j.existed.Contains(relative)){if(!CoreDeployment.Matches(path,old))ReplaceReleaseFile(root,j,"old",old,()=>{Checkpoint(checkpoint,"rollback-prepared:"+relative);ValidateRollbackFile(root,j,relative,old,next);});}
    else if(File.Exists(path))File.Delete(CoreReleaseProtocol.Target(root,relative));
   }
   ValidateRollbackRecord(root,j);
   if(j.baselineRecord==null){if(File.Exists(CoreDeployment.Safe(root,CoreDeployment.RecordName)))File.Delete(CoreDeployment.Safe(root,CoreDeployment.RecordName));}
   else CoreDeployment.Atomic(root,CoreDeployment.RecordName,Convert.FromBase64String(j.baselineRecord),()=>{Checkpoint(checkpoint,"rollback-record-prepared");ValidateRollbackRecord(root,j);});
   j.state="RolledBack";Save(root,j);
  }
  public static List<CoreUpdateResult> Recover(string root) {return Recover(root,CoreUpdateTrust.PublicKeyXml);}
  public static List<CoreUpdateResult> Recover(string root,string publicKeyXml) {
   root=CoreDeployment.Root(root);using(CoreDeployment.Acquire(root)) {
    var results=new List<CoreUpdateResult>();foreach(string task in TaskIds(root)) {
     var j=Load(root,task);if(Terminal(j.state))continue;
     try{var target=VerifyStored(root,j,publicKeyXml);ValidateTargetRecord(root,j,target);Rollback(root,j,target);}
     catch(Exception error){j.state="Conflicted";j.error=error.Message;try{Save(root,j);}catch{}results.Add(Result(j));throw new CoreUpdateException("Conflicted","Recovery conflict; retained owned backups. "+error.Message,error);}
     results.Add(Result(j));
    }return results;
   }
  }
 }
}
