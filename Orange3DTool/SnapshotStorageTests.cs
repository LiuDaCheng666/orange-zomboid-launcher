using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PZ3D {
 static class SnapshotStorageTests {
  static readonly BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  static Type Api;static int assertions;
  static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static string Call(string name,params object[] args){try{return (string)Api.GetMethod(name,Flags).Invoke(null,args);}catch(TargetInvocationException error){throw error.InnerException;}}
  static string Failure(Action action){try{action();return "";}catch(Exception error){return error.Message;}}
  static string Fixture(){string root=Path.Combine(Path.GetTempPath(),"ozl-storage-"+Guid.NewGuid().ToString("N"));System.IO.Directory.CreateDirectory(root);return root;}
  static Engine EngineAt(string root){return new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);}
  static void Run(){
   Api=typeof(Engine).Assembly.GetType("PZ3D.SnapshotStorage");Check(Api!=null,"snapshot storage helper is available without changing the snapshot controller");
   string root=Fixture(),install=Path.Combine(root,"OrangeLauncher");var e=EngineAt(root);
#if OZL_VELOPACK
   string previous=DistributionPaths.InstallationRoot;DistributionPaths.InstallationRoot=install;
   try{
#endif
    string expected=install+"快照";Check(Call("Directory",e)==expected,"default is a stable sibling of the launcher installation, outside current and the uninstall root");
    Check(!System.IO.Directory.Exists(e.Data)&&!System.IO.Directory.Exists(expected),"displaying the default neither saves a preference nor creates the snapshot directory");
    string selected=Path.Combine(root,"自选目录","独立快照"),saved=Call("SaveDirectory",e,selected+Path.DirectorySeparatorChar);
    Check(saved==selected&&Call("Directory",e)==selected,"explicit custom choice persists the normalized absolute path");
    string setting=Path.Combine(e.Data,"snapshot-directory.txt");byte[] before=File.ReadAllBytes(setting);
    Check(!System.IO.Directory.Exists(selected),"choosing storage saves only the preference until snapshot creation");
#if OZL_VELOPACK
    DistributionPaths.InstallationRoot=Path.Combine(root,"MovedLauncher");Check(Call("Directory",e)==selected,"a custom path survives a launcher installation-root change");DistributionPaths.InstallationRoot=install;
#endif
    foreach(string invalid in new[]{"relative",@"C:relative",@"\current-drive-dependent",install,Path.Combine(install,"current","snapshots"),Path.Combine(install,"快照")})Check(Failure(()=>Call("SaveDirectory",e,invalid))!=""&&File.ReadAllBytes(setting).SequenceEqual(before),"invalid or uninstall-owned choice preserves the previous preference: "+invalid);
    Check(Failure(()=>Call("SaveDirectory",e,Path.Combine(install,"快照"))).Contains("卸载"),"install-root rejection explains actual uninstall deletion instead of a generic safety failure");
    string file=Path.Combine(root,"occupied-file");File.WriteAllText(file,"keep");Check(Failure(()=>Call("SaveDirectory",e,file))!=""&&File.ReadAllText(file)=="keep","a file cannot become a snapshot directory and its bytes are retained");
    using(var held=new FileStream(setting,FileMode.Open,FileAccess.Read,FileShare.Read)){Check(Failure(()=>Call("SaveDirectory",e,Path.Combine(root,"other-choice")))!="","a locked preference reports the actual failed save");}
    Check(File.ReadAllBytes(setting).SequenceEqual(before)&&System.IO.Directory.GetFiles(e.Data).Length==1,"failed preference replacement retains exact prior bytes and clears its temporary file");
    File.WriteAllText(setting,"relative-corrupt-setting",new UTF8Encoding(false));byte[] corrupt=File.ReadAllBytes(setting);Check(Failure(()=>Call("Directory",e))!=""&&File.ReadAllBytes(setting).SequenceEqual(corrupt),"an invalid saved preference reports an error without silently resetting or writing it");Call("SaveDirectory",e,selected);
    string outside=Path.Combine(root,"outside"),link=Path.Combine(root,"linked");System.IO.Directory.CreateDirectory(outside);File.WriteAllText(Path.Combine(outside,"keep"),"unchanged");
    using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){process.WaitForExit();Check(process.ExitCode==0,"owned storage-junction fixture created");}
    Check(Failure(()=>Call("SaveDirectory",e,Path.Combine(link,"snapshots")))!=""&&Call("Directory",e)==selected&&File.ReadAllText(Path.Combine(outside,"keep"))=="unchanged","a reparse choice is refused without changing the preference or external fixture bytes");
    Console.WriteLine("SNAPSHOT STORAGE TESTS PASSED: "+assertions+" assertions; fixture "+root);
#if OZL_VELOPACK
   }finally{DistributionPaths.InstallationRoot=previous;}
#endif
  }
  public static int Main(){try{LauncherRuntime.Initialize();Run();return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
