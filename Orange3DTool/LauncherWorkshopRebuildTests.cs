using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
namespace PZ3D {
 static class LauncherWorkshopRebuildTests {
  static void Check(bool v,string msg){if(!v)throw new Exception("FAIL: "+msg);Console.WriteLine("PASS: "+msg);}
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  static Type Type {get{return typeof(Engine).Assembly.GetType("PZ3D.LauncherWorkshopRebuild");}}
  static object Call(string name,params object[] args){try{return Type.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static bool Refused(Action a){try{a();return false;}catch{return true;}}
  public static void Run(){Check(Type!=null,"isolated workshop rebuild worker exists");string root=Path.Combine(LauncherTestPaths.Root,"orange-rebuild-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"steamapps","common","ProjectZomboid"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx1g\"]}");File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid64.exe"),"fixture executable marker");Directory.CreateDirectory(e.Workshop);
   foreach(string id in new[]{"100","200"}){Directory.CreateDirectory(Path.Combine(e.Workshop,id));File.WriteAllText(Path.Combine(e.Workshop,id,"mod.info"),"fixture "+id);}
   string manifest=Path.GetFullPath(Path.Combine(e.Workshop,"..","..","appworkshop_108600.acf"));File.WriteAllText(manifest,"preserve manifest");string other=Path.GetFullPath(Path.Combine(e.Workshop,"..","999","100"));Directory.CreateDirectory(other);File.WriteAllText(Path.Combine(other,"mod.info"),"other game");Directory.CreateDirectory(Path.Combine(e.Workshop,"unknown"));File.WriteAllText(Path.Combine(e.Workshop,"unknown","keep"),"unknown");
   Check(Refused(()=>Call("Apply",e,null,null,null,CancellationToken.None))&&File.Exists(Path.Combine(e.Workshop,"100","mod.info")),"missing confirmed subscription snapshot refuses before deletion");
   Action<ulong[],Action<int,string>,CancellationToken> fail=(ids,p,c)=>{throw new Exception("download failed fixture");};
   Check(Refused(()=>Call("Apply",e,new ulong[]{100},fail,null,CancellationToken.None))&&File.Exists(Path.Combine(e.Workshop,"100","mod.info")),"unknown workshop folder refuses entire rebuild before deletion");
   Directory.Move(Path.Combine(e.Workshop,"unknown"),Path.Combine(root,"unknown"));
   File.Delete(Path.Combine(e.Game,"ProjectZomboid64.exe"));Check(Refused(()=>Call("Apply",e,new ulong[]{100},fail,null,CancellationToken.None))&&File.Exists(Path.Combine(e.Workshop,"100","mod.info")),"missing game executable refuses workshop deletion");File.WriteAllText(Path.Combine(e.Game,"ProjectZomboid64.exe"),"fixture executable marker");
   Check(Refused(()=>Call("Apply",e,new ulong[0],fail,null,CancellationToken.None))&&File.Exists(Path.Combine(e.Workshop,"100","mod.info")),"ambiguous empty subscription snapshot refuses cached file deletion");
   bool preflightRan=false;Action invalidPreflight=()=>{preflightRan=true;throw new Exception("fixture disk/SDK preflight failure");};Check(Refused(()=>Call("PreflightAndApply",e,new ulong[]{100},invalidPreflight,fail,null,CancellationToken.None))&&preflightRan&&File.Exists(Path.Combine(e.Workshop,"100","mod.info"))&&!File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending")),"SDK or capacity preflight failure precedes marker and deletion");
   Check(Refused(()=>Call("Apply",e,new ulong[]{100},fail,null,CancellationToken.None)),"download failure is reported");
   Check(File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending"))&&File.ReadAllText(manifest)=="preserve manifest"&&File.ReadAllText(Path.Combine(other,"mod.info"))=="other game","failure marker retained and manifest / other games preserved");
   Check(!Directory.Exists(Path.Combine(e.Workshop,"200")),"unsubscribed numeric cache folder removed only within App108600");
   bool requested=false;Action<ulong[],Action<int,string>,CancellationToken> fetch=(ids,p,c)=>{requested=ids.SequenceEqual(new ulong[]{100,300});foreach(ulong id in ids){Directory.CreateDirectory(Path.Combine(e.Workshop,id.ToString()));File.WriteAllText(Path.Combine(e.Workshop,id.ToString(),"mod.info"),"download fixture");}};
   Call("Apply",e,new ulong[]{100,300},fetch,null,CancellationToken.None);Check(requested&&!File.Exists(Path.Combine(e.Data,"workshop-rebuild.pending"))&&File.Exists(Path.Combine(e.Workshop,"300","mod.info")),"redownload all confirmed subscriptions and remove marker only after success");
   Check(Refused(()=>Call("Apply",e,new ulong[]{100},fetch,null,new CancellationToken(true)))&&File.Exists(Path.Combine(e.Workshop,"100","mod.info")),"cancel before plan leaves cached files intact");
   string linked=Path.Combine(e.Workshop,"400");var child=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+linked+"\" \""+other+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true});child.WaitForExit();Check(child.ExitCode==0,"workshop fixture junction created");Check(Refused(()=>Call("Apply",e,new ulong[]{100},fetch,null,CancellationToken.None))&&File.ReadAllText(Path.Combine(other,"mod.info"))=="other game","workshop junction rejects before deletion");
   Console.WriteLine("WORKSHOP REBUILD TESTS PASSED; fixture retained: "+root);
  }
 }
}
