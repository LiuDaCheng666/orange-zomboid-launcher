using System;
using System.IO;
using System.Reflection;
namespace PZ3D {
 static class DiscoveryTests {
  static void Check(bool v,string s){if(!v)throw new Exception("FAIL: "+s);Console.WriteLine("PASS: "+s);}
  public static void Run(){var type=typeof(Engine).Assembly.GetType("PZ3D.GameDiscovery");Check(type!=null,"portable Steam game directory discovery exists");string root=Path.Combine(Path.GetTempPath(),"orange-discovery-test-"+Guid.NewGuid().ToString("N")),steam=Path.Combine(root,"steam client"),library=Path.Combine(root,"different library");Directory.CreateDirectory(Path.Combine(steam,"steamapps"));string game=Path.Combine(library,"steamapps","common","Custom PZ Folder");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{}");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");File.WriteAllText(Path.Combine(steam,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"1\" { \"path\" \""+library.Replace("\\","\\\\")+"\" } }");File.WriteAllText(Path.Combine(library,"steamapps","appmanifest_108600.acf"),"\"AppState\" { \"appid\" \"108600\" \"installdir\" \"Custom PZ Folder\" }");var find=type.GetMethod("Find");Check((string)find.Invoke(null,new object[]{new[]{steam}})==game,"game in another Steam library detected using manifest install folder");File.Delete(Path.Combine(game,"ProjectZomboid64.exe"));Check((string)find.Invoke(null,new object[]{new[]{steam}})=="","missing executable rejects false game directory");Check((string)find.Invoke(null,new object[]{new[]{Path.Combine(root,"missing")}})=="","unknown installation returns manual-selection state not creator-specific path");}
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
