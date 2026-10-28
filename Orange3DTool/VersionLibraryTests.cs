using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Diagnostics;
namespace PZ3D {
 static class VersionLibraryTests {
  static int checks;static Type library;static object service;
  static void Check(bool ok,string message){if(!ok)throw new Exception("FAIL "+message);checks++;}
  static object Call(string name,params object[] args){try{return library.GetMethod(name).Invoke(service,args);}catch(TargetInvocationException x){throw x.InnerException;}}
  static string S(object row,string key){return (string)row.GetType().GetField(key).GetValue(row);}
  static bool B(object row,string key){return (bool)row.GetType().GetField(key).GetValue(row);}
  static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text);}
  static void Refuses(Action action,string message){bool refused=false;try{action();}catch(Exception){refused=true;}Check(refused,message);}
  static string Game(string root,string name){string path=Path.Combine(root,name);Put(Path.Combine(path,"ProjectZomboid64.exe"),"binary");Put(Path.Combine(path,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");Put(Path.Combine(path,"media","中文.bin"),"original bytes");Directory.CreateDirectory(Path.Combine(path,"empty"));return path;}
  public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
  public static void Run(){
   library=typeof(Engine).Assembly.GetType("PZ3D.VersionLibrary");Check(library!=null,"version library exists");
   string root=Path.Combine(LauncherTestPaths.Root,"versions-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   try{
    string game=Game(root,"steam"),user=Path.Combine(root,"legacy-user"),data=Path.Combine(root,"data");Put(Path.Combine(user,"options.ini"),"legacy settings");
    service=Activator.CreateInstance(library,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{data,game,user},null);
    object legacy=Call("Selected");Check(S(legacy,"Id")=="steam"&&S(legacy,"UserPath")==user&&!B(legacy,"Managed"),"first run retains exact legacy user path and Steam behavior");Check(VersionLibrary.Full(Path.GetPathRoot(root))==Path.GetPathRoot(root),"drive root canonicalization stays absolute");
    string importedGame=Game(root,"import");object imported=Call("Import",importedGame,"B41 旧版");string id=S(imported,"Id"),managedUser=S(imported,"UserPath");Check(id.Length==32&&B(imported,"Managed")&&S(imported,"GamePath")==importedGame,"import records validated existing installation with opaque identity");
    Check(managedUser!=user&&Directory.Exists(managedUser)&&S(imported,"DataPath")==data,"import creates independent user directory with shared launcher metadata root");
    Check(VersionLibrary.OwnsUser(data,importedGame,managedUser)&&!VersionLibrary.OwnsUser(data,game,managedUser),"exit workers accept exact registered game/user pair and reject another installation");
    Put(Path.Combine(managedUser,"options.ini"),"version settings");Put(Path.Combine(managedUser,"mods","default.txt"),"mod A");Put(Path.Combine(managedUser,"Saves","Sandbox","世界","map.bin"),"world");
    Call("Select",id);service=Activator.CreateInstance(library,new object[]{data,game,user});Check(S(Call("Selected"),"Id")==id&&File.ReadAllText(Path.Combine(user,"options.ini"))=="legacy settings","selected identity persists without moving legacy settings");
    Refuses(()=>Call("Select","../foreign"),"unregistered selection rejected");Refuses(()=>Call("Import",importedGame,"duplicate"),"same game cannot silently receive two unrelated profiles");Refuses(()=>Call("Import",root,"invalid"),"invalid game folder rejected");Refuses(()=>Call("Import",game,"Steam alias"),"canonical Steam directory must be preserved instead of adding mutable alias");
    string destination=Path.Combine(root,"copies","B42");object preserved=Call("Preserve",game,destination,"保留 B42",CancellationToken.None,null);string preservedUser=S(preserved,"UserPath");
    Check(File.ReadAllText(Path.Combine(destination,"media","中文.bin"))=="original bytes"&&Directory.Exists(Path.Combine(destination,"empty")),"preserve copies exact nested content and empty directories");
    File.WriteAllText(Path.Combine(destination,"media","中文.bin"),"copy edit");Check(File.ReadAllText(Path.Combine(game,"media","中文.bin"))=="original bytes","preserved install has independent writable files");
    Check(preservedUser!=managedUser&&!File.Exists(Path.Combine(preservedUser,"mods","default.txt"))&&!Directory.Exists(Path.Combine(preservedUser,"Saves")),"versions do not inherit another version's saves/settings/mod list");
    Refuses(()=>Call("Preserve",game,destination,"overwrite",CancellationToken.None,null),"existing destination refused");Refuses(()=>Call("Preserve",game,Path.Combine(game,"nested"),"recursive",CancellationToken.None,null),"copy into source refused");
    var cancelled=new CancellationTokenSource();cancelled.Cancel();string cancelledPath=Path.Combine(root,"cancelled");Refuses(()=>Call("Preserve",game,cancelledPath,"cancel",cancelled.Token,null),"cancelled preservation fails");Check(!Directory.Exists(cancelledPath)&&!Directory.GetDirectories(root,"*.partial-*").Any(),"cancel leaves no published destination or copy staging");
    var copyProgressType=typeof(Engine).Assembly.GetType("PZ3D.VersionCopyProgress");Check(copyProgressType!=null,"preserve progress exposes bytes for long copies");
    string during=Path.Combine(root,"cancel-during");var midway=new CancellationTokenSource();Action<VersionCopyProgress> cancelProgress=p=>{if(p.Stage=="copy"&&p.BytesCopied>0)midway.Cancel();};Refuses(()=>Call("Preserve",game,during,"cancel during",midway.Token,cancelProgress),"cancellation during streaming stops copy");Check(!Directory.Exists(during)&&!Directory.GetDirectories(root,"*.partial-*").Any(),"partial streaming cancellation rolls back owned staging");
    string changed=Path.Combine(root,"changed-source");bool altered=false;Action<VersionCopyProgress> changeProgress=p=>{if(p.Stage=="copy"&&!altered){altered=true;File.WriteAllText(Path.Combine(game,"Steam-update.bin"),"Steam update");}};Refuses(()=>Call("Preserve",game,changed,"changed",CancellationToken.None,changeProgress),"source mutation during copy rejected before publishing");Check(!Directory.Exists(changed)&&File.ReadAllText(Path.Combine(game,"Steam-update.bin"))=="Steam update","failed copy keeps changed source untouched");
    string occupied=Path.Combine(root,"occupied-during");Action<VersionCopyProgress> occupy=p=>{if(!Directory.Exists(occupied))Put(Path.Combine(occupied,"foreign.txt"),"keep");};Refuses(()=>Call("Preserve",game,occupied,"occupied",CancellationToken.None,occupy),"destination race cannot overwrite a foreign directory");Check(File.ReadAllText(Path.Combine(occupied,"foreign.txt"))=="keep","failure rollback never deletes foreign destination");
    string junction=Path.Combine(game,"linked"),foreign=Path.Combine(root,"foreign");Put(Path.Combine(foreign,"do-not-copy.txt"),"keep");var junctionStart=new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+junction+"\" \""+foreign+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var process=Process.Start(junctionStart)){process.StandardOutput.ReadToEnd();string error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception("Junction fixture failed: "+error);}try{string linkTarget=Path.Combine(root,"link-copy");Refuses(()=>Call("Preserve",game,linkTarget,"linked",CancellationToken.None,null),"copy refuses source junction instead of traversing into another directory");Check(!Directory.Exists(linkTarget)&&File.ReadAllText(Path.Combine(foreign,"do-not-copy.txt"))=="keep","junction rejection preserves unrelated target content");}finally{if(Directory.Exists(junction))Directory.Delete(junction);}
    string broken=S(imported,"GamePath");File.Delete(Path.Combine(broken,"ProjectZomboid64.exe"));Check(((IEnumerable)Call("List")).Cast<object>().Any(r=>S(r,"Id")==id&&!B(r,"Installed")),"missing install remains visible as unavailable");Refuses(()=>Call("Select",id),"missing install cannot be newly selected");
    string badState=Path.Combine(data,"version-library.json");File.WriteAllText(badState,"{broken");Refuses(()=>Call("List"),"corrupt library is surfaced rather than discarded/overwritten");
    Console.WriteLine("VersionLibrary PASS: "+checks+" assertions");
   }finally{string full=Path.GetFullPath(root),allowed=Path.GetFullPath(LauncherTestPaths.Root)+Path.DirectorySeparatorChar;if(!full.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(full).StartsWith("versions-"))throw new IOException("Unsafe fixture cleanup");if(Directory.Exists(full))Directory.Delete(full,true);}
  }
 }
}
