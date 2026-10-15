using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
namespace PZ3D {
 static class InstallerExperienceTests {
  static string boundary,captures;static int passed;
  static void Check(bool yes,string name){if(!yes)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
  static void Reject(Action action,string name){bool rejected=false;try{action();}catch(Exception e){if(e.Message.StartsWith("FAIL:"))throw;rejected=true;}Check(rejected,name);}
  static string NewRoot(string label){return Path.Combine(boundary,label+"-"+Guid.NewGuid().ToString("N"));}
  static InstallerDriveFact Drive(string root,long free,DriveType type=DriveType.Fixed,bool ready=true){return new InstallerDriveFact{Root=root,AvailableFreeSpace=free,Type=type,Ready=ready};}
  static InstallerPathProbe Missing(string path){return new InstallerPathProbe();}
  static void Paths(){
   var drives=new[]{Drive(@"C:\",900),Drive(@"D:\",800),Drive(@"E:\",100),Drive(@"F:\",999,DriveType.Removable),Drive(@"G:\",999,DriveType.Fixed,false)};
   var recommendation=InstallerDestinationPolicy.Recommend(drives,@"C:\Windows",@"E:\Downloads\setup.exe",@"C:\Users\fixture\AppData\Local\Programs",Missing);
   Check(recommendation.Root==@"E:\OrangeLauncher","ready non-system fixed setup drive beats larger drive");
   Check(InstallerDestinationPolicy.Recommend(drives,@"C:\Windows",@"C:\setup.exe",@"C:\fixture\Programs",Missing).Root==@"D:\OrangeLauncher","system/removable/unready drives cannot win free-space ranking");
   Check(InstallerDestinationPolicy.Recommend(new[]{Drive(@"E:\",100),Drive(@"D:\",100)},@"C:\",@"Z:\setup.exe",@"C:\fixture\Programs",Missing).Root==@"D:\OrangeLauncher","equal free space has stable drive-root ordering");
   var fallback=InstallerDestinationPolicy.Recommend(new[]{Drive(@"C:\",900)},@"C:\Windows",@"C:\setup.exe",@"C:\fixture\Programs",Missing);
   Check(fallback.Root==@"C:\fixture\Programs\OrangeLauncher"&&fallback.UsesUserProgramsFallback&&!string.IsNullOrEmpty(fallback.Reason),"only-system layout explains user Programs fallback");
   Check(InstallerDestinationPolicy.FromParent(@"D:\",p=>new InstallerPathProbe{Exists=p==@"D:\OrangeLauncher"||p==@"D:\OrangeLauncher-2"}).Root==@"D:\OrangeLauncher-3","browse parent appends child and skips occupied suffixes");
   int probes=0;var exhausted=InstallerDestinationPolicy.FromParent(@"D:\",p=>{probes++;return new InstallerPathProbe{Exists=true};});
   Check(probes==100&&!string.IsNullOrEmpty(exhausted.Error),"occupied recommendation search is bounded with visible error");
   var unsafePath=InstallerDestinationPolicy.Recommend(drives,@"C:\",@"E:\setup.exe",@"C:\fixture\Programs",p=>new InstallerPathProbe{Error="fixture unsafe path"});
   Check(unsafePath.Root==@"E:\OrangeLauncher"&&unsafePath.Error.Contains("unsafe"),"unsafe selected path stays visible without silent drive fallback");
   string parent=NewRoot("readonly-parent");Directory.CreateDirectory(parent);string existing=Path.Combine(parent,"OrangeLauncher");Directory.CreateDirectory(existing);File.WriteAllText(Path.Combine(existing,"personal.txt"),"preserve");
   var before=Directory.GetFileSystemEntries(parent);var fresh=InstallerDestinationPolicy.FromParent(parent,InstallerPlatform.ProbeRecommendation);
   Check(fresh.Root==Path.Combine(parent,"OrangeLauncher-2")&&!Directory.Exists(fresh.Root)&&before.SequenceEqual(Directory.GetFileSystemEntries(parent))&&File.ReadAllText(Path.Combine(existing,"personal.txt"))=="preserve","recommendation is read-only and preserves occupied directory");
   string empty=Path.Combine(parent,"custom-empty");Directory.CreateDirectory(empty);Check(InstallerDestinationPolicy.ValidateInstallRoot(empty)==empty,"explicit custom empty root is accepted exactly");
   Reject(()=>InstallerDestinationPolicy.ValidateInstallRoot(existing),"nonempty custom root refused without overwrite");
   Reject(()=>InstallerDestinationPolicy.ValidateInstallRoot(@"C:\"),"drive root refused");
   Reject(()=>InstallerDestinationPolicy.ValidateInstallRoot("relative-install"),"relative custom root refused");
   string game=Path.Combine(parent,"game");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");Reject(()=>InstallerDestinationPolicy.ValidateInstallRoot(game),"game directory refused");
   string fileRoot=Path.Combine(parent,"file");File.WriteAllText(fileRoot,"keep");Reject(()=>InstallerDestinationPolicy.ValidateInstallRoot(fileRoot),"file occupying custom root refused");
   string junction=Path.Combine(parent,"junction");var start=new ProcessStartInfo("cmd.exe","/c mklink /J \""+junction+"\" \""+empty+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
   using(var p=Process.Start(start)){p.StandardOutput.ReadToEnd();string error=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception("Fixture junction could not be created: "+error);}
   Reject(()=>InstallerDestinationPolicy.ValidateInstallRoot(Path.Combine(junction,"child")),"reparse ancestor refused before creating custom root");
   Check(!Directory.Exists(Path.Combine(empty,"child")),"reparse rejection leaves target unchanged");
  }
  sealed class Package {
   internal Dictionary<string,byte[]> Files,Resources;
   internal InstallerInstallService Service;
  }
  static Package Signed(Action<Dictionary<string,byte[]>> change=null,string version=null){
   var files=CoreReleaseProtocol.OwnedPaths.ToDictionary(p=>p,p=>CoreFixture.Bytes(p=="VERSION"?AppVersion.Current+"\n":"fixture-only "+p));
   var manifest=CoreFixture.Manifest(version??AppVersion.Current,files);var resources=new Dictionary<string,byte[]>{{"Setup.Manifest.json",manifest.VerifiedEnvelope.Raw},{"Setup.Manifest.sig",manifest.VerifiedEnvelope.Signature}};
   var entries=new Dictionary<string,byte[]>(files){{CoreDeployment.BootstrapManifest,manifest.VerifiedEnvelope.Raw},{CoreDeployment.BootstrapSignature,manifest.VerifiedEnvelope.Signature}};
   if(change!=null)change(entries);
   using(var output=new MemoryStream()){using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)){foreach(var entry in entries)using(var stream=zip.CreateEntry(entry.Key).Open())stream.Write(entry.Value,0,entry.Value.Length);}resources["Setup.Core.zip"]=output.ToArray();}
   return new Package{Files=files,Resources=resources,Service=new InstallerInstallService(n=>resources[n],CoreFixture.Key,AppVersion.Current)};
  }
  static void Service(){
   var package=Signed();string root=NewRoot("signed-service");var progress=new List<InstallerProgress>();
   var result=package.Service.Install(root,p=>{if(p.Phase==InstallerPhase.Complete){CoreDeployment.ReadAuthenticated(root,CoreFixture.Key);foreach(var f in package.Files)Check(File.ReadAllBytes(CoreFixture.FileAt(root,f.Key)).SequenceEqual(f.Value),"Complete only follows exact owned bytes: "+f.Key);}progress.Add(p);});
   Check(result.Root==root&&result.LauncherPath==Path.Combine(root,"橙子启动器.exe")&&result.UninstallerPath==Path.Combine(root,"卸载橙子启动器.exe"),"service returns exact selected root and owned executable paths");
   Check(progress.First().Phase==InstallerPhase.ValidatePackage&&progress.Last().Phase==InstallerPhase.Complete&&progress.Count(p=>p.Phase==InstallerPhase.Complete)==1,"service has ordered initial and single final phase");
   var archive=progress.Where(p=>p.Phase==InstallerPhase.ReadArchive).ToArray();Check(archive.Length>1&&archive.Last().Current==package.Files.Sum(f=>f.Value.LongLength)&&archive.All(p=>p.Total==package.Files.Sum(f=>f.Value.LongLength))&&archive.Zip(archive.Skip(1),(a,b)=>b.Current>=a.Current).All(x=>x),"archive reports actual monotonic uncompressed payload bytes");
   Check(progress.Single(p=>p.Phase==InstallerPhase.Deploy).Total==0,"transaction deployment honestly remains indeterminate");
   var verification=progress.Where(p=>p.Phase==InstallerPhase.VerifyInstalledFiles).ToArray();Check(verification.First().Current==0&&verification.Last().Current==18&&verification.All(p=>p.Total==18),"final verification counts authenticated owned files");
   var info=package.Service.LauncherStartInfo(result);Check(info.FileName==Path.Combine(root,"橙子启动器.exe")&&info.WorkingDirectory==root&&!info.UseShellExecute&&info.Arguments=="","explicit-open preparation targets exact owned launcher without shell or arguments");
   File.WriteAllText(info.FileName,"tampered fixture");Reject(()=>package.Service.LauncherStartInfo(result),"explicit open rechecks installed launcher bytes");
   foreach(string damage in new[]{"file","record"}){var p=Signed();string target=NewRoot("final-"+damage);bool complete=false;Reject(()=>p.Service.Install(target,s=>{if(s.Phase==InstallerPhase.VerifyInstalledFiles&&s.Current==0){if(damage=="file")File.WriteAllText(Path.Combine(target,"橙子启动器.exe"),"tamper");else File.WriteAllText(Path.Combine(target,"orange-installation.json"),"{}");}if(s.Phase==InstallerPhase.Complete)complete=true;}),"final "+damage+" corruption rejects completion");Check(!complete&&Directory.Exists(Path.Combine(target,"OrangeLauncherRuntime")),"failed final "+damage+" check preserves evidence without Complete");}
   foreach(string bad in new[]{"signature","hash","missing","extra","version"}){var p=Signed(e=>{if(bad=="hash")e["Web/app.js"]=CoreFixture.Bytes("wrong");if(bad=="missing")e.Remove("Web/app.js");if(bad=="extra")e.Add("../escape",new byte[]{1});},bad=="version"?"1.1.0":null);if(bad=="signature")p.Resources["Setup.Manifest.sig"]=new byte[256];string target=NewRoot("reject-"+bad);bool complete=false;Reject(()=>p.Service.Install(target,s=>{if(s.Phase==InstallerPhase.Complete)complete=true;}),"invalid "+bad+" package rejected");Check(!complete&&!Directory.Exists(target),"invalid "+bad+" package creates no destination");}
   string occupied=NewRoot("occupied");Directory.CreateDirectory(occupied);File.WriteAllText(Path.Combine(occupied,"personal.txt"),"keep");Reject(()=>Signed().Service.Install(occupied,null),"real service refuses occupied selected directory");Check(File.ReadAllText(Path.Combine(occupied,"personal.txt"))=="keep"&&Directory.GetFileSystemEntries(occupied).Length==1,"occupied install preserves all existing data");
   var legacy=new Dictionary<string,byte[]>{{"Setup.Launcher.exe",new byte[]{1,2,3}},{"Setup.Uninstall.exe",new byte[]{4,5,6}},{"Setup.Guide.md",CoreFixture.Bytes("legacy guide")}};var service=InstallerInstallService.Legacy(n=>legacy[n],AppVersion.Current);string legacyRoot=NewRoot("legacy");var installed=service.Install(legacyRoot,null);Check(service.LauncherStartInfo(installed).FileName==Path.Combine(legacyRoot,"橙子启动器.exe"),"legacy resource mode keeps verified explicit-open adapter");File.WriteAllText(Path.Combine(legacyRoot,"orange-installation.json"),"{}");Reject(()=>service.LauncherStartInfo(installed),"legacy explicit open refuses invalid ownership record");
  }
  static T Control<T>(Form form,string name)where T:System.Windows.Forms.Control{return (T)form.Controls.Find(name,true).Single();}
  static void Pump(Func<bool> condition){var until=DateTime.UtcNow.AddSeconds(8);while(!condition()){if(DateTime.UtcNow>until)throw new Exception("Fixture UI deadline expired");Application.DoEvents();Thread.Sleep(10);}Application.DoEvents();}
  static InstallerInstallResult Result(string root){return new InstallerInstallResult(root,AppVersion.Current,"fixture-id");}
  static double GoldCoverage(ProgressBar bar){using(var bitmap=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(bitmap,bar.ClientRectangle);int gold=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){var color=bitmap.GetPixel(x,y);if(color.R>180&&color.G>100&&color.B<140)gold++;}return (double)gold/(bar.Width*bar.Height);}}
  static void Capture(InstallerForm form,string name){Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(captures,name+".png"),ImageFormat.Png);}using(var g=form.CreateGraphics())File.AppendAllText(Path.Combine(captures,"dimensions.txt"),name+": client="+form.ClientSize+", dpi="+g.DpiX+"x"+g.DpiY+", font="+form.Font.SizeInPoints+"pt\r\n");}
  static void Ui(){
   string parent=NewRoot("ui-parent");Directory.CreateDirectory(parent);string proposed=Path.Combine(parent,"OrangeLauncher"),custom=Path.Combine(parent,"自定义工具目录");int calls=0,opens=0;string seen=null;
   using(var f=new InstallerForm(new InstallerDestination{Root="relative-root",Reason="fixture invalid path"},(root,p)=>{calls++;return Result(root);},r=>opens++)){
    f.Show();Control<Button>(f,"installer-install").PerformClick();Application.DoEvents();Check(calls==0&&Control<Button>(f,"installer-install").Visible&&Control<Button>(f,"installer-browse").Enabled,"read-only path validation failure permits changing location without starting worker");Control<TextBox>(f,"installer-path").Text=custom;Control<Button>(f,"installer-install").PerformClick();Pump(()=>Control<Button>(f,"installer-open").Visible);Check(calls==1,"corrected custom path can install after preflight rejection");f.Close();calls=0;
   }
   using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var f=new InstallerForm(new InstallerDestination{Root=proposed,Reason="fixture measured progress"},(root,p)=>{p(new InstallerProgress(InstallerPhase.ReadArchive,"fixture measured bytes",50,100,InstallerProgressUnit.Bytes));entered.Set();if(!release.Wait(5000))throw new Exception("measured fixture deadline");return Result(root);},r=>opens++)){
    try{f.Show();Control<Button>(f,"installer-install").PerformClick();Pump(()=>entered.IsSet&&Control<ProgressBar>(f,"installer-progress").Value==500);var bar=Control<ProgressBar>(f,"installer-progress");double coverage=GoldCoverage(bar);Check(bar.Style==ProgressBarStyle.Continuous&&coverage>.2&&coverage<.7&&!Control<Button>(f,"installer-open").Visible,"measured partial progress shows real proportional gold fill without completion");Capture(f,"measured-normal");release.Set();Pump(()=>Control<Button>(f,"installer-open").Visible);f.Close();}finally{release.Set();}
   }
   using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())using(var f=new InstallerForm(new InstallerDestination{Root=proposed,Reason="隔离 fixture 推荐；不会安装到真实默认目录。"},(root,p)=>{calls++;seen=root;p(new InstallerProgress(InstallerPhase.Deploy,"正在事务部署"));entered.Set();if(!release.Wait(5000))throw new Exception("held fixture deadline");return Result(root);},r=>{Check(r.Root==custom&&r.LauncherPath==Path.Combine(custom,"橙子启动器.exe"),"explicit UI open receives exact validated custom root");opens++;})){
    try{f.Show();Application.DoEvents();Capture(f,"ready-normal");Check(calls==0&&!Directory.Exists(proposed)&&Directory.GetFileSystemEntries(parent).Length==0,"initial native form and recommendation create no folders and start no install");
     Control<TextBox>(f,"installer-path").Text=custom;Application.DoEvents();Check(!Directory.Exists(custom)&&Directory.GetFileSystemEntries(parent).Length==0,"editing complete custom path remains read-only");
     Control<Button>(f,"installer-install").PerformClick();Pump(()=>entered.IsSet);Check(seen==custom&&calls==1,"install receives exact editable custom root once");
     Check(!Control<TextBox>(f,"installer-path").Enabled&&!Control<Button>(f,"installer-browse").Enabled&&!Control<Button>(f,"installer-install").Enabled,"busy disables destination and duplicate install");
     int ticks=0;using(var timer=new System.Windows.Forms.Timer{Interval=20}){timer.Tick+=(s,e)=>ticks++;timer.Start();Pump(()=>ticks>=3);}Check(ticks>=3,"native message loop remains responsive during held worker");
     Check(Control<ProgressBar>(f,"installer-progress").Style==ProgressBarStyle.Marquee,"transaction phase shows indeterminate native progress");Capture(f,"busy-normal");f.Close();Application.DoEvents();Check(f.Visible&&!f.IsDisposed,"closing is protected while installation runs");
     release.Set();Pump(()=>Control<Button>(f,"installer-open").Visible);Check(opens==0&&Control<Label>(f,"installer-status").Text.Contains("完成"),"validated result completes UI without automatic open");Check(GoldCoverage(Control<ProgressBar>(f,"installer-progress"))>.5,"completed native progress visibly fills in gold");Capture(f,"completed-normal");Control<Button>(f,"installer-open").PerformClick();Check(opens==1,"only explicit completed Open click invokes launch boundary");f.Close();
    }catch(Exception error){Console.WriteLine("UI fixture original failure: "+error+"\r\nStatus: "+Control<Label>(f,"installer-status").Text+"\r\nDetails: "+Control<TextBox>(f,"installer-details").Text);throw;}finally{release.Set();}
   }
   using(var f=new InstallerForm(new InstallerDestination{Root=proposed,Reason="隔离 fixture"},(root,p)=>{p(new InstallerProgress(InstallerPhase.Complete,"premature completion",1,1));throw new InstallerInstallException(InstallerPhase.VerifyInstalledFiles,root,Path.Combine(root,"OrangeLauncherRuntime"),new IOException("fixture verification failure: preserve evidence"));},r=>opens++)){
    f.Show();Control<Button>(f,"installer-install").PerformClick();Pump(()=>f.ExitCode==1);Check(!Control<Button>(f,"installer-open").Visible&&Control<TextBox>(f,"installer-details").Text.Contains("fixture verification failure")&&Control<TextBox>(f,"installer-details").Text.Contains("OrangeLauncherRuntime"),"failure retains copyable reason/evidence and rejects premature Complete");Capture(f,"failure-normal");f.Close();
   }
   using(var f=new InstallerForm(new InstallerDestination{Root=proposed,Reason="fixture fallback",UsesUserProgramsFallback=true},(root,p)=>Result(root),r=>opens++)){
    f.ClientSize=new Size(520,420);f.Show();Application.DoEvents();Capture(f,"ready-small");Check(f.Controls.Find("installer-install",true).Single().Width>=100,"small-window installation action remains usable");f.AutoScaleMode=AutoScaleMode.None;f.Scale(new SizeF(1.5f,1.5f));ScaleFonts(f,1.5f);f.ClientSize=new Size(780,630);Application.DoEvents();Capture(f,"ready-scaled-150");Check(!Directory.Exists(proposed),"small and scaled native layout capture is read-only");f.Close();
   }
   Console.WriteLine("CAPTURES: "+captures);
  }
  static void ScaleFonts(System.Windows.Forms.Control control,float scale){foreach(System.Windows.Forms.Control child in control.Controls)ScaleFonts(child,scale);control.Font=new Font(control.Font.FontFamily,control.Font.SizeInPoints*scale,control.Font.Style);}
  [STAThread] public static int Main(string[] args){try{LauncherRuntime.Initialize();System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls=true;Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);boundary=LauncherTestPaths.Root;captures=NewRoot("installer-captures");Directory.CreateDirectory(captures);CoreFixture.Initialize();Paths();Service();Ui();Console.WriteLine("INSTALLER EXPERIENCE: "+passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
