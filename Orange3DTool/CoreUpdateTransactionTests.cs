using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace PZ3D {
 static class CoreUpdateTransactionTests {
  static object Tx(string method,params object[] args) {return CoreFixture.Call("CoreUpdateTransaction",method,args);}
  static string Stage(string root,Dictionary<string,byte[]> next,Dictionary<string,byte[]> changed) {return (string)Tx("Stage",root,CoreFixture.Manifest("1.1.1",next),changed,CoreFixture.Key);}
  static object Apply(string root,string task,Func<string,bool> health,Action<string> checkpoint=null) {return Tx("Apply",root,task,health,checkpoint,CoreFixture.Key);}
  static string Record(string root) {return Path.Combine(root,"orange-installation.json");}
  public static int Main(string[] args) {
   try {
    if(args.Length>0&&args[0]=="--preflight") {
     CoreFixture.Initialize();PreflightRefusals(args.Length>1?args[1]:null);Console.WriteLine("CORE PREFLIGHT: "+CoreFixture.Passed+" PASS");return 0;
    }
    if(args.Length>0&&args[0]=="--health") {
     Tx("ConfirmHealth",args[1],args[2],args[4],File.ReadAllText(args[3]));return 0;
    }
    if(args.Length>0&&args[0]=="--prepare-child") {
     string key=File.ReadAllText(args[3]);var release=CoreReleaseProtocol.Verify(File.ReadAllBytes(args[4]),File.ReadAllBytes(args[5]),key,"1.1.0");
     Tx("Stage",args[1],release,CoreFixture.Changed(),new Action<string>(point=>{if(point==args[2])Environment.Exit(86);}),key);return 0;
    }
    if(args.Length>0&&args[0]=="--child") {
     CoreFixture.Key=File.ReadAllText(args[3]);
     Apply(args[1],args[2],exe=>!args[4].StartsWith("rollback-"),point=>{if(point==args[4]) {if(args.Length>5){File.WriteAllText(args[5],"locked");Thread.Sleep(30000);}else Environment.Exit(86);}});
     return 0;
    }
    CoreFixture.Initialize();CoreDeploymentTests.Run();Run();Console.WriteLine("CORE TRANSACTIONS: "+CoreFixture.Passed+" PASS");return 0;
   }catch(Exception e){Console.WriteLine(e);return 1;}
  }
  static void Run() {
   string root=CoreFixture.Install();File.WriteAllText(Path.Combine(root,"personal.txt"),"keep");
   var manifest=CoreFixture.Manifest("1.1.1",CoreFixture.B());
   var plan=Tx("Plan",root,manifest,CoreFixture.Key);
   CoreFixture.Check(((IEnumerable<CoreFile>)CoreFixture.Field(plan,"Changed")).Select(f=>f.path).SequenceEqual(new[]{"Web/app.js"}),"only changed JS needs download");
   CoreFixture.Check((long)CoreFixture.Field(plan,"Bytes")==6,"download total is changed bytes");
   byte[] oldRecord=File.ReadAllBytes(Record(root));string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   CoreFixture.Reject(()=>Apply(root,task,exe=>false),"health rejection reports rollback");
   CoreFixture.Same(root,CoreFixture.A,"health failure restores all prior file bytes");
   CoreFixture.Check(File.ReadAllBytes(Record(root)).SequenceEqual(oldRecord),"health failure restores exact prior installation record");
   CoreFixture.Check((string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"State")=="RolledBack","rollback result persists");
   CoreFixture.Check(!string.IsNullOrEmpty((string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Error")),"rolled back health failure retains a diagnostic reason");
   CoreFixture.Reject(()=>Apply(root,task,exe=>true),"rolled back task cannot be applied again");
   task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   string args=(string)Tx("HealthArguments",root,task,CoreFixture.Key);
   CoreFixture.Check(args.Contains(task)&&args.Contains("--core-update-health"),"health arguments bind root and task");
   CoreFixture.Reject(()=>Tx("ConfirmHealth",root,task,"bad",CoreFixture.Key),"invalid health nonce rejected");
   string nonce=(string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Nonce");
   CoreFixture.Reject(()=>Tx("ConfirmHealth",root,task,nonce,CoreFixture.Key),"health cannot confirm before pending phase");
   Apply(root,task,exe=>{CoreFixture.Check(exe==CoreFixture.FileAt(root,"橙子启动器.exe"),"health receives installed executable");Tx("ConfirmHealth",root,task,nonce,CoreFixture.Key);return true;});
   CoreFixture.Same(root,CoreFixture.B(),"health success keeps complete new version");
   CoreFixture.Check((string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"State")=="Succeeded","success persists distinctly");
   CoreFixture.Reject(()=>Apply(root,task,exe=>true),"successful task cannot apply twice");
   PortableDeployment.Uninstall(root);
   CoreFixture.Check(File.ReadAllText(Path.Combine(root,"personal.txt"))=="keep"&&!File.Exists(CoreFixture.FileAt(root,"Web/app.js")),"legacy entry dispatches v2 owned uninstall after update");
   Authentication();Conflicts();Interruptions();CrossProcess();Hardening();PublicationConflicts();ChildHealth();StagedRecovery();PreparationFailure();PreflightRefusals();
  }
  static void Authentication() {
   string root=CoreFixture.Install();var manifest=CoreFixture.Manifest("1.1.1",CoreFixture.B());manifest.version="9.9.9";manifest.files[1].sha256=new string('0',64);manifest.files[1].path="VERSION";
   string task=(string)Tx("Stage",root,manifest,CoreFixture.Changed(),CoreFixture.Key);Apply(root,task,exe=>true);
   CoreFixture.Check(((CoreInstallRecord)CoreFixture.Call("CoreDeployment","Read",root)).version=="1.1.1"&&!File.Exists(Path.Combine(root,"VERSION")),"mutable manifest cannot change signed stage authority");
   CoreFixture.Reject(()=>Tx("Plan",root,CoreFixture.Manifest("1.1.0",CoreFixture.A),CoreFixture.Key),"downgrade is rejected");
   root=CoreFixture.Install();var unsigned=CoreReleaseProtocol.ParseManifest(CoreFixture.Manifest("1.1.1",CoreFixture.B()).VerifiedEnvelope.Raw);
   CoreFixture.Reject(()=>Tx("Stage",root,unsigned,CoreFixture.Changed(),CoreFixture.Key),"unsigned staging rejected");
   File.WriteAllText(Path.Combine(root,"release-public-key.xml"),CoreFixture.Key);
   CoreFixture.Reject(()=>Tx("Stage",root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed()),"default staging refuses local public key in absence of embedded trust");
   using(var wrong=new System.Security.Cryptography.RSACryptoServiceProvider(2048)) {wrong.PersistKeyInCsp=false;CoreFixture.Reject(()=>Tx("Stage",root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),wrong.ToXmlString(false)),"envelope signed by wrong trusted dependency rejected");}
   CoreFixture.Reject(()=>Stage(root,CoreFixture.B(),new Dictionary<string,byte[]>{{"Web/app.js",CoreFixture.Bytes("bad")}}),"download byte tampering rejected before apply");
   task=Stage(root,CoreFixture.B(),CoreFixture.Changed());var info=Tx("GetTaskInfo",root,task,CoreFixture.Key);string dir=(string)CoreFixture.Field(info,"Directory");
   File.WriteAllText(Path.Combine(dir,"new","Web","app.js"),"tampered");
   CoreFixture.Reject(()=>Apply(root,task,exe=>true),"staged byte tampering blocks replacement");CoreFixture.Same(root,CoreFixture.A,"stage tampering preserves baseline");
   string rawRoot=CoreFixture.Install();string rawTask=Stage(rawRoot,CoreFixture.B(),CoreFixture.Changed());string rawDir=(string)CoreFixture.Field(Tx("GetTaskInfo",rawRoot,rawTask,CoreFixture.Key),"Directory");
   File.WriteAllText(Path.Combine(rawDir,"manifest.json"),"{}");CoreFixture.Reject(()=>Apply(rawRoot,rawTask,exe=>true),"persisted manifest signature revalidated");
  }
  static void Conflicts() {
   string root=CoreFixture.Install();string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());File.WriteAllText(CoreFixture.FileAt(root,"Web/app.css"),"independent");
   CoreFixture.Reject(()=>Apply(root,task,exe=>true),"modified unchanged baseline blocks update");CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="old-js","baseline conflict happens before replacement");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   using(var handle=new FileStream(CoreFixture.FileAt(root,"Web/app.js"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){CoreFixture.Reject(()=>Apply(root,task,exe=>true),"occupied candidate blocks apply");}
   CoreFixture.Same(root,CoreFixture.A,"occupied apply preserves prior version");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   CoreFixture.Reject(()=>Apply(root,task,exe=>false,point=>{if(point=="replaced:Web/app.js")File.WriteAllText(CoreFixture.FileAt(root,"Web/app.js"),"independent");}),"independent edit during rollback reports conflict");
   CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="independent","rollback never overwrites independently changed candidate");
   CoreFixture.Check((string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"State")=="Conflicted","rollback conflict is durable");
   CoreFixture.Reject(()=>Tx("Stage",root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),CoreFixture.Key),"unresolved recovery conflict blocks new task");
   root=CoreFixture.Install();var next=CoreFixture.B();next["VERSION"]=CoreFixture.Bytes("1.1.1");Directory.CreateDirectory(Path.Combine(root,"VERSION"));
   CoreFixture.Reject(()=>Stage(root,next,new Dictionary<string,byte[]>{{"Web/app.js",CoreFixture.Bytes("new-js")},{"VERSION",CoreFixture.Bytes("1.1.1")}}),"directory occupying owned target is a conflict");
  }
  static void Interruptions() {
   foreach(string point in new[]{"staged","prepared:Web/app.js","replaced:橙子启动器.exe","replaced:Web/app.js","replaced:Web/app.css","replaced:VERSION","record-prepared","record-committed","rollback-prepared:Web/app.js","rollback-record-prepared"}) {
    string root=CoreFixture.Install();byte[] before=File.ReadAllBytes(Record(root));var next=new Dictionary<string,byte[]>{{"橙子启动器.exe",CoreFixture.Bytes("launcher-b")},{"Web/app.js",CoreFixture.Bytes("new-js")},{"VERSION",CoreFixture.Bytes("1.1.1")}};
    string task=Stage(root,next,next);using(var child=Child(root,task,point,null)){CoreFixture.Check(child.WaitForExit(15000)&&child.ExitCode==86,"child interrupted at "+point);}
    Tx("Recover",root,CoreFixture.Key);CoreFixture.Same(root,CoreFixture.A,"recovery restores mixed old/new/removed files at "+point);
    CoreFixture.Check(!File.Exists(Path.Combine(root,"VERSION"))&&File.ReadAllBytes(Record(root)).SequenceEqual(before),"recovery removes new file and restores record at "+point);
    Tx("Recover",root,CoreFixture.Key);
   }
   string failed=CoreFixture.Install();string failedTask=Stage(failed,CoreFixture.B(),CoreFixture.Changed());
   CoreFixture.Reject(()=>Apply(failed,failedTask,exe=>true,point=>{if(point=="replaced:Web/app.js")throw new IOException("fixture disk failure");}),"I/O failure checkpoint reports failed apply");CoreFixture.Same(failed,CoreFixture.A,"I/O failure restores prior bytes");
  }
  static Process Child(string root,string task,string point,string signal) {
   string keyFile=root+".fixture-key.xml";File.WriteAllText(keyFile,CoreFixture.Key);
   string args="--child \""+root+"\" "+task+" \""+keyFile+"\" \""+point+"\""+(signal==null?"":" \""+signal+"\"");
   return Process.Start(new ProcessStartInfo(typeof(CoreUpdateTransactionTests).Assembly.Location,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true});
  }
  static void CrossProcess() {
   string root=CoreFixture.Install();string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());string signal=root+".signal";
   using(var child=Child(root,task,"staged",signal)) {
    var watch=Stopwatch.StartNew();while(!File.Exists(signal)&&watch.ElapsedMilliseconds<10000)Thread.Sleep(20);
    CoreFixture.Check(File.Exists(signal),"child owns transaction root lock");
    CoreFixture.Reject(()=>Apply(root,task,exe=>true),"competing process cannot apply");
    CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","Install",root,CoreFixture.Manifest("1.1.0",CoreFixture.A),CoreFixture.A,CoreFixture.Key),"install shares cross-process exclusion");
    CoreFixture.Reject(()=>CoreFixture.Call("CoreDeployment","Uninstall",root,null),"uninstall shares cross-process exclusion");
    child.Kill();child.WaitForExit();
   }
   Tx("Recover",root,CoreFixture.Key);CoreFixture.Same(root,CoreFixture.A,"abandoned process lock permits recovery");
  }
  static void Hardening() {
   string root=CoreFixture.Install();string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   string dir=(string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Directory");
   string journal=Path.Combine(dir,"journal.json");File.WriteAllText(journal,File.ReadAllText(journal).Replace("\"state\":\"Staged\"","\"state\":\"Applying\",\"state\":\"Staged\""));
   CoreFixture.Reject(()=>Apply(root,task,exe=>true),"ambiguous journal fields rejected");
   root=CoreFixture.Install();File.Delete(CoreFixture.FileAt(root,"Web/app.css"));var changed=CoreFixture.Changed();changed["Web/app.css"]=CoreFixture.A["Web/app.css"];task=Stage(root,CoreFixture.B(),changed);
   File.WriteAllBytes(CoreFixture.FileAt(root,"Web/app.css"),CoreFixture.A["Web/app.css"]);
   CoreFixture.Reject(()=>Apply(root,task,exe=>true),"baseline absence change after stage is a conflict");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());dir=(string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Directory");
   CoreFixture.Check(Tx("GetTaskInfo",root,task,CoreFixture.Key).GetType().GetField("HealthConfirmed")!=null,"validated helper health receipt is available");
   CoreFixture.Check(!(bool)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"HealthConfirmed"),"helper initially sees no health receipt");
   string nonce=(string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Nonce");
   Apply(root,task,exe=>{Tx("ConfirmHealth",root,task,nonce,CoreFixture.Key);return (bool)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"HealthConfirmed");});
   CoreFixture.Check((bool)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"HealthConfirmed"),"helper can verify persisted successful health receipt");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   string js=CoreFixture.FileAt(root,"Web/app.js");File.SetAttributes(js,FileAttributes.ReadOnly);
   try {CoreFixture.Reject(()=>Apply(root,task,exe=>true),"read-only destination simulates real replacement permission failure");}
   finally {File.SetAttributes(js,FileAttributes.Normal);}
   CoreFixture.Same(root,CoreFixture.A,"permission failure preserves complete old version");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());FileStream busy=null;
   try {CoreFixture.Reject(()=>Apply(root,task,exe=>{busy=new FileStream(CoreFixture.FileAt(root,"Web/app.js"),FileMode.Open,FileAccess.ReadWrite,FileShare.None);return false;}),"occupied rollback reports recovery failure");}
   finally {if(busy!=null)busy.Dispose();}
   CoreFixture.Check((string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"State")=="Conflicted","rollback I/O failure remains diagnosable");
   Tx("Recover",root,CoreFixture.Key);CoreFixture.Same(root,CoreFixture.A,"rollback can resume after external file lock is released");
   root=CoreFixture.Install();File.Delete(CoreFixture.FileAt(root,"Web/app.js"));
   task=(string)Tx("Stage",root,CoreFixture.Manifest("1.1.0",CoreFixture.A),new Dictionary<string,byte[]>{{"Web/app.js",CoreFixture.A["Web/app.js"]}},CoreFixture.Key);
   Apply(root,task,exe=>true);CoreFixture.Same(root,CoreFixture.A,"same-version repair restores missing owned bytes");
  }
  static void PublicationConflicts() {
   string root=CoreFixture.Install();string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   string js=CoreFixture.FileAt(root,"Web/app.js");
   CoreFixture.Reject(()=>Apply(root,task,exe=>true,point=>{if(point=="prepared:Web/app.js")File.WriteAllText(js,"independent-after-prepare");}),"independent edit after preparing replacement blocks publication");
   CoreFixture.Check(File.ReadAllText(js)=="independent-after-prepare","prepared replacement never overwrites independent edit");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
   CoreFixture.Reject(()=>Apply(root,task,exe=>true,point=>{if(point=="record-prepared")File.WriteAllText(Record(root),"independent-record");}),"record edit after preparation blocks publication");
   CoreFixture.Check(File.ReadAllText(Record(root))=="independent-record","record publication preserves independent edit");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());js=CoreFixture.FileAt(root,"Web/app.js");bool restoring=false;
   CoreFixture.Reject(()=>Apply(root,task,exe=>false,point=>{if(point=="rollback-prepared:Web/app.js"){restoring=true;File.WriteAllText(js,"independent-during-rollback");}}),"rollback detects edit after preparing restoration");
   CoreFixture.Check(restoring&&File.ReadAllText(js)=="independent-during-rollback","rollback publication never overwrites independent edit");
   root=CoreFixture.Install();task=Stage(root,CoreFixture.B(),CoreFixture.Changed());bool restoringRecord=false;
   CoreFixture.Reject(()=>Apply(root,task,exe=>false,point=>{if(point=="rollback-record-prepared"){restoringRecord=true;File.WriteAllText(Record(root),"independent-during-record-rollback");}}),"rollback detects record edit after preparing restoration");
   CoreFixture.Check(restoringRecord&&File.ReadAllText(Record(root))=="independent-during-record-rollback","rollback record publication preserves independent edit");
  }
  static void ChildHealth() {
   string root=CoreFixture.Install();string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());string keyFile=root+".fixture-key.xml";File.WriteAllText(keyFile,CoreFixture.Key);
   string nonce=(string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Nonce");
   Apply(root,task,exe=>{
    string args="--health \""+root+"\" "+task+" \""+keyFile+"\" "+nonce;
    using(var child=Process.Start(new ProcessStartInfo(typeof(CoreUpdateTransactionTests).Assembly.Location,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true})) {
     if(!child.WaitForExit(10000)){child.Kill();child.WaitForExit();throw new Exception("FAIL: health child must acknowledge without waiting for root mutex");}
     CoreFixture.Check(child.ExitCode==0,"real health child confirms while helper holds root mutex");
    }
    return (bool)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"HealthConfirmed");
   });
   CoreFixture.Check((string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"State")=="Succeeded","child health receipt allows successful commit");
  }
  static void StagedRecovery() {
   string root=CoreFixture.Install();var next=CoreFixture.B();next["VERSION"]=CoreFixture.Bytes("1.1.1");var changed=CoreFixture.Changed();changed["VERSION"]=next["VERSION"];
   string task=Stage(root,next,changed);File.WriteAllBytes(Path.Combine(root,"VERSION"),next["VERSION"]);
   CoreFixture.Reject(()=>Tx("Recover",root,CoreFixture.Key),"unapplied task recovery detects independently created target");
   CoreFixture.Check(File.ReadAllText(Path.Combine(root,"VERSION"))=="1.1.1","staged recovery preserves independently created target even if bytes match release");
   CoreFixture.Reject(()=>Tx("Recover",root,CoreFixture.Key),"repeated staged recovery keeps reporting the conflict");
   CoreFixture.Check(File.Exists(Path.Combine(root,"VERSION")),"repeated recovery does not reinterpret conflict as updater-owned bytes");
  }
  static void PreparationFailure() {
   foreach(string stop in new[]{"staging-started","staging-ready"}) {
    string root=CoreFixture.Install();byte[] record=File.ReadAllBytes(Record(root));bool interrupted=false;
    CoreFixture.Reject(()=>Tx("Stage",root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),new Action<string>(point=>{if(point==stop){interrupted=true;throw new IOException("fixture staging write failure");}}),CoreFixture.Key),"staging failure propagates at "+stop);
    CoreFixture.Check(interrupted,"staging interruption reaches real preparation at "+stop);
    CoreFixture.Same(root,CoreFixture.A,"failed preparation leaves complete baseline at "+stop);
    CoreFixture.Check(File.ReadAllBytes(Record(root)).SequenceEqual(record),"failed preparation preserves exact record at "+stop);
    bool recovered=true;try{Tx("Recover",root,CoreFixture.Key);}catch{recovered=false;}
    CoreFixture.Check(recovered,"incomplete preparation is outside recoverable published tasks at "+stop);
    string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());Apply(root,task,exe=>true);
    CoreFixture.Same(root,CoreFixture.B(),"incomplete preparation cannot block next transaction at "+stop);
    root=CoreFixture.Install();var manifest=CoreFixture.Manifest("1.1.1",CoreFixture.B());string keyFile=root+".fixture-key.xml",rawFile=root+".fixture-manifest.json",sigFile=root+".fixture-manifest.sig";
    File.WriteAllText(keyFile,CoreFixture.Key);File.WriteAllBytes(rawFile,manifest.VerifiedEnvelope.Raw);File.WriteAllBytes(sigFile,manifest.VerifiedEnvelope.Signature);
    string args="--prepare-child \""+root+"\" "+stop+" \""+keyFile+"\" \""+rawFile+"\" \""+sigFile+"\"";
    using(var child=Process.Start(new ProcessStartInfo(typeof(CoreUpdateTransactionTests).Assembly.Location,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true}))CoreFixture.Check(child.WaitForExit(10000)&&child.ExitCode==86,"real preparation child interrupted at "+stop);
    Tx("Recover",root,CoreFixture.Key);task=Stage(root,CoreFixture.B(),CoreFixture.Changed());Apply(root,task,exe=>true);
    CoreFixture.Same(root,CoreFixture.B(),"process-interrupted preparation does not block subsequent update at "+stop);
   }
  }
  static void PreflightRefusals(string only=null) {
   foreach(string kind in new[]{"baseline","record","backup","staged"}) {
    if(only!=null&&only!=kind)continue;
    string root=CoreFixture.Install();string task=Stage(root,CoreFixture.B(),CoreFixture.Changed());
    string dir=(string)CoreFixture.Field(Tx("GetTaskInfo",root,task,CoreFixture.Key),"Directory");
    if(kind=="baseline")File.WriteAllText(CoreFixture.FileAt(root,"Web/app.css"),"independent-baseline");
    if(kind=="record")File.WriteAllText(Record(root),"independent-record");
    if(kind=="backup")File.WriteAllText(Path.Combine(dir,"old","Web","app.js"),"damaged-backup");
    if(kind=="staged")File.WriteAllText(Path.Combine(dir,"new","Web","app.js"),"damaged-staging");
    var before=CoreFixture.A.ToDictionary(pair=>pair.Key,pair=>File.ReadAllBytes(CoreFixture.FileAt(root,pair.Key)));
    byte[] record=File.ReadAllBytes(Record(root));Exception refusal=null;
    try{Apply(root,task,exe=>true);}catch(Exception error){refusal=error;}
    CoreFixture.Same(root,before,"preflight refusal preserves existing installed bytes: "+kind);
    CoreFixture.Check(File.ReadAllBytes(Record(root)).SequenceEqual(record),"preflight refusal preserves exact existing record: "+kind);
    var info=Tx("GetTaskInfo",root,task,CoreFixture.Key);
    CoreFixture.Check((string)CoreFixture.Field(info,"State")=="Conflicted","preflight refusal persists Conflicted: "+kind);
    CoreFixture.Check(!string.IsNullOrWhiteSpace((string)CoreFixture.Field(info,"Error")),"preflight refusal persists nonempty diagnostic: "+kind);
    var conflict=refusal as CoreUpdateException;
    CoreFixture.Check(conflict!=null&&conflict.State=="Conflicted"&&conflict.InnerException!=null,"preflight refusal throws typed conflict with cause: "+kind);
    var journal=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(dir,"journal.json")));
    CoreFixture.Check(Convert.ToInt32(journal["started"])==0,"preflight refusal never begins installed mutation: "+kind);
   }
  }
 }
}
