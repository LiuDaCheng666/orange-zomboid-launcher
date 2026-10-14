using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace PZ3D {
 static class LauncherFeaturesTests {
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  public static void Run(){
   var legacy=typeof(MainForm).GetMethod("ConfirmBuddyTakeover",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);Check(legacy!=null,"legacy takeover has equivalent explicit scope and Steam confirmation route");Check(typeof(MainForm).GetMethod("LegacyInstallConflict",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!=null,"legacy failed installation offers takeover only after native conflict scan");LegacyBuddyConfirmation();
   var type=typeof(Engine).Assembly.GetType("PZ3D.LauncherLaunch");Check(type!=null,"Steam launcher preflight policy exists");
   string root=Path.Combine(LauncherTestPaths.Root,"orange-launch-preflight-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string game=Path.Combine(root,"game");Directory.CreateDirectory(game);var e=new Engine(game,Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Data);
   File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx8g\"]}");
   var reason=type.GetMethod("Reason");
   Check((string)reason.Invoke(null,new object[]{e,false,false})=="","valid vanilla game can launch without forcing 3D");
   Check(((string)reason.Invoke(null,new object[]{e,true,false})).Contains("任务"),"busy installation blocks game launch");
   Check(((string)reason.Invoke(null,new object[]{e,false,true})).Contains("运行"),"running game prevents duplicate launch");
   File.WriteAllText(Path.Combine(e.Data,"workshop-rebuild.pending"),"failed");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("工坊"),"unfinished destructive redownload cannot be launched as ready");File.Delete(Path.Combine(e.Data,"workshop-rebuild.pending"));
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("Buddy"),"configured native loader requires both deployed files");
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-javaagent:OrangeShootPresentation.jar\"]}");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("旧"),"known instrument-breaking legacy agent blocks launch with repair hint");
   File.WriteAllText(e.Config,"broken");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("配置"),"malformed launch config is diagnosed before Steam launch");
   foreach(string bad in new[]{"{\"vmArgs\":[123]}","{\"vmArgs\":[\"-Xmxgarbage\"]}","{\"vmArgs\":[],\"windows\":{\"10\":7}}","{\"vmArgs\":[\"-javaagent:unknown.jar\"]}","{\"vmArgs\":[\"-javaagent:OtherZombieBuddy.jar\"]}","{\"vmArgs\":[\"-javaagent:ZombieBuddy.jar\"]}","{\"vmArgs\":[\"-Xmx999999999999999999g\"]}"}){File.WriteAllText(e.Config,bad);Check((string)reason.Invoke(null,new object[]{e,false,false})!="","unsafe or malformed launch arguments block Steam launch: "+bad);}
   string original="{\"vmArgs\":[\"-Xmx4096g\",\"-Dcustom=keep\"]}";File.WriteAllText(e.Config,original);string prefs=Path.Combine(root,"legacy-prefs");Directory.CreateDirectory(prefs);File.WriteAllText(Path.Combine(prefs,"game-path.txt"),game);bool preview=Program.PreviewMode;Program.PreviewMode=false;
   try{using(var form=new MainForm(prefs)){var input=form.Controls.Find("launcher-memory-number",true).SingleOrDefault() as NumericUpDown;Check(input!=null&&input.Value==4096,"legacy page automatically reads actual existing heap without truncation");Check(File.ReadAllText(e.Config)==original,"legacy automatic memory read does not apply or rewrite configuration");}}finally{Program.PreviewMode=preview;}
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  sealed class LegacyOS:BuddyTakeoverEnvironment {
   internal string Root;internal bool Running=true;internal int Shutdowns;
   internal override BuddySteamIdentity Capture(){return new BuddySteamIdentity(SteamLaunchOptions.Read(Root,42),77,100,Path.Combine(Root,"steam.exe"));}
   internal override BuddySteamState Inspect(BuddySteamIdentity bound){return Running?BuddySteamState.Running:BuddySteamState.Exited;}
   internal override bool GameRunning(string game){return false;}
   internal override void Shutdown(BuddySteamIdentity bound){Shutdowns++;Running=false;}
   internal override bool Reopen(BuddySteamIdentity bound){return true;}
   internal override long Milliseconds{get{return 0;}}
   internal override void Wait(int ms){throw new Exception("unexpected fixture wait");}
  }
  static void LegacyBuddyConfirmation(){
   string root=Path.Combine(LauncherTestPaths.Root,"legacy-buddy-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx6g\",\"-javaagent:ZombieBuddy.jar\"]}");string before=File.ReadAllText(e.Config);var os=new LegacyOS{Root=Path.Combine(root,"steam")};SteamLaunchOptionsTests.Fixture(os.Root,42,SteamLaunchOptionsTests.Wrap("\"108600\" { \"LaunchOptions\" \"-agentlib:zbNative -nosteam\" }"));File.WriteAllText(Path.Combine(os.Root,"steam.exe"),"fixture");int decisions=0;
   Check(MainForm.LegacyInstallConflict(e,"InstallComponents",os)!=null&&os.Shutdowns==0&&!Directory.Exists(e.Data),"legacy actual supported install conflict offers a pure scan before consent");Check(MainForm.LegacyInstallConflict(e,"SetHeapGiB",os)==null,"noninstallation error never offers migration");File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid64.bat"),"if 1==1 (\njava -agentlib:zbNative -jar game.jar\n)\n");Check(MainForm.LegacyInstallConflict(e,"InstallComponents",os)==null,"unknown complex BAT does not offer automatic legacy takeover");File.Delete(Path.Combine(e.Game,"ProjectZomboid64.bat"));
   var result=MainForm.ConfirmBuddyTakeover(e,false,(title,body)=>{decisions++;return false;},null,System.Threading.CancellationToken.None,os);Check(result.status=="cancelled"&&decisions==1&&os.Shutdowns==0&&File.ReadAllText(e.Config)==before,"legacy scope cancellation preserves files and client");decisions=0;
   result=MainForm.ConfirmBuddyTakeover(e,false,(title,body)=>{decisions++;return decisions==1;},null,System.Threading.CancellationToken.None,os);Check(result.status=="cancelled"&&decisions==2&&os.Shutdowns==0&&File.ReadAllText(e.Config)==before,"legacy separate Steam confirmation cancellation preserves files");decisions=0;
   result=MainForm.ConfirmBuddyTakeover(e,false,(title,body)=>{decisions++;return true;},null,System.Threading.CancellationToken.None,os);Check(result.cleanupCompleted&&!result.componentInstallCompleted&&decisions==2&&os.Shutdowns==1&&!File.ReadAllText(e.Config).Contains("javaagent"),"legacy two confirmations consume real isolated native cleanup without installation");
  }
 }
}
