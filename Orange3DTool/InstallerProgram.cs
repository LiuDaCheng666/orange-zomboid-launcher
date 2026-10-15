using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace PZ3D {
 static class InstallerProgram {
  static bool IsCore {get{return typeof(InstallerProgram).Assembly.GetManifestResourceInfo("Setup.Core.zip")!=null;}}
  static InstallerInstallService Service(){return IsCore?new InstallerInstallService(PortableDeployment.Resource,CoreUpdateTrust.PublicKeyXml,AppVersion.Current):InstallerInstallService.Legacy(PortableDeployment.Resource,AppVersion.Current);}
  internal static void Install(string root){Service().Install(root,null);}
  static int Verify(){try{string root=Path.Combine(Path.GetTempPath(),"orange-setup-"+Guid.NewGuid().ToString("N"));Install(root);if(IsCore){var record=CoreDeployment.Read(root);foreach(var file in record.files)CoreDeployment.RequireFile(root,file.path,file);if(record.schema!=2||record.version!=AppVersion.Current)throw new Exception("Installation version mismatch");}else if(DlssInstaller.HashFile(Path.Combine(root,"橙子启动器.exe"))!=DlssInstaller.Hash(PortableDeployment.Resource("Setup.Launcher.exe")))throw new Exception("Installed launcher differs");File.WriteAllText(Path.Combine(root,"personal.txt"),"keep");PortableDeployment.Uninstall(root);if(File.Exists(Path.Combine(root,"橙子启动器.exe"))||File.Exists(Path.Combine(root,"卸载橙子启动器.exe"))||File.ReadAllText(Path.Combine(root,"personal.txt"))!="keep")throw new Exception("Scoped uninstall incomplete");Console.WriteLine("PASS: packed installer exact extraction and owned uninstall; schema="+(IsCore?"2":"legacy"));return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
  [STAThread] public static int Main(string[] args){LauncherRuntime.Initialize();if(args.Length==1&&args[0]=="--verify-setup")return Verify();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);try{var service=Service();var destination=InstallerDestinationPolicy.Recommend(InstallerPlatform.ReadDriveFacts(),Environment.GetFolderPath(Environment.SpecialFolder.Windows),Application.ExecutablePath,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs"),InstallerPlatform.ProbeRecommendation);using(var form=new InstallerForm(destination,service.Install,result=>{using(var process=Process.Start(service.LauncherStartInfo(result))){} },result=>IsCore?InstallerShortcut.Create(result):new InstallerShortcutResult(false,"","兼容版安装器不支持创建已认证的桌面快捷方式，请手动创建。"))){Application.Run(form);return form.ExitCode;}}catch(Exception ex){MessageBox.Show("安装器无法初始化："+ex.Message+"\n未开始安装，请保留原目录供排查。","安装未完成",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
 }
 static class UninstallerProgram {
  internal static string UninstallCore(string root,string self){return UninstallCore(root,self,CoreUpdateTrust.PublicKeyXml,InstallerShortcut.RemoveOwned,()=>ScheduleCompletion(root,self));}
  internal static string UninstallCore(string root,string self,string key,Func<string,string> removeShortcut,Action schedule){CoreDeployment.AdoptPortable(root,key);string result=CoreDeployment.Uninstall(root,self);string shortcut=removeShortcut(root);schedule();return result+(string.IsNullOrEmpty(shortcut)?"":"\r\n"+shortcut);}
  static string Worker(string root,string id){return CoreDeployment.Safe(CoreDeployment.Root(root),"OrangeLauncherRuntime/CoreUninstall/"+id+".exe");}
  internal static void ScheduleCompletion(string root,string self) {
   var record=CoreDeployment.ReadAuthenticated(root);var entry=record.files.Single(f=>f.path=="卸载橙子启动器.exe");CoreDeployment.RequireFile(root,entry.path,entry);
   if(!CoreDeployment.SamePath(self,CoreReleaseProtocol.Target(root,entry.path)))throw new IOException("Uninstaller path mismatch.");
   string worker=Worker(root,record.installationId);Directory.CreateDirectory(Path.GetDirectoryName(worker));if(!File.Exists(worker))File.Copy(self,worker);if(CoreReleaseProtocol.HashFile(worker)!=entry.sha256)throw new IOException("Uninstall worker conflict.");
   using(var process=Process.GetCurrentProcess())Process.Start(new ProcessStartInfo(worker,"--complete-core-uninstall \""+root+"\" "+record.installationId+" "+entry.sha256+" "+process.Id+" "+process.StartTime.ToUniversalTime().Ticks){UseShellExecute=false,CreateNoWindow=true}).Dispose();
  }
  static int Complete(string[] args) {
   string root=CoreDeployment.Root(args[1]),id=args[2],hash=args[3];if(!CoreDeployment.Id(id))throw new IOException("Invalid uninstall identity.");
   string self=Process.GetCurrentProcess().MainModule.FileName;if(!CoreDeployment.SamePath(self,Worker(root,id))||CoreReleaseProtocol.HashFile(self)!=hash)throw new IOException("Unexpected uninstall worker.");
   var record=CoreDeployment.ReadAuthenticated(root);var entry=record.files.Single(f=>f.path=="卸载橙子启动器.exe");if(record.installationId!=id||entry.sha256!=hash)throw new IOException("Uninstall ownership changed.");
   int pid=int.Parse(args[4]);long ticks=long.Parse(args[5]);Process parent=null;try{parent=Process.GetProcessById(pid);}catch(ArgumentException){}
   if(parent!=null)using(parent){if(parent.StartTime.ToUniversalTime().Ticks!=ticks||!CoreDeployment.SamePath(parent.MainModule.FileName,CoreReleaseProtocol.Target(root,entry.path)))throw new IOException("Uninstaller parent identity changed.");parent.WaitForExit();}
   CoreDeployment.CompleteUninstall(root,id,hash);return 0;
  }
  [STAThread] public static int Main(string[] args){try{if(args.Length==6&&args[0]=="--complete-core-uninstall")return Complete(args);Application.EnableVisualStyles();string self=Process.GetCurrentProcess().MainModule.FileName,root=Path.GetDirectoryName(self);if(MessageBox.Show("卸载当前目录的橙子启动器？\n保留个人文件与更新诊断，不删除游戏、存档或工坊模组。\n请先退出橙子启动器。","卸载橙子启动器",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return 0;bool core=typeof(UninstallerProgram).Assembly.GetManifestResourceInfo("Launcher.UpdatePublicKey.xml")!=null;string result=core?UninstallCore(root,self):PortableDeployment.Uninstall(root,self);if(!core&&!File.Exists(Path.Combine(root,"orange-installation.json")))PortableDeployment.DeleteSelfAfterExit(root,self);MessageBox.Show(result,"卸载结果");return 0;}catch(Exception ex){if(args.Length>0){Console.Error.WriteLine(ex.Message);return 1;}MessageBox.Show(ex.Message,"未完成卸载",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
 }
}
