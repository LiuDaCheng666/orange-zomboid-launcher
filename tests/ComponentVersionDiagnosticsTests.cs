using System;using System.IO;using System.Threading;
namespace PZ3D {
 static class ComponentVersionDiagnosticsTests {
  static int checks;
  static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
  static void Main(string[] args){try{
   string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
   var context=LauncherConfiguration.Resolve(Path.Combine(root,"data"),Path.Combine(root,"game"),Path.Combine(root,"user"));
   var store=new LauncherComponents(context,new Version(42,22));
   foreach(var item in new[]{new[]{"Minimum","versionMin=42.22.2","42.22.2","游戏版本","42.22"},new[]{"Maximum","versionMax=42.21","42.21","游戏版本","42.22"},new[]{"BuddyMinimum","ZBVersionMin=3.0.0","3.0.0","ZombieBuddy","2.3.4"}}){
    string folder=Path.Combine(root,item[0],"mods",item[0]);Directory.CreateDirectory(Path.Combine(folder,"42"));
    File.WriteAllText(Path.Combine(folder,"42","mod.info"),"id="+item[0]+"\nname=Named "+item[0]+"\n"+item[1]+"\n");
    byte[] original=File.ReadAllBytes(Path.Combine(folder,"42","mod.info"));string message="";
    try{store.Inspect(folder,item[0],CancellationToken.None);}catch(IOException error){message=error.Message;}
    Check(message.Contains("Named "+item[0])&&message.Contains(item[0]),"failed component must be named: "+message);
    Check(message.Contains(item[2])&&message.Contains(item[3])&&message.Contains(item[4]),"actual runtime and required range must be visible: "+message);
    Check(Convert.ToBase64String(original)==Convert.ToBase64String(File.ReadAllBytes(Path.Combine(folder,"42","mod.info"))),"metadata unchanged");
   }
   Console.WriteLine("PASS "+checks+" component version diagnostics assertions");
  }catch(Exception error){Console.Error.WriteLine(error);Environment.Exit(1);}}
 }
}
