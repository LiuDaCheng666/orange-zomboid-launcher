using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
namespace PZ3D {
 static class CoreUpdaterIntegrationTests {
  public static int Main(string[] args){try{
   if(args.Length==2&&args[0]=="--hold"){File.WriteAllText(args[1],"ready");Thread.Sleep(2500);return 0;}
   if(args.Length==3&&args[0]=="--core-apply")return (int)typeof(CoreFixture).Assembly.GetType("PZ3D.CoreUpdaterProgram").GetMethod("Run",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{args,File.ReadAllText(Path.Combine(args[1],"fixture-public.xml"))});
   if(args.Length==4&&args[0]=="--core-update-health"){if(File.Exists(Path.Combine(args[1],"fixture-fail")))return 3;CoreUpdateTransaction.ConfirmHealth(args[1],args[2],args[3],File.ReadAllText(Path.Combine(args[1],"fixture-public.xml")));Thread.Sleep(3000);return 0;}
   if(args.Length==0&&File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-public.xml"))){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"old-restarted"),"once");return 0;}
   if(args.Length==5&&args[0]=="--receipt"){CoreUpdateTransaction.ConfirmHealth(args[1],args[2],args[3],File.ReadAllText(args[4]));return 0;}CoreFixture.Initialize();HealthIdentity();Lifecycle(false);Lifecycle(true);Console.WriteLine("CORE HELPER: "+CoreFixture.Passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  static void Lifecycle(bool fail){
   var type=typeof(CoreFixture).Assembly.GetType("PZ3D.CoreUpdaterProgram");CoreFixture.Check(type!=null,"native updater helper lifecycle exists");
   string root=CoreFixture.Root();var files=new Dictionary<string,byte[]>{{"橙子启动器.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"橙子更新助手.exe",File.ReadAllBytes(typeof(CoreFixture).Assembly.Location)},{"Web/app.js",CoreFixture.Bytes("old-js")}};
   CoreDeployment.Install(root,CoreFixture.Manifest("1.1.0",files),files,CoreFixture.Key);File.WriteAllText(Path.Combine(root,"fixture-public.xml"),CoreFixture.Key);if(fail)File.WriteAllText(Path.Combine(root,"fixture-fail"),"exit");var next=new Dictionary<string,byte[]>(files);next["Web/app.js"]=CoreFixture.Bytes("new-js");string task=CoreUpdateTransaction.Stage(root,CoreFixture.Manifest("1.1.1",next),CoreFixture.Changed(),CoreFixture.Key),ready=Path.Combine(root,"parent-ready");
   using(var original=Process.Start(new ProcessStartInfo(Path.Combine(root,"橙子启动器.exe"),"--hold \""+ready+"\""){UseShellExecute=false,CreateNoWindow=true})) {
    var end=DateTime.UtcNow.AddSeconds(5);while(!File.Exists(ready)&&DateTime.UtcNow<end)Thread.Sleep(20);
    var start=type.GetMethod("Start",BindingFlags.Static|BindingFlags.NonPublic);using(var helper=(Process)start.Invoke(null,new object[]{root,task,original,CoreFixture.Key})) {
     CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))=="old-js","helper waits for matching original launcher exit");
     CoreFixture.Check(helper.WaitForExit(15000),"helper completes bounded native lifecycle");
     var info=CoreUpdateTransaction.GetTaskInfo(root,task,CoreFixture.Key);CoreFixture.Check(info.State==(fail?"RolledBack":"Succeeded"),fail?"failed candidate rolls back":"healthy candidate commits");
     CoreFixture.Check(File.ReadAllText(CoreFixture.FileAt(root,"Web/app.js"))==(fail?"old-js":"new-js"),"native lifecycle leaves expected installed bytes");
     if(fail){end=DateTime.UtcNow.AddSeconds(3);while(!File.Exists(Path.Combine(root,"old-restarted"))&&DateTime.UtcNow<end)Thread.Sleep(20);CoreFixture.Check(File.Exists(Path.Combine(root,"old-restarted")),"rollback restarts old launcher");}
    }
   }
  }
  static void HealthIdentity(){
   var safeApply=typeof(CoreUpdateTransaction).GetMethod("ApplyWithRelease",BindingFlags.NonPublic|BindingFlags.Static);CoreFixture.Check(safeApply!=null,"transaction releases spawned candidate before rollback");
   string refused=CoreFixture.Install();string refusedTask=CoreUpdateTransaction.Stage(refused,CoreFixture.Manifest("1.1.1",CoreFixture.B()),CoreFixture.Changed(),CoreFixture.Key);
   CoreFixture.Reject(()=>safeApply.Invoke(null,new object[]{refused,refusedTask,new Func<string,bool>(exe=>false),new Action(()=>{throw new IOException("candidate still running");}),CoreFixture.Key}),"candidate release failure refuses unsafe rollback");
   CoreFixture.Check(CoreUpdateTransaction.GetTaskInfo(refused,refusedTask,CoreFixture.Key).State=="Conflicted"&&File.ReadAllText(CoreFixture.FileAt(refused,"Web/app.js"))=="new-js","unreleased candidate leaves durable conflict without replacing its files");CoreUpdateTransaction.Recover(refused,CoreFixture.Key);CoreFixture.Same(refused,CoreFixture.A,"later recovery restores after candidate is released");
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
