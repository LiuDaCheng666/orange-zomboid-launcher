using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
namespace PZ3D {
 static class InstallerProgram {
  static int Verify(){try{string root=Path.Combine(LauncherTestPaths.Root,"orange-setup-verify-"+Guid.NewGuid().ToString("N"));byte[] launcher=PortableDeployment.Resource("Setup.Launcher.exe"),uninstaller=PortableDeployment.Resource("Setup.Uninstall.exe");PortableDeployment.Install(root,new Dictionary<string,byte[]>{{"橙子启动器.exe",launcher},{"卸载橙子启动器.exe",uninstaller}});if(DlssInstaller.HashFile(Path.Combine(root,"橙子启动器.exe"))!=DlssInstaller.Hash(launcher))throw new Exception("Installed launcher differs from embedded resource");PortableDeployment.Uninstall(root);if(File.Exists(Path.Combine(root,"橙子启动器.exe"))||File.Exists(Path.Combine(root,"卸载橙子启动器.exe")))throw new Exception("Scoped uninstall incomplete");Console.WriteLine("PASS: packed installer extracts exact launcher and uninstaller; scoped fixture uninstall removes both without game or registry changes");return 0;}catch(Exception ex){Console.WriteLine(ex.Message);return 1;}}
  [STAThread] public static int Main(string[] args){LauncherRuntime.Initialize();if(args.Length==1&&args[0]=="--verify-setup")return Verify();Application.EnableVisualStyles();try{using(var picker=new FolderBrowserDialog{Description="选择新的空文件夹安装橙子启动器（不写注册表）"}){if(picker.ShowDialog()!=DialogResult.OK)return 0;string root=PortableDeployment.ValidateRoot(picker.SelectedPath);if(MessageBox.Show("安装到："+root+"\n仅安装工具与卸载程序，不安装游戏模组，不写注册表。\n首次打开需要系统 WebView2 Runtime。是否继续？","安装橙子启动器",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return 0;PortableDeployment.Install(root,new Dictionary<string,byte[]>{{"橙子启动器.exe",PortableDeployment.Resource("Setup.Launcher.exe")},{"卸载橙子启动器.exe",PortableDeployment.Resource("Setup.Uninstall.exe")},{"使用说明.txt",PortableDeployment.Resource("Setup.Guide.md")}});MessageBox.Show("安装完成。\n"+root+"\n打开其中橙子启动器.exe 即可使用。","安装完成");return 0;}}catch(Exception ex){MessageBox.Show(ex.Message,"安装未完成",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
 }
 static class UninstallerProgram {
  [STAThread] public static int Main(string[] args){Application.EnableVisualStyles();try{string self=Process.GetCurrentProcess().MainModule.FileName,root=Path.GetDirectoryName(self);if(MessageBox.Show("卸载当前目录的橙子启动器及其运行缓存？\n不删除游戏、存档、工坊模组或个人设置；不会自动关闭已安装的3D模组。\n请先退出橙子启动器。","卸载橙子启动器",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return 0;string result=PortableDeployment.Uninstall(root,self);MessageBox.Show(result,"卸载结果");if(!File.Exists(Path.Combine(root,"orange-installation.json")))PortableDeployment.DeleteSelfAfterExit(root,self);return 0;}catch(Exception ex){MessageBox.Show(ex.Message,"未完成卸载",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
 }
}
