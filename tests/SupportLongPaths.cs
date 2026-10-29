using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;
namespace PZ3D {
 static class SupportLongPaths {
  static int checks;
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+(++checks)+" "+message);}
  static void Put(string path,byte[] bytes){SnapshotFileSystem.Directories.CreateDirectory(Path.GetDirectoryName(path));SnapshotFileSystem.Files.WriteAllBytes(path,bytes);}
  static byte[] Read(string path){using(var input=new SnapshotFileSystem.Stream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var output=new MemoryStream()){input.CopyTo(output);return output.ToArray();}}
  public static int Main(string[] args){LauncherRuntime.Initialize();string root=Path.GetFullPath(args[0]);try{
   Console.WriteLine("LongPathsEnabled="+Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem","LongPathsEnabled","unknown"));
   string game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data"),source=Path.Combine(root,"workshop","mods","ZombieBuddy");
   Put(Path.Combine(game,"version.txt"),Encoding.UTF8.GetBytes("42.21.0"));
   Put(Path.Combine(source,"42","mod.info"),Encoding.UTF8.GetBytes("id=ZombieBuddy\nname=ZombieBuddy\n"));
   byte[] buddy=File.ReadAllBytes(Path.Combine(Environment.CurrentDirectory,"Dependencies","ZombieBuddy-v2.3.4.jar"));Put(Path.Combine(source,"libs","ZombieBuddy.jar"),buddy);
   string relative=Path.Combine("java","src","main","java","me","zed_0xff","zombie_buddy","frontend","ConsoleModApprovalFrontend.java");byte[] content=Encoding.UTF8.GetBytes("source files must be copied completely, not skipped to hide a path error\n");Put(Path.Combine(source,relative),content);
   string longRelative=Path.Combine("java","src",new string('x',100),"nested",new string('y',90),"source.java");
   var context=LauncherConfiguration.Resolve(data,game,user).Bind("launcher");var store=new LauncherComponents(context,new Version(42,21));var copies=new OwnedSupportInstallations(store);
   Console.WriteLine("Reported-shape target characters="+Path.Combine(copies.Payload("ZombieBuddy"),relative).Length);
   Check(Path.Combine(copies.Payload("ZombieBuddy"),relative).Length>=260,"the real configuration copy crosses the Windows legacy path limit");
   string output=copies.Enable("ZombieBuddy",source,CancellationToken.None);
   Check(copies.Installed("ZombieBuddy")&&Read(Path.Combine(output,relative)).SequenceEqual(content),"Buddy installation preserves the reported nested source file bytes");
   var receipt=copies.Read("ZombieBuddy");Check(receipt.Files.Length==3&&receipt.Files.All(f=>!f.Path.StartsWith(@"\\?\")),"receipt records every file and normal relative paths");
   copies.Disable("ZombieBuddy",CancellationToken.None);Check(copies.Installed("ZombieBuddy"),"disable retains the complete long-path payload");
   Check(copies.Enable("ZombieBuddy",source,CancellationToken.None)==output,"reenable validates and reuses the complete installed copy");
   byte[] newer=Encoding.UTF8.GetBytes("updated nested source bytes\n");Put(Path.Combine(source,longRelative),newer);copies.Enable("ZombieBuddy",source,CancellationToken.None,true);
   Check(Read(Path.Combine(output,longRelative)).SequenceEqual(newer),"update replaces the old long-path payload and removes retired files");
   copies.Uninstall("ZombieBuddy",CancellationToken.None);Check(!SnapshotFileSystem.Directories.Exists(Path.GetDirectoryName(Path.GetDirectoryName(output))),"uninstall physically removes the entire receipt-owned long-path container");
   Check(Read(Path.Combine(source,longRelative)).SequenceEqual(newer)&&Read(Path.Combine(source,"libs","ZombieBuddy.jar")).SequenceEqual(buddy),"install, update, and uninstall leave original Workshop source unchanged");
   var cancel=new CancellationTokenSource();var cancelling=new OwnedSupportInstallations(store,(progress,message)=>{if(message.StartsWith("正在复制")&&message.Contains("字节")&&progress>0)cancel.Cancel();});bool cancelled=false;try{cancelling.Enable("ZombieBuddy",source,cancel.Token);}catch(OperationCanceledException){cancelled=true;}
   Check(cancelled&&!copies.Installed("ZombieBuddy")&&!SnapshotFileSystem.Directories.EnumerateFileSystemEntries(Path.Combine(context.ComponentsDirectory,"installed")).Any(),"cancel removes real deep staging files and leaves no installation or task ownership");
   output=copies.Enable("ZombieBuddy",source,CancellationToken.None);Check(copies.Installed("ZombieBuddy"),"installation can immediately retry after cancellation");copies.Uninstall("ZombieBuddy",CancellationToken.None);
   Console.WriteLine("PASS total="+checks+" root="+root);return 0;
  }catch(Exception error){Console.Error.WriteLine(error);Console.Error.WriteLine("Retained isolated fixture: "+root);return 1;}}
 }
}
