using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ConfigurationRuntimeTests {
  static Dictionary<string,object> D(object value){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}
  static void Check(bool okay,string label){if(!okay)throw new Exception(label);Console.WriteLine("PASS: "+label);}
  // Storage of a named draft must not depend on a valid launch configuration.
  static void NativeDraftStorage(){string root=Path.Combine(LauncherTestPaths.Root,"config-storage-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");using(var c=new ModernController(game,user,data,true)){var list=D(c.Execute("modProfiles",null));var saved=D(c.Execute("modProfileSave",new Dictionary<string,object>{{"id",""},{"name","Draft"},{"stamp",list["stamp"]},{"mods",new[]{"MissingUntilInstalled"}}}));var profile=saved;Check((string)profile["name"]=="Draft","native named draft saves before a game is selected");list=D(c.Execute("modProfiles",null));c.Execute("modProfileDelete",new Dictionary<string,object>{{"id",profile["id"]},{"stamp",list["stamp"]}});Check(((System.Collections.IEnumerable)D(c.Execute("modProfiles",null))["profiles"]).Cast<object>().Count()==0,"native named draft deletes without launch prerequisites");}}
  // The native reorder is a real mutation and journals its user-visible result.
  static void NativeAutoSort(){string root=Path.Combine(LauncherTestPaths.Root,"config-sort-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");Directory.CreateDirectory(Path.Combine(user,"mods","B","42"));Directory.CreateDirectory(Path.Combine(user,"mods","A","42"));File.WriteAllText(Path.Combine(user,"mods","A","42","mod.info"),"id=A\n");File.WriteAllText(Path.Combine(user,"mods","B","42","mod.info"),"id=B\nrequire=A\n");string file=Path.Combine(user,"mods","default.txt");File.WriteAllText(file,"mods\n{\n mod = B,\n mod = A,\n}\nmaps\n{\n map = Keep,\n}\n",new UTF8Encoding(true));using(var c=new ModernController(game,user,data,true)){bool refused=false;try{c.Execute("modAutoSort",new Dictionary<string,object>());}catch{refused=true;}Check(refused,"native default reorder requires explicit confirmation");var result=D(c.Execute("modAutoSort",new Dictionary<string,object>{{"confirmed",true}}));string text=File.ReadAllText(file);Check(Convert.ToBoolean(result["changed"])&&text.IndexOf("mod = A,")<text.IndexOf("mod = B,")&&text.Contains("map = Keep,"),"native confirmed reorder writes dependency order without launch prerequisites");Check(new JavaScriptSerializer().Serialize(c.Execute("taskGet",null)).Contains("modAutoSort"),"native reorder is recorded as a mutation task");}}
  public static void Run(){NativeDraftStorage();NativeAutoSort();ConfigurationLifecycleTests.Run();SaveDeletionTests.Run();}
  [STAThread] public static int Main(){try{Run();Console.WriteLine("CONFIGURATION RUNTIME REGRESSIONS PASSED");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
 }
}
