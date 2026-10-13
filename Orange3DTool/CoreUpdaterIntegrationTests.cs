using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
namespace PZ3D {
 static class CoreUpdaterIntegrationTests {
  public static int Main(string[] args){try{
   if(args.Length==1&&args[0]=="--candidate-exit-now")return 3;
   if(args.Length==4&&args[0]=="--record-order"){typeof(CoreFile).GetField("length");typeof(CoreInstallRecord).GetField("version");CoreUpdateTransaction.GetTaskInfo(args[1],args[2],File.ReadAllText(args[3]));return 0;}
   if(args.Length==3&&args[0]=="--crash-owner"){string root=args[1],task=args[2],key=File.ReadAllText(Path.Combine(root,"fixture-public.xml"));CoreUpdateTransaction.ApplyWithRelease(root,task,exe=>{CoreUpdateTransaction.BeginCandidate(root,task,key);using(var child=Process.Start(new ProcessStartInfo(exe,"--candidate-hold \""+Path.Combine(root,"candidate-exit")+"\""){UseShellExecute=false,CreateNoWindow=true})){CoreUpdateTransaction.RecordCandidate(root,task,child,key);File.WriteAllText(Path.Combine(root,"owner-ready"),child.Id.ToString());Thread.Sleep(40000);}return false;},()=>{},key);return 0;}
   if(args.Length==2&&args[0]=="--candidate-hold"){File.WriteAllText(args[1]+".ready","ready");var until=DateTime.UtcNow.AddSeconds(40);while(!File.Exists(args[1])&&DateTime.UtcNow<until)Thread.Sleep(20);return 0;}
   if(args.Length==2&&args[0]=="--hold"){File.WriteAllText(args[1],"ready");Thread.Sleep(2500);return 0;}
   if(args.Length==3&&args[0]=="--core-apply")return (int)typeof(CoreFixture).Assembly.GetType("PZ3D.CoreUpdaterProgram").GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{args,File.ReadAllText(Path.Combine(args[1],"fixture-public.xml"))});
   if(args.Length==4&&args[0]=="--core-update-health"){if(File.Exists(Path.Combine(args[1],"fixture-fail")))return 3;CoreUpdateTransaction.ConfirmHealth(args[1],args[2],args[3],File.ReadAllText(Path.Combine(args[1],"fixture-public.xml")));Thread.Sleep(3000);return 0;}
   if(args.Length==0&&File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-public.xml"))){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"old-restarted"),"once");return 0;}
   if(args.Length==5&&args[0]=="--receipt"){CoreUpdateTransaction.ConfirmHealth(args[1],args[2],args[3],File.ReadAllText(args[4]));return 0;}CoreFixture.Initialize();if(args.Length==1&&args[0]=="--test-exited"){ExitedBeforeIdentity();return 0;}Lifecycle(true,true);ExitedBeforeIdentity();UnknownSpawn();RecordOrder();DurableRecovery(false);DurableRecovery(true);InterruptedOwner();HealthIdentity();Lifecycle(false);Lifecycle(true);Console.WriteLine("CORE HELPER: "+CoreFixture.Passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
static void UnknownSpawn(){string root=CoreFixture.Install(),task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),CoreFixture.Key);var type=typeof(CoreUpdaterProgram).GetNestedType("Candidate",BindingFlags.NonPublic);object candidate=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{root,task,CoreFixture.Key},null);try{CoreFixture.Reject(()=>CoreUpdateTransaction.ApplyWithRelease(root,task,exe=>{CoreUpdateTransaction.BeginCandidate(root,task,CoreFixture.Key);type.GetField("spawnAttempted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(candidate,true);throw new IOException("Outcome unknown, no returned process handle");},()=>type.GetMethod("Release",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(candidate,null),CoreFixture.Key),"unknown attempted spawn cannot certify absence");CoreFixture.Reject(()=>CoreUpdateTransaction.Recover(root,CoreFixture.Key),"unknown attempted spawn still blocks later recovery");CoreFixture.Reject(()=>CoreUpdateTransaction.PendingRecovery(root,CoreFixture.Key),"ordinary startup refuses unknown candidate before copying helper");CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="new-js","unknown spawn preserves installed candidate bytes");}finally{((IDisposable)candidate).Dispose();}}
  static void ExitedBeforeIdentity(){
   string root=CoreFixture.Root();var files=new Dictionary<string,byte[]>{{"橙子启动器.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"Web/app.js",CoreFixture.Bytes("old-js")}};CoreDeployment.Install(root,CoreFixture.Manifest("1.1.0",files),files,CoreFixture.Key);var next=new Dictionary<string,byte[]>(files);next["Web/app.js"]=CoreFixture.Bytes("new-js");string task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",next),CoreFixture.Changed(),CoreFixture.Key);var type=typeof(CoreUpdaterProgram).GetNestedType("Candidate",BindingFlags.NonPublic);object candidate=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{root,task,CoreFixture.Key},null);
   try{try{CoreUpdateTransaction.ApplyWithRelease(root,task,exe=>{CoreUpdateTransaction.BeginCandidate(root,task,CoreFixture.Key);var child=Process.Start(new ProcessStartInfo(exe,"--candidate-exit-now"){UseShellExecute=false,CreateNoWindow=true});
     // Fault injection: retain the real returned process handle exactly where
     // Healthy owns it, but force exit before PID/start/path recording can run.
     type.GetField("process",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(candidate,child);CoreFixture.Check(child.WaitForExit(5000),"real owned candidate exited before identity persistence");return false;
    },()=>type.GetMethod("Release",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(candidate,null),CoreFixture.Key);}catch(CoreUpdateException e){CoreFixture.Check(e.State=="RolledBack","known exited candidate resolves startup intent and permits rollback");}
    CoreFixture.Same(root,files,"known exited candidate restores baseline");CoreUpdateTransaction.Recover(root,CoreFixture.Key);CoreFixture.Check(true,"resolved exited candidate does not block subsequent recovery");
   }finally{((IDisposable)candidate).Dispose();}
  }
  static void RecordOrder(){string root=CoreFixture.Install(),task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),CoreFixture.Key),key=Path.Combine(root,"fixture-public.xml");File.WriteAllText(key,CoreFixture.Key);using(var child=Process.Start(new ProcessStartInfo(typeof(CoreFixture).Assembly.Location,"--record-order \""+root+"\" "+task+" \""+key+"\""){UseShellExecute=false,CreateNoWindow=true})){CoreFixture.Check(child.WaitForExit(5000)&&child.ExitCode==0,"cross-process reflection cache field ordering cannot invalidate authentic target record");}
   string journal=Path.Combine(CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key).Directory,"journal.json"),original=File.ReadAllText(journal);var map=CoreDeployment.Json.Deserialize<Dictionary<string,object>>(original);string record=CoreDeployment.Utf8.GetString(Convert.FromBase64String((string)map["targetRecord"]));
   foreach(string changed in new[]{record.Replace("\"path\":","\"untrusted\":\"extra\",\"path\":"),record.Replace("\"schema\":2","\"schema\":\"2\""),record.Replace("\"length\":6","\"length\":\"6\""),record.Replace("\"length\":6","\"length\":7")}){if(changed==record)throw new Exception("Target record mutation fixture did not change bytes");map["targetRecord"]=Convert.ToBase64String(CoreFixture.Bytes(changed));File.WriteAllText(journal,CoreDeployment.Json.Serialize(map));CoreFixture.Reject(()=>CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key),"target record refuses unsigned field changes/additions/type coercion");}File.WriteAllText(journal,original);
  }
  static void InterruptedOwner(){
   string root=CoreFixture.Root();var files=new Dictionary<string,byte[]>{{"橙子启动器.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"Web/app.js",CoreFixture.Bytes("old-js")}};CoreDeployment.Install(root,CoreFixture.Manifest("1.1.0",files),files,CoreFixture.Key);var next=new Dictionary<string,byte[]>(files);next["Web/app.js"]=CoreFixture.Bytes("new-js");string task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",next),CoreFixture.Changed(),CoreFixture.Key);File.WriteAllText(Path.Combine(root,"fixture-public.xml"),CoreFixture.Key);
   using(var owner=Process.Start(new ProcessStartInfo(typeof(CoreFixture).Assembly.Location,"--crash-owner \""+root+"\" "+task){UseShellExecute=false,CreateNoWindow=true}))try{
    var end=DateTime.UtcNow.AddSeconds(8);while(!File.Exists(Path.Combine(root,"owner-ready"))&&DateTime.UtcNow<end)Thread.Sleep(20);CoreFixture.Check(File.Exists(Path.Combine(root,"owner-ready")),"separate helper durably records real candidate before interruption");owner.Kill();CoreFixture.Check(owner.WaitForExit(5000),"owned helper is terminated during PendingHealth");
    CoreFixture.Reject(()=>CoreUpdateTransaction.Recover(root,CoreFixture.Key),"abandoned helper mutex does not authorize rollback under live candidate");CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="new-js","interrupted helper recovery preserves sparse candidate bytes");
    using(var child=Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root,"owner-ready"))))){File.WriteAllText(Path.Combine(root,"candidate-exit"),"exit");CoreFixture.Check(child.WaitForExit(5000),"orphaned candidate exits normally");}CoreUpdateTransaction.Recover(root,CoreFixture.Key);CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="old-js","startup recovery restores after orphaned candidate exit");
   }finally{File.WriteAllText(Path.Combine(root,"candidate-exit"),"exit");if(!owner.HasExited){owner.Kill();owner.WaitForExit(5000);}}
  }
  static void DurableRecovery(bool intentOnly){
   string root=CoreFixture.Root();var files=new Dictionary<string,byte[]>{{"橙子启动器.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"Web/app.js",CoreFixture.Bytes("old-js")}};
   CoreDeployment.Install(root,CoreFixture.Manifest("1.1.0",files),files,CoreFixture.Key);var next=new Dictionary<string,byte[]>(files);next["Web/app.js"]=CoreFixture.Bytes("new-js");string task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",next),CoreFixture.Changed(),CoreFixture.Key),exit=Path.Combine(root,"candidate-exit"),exe=Path.Combine(root,"橙子启动器.exe");Process child=null;
   try{
    CoreFixture.Reject(()=>CoreUpdateTransaction.ApplyWithRelease(root,task,path=>{
     CoreUpdateTransaction.BeginCandidate(root,task,CoreFixture.Key);
     child=Process.Start(new ProcessStartInfo(exe,"--candidate-hold \""+exit+"\""){UseShellExecute=false,CreateNoWindow=true});
     if(!intentOnly)CoreUpdateTransaction.RecordCandidate(root,task,child,CoreFixture.Key);
     var end=DateTime.UtcNow.AddSeconds(5);while(!File.Exists(exit+".ready")&&DateTime.UtcNow<end)Thread.Sleep(20);CoreFixture.Check(File.Exists(exit+".ready")&&!child.HasExited,"real candidate remains alive before simulated helper release failure");return false;
    },()=>{throw new IOException("simulated release failure");},CoreFixture.Key),"candidate release failure is retained");
    CoreFixture.Reject(()=>CoreUpdateTransaction.Recover(root,CoreFixture.Key),intentOnly?"unresolved spawn intent blocks recovery":"recovery refuses rollback under live exact candidate");
    CoreFixture.Reject(()=>CoreUpdateTransaction.PendingRecovery(root,CoreFixture.Key),"ordinary startup refuses live or unresolved candidate before helper handoff");
    CoreFixture.Check(!child.HasExited&&File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="new-js","refused recovery preserves candidate and sparse changed bytes");
    File.WriteAllText(exit,"exit");CoreFixture.Check(child.WaitForExit(5000),"owned test candidate exits normally");
    if(intentOnly)CoreFixture.Reject(()=>CoreUpdateTransaction.Recover(root,CoreFixture.Key),"unknown spawn outcome stays blocked even after unrecorded child exits");
    else{CoreUpdateTransaction.Recover(root,CoreFixture.Key);CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="old-js","exact candidate absence permits safe recovery");}
   }finally{if(child!=null){File.WriteAllText(exit,"exit");if(!child.WaitForExit(5000))child.Kill();child.Dispose();}}
  }
  static void Lifecycle(bool fail,bool badExecutable=false){
   var type=typeof(CoreFixture).Assembly.GetType("PZ3D.CoreUpdaterProgram");CoreFixture.Check(type!=null,"native updater helper lifecycle exists");
   string root=CoreFixture.Root();var files=new Dictionary<string,byte[]>{{"橙子启动器.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"橙子更新助手.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"Web/app.js",CoreFixture.Bytes("old-js")}};
   CoreDeployment.Install(root,CoreFixture.Manifest("1.1.0",files),files,CoreFixture.Key);File.WriteAllText(Path.Combine(root,"fixture-public.xml"),CoreFixture.Key);if(fail&&!badExecutable)File.WriteAllText(Path.Combine(root,"fixture-fail"),"exit");var next=new Dictionary<string,byte[]>(files);next["Web/app.js"]=CoreFixture.Bytes("new-js");var changed=CoreFixture.Changed();if(badExecutable){next["橙子启动器.exe"]=CoreFixture.Bytes("signed but not an executable");changed["橙子启动器.exe"]=next["橙子启动器.exe"];}string task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",next),changed,CoreFixture.Key),ready=Path.Combine(root,"parent-ready");
   using(var original=Process.Start(new ProcessStartInfo(Path.Combine(root,"橙子启动器.exe"),"--hold \""+ready+"\""){UseShellExecute=false,CreateNoWindow=true})) {
    var end=DateTime.UtcNow.AddSeconds(5);while(!File.Exists(ready)&&DateTime.UtcNow<end)Thread.Sleep(20);
    var start=type.GetMethod("Start",BindingFlags.Static|BindingFlags.NonPublic);using(var helper=(Process)start.Invoke(null,new object[]{root,task,original,CoreFixture.Key})) {
     CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="old-js","helper waits for matching original launcher exit");
     CoreFixture.Check(helper.WaitForExit(15000),"helper completes bounded native lifecycle");
     var info=CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key);CoreFixture.Check(info.State==(fail?"RolledBack":"Succeeded"),badExecutable?"CreateProcess rejected executable resolves intent and rolls back":fail?"failed candidate rolls back":"healthy candidate commits");
     CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))==(fail?"old-js":"new-js"),"native lifecycle leaves expected installed bytes");
     if(badExecutable){CoreFixture.Check(CoreReleaseProtocol.HashFile(Path.Combine(root,"橙子启动器.exe"))==CoreReleaseProtocol.Hash(files["橙子启动器.exe"]),"rejected spawn restores exact original executable bytes");CoreUpdateTransaction.Recover(root,CoreFixture.Key);CoreFixture.Check(true,"known failed spawn does not block subsequent recovery");}
     if(fail){end=DateTime.UtcNow.AddSeconds(3);while(!File.Exists(Path.Combine(root,"old-restarted"))&&DateTime.UtcNow<end)Thread.Sleep(20);CoreFixture.Check(File.Exists(Path.Combine(root,"old-restarted")),"rollback restarts old launcher");}
    }
   }
  }
  static void HealthIdentity(){
   var safeApply=typeof(CoreUpdateTransaction).GetMethod("ApplyWithRelease",BindingFlags.NonPublic|BindingFlags.Static);CoreFixture.Check(safeApply!=null,"transaction releases spawned candidate before rollback");
   CoreFixture.Check(typeof(ModernForm).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(string[]),typeof(ModernController),typeof(CoreUpdateService)},null)!=null,"real UI can receive isolated controller and update source");
   string root=CoreFixture.Install(),task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),CoreFixture.Key),key=Path.Combine(root,"fixture-public.xml");File.WriteAllText(key,CoreFixture.Key);
   CoreUpdateTransaction.Apply(root,task,exe=>{
    var info=CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key);
    var healthGuard=typeof(CoreHealthStartup).GetMethod("ValidateAction",BindingFlags.Static|BindingFlags.NonPublic);CoreFixture.Check(healthGuard!=null,"health candidate has a native mutation gate until commit");
    healthGuard.Invoke(null,new object[]{"status",info});CoreFixture.Check(true,"health candidate reads do not deadlock handshake");
    foreach(string action in new[]{"launch","profileLaunch","componentsAction","coreUpdateApply","javaSave"})CoreFixture.Reject(()=>healthGuard.Invoke(null,new object[]{action,info}),"pending health refuses mutation: "+action);
    using(var child=Process.Start(new ProcessStartInfo(typeof(CoreFixture).Assembly.Location,"--receipt \""+root+"\" "+task+" "+info.Nonce+" \""+key+"\""){UseShellExecute=false,CreateNoWindow=true})){
     int pid=child.Id;long ticks=child.StartTime.ToUniversalTime().Ticks;
     CoreFixture.Check(child.WaitForExit(5000)&&child.ExitCode==0,"health child confirms while helper owns root mutex");
     info=CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key);var field=typeof(CoreUpdateTaskInfo).GetField("HealthPid");CoreFixture.Check(field!=null,"health receipt exposes candidate process identity");
     CoreFixture.Check((int)field.GetValue(info)==pid&&(long)typeof(CoreUpdateTaskInfo).GetField("HealthStartTicks").GetValue(info)==ticks,"health receipt binds exact child PID and birth");
     CoreFixture.Check(CoreDeployment.SamePath((string)typeof(CoreUpdateTaskInfo).GetField("HealthPath").GetValue(info),typeof(CoreFixture).Assembly.Location),"health receipt binds executable path");return info.HealthConfirmed;
    }
   },null,CoreFixture.Key);
   typeof(CoreHealthStartup).GetMethod("ValidateAction",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"launch",CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key)});CoreFixture.Check(true,"committed healthy candidate permits normal mutations");
  }
 }
}
