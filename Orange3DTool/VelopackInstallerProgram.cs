using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace PZ3D {
 // Only the interactive front end is ours. The embedded, unmodified official
 // Setup performs installation and registers the official updater/uninstaller.
 internal sealed class VelopackInstallerService {
  internal readonly string AppId,Title;
  readonly string expectedHash;
  internal VelopackInstallerService(){var fields=ReadText("Setup.Identity").Trim().Split('\n');if(fields.Length!=3)throw new InvalidDataException("安装包身份不完整。");AppId=fields[0].Trim();Title=fields[1].Trim();expectedHash=fields[2].Trim();}
  static Stream Resource(string name){var stream=typeof(VelopackInstallerService).Assembly.GetManifestResourceStream(name);if(stream==null)throw new InvalidDataException("安装包资源缺失："+name);return stream;}
  static string ReadText(string name){using(var stream=Resource(name))using(var reader=new StreamReader(stream,Encoding.UTF8))return reader.ReadToEnd();}
  internal InstallerInstallResult Install(string root,bool desktop,Action<InstallerProgress> progress){
   root=InstallerDestinationPolicy.ValidateInstallRoot(root);
   string temp=Path.Combine(Path.GetTempPath(),"ozl-setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
   string setup=Path.Combine(temp,"Setup.exe"),log=Path.Combine(temp,"setup.log");bool complete=false;
   try{
    if(progress!=null)progress(new InstallerProgress(InstallerPhase.ValidatePackage,"正在校验官方安装程序"));
    using(var source=Resource("Setup.Official.exe"))using(var output=new FileStream(setup,FileMode.CreateNew,FileAccess.Write,FileShare.None))source.CopyTo(output);
    using(var lease=new FileStream(setup,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=SHA256.Create())if(!string.Equals(BitConverter.ToString(sha.ComputeHash(lease)).Replace("-",""),expectedHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("官方安装程序校验失败。");
     if(progress!=null)progress(new InstallerProgress(InstallerPhase.Deploy,"正在安装到 "+root));
     var start=new ProcessStartInfo(setup,"--silent --installto \""+root+"\" --log \""+log+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=temp};
     using(var process=Process.Start(start)){process.WaitForExit();if(process.ExitCode!=0)throw new IOException("官方安装程序未完成，退出码："+process.ExitCode);}
    }
    var result=new InstallerInstallResult(root,AppVersion.Current,AppId,Path.Combine(root,"current","橙子启动器.exe"),"\""+Path.Combine(root,"Update.exe")+"\" uninstall（也可从 Windows 应用列表卸载）");
    Verify(result);
    if(!desktop)Desktop(result,false);
    Directory.CreateDirectory(result.EvidenceDirectory);if(File.Exists(log))File.Copy(log,Path.Combine(result.EvidenceDirectory,"install.log"),true);
    if(progress!=null)progress(new InstallerProgress(InstallerPhase.Complete,"安装完成，官方安装记录和启动器文件已核验",1,1));
    complete=true;return result;
   }catch(Exception error){throw new IOException(error.Message+(File.Exists(log)?"\r\n安装日志（已保留）："+log:""),error);}
   finally{try{File.Delete(setup);if(complete&&File.Exists(log))File.Delete(log);if(complete||!File.Exists(log))Directory.Delete(temp);}catch{}}
  }
  void Verify(InstallerInstallResult result){
   string current=Path.Combine(result.Root,"current");DistributionBootstrap.Read(current,CoreUpdateTrust.PublicKeyXml);
   var xml=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(Path.Combine(current,"sq.version"),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))xml.Load(reader);
   Func<string,string> value=name=>{var node=xml.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='"+name+"']");return node==null?"":node.InnerText;};
   if(value("id")!=AppId||value("version")!=AppVersion.Current||value("mainExe")!="橙子启动器.exe"||value("title")!=Title||!File.Exists(Path.Combine(result.Root,"Update.exe")))throw new InvalidDataException("安装结果的身份、版本或官方卸载器不匹配。");
  }
  internal InstallerShortcutResult Desktop(InstallerInstallResult result,bool retain){
   InstallerShortcutResult outcome=null;Exception failure=null;
   var thread=new Thread(()=>{try{
    string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Title+".lnk");
    if(!File.Exists(path)){outcome=new InstallerShortcutResult(false,path,retain?"桌面快捷方式未创建，可从安装目录打开启动器。":"已按选择跳过桌面快捷方式。");return;}
    var link=InstallerShortcut.ReadLink(path);if(!CoreDeployment.SamePath(link.Target,result.LauncherPath)&&!CoreDeployment.SamePath(link.Target,Path.Combine(result.Root,AppId+".exe")))throw new IOException("桌面同名快捷方式属于其他目录，已保留。");
    if(!retain)File.Delete(path);
    outcome=new InstallerShortcutResult(retain,path,retain?"桌面快捷方式已创建："+path:"已按选择跳过桌面快捷方式。");
   }catch(Exception error){failure=error;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(failure!=null)throw failure;return outcome;
  }
  internal void Open(InstallerInstallResult result){Verify(result);using(var process=Process.Start(new ProcessStartInfo(result.LauncherPath){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(result.LauncherPath)})){} }
 }
 internal static class VelopackInstallerProgram {
  [STAThread]public static int Main(){
   LauncherRuntime.Initialize();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{
    var service=new VelopackInstallerService();
    var destination=InstallerDestinationPolicy.Recommend(InstallerPlatform.ReadDriveFacts(),Environment.GetFolderPath(Environment.SpecialFolder.Windows),Application.ExecutablePath,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs"),InstallerPlatform.ProbeRecommendation);
    using(var form=new InstallerForm(destination,service.Install,service.Open,result=>service.Desktop(result,true))){form.Text="安装橙子启动器 · "+AppVersion.Current;Application.Run(form);return form.ExitCode;}
   }catch(Exception error){MessageBox.Show("安装未完成："+error.Message,"橙子启动器安装",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
 }
}
