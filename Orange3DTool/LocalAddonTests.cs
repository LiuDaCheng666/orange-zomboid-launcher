using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
namespace PZ3D {
 static class LocalAddonTests {
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  public static void Run(){
   var type=typeof(Engine).Assembly.GetType("PZ3D.LocalAddon");Check(type!=null,"client-only addon eligibility policy exists");
   string root=Path.Combine(LauncherTestPaths.Root,"orange-addons-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string info=Path.Combine(root,"mod.info"),jar=Path.Combine(root,"addon.jar");
   File.WriteAllText(info,"id=ViewpointSmoothFrames\njavaJarFile=media/java/client/SmoothFrames.jar\njavaPkgName=smoothframes\njavaPreload=true\nversionMin=42.21\nZBVersionMin=2.3.0\n");
   using(var archive=ZipFile.Open(jar,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("META-INF/MANIFEST.MF").Open()))writer.Write("Manifest-Version: 1.0\r\n\r\n");}
   var assess=type.GetMethod("ClientOnlyReason");
   Check(((string)assess.Invoke(null,new object[]{info,jar,"42.21.0","2.3.4"})).Contains("ZB-Preload"),"client Java folder alone cannot advertise persistent multiplayer loading");
   File.Delete(jar);using(var archive=ZipFile.Open(jar,ZipArchiveMode.Create)){using(var writer=new StreamWriter(archive.CreateEntry("META-INF/MANIFEST.MF").Open()))writer.Write("Manifest-Version: 1.0\r\nZB-Preload: true\r\n\r\n");}
   Check((string)assess.Invoke(null,new object[]{info,jar,"42.21.0","2.3.4"})=="","declared author preload can pass capability policy with compatible versions");
   Check(((string)assess.Invoke(null,new object[]{info,jar,"42.20","2.3.4"})).Contains("游戏版本"),"older game cannot enable addon requiring newer version");
   Check(((string)assess.Invoke(null,new object[]{info,jar,"42.21","2.2.0"})).Contains("Buddy"),"old Buddy rejected independently of game version");
   File.AppendAllText(info,"versionMax=42.21\n");Check((string)assess.Invoke(null,new object[]{info,jar,"42.21.1","2.3.4"})=="","author two-segment maximum includes patches within that game release");
   Check(((string)assess.Invoke(null,new object[]{info,jar,"42.22","2.3.4"})).Contains("游戏版本"),"new game outside author declared range is rejected");
   Check(((string)assess.Invoke(null,new object[]{info,jar,"unknown","2.3.4"})).Contains("无法确认"),"unknown game version cannot pass eligibility");
   File.WriteAllText(info,"javaJarFile=media/java/client/SmoothFrames.jar\njavaPkgName=smoothframes\n");Check(((string)assess.Invoke(null,new object[]{info,jar,"42.21","2.3.4"})).Contains("javaPreload"),"manifest alone cannot opt author mod into persistent approval preload");
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
