using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class LauncherGameVersionTests {
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static void Put(string file,string text){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,text,new UTF8Encoding(false));}
  public static int Main(){string root=Path.Combine(LauncherTestPaths.Root,"game-version-"+Guid.NewGuid().ToString("N"));try{
   string game=Path.Combine(root,"steamapps/common/ProjectZomboid"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");
   Put(Path.Combine(game,"ProjectZomboid64.exe"),"never executed fixture");Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx2g\"]}");Put(Path.Combine(user,"mods/default.txt"),"VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\n");
   byte[] core=(byte[])typeof(GameVersionProbeTests).GetMethod("Compile",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{root,"new GameVersion(42,21,\".0\")"});
   using(var zip=ZipFile.Open(Path.Combine(game,"projectzomboid.jar"),ZipArchiveMode.Create))using(var output=zip.CreateEntry("zombie/core/Core.class").Open())output.Write(core,0,core.Length);
   using(var controller=new ModernController(game,user,data,true)){
    var json=new JavaScriptSerializer();var status=json.Deserialize<Dictionary<string,object>>(json.Serialize(controller.Execute("status",null)));var v=(Dictionary<string,object>)status["gameVersion"];
    Check((string)v["value"]=="42.21.0"&&(string)v["state"]=="known"&&(string)v["source"]=="projectzomboid.jar"&&(string)v["warning"]=="","actual JAR supplies display version when optional version.txt is absent");
   }
   Put(Path.Combine(game,"version.txt"),"41.78.16");Check(LauncherStatusMetadata.GameVersion(game).value=="42.21.0","display uses actual game JAR instead of stale optional metadata");File.Delete(Path.Combine(game,"version.txt"));
   File.Delete(Path.Combine(game,"projectzomboid.jar"));var engine=new Engine(game,user,data,true);var result=LaunchPreflight.Check(engine,false,false);
   Check(result.canLaunch,"missing version metadata alone does not block an otherwise valid game launch");
   core=(byte[])typeof(GameVersionProbeTests).GetMethod("Compile",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{root,"new GameVersion(41,78,\".16\")"});using(var zip=ZipFile.Open(Path.Combine(game,"projectzomboid.jar"),ZipArchiveMode.Create))using(var output=zip.CreateEntry("zombie/core/Core.class").Open())output.Write(core,0,core.Length);
   using(var controller=new ModernController(game,user,Path.Combine(root,"legacy-data"),true)){bool refused=false;try{controller.Execute("componentsAction",new Dictionary<string,object>{{"component","buddy"},{"operation","install"}});}catch(Exception ex){refused=ex.Message.Contains("B42");}Check(refused,"B42-only managed component installation is refused before touching a B41 installation");}
   Check(!File.Exists(Path.Combine(game,"version.txt")),"version lookup never fabricates metadata files");return 0;
  }catch(Exception ex){Console.WriteLine(ex);return 1;}finally{
   string prefix=Path.GetFullPath(LauncherTestPaths.Root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;string full=Path.GetFullPath(root);
   if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(full),"^game-version-[a-f0-9]{32}$"))throw new IOException("Unsafe fixture cleanup");if(Directory.Exists(full))Directory.Delete(full,true);
  }}
 }
}
