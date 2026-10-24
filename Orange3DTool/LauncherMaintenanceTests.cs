using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
namespace PZ3D {
 static class LauncherMaintenanceTests {
  static Type Service {get{return typeof(Engine).Assembly.GetType("PZ3D.LauncherMaintenance");}}
  static void Check(bool v,string m){if(!v)throw new Exception("FAIL: "+m);Console.WriteLine("PASS: "+m);}
  static object Call(string name,params object[] args){if(name=="SetHeapGiB")return LauncherMaintenance.SetHeapGiB((Engine)args[0],(int)args[1],()=>LauncherMaintenance.CalculateMemoryInfo(64,1));try{return Service.GetMethod(name).Invoke(null,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static bool Denied(Action action){try{action();return false;}catch{return true;}}
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  public static void Run(){
   Check(Service!=null,"maintenance service exists");
   string root=Path.Combine(LauncherTestPaths.Root,"orange-maintenance-"+Guid.NewGuid().ToString("N"));
   var e=new Engine(Path.Combine(root,"steamapps","common","ProjectZomboid"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);
   string original="{\"vmArgs\":[\"-Xmx1024m\",\"-Xmx2g\",\"-Xms8g\",\"-agentlib:zbNative\",\"-Dcustom=keep\"],\"windows\":{\"10\":{\"vmArgs\":[\"-Xmx3072m\",\"-XX:+UseZGC\"]}},\"extra\":123}";
   File.WriteAllText(e.Config,original);
   Check((int)Call("ReadHeapGiB",e)==2,"read uses effective final Xmx");
   Call("SetHeapGiB",e,1);string result=File.ReadAllText(e.Config);
   Check((int)Call("ReadHeapGiB",e)==1&&result.Contains("-Xmx1g")&&!result.Contains("-Xmx2g")&&!result.Contains("-Xmx3072m"),"normalize all launch branches and duplicates");
   Check(result.Contains("-Xms1g")&&result.Contains("-agentlib:zbNative")&&result.Contains("-Dcustom=keep")&&result.Contains("-XX:+UseZGC")&&result.Contains("123"),"clamp initial heap and preserve nonheap arguments");
   Check(Directory.EnumerateFiles(e.Data,"ProjectZomboid64.json",SearchOption.AllDirectories).Any(f=>File.ReadAllText(f)==original),"atomic replace retains exact original backup");
   Check(Denied(()=>Call("SetHeapGiB",e,int.MaxValue))&&File.ReadAllText(e.Config)==result,"insufficient RAM refuses before write");
   string external="{\"vmArgs\":[\"-Xmx2g\",\"-Dexternal=keep\"]}";e.Progress=(v,t)=>File.WriteAllText(e.Config,external);Check(Denied(()=>Call("SetHeapGiB",e,1))&&File.ReadAllText(e.Config)==external,"concurrent config update refuses and preserves external bytes");e.Progress=null;
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmxgarbage\"]}");string malformed=File.ReadAllText(e.Config);Check(Denied(()=>Call("SetHeapGiB",e,1))&&File.ReadAllText(e.Config)==malformed,"malformed heap config refuses mutation");
   File.WriteAllText(e.Config,original);
   LauncherMultiplayerCacheTests.Run();
   LauncherMultiplayerDataTests.Run();
   Check(Denied(()=>Call("RebuildWorkshop",e,null,new CancellationToken(true))),"cancelled workshop rebuild does not spawn child");
   Console.WriteLine("MAINTENANCE TESTS PASSED; fixture retained: "+root);
  }
 }
}
