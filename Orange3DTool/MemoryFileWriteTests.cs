using System;
using System.IO;
using System.Linq;
using System.Text;

namespace PZ3D {
 static class MemoryFileWriteTests {
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  public static int Main(string[] args){try{Run(args.Length>0?args[0]:LauncherTestPaths.Root,args.Length>1?args[1]:LauncherTestPaths.Root);return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
  internal static void Run(string gameParent,string dataParent){
   string id="memory-write-"+Guid.NewGuid().ToString("N"),game=Path.Combine(gameParent,id,"game"),data=Path.Combine(dataParent,id,"data"),user=Path.Combine(dataParent,id,"user");
   Directory.CreateDirectory(game);Directory.CreateDirectory(data);Directory.CreateDirectory(user);
   var engine=new Engine(game,user,data,true);
   byte[] original=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"mainClass\":\"zombie/gameStates/MainScreenState\",\"classpath\":[\".\",\"projectzomboid.jar\"],\"vmArgs\":[\"-Djava.awt.headless=true\",\"-Xms12288m\",\"-Xmx12288m\",\"-agentlib:zbNative\"],\"windows\":{\"10.0.17134\":{\"vmArgs\":[\"-XX:+UseShenandoahGC\"]}},\"label\":\"保留字段\"}\r\n")).ToArray();
   File.WriteAllBytes(engine.Config,original);
   Console.WriteLine("Game volume: "+Path.GetPathRoot(game)+"; backup volume: "+Path.GetPathRoot(data));
   LauncherMaintenance.SetHeapGiB(engine,8,()=>LauncherMaintenance.CalculateMemoryInfo(64,28));
   Check(LauncherMaintenance.ReadHeapGiB(engine)==8,"actual configuration reads back saved 8 GiB");
   string firstBackup=Path.Combine(engine.LastBackup,"ProjectZomboid64.json");
   Check(File.ReadAllBytes(firstBackup).SequenceEqual(original),"backup on data volume preserves original bytes including BOM");
   string saved=File.ReadAllText(engine.Config);
   Check(saved.Contains("-agentlib:zbNative")&&saved.Contains("-XX:+UseShenandoahGC")&&saved.Contains("projectzomboid.jar")&&saved.Contains("保留字段"),"real launch fields and non-heap JVM settings retained");
   byte[] secondOriginal=File.ReadAllBytes(engine.Config);
   LauncherMaintenance.SetHeapGiB(engine,12,()=>LauncherMaintenance.CalculateMemoryInfo(64,28));
   Check(LauncherMaintenance.ReadHeapGiB(engine)==12&&File.ReadAllBytes(Path.Combine(engine.LastBackup,"ProjectZomboid64.json")).SequenceEqual(secondOriginal),"second save succeeds with independent exact backup");
   Check(!Directory.GetFiles(game,"*.heap-*.tmp").Any(),"successful saves leave no pending replacement");
   byte[] beforeLocked=File.ReadAllBytes(engine.Config);bool refused=false;
   using(var held=new FileStream(engine.Config,FileMode.Open,FileAccess.Read,FileShare.Read)){try{LauncherMaintenance.SetHeapGiB(engine,6,()=>LauncherMaintenance.CalculateMemoryInfo(64,28));}catch(IOException){refused=true;}}
   Check(refused&&File.ReadAllBytes(engine.Config).SequenceEqual(beforeLocked),"locked target reports failure and preserves configuration");
   Check(!Directory.GetFiles(game,"*.heap-*.tmp").Any(),"failed save clears pending replacement");
   Console.WriteLine("MEMORY FILE WRITE TESTS PASSED; fixture retained: "+id);
  }
 }
}
