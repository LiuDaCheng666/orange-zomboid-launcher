using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ModSaveNativeFixture {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2097152};
  static Engine E; static string ProfileId;static bool ReadOnlyReal;
  static Dictionary<string,object> D(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
  static void Put(string path,string value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value,new UTF8Encoding(false));}
  static string[] Ids(object value){return ((IEnumerable)value).Cast<object>().Select(Convert.ToString).ToArray();}
  static void Default(string[] mods){Put(E.Mods,"VERSION = 1,\r\nmods\r\n{\r\n"+string.Concat(mods.Select(id=>"    mod = "+id+",\r\n"))+"}\r\nmaps\r\n{\r\n    map = OriginalMap,\r\n}\r\ncustom\r\n{\r\n    keep = original,\r\n}\r\n");}
  static string Hash(string path){return File.Exists(path)?DlssInstaller.Hash(File.ReadAllBytes(path)):"absent";}
  static object Execute(string action,Dictionary<string,object> p){if(ReadOnlyReal&&!new[]{"info","status","modProfiles","modProfileGet","modCatalog","modCover"}.Contains(action))throw new Exception("Read-only catalog fixture rejects mutation");switch(action){
   case "info":return new{root=Path.GetDirectoryName(E.Data),profileId=ProfileId};
   case "fixtureDefault":Default(Ids(p["mods"]));return new{message="Isolated fixture default replaced"};
   case "fixtureHashes":return new{defaults=Hash(E.Mods),store=Hash(Path.Combine(E.Data,"mod-profiles",LaunchProfiles.Key(E)+".json"))};
   case "status":if(ReadOnlyReal){using(var controller=new ModernController(E.Game,E.User,E.Data,true))return controller.Execute("status",new Dictionary<string,object>());}return new{gamePath=E.Game,validGame=true,running=false,theme="dark",configured=false,buddyFiles=false,components=new object[0],configStamp=Hash(E.Config)+Hash(E.Mods),task=new{active=false}};
   case "modProfiles":return ModConfigurations.List(E);
   case "modProfileGet":return ModConfigurations.Read(E,(string)p["id"]);
   case "modProfileSave":return ModConfigurations.Save(E,p);
   case "modProfileActivate":return ModConfigurations.Activate(E,(string)p["id"],(string)p["stamp"],p.ContainsKey("confirmed")&&(bool)p["confirmed"]);
   case "modProfileDelete":return ModConfigurations.Delete(E,(string)p["id"],(string)p["stamp"]);
   case "modSort":return ModConfigurations.Sort(E,Ids(p["mods"]));
   case "modCatalog":return SaveProfiles.Catalog(E);
   case "modCover":return SaveProfiles.Cover(E,(string)p["token"]);
   default:throw new Exception("Unsupported isolated fixture operation: "+action);
  }}
  public static int Main(string[] args){try{return Run(args);}catch(Exception ex){Console.Error.WriteLine(ex.GetType().FullName);Console.Error.WriteLine(ex.Message);Console.Error.WriteLine(ex.StackTrace);return 1;}}
  static int Run(string[] args){Console.InputEncoding=new UTF8Encoding(false);Console.OutputEncoding=new UTF8Encoding(false);string root=Path.GetFullPath(Path.Combine(args[0],Guid.NewGuid().ToString("N")));ReadOnlyReal=args.Length==4&&args[1]=="--read-only-catalog";E=ReadOnlyReal?new Engine(args[2],args[3],Path.Combine(root,"Data"),true):new Engine(Path.Combine(root,"steamapps/common/FixtureGame"),Path.Combine(root,"User"),Path.Combine(root,"Data"),true);
   if(!ReadOnlyReal){Put(E.Config,"{\"vmArgs\":[\"-Xmx2g\"]}");Put(Path.Combine(E.Game,"game-version.txt"),"42.12.3");Put(Path.Combine(E.Game,"ProjectZomboid64.exe"),"fixture-never-executed");foreach(string id in new[]{"A","B"})Put(Path.Combine(E.User,"mods",id,"42/mod.info"),"id="+id+"\nname=Fixture "+id+"\n");Default(new[]{"A","B"});var list=D(ModConfigurations.List(E));ProfileId=(string)D(ModConfigurations.Save(E,new Dictionary<string,object>{{"id",""},{"name","Saved A"},{"mods",new[]{"A"}},{"stamp",list["stamp"]}}))["id"];}
   string line;while((line=Console.ReadLine())!=null){object requestId=null;try{var request=Json.Deserialize<Dictionary<string,object>>(line);requestId=request["id"];object raw;var payload=request.TryGetValue("payload",out raw)&&raw!=null?(Dictionary<string,object>)raw:new Dictionary<string,object>();var result=Execute((string)request["action"],payload);Console.WriteLine(Json.Serialize(new{id=requestId,ok=true,data=result}));}catch(Exception ex){Console.WriteLine(Json.Serialize(new{id=requestId,ok=false,error=ex.Message}));}}return 0;}
 }
}
