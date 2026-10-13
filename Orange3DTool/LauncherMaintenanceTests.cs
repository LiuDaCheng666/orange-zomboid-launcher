using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
namespace PZ3D {
 static class LauncherMaintenanceTests {
  static Type Service {get{return typeof(Engine).Assembly.GetType("PZ3D.LauncherMaintenance");}}
  static void Check(bool v,string m){if(!v)throw new Exception("FAIL: "+m);Console.WriteLine("PASS: "+m);}
  static object Call(string name,params object[] args){try{return Service.GetMethod(name).Invoke(null,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static bool Denied(Action action){try{action();return false;}catch{return true;}}
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  public static void Run(){
   Check(Service!=null,"maintenance service exists");
   string root=Path.Combine(Path.GetTempPath(),"orange-maintenance-"+Guid.NewGuid().ToString("N"));
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
   string mp=Path.Combine(e.User,"Saves","Multiplayer","server_hash");Directory.CreateDirectory(Path.Combine(mp,"map","12"));Directory.CreateDirectory(Path.Combine(mp,"custom"));
   foreach(string f in new[]{"players.db","vehicles.db","map_symbols.bin","map_t.bin","unknown.bin","map_1_2.bin","custom/map_1_2.bin","map/12/34.bin","map/12/note.bin"})File.WriteAllText(Path.Combine(mp,f),"fixture");
   string mods=Path.Combine(e.User,"mods","author");Directory.CreateDirectory(mods);File.WriteAllText(Path.Combine(mods,"mod.info"),"id=keep");
   Call("ClearMultiplayerCache",e);
   Check(!File.Exists(Path.Combine(mp,"map","12","34.bin")),"verified B42 numeric map chunk removed");
   Check(new[]{"players.db","vehicles.db","map_symbols.bin","map_t.bin","unknown.bin","map_1_2.bin","custom/map_1_2.bin","map/12/note.bin"}.All(f=>File.ReadAllText(Path.Combine(mp,f))=="fixture")&&File.Exists(Path.Combine(mods,"mod.info")),"all unknown saves and local mods preserved");
   string outside=Path.Combine(root,"outside");Directory.CreateDirectory(outside);File.WriteAllText(Path.Combine(outside,"9.bin"),"outside");
   string link=Path.Combine(mp,"map","99");var p=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true});p.WaitForExit();Check(p.ExitCode==0,"fixture junction created");
   Check(Denied(()=>Call("ClearMultiplayerCache",e))&&File.ReadAllText(Path.Combine(outside,"9.bin"))=="outside","junction rejects cleanup without traversal");
   Check(Denied(()=>Call("RebuildWorkshop",e,null,new CancellationToken(true))),"cancelled workshop rebuild does not spawn child");
   Console.WriteLine("MAINTENANCE TESTS PASSED; fixture retained: "+root);
  }
 }
}
