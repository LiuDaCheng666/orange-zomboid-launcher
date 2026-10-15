using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Thread=System.Threading.Thread;
using System.Web.Script.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
namespace PZ3D {
 // Compiled only as the entry point of ephemeral signed fixture launchers.
 // Production Program never dispatches these fixture commands or feed paths.
 static class CoreUpdateNativeFixtureTests {
  [STAThread] public static int Main(string[] args){string root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);try{
   LauncherRuntime.Initialize();if(root.IndexOf("TestFixtures",StringComparison.OrdinalIgnoreCase)<0)throw new IOException("Native fixture requires isolated root.");
   if(args.Length==1&&args[0]=="--fixture-game"){Thread.Sleep(240000);return 0;}
   if(args.Length==3&&args[0]=="--playtime-watch"){
    string data=Path.GetFullPath(args[1]);if(data.IndexOf("TestFixtures",StringComparison.OrdinalIgnoreCase)<0)throw new IOException("Tracker fixture data is not isolated.");
    using(var self=Process.GetCurrentProcess())File.WriteAllText(Path.Combine(data,"monitor-process.json"),new JavaScriptSerializer().Serialize(new{pid=self.Id,birth=self.StartTime.ToUniversalTime().Ticks,image=self.MainModule.FileName}));
    return PlaytimeTracker.Watch(data,args[2]);
   }
   if(args.Length>0&&args[0]=="--session-watch")throw new IOException("Fixture never invokes the real-user session watcher.");
   if(args.Length==1&&args[0]=="--fixture-start-tracker"){
    CoreDeployment.AdoptPortable(root);var engine=FixtureEngine(root);var seed=new PlaytimeStore{totalSeconds=1234.5,reminderMinutes=45,session="prior-fixture-session"};seed.days[DateTime.Now.ToString("yyyy-MM-dd")]=100;
    LauncherNativeState.Write(Path.Combine(engine.Data,"playtime","time.json"),seed);string message=PlaytimeTracker.Start(engine);if(!message.Contains("已启动独立"))throw new Exception(message);return 0;
   }
   if(args.Length==1&&args[0]=="--fixture-session-protection"){SessionProtection(root);return 0;}
   if(args.Length==1&&args[0]=="--fixture-session-recovery"){
    var engine=FixtureEngine(root);if(GameProcessGuard.IsRunning(engine.Game))throw new Exception("Owned fixture game is still running.");LaunchProfiles.Recover(engine,true);
    if(LaunchProfiles.Pending(engine)||File.ReadAllText(engine.Mods)!="original fixture list\n")throw new Exception("Controlled launch journal did not recover.");File.WriteAllText(Path.Combine(root,"native-session-recovered"),"PASS");return 0;
   }
   if(args.Length>0&&args[0]=="--complete-core-uninstall")return UninstallerProgram.Main(args);
   if(args.Length==1&&args[0]=="--fixture-uninstall"){UninstallerProgram.UninstallCore(root,Process.GetCurrentProcess().MainModule.FileName);return 0;}
   if(args.Length==1&&args[0]=="--fixture-interrupt"){CoreDeployment.AdoptPortable(root);string feed=Path.Combine(root,"fixture-feed");var target=CoreReleaseProtocol.Verify(File.ReadAllBytes(Path.Combine(feed,"update-manifest.json")),File.ReadAllBytes(Path.Combine(feed,"update-manifest.sig")),CoreUpdateTrust.PublicKeyXml,AppVersion.Current);var plan=CoreUpdateTransaction.Plan(root,target);var files=new System.Collections.Generic.Dictionary<string,byte[]>();foreach(var file in plan.Changed){string name=Uri.UnescapeDataString(new Uri(file.url).Segments[new Uri(file.url).Segments.Length-1]);files.Add(file.path,File.ReadAllBytes(Path.Combine(feed,name)));File.AppendAllText(Path.Combine(root,"downloaded-files.txt"),name+Environment.NewLine);}string task=CoreUpdateTransaction.Stage(root,target,files);File.WriteAllText(Path.Combine(root,"native-task.txt"),task);CoreUpdateTransaction.Apply(root,task,p=>true,point=>{if(point=="record-committed")Environment.Exit(77);});return 3;}
   bool health=args.Length>0&&args[0]=="--core-update-health";if(!health){CoreDeployment.AdoptPortable(root);if(CoreUpdaterProgram.HandoffRecovery(root,CoreUpdateTrust.PublicKeyXml))return 0;}else new CoreHealthStartup(args);
   string assets=ModernAssets.Prepare();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);return Start(root,assets,args,health);
  }catch(Exception e){File.WriteAllText(Path.Combine(root,"native-fixture-error.txt"),e.ToString());return 1;}}
  static Engine FixtureEngine(string root){string fixture=Path.Combine(root,"OrangeLauncherRuntime","Synthetic");return new Engine(Path.Combine(fixture,"game"),Path.Combine(fixture,"user"),Path.Combine(fixture,"data"));}
  static void SessionProtection(string root){
   var engine=FixtureEngine(root);if(!GameProcessGuard.IsRunning(engine.Game))throw new Exception("Missing owned running-game evidence.");Directory.CreateDirectory(Path.GetDirectoryName(engine.Mods));File.WriteAllText(engine.Mods,"temporary fixture list\n");
   var record=new ProfileSessionRecord{Game=engine.Game,User=engine.User,Original=Convert.ToBase64String(Encoding.UTF8.GetBytes("original fixture list\n")),AppliedHash=DlssInstaller.HashFile(engine.Mods),CreatedUtc=DateTime.UtcNow.ToString("o"),State="launched",Token=LaunchProfiles.Key(engine),Nonce=Guid.NewGuid().ToString("N")};
   string folder=Path.Combine(engine.User,"mods","OrangeLauncherSession");foreach(string path in new[]{Path.Combine(folder,"common","mod.info"),Path.Combine(folder,"42","mod.info"),Path.Combine(folder,"42","media","lua","client","OrangeLaunchBridge.lua")})record.Files[path]=new string('A',64);
   string journal=Path.Combine(engine.Data,"launch-sessions",LaunchProfiles.Key(engine)+".json");LauncherNativeState.Write(journal,record);string before=File.ReadAllText(journal),mods=File.ReadAllText(engine.Mods);
   using(var controller=new ModernController(engine.Game,engine.User,engine.Data,false)){
    var method=typeof(ModernHostPolicy).GetMethod("CorePreflight",BindingFlags.Static|BindingFlags.NonPublic);object readiness=method.Invoke(null,new object[]{controller,new ModernDispatchQueue(),false});
    if((bool)readiness.GetType().GetProperty("allowed").GetValue(readiness,null))throw new Exception("Active launch journal allowed core preflight.");
    controller.RecoverStartup();if(File.ReadAllText(journal)!=before||File.ReadAllText(engine.Mods)!=mods)throw new Exception("Active session was prematurely restored.");
   }
   File.WriteAllText(Path.Combine(root,"native-session-blocked"),"PASS: preflight denied; journal and temporary list unchanged while own game lives");
  }
  [MethodImpl(MethodImplOptions.NoInlining)]static int Start(string root,string assets,string[] args,bool health){
   string fixture=Path.Combine(root,"OrangeLauncherRuntime","Synthetic");var controller=new ModernController(Path.Combine(fixture,"game"),Path.Combine(fixture,"user"),Path.Combine(fixture,"data"),true);
   string feed=Path.Combine(root,"fixture-feed");var updates=new CoreUpdateService(root,CoreUpdateTrust.PublicKeyXml,token=>new[]{File.ReadAllBytes(Path.Combine(feed,"update-manifest.json")),File.ReadAllBytes(Path.Combine(feed,"update-manifest.sig"))},(url,token)=>{string file=Uri.UnescapeDataString(new Uri(url).Segments[new Uri(url).Segments.Length-1]);File.AppendAllText(Path.Combine(root,"downloaded-files.txt"),file+Environment.NewLine);return File.OpenRead(Path.Combine(feed,file));});
   using(var form=new ModernForm(assets,health?args:new string[0],controller,updates))using(var timer=new Timer{Interval=100}) {
    form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new System.Drawing.Point(-25000,-25000);var web=(WebView2)typeof(ModernForm).GetField("web",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);int step=0;bool ticking=false;var deadline=DateTime.UtcNow.AddSeconds(75);
    timer.Tick+=async(sender,e)=>{if(ticking)return;ticking=true;try{
     if(DateTime.UtcNow>deadline)throw new Exception("Native UI fixture deadline elapsed.");if(web.CoreWebView2==null)return;
     if(await web.CoreWebView2.ExecuteScriptAsync("Boolean(window.orange?.state?.status)")!="true")return;
     if(health){File.WriteAllText(Path.Combine(root,"native-health-page-ready"),Process.GetCurrentProcess().Id.ToString());var info=CoreUpdateTransaction.GetTaskInfo(root,args[2]);if(info.State=="Succeeded"){await AssertOutcome(web,updates,"Succeeded",root);File.WriteAllText(Path.Combine(root,"native-running-version"),AppVersion.Current);File.WriteAllText(Path.Combine(root,"native-health-succeeded"),info.HealthPid+":"+info.HealthStartTicks);form.Close();}return;}
     if(args.Length==0){string outcome=updates.Status().state;await AssertOutcome(web,updates,outcome,root);File.WriteAllText(Path.Combine(root,"native-running-version"),AppVersion.Current);File.AppendAllText(Path.Combine(root,outcome=="Succeeded"?"native-success-restarted":"native-rollback-restarted"),"once\n");form.Close();return;}
     if(step==0){step=1;await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-core-open]').click();document.querySelector('[data-core-check]').click()");}
     else if(step==1&&updates.Status().state=="Available"){step=2;await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-core-download]').click()");}
     else if(step==2&&updates.Status().state=="Ready"){
      File.WriteAllText(Path.Combine(root,"native-task.txt"),updates.Status().taskId);
      if(await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#confirm').open")=="true"){
       string prompt=await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#confirm-body').innerText");if(!prompt.Contains("只重启启动器")||!prompt.Contains("游戏可以继续"))throw new Exception("Unexpected real Ready confirmation: "+prompt);
       step=3;File.WriteAllText(Path.Combine(root,"native-ready-confirmation.txt"),prompt);await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#confirm-yes').click()");
      }
     }
     else if(updates.Status().state=="Error")throw new Exception(updates.Status().message);
    }catch(Exception error){File.WriteAllText(Path.Combine(root,"native-fixture-error.txt"),error.ToString());form.ExitCode=1;form.Close();}finally{ticking=false;}};
    timer.Start();Application.Run(form);return form.ExitCode;
   }
  }
  static async Task AssertOutcome(WebView2 web,CoreUpdateService updates,string expected,string root){if(expected!="Succeeded"&&expected!="RolledBack")throw new Exception("Restart lost durable outcome: "+expected);await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-core-open]').click()");await Task.Delay(200);string text=await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#core-update-panel').innerText"),history=await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-core-history]')?.innerText||''");string label=expected=="Succeeded"?"更新成功":"已回退";if(!text.Contains(label)||!history.Contains(label))throw new Exception("Real native page lost outcome/history: "+text);var result=updates.Status().history[0];if(result.Message!=""&&!text.Contains(result.Message))throw new Exception("Real native page lost rollback reason");File.WriteAllText(Path.Combine(root,"native-outcome-ui.txt"),text);}
 }
}
