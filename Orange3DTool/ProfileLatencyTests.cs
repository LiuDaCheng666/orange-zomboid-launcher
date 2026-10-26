using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class ProfileLatencyTests {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=8388608};
  static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
  static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);}
  static Dictionary<string,object> Data(object value){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(value));}
  static double Time(Func<object> action){var watch=Stopwatch.StartNew();var result=action();GC.KeepAlive(result);return Math.Round(watch.Elapsed.TotalMilliseconds,3);}
  static double[] Times(Func<object> action,int count){return Enumerable.Range(0,count).Select(i=>Time(action)).ToArray();}
  static void Measure(string game,string user,string data){
   // Constructor and APIs below are read-only. Never instantiate a controller or
   // invoke mutation, startup, native Steam, or session initialization here.
   var e=new Engine(game,user,data,true);
   var output=new Dictionary<string,object>();
   output["utc"]=DateTime.UtcNow.ToString("o");output["game"]=game;
   output["workshopItems"]=Directory.Exists(e.Workshop)?Directory.GetDirectories(e.Workshop).Length:0;
   output["savesListMs"]=Times(()=>SaveProfiles.List(e),3);
   output["modProfilesMs"]=Times(()=>ModConfigurations.List(e),3);
   var warnings=new List<string>();
   output["entriesMs"]=Times(()=>SaveProfiles.Entries(e,warnings),3);
   output["catalogForcedMs"]=Times(()=>{SaveProfiles.InvalidateDisplay(e);return SaveProfiles.Catalog(e);},3);
   output["catalogCachedMs"]=Times(()=>SaveProfiles.Catalog(e),3);
   output["catalogBytes"]=Encoding.UTF8.GetByteCount(Json.Serialize(SaveProfiles.Catalog(e)));
   output["catalogSerializeMs"]=Times(()=>Json.Serialize(SaveProfiles.Catalog(e)),3);
   output["warnings"]=warnings.Distinct().Take(10).ToArray();
   SaveProfiles.InvalidateDisplay(e);Console.WriteLine(Json.Serialize(output));
  }
  static void CachedControllerReads(){
   string root=Path.Combine(Path.GetTempPath(),"ozl-pl-"+Guid.NewGuid().ToString("N").Substring(0,10));
   string game=Path.Combine(root,"steamapps/common/Game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");
   string info=Path.Combine(root,"steamapps/workshop/content/108600/123/mods/Fixture/42/mod.info");
   Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");Put(Path.Combine(game,"game-version.txt"),"42.21.0");
   Put(Path.Combine(user,"mods/default.txt"),"mods\n{\n    mod = fixture,\n}\nmaps\n{\n}\n");
   Put(info,"id=fixture\nname=Cached metadata\n");
   Directory.CreateDirectory(Path.Combine(data,"diagnostics"));Directory.CreateDirectory(Path.Combine(data,"mod-profiles"));
   using(var c=new ModernController(game,user,data,true)){
    // Settle notifications from fixture directory creation before checking
    // reuse of a stable source tree, as with an established user installation.
    for(int i=0;i<3;i++)Check(Json.Serialize(c.Execute("modCatalog",null)).Contains("Cached metadata"),"warm catalog contains fixture metadata");
    using(var held=new FileStream(info,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
     Check(Json.Serialize(c.Execute("modCatalog",null)).Contains("Cached metadata"),"unchanged display read reuses metadata instead of rescanning busy source");
    var profiles=Data(c.Execute("modProfiles",null));
    byte[] originalDefault=File.ReadAllBytes(Path.Combine(user,"mods/default.txt"));
    c.Execute("modProfileSave",new Dictionary<string,object>{{"name","Draft"},{"mods",new[]{"fixture"}},{"stamp",profiles["stamp"]}});
    using(var held=new FileStream(info,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
     Check(Json.Serialize(c.Execute("modCatalog",null)).Contains("Cached metadata"),"draft save does not rescan unchanged catalog sources");
    Check(originalDefault.SequenceEqual(File.ReadAllBytes(Path.Combine(user,"mods/default.txt"))),"draft save preserves actual default bytes");
    using(var held=new FileStream(info,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
     Check(!Json.Serialize(c.Execute("modCatalog",new Dictionary<string,object>{{"refresh",true}})).Contains("Cached metadata"),"explicit refresh rechecks sources instead of returning cached metadata");
    c.Execute("modCatalog",new Dictionary<string,object>{{"refresh",true}});
    string save=Path.Combine(user,"Saves/Sandbox/Fixture/mods.txt");Put(save,"mods\n{\n    mod = fixture,\n}\nmaps\n{\n}\n");
    var detail=Data(c.Execute("saveGet",new Dictionary<string,object>{{"id","Sandbox/Fixture"}}));
    DateTime originalTime=File.GetLastWriteTimeUtc(info);
    Put(info,"id=fixture\nname=Cached metadata\nrequire=missing\n");File.SetLastWriteTimeUtc(info,originalTime);
    byte[] originalSave=File.ReadAllBytes(save);bool rejected=false;
    try{c.Execute("saveSave",new Dictionary<string,object>{{"id","Sandbox/Fixture"},{"stamp",detail["stamp"]},{"mods",new[]{"fixture"}},{"confirmed",true}});}catch(Exception ex){rejected=ex.Message.Contains("依赖");}
    Check(rejected&&originalSave.SequenceEqual(File.ReadAllBytes(save)),"save validates current dependency bytes even with a previously warm catalog");
   }
   Console.WriteLine("PASS unchanged catalog and draft reads reuse display metadata, explicit refresh and authoritative dependency checks remain fresh");
  }
  [STAThread] public static int Main(string[] args){try{if(args.Length==4&&args[0]=="--measure")Measure(args[1],args[2],args[3]);else{CachedControllerReads();if(args.Contains("--regressions")){CatalogCacheTests.Run();SaveProfilesTests.Run();ModConfigurationsTests.Run();}}return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
 }
}
