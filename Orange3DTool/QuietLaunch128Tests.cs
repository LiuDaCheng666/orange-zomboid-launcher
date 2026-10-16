using System;
using System.IO;
using System.Text;
namespace PZ3D {
 static class QuietLaunch128Tests {
  static void Put(string path,string value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value,new UTF8Encoding(false));}
  public static int Main(){try{Run();return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
  public static void Run(){Delete(false);Delete(true);}
  static void Delete(bool session){
   string root=Path.Combine(LauncherTestPaths.Root,"delete128-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"Game"),Path.Combine(root,"User"),Path.Combine(root,"Data"),true,null,name=>{throw new Exception("save deletion must not enumerate game/server/Java processes");});
   Put(e.Config,"{\"vmArgs\":[]}");Put(e.Mods,"mods\n{\n}\n");string path=Path.Combine(e.User,"Saves","Sandbox","Selected"),other=Path.Combine(e.User,"Saves","Sandbox","Other","keep.bin");Put(Path.Combine(path,"marker.bin"),"selected");Put(other,"unrelated");
   if(session)LaunchProfiles.Stage(e,new ProfileLaunchRequest{Kind="save",Mode="Sandbox",Save="Selected"});
   var preview=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(SaveDeletion.Preview(e,"Sandbox/Selected")));
   string trash=Path.Combine(root,"recycled");bool recycled=false;SaveDeletion.Delete(e,"Sandbox/Selected",(string)preview["stamp"],true,p=>{if(p!=path)throw new Exception("wrong recycle target");Directory.Move(p,trash);recycled=true;});
   if(!recycled||Directory.Exists(path)||File.ReadAllText(Path.Combine(trash,"marker.bin"))!="selected"||File.ReadAllText(other)!="unrelated")throw new Exception("runtime-independent deletion must recycle only the selected save");
   if(session){LaunchProfiles.Recover(e,true);if(LaunchProfiles.Pending(e))throw new Exception("save recycling must not corrupt session cleanup");}
   Console.WriteLine("PASS: confirmed selected save recycling ignores global runtime"+(session?" and active direct-launch session":""));
  }
 }
}
