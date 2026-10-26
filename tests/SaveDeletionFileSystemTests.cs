using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PZ3D {
 // Every writable/deletable target belongs to this unique run. There is no fake
 // filesystem deletion and no access to a user's real save directory.
 static class SaveDeletionFileSystemTests {
  static int checks;static string ownedRoot;
  static void Check(bool value,string label){checks++;if(!value)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  static string Owned(string path){string full=Path.GetFullPath(path);if(!full.StartsWith(ownedRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Outside unique deletion fixture: "+full);LauncherMaintenance.NoReparse(full);return full;}
  static void Put(string path,string text){path=Owned(path);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
  sealed class Fixture {
   internal readonly string Root,Path,Marker,Other,Id;internal readonly Engine E;
   internal Fixture(bool test=false,string mode="Survivor"){
    Root=Owned(System.IO.Path.Combine(ownedRoot,"f"+Guid.NewGuid().ToString("N").Substring(0,8)));
    E=new Engine(System.IO.Path.Combine(Root,"Game"),System.IO.Path.Combine(Root,"User"),System.IO.Path.Combine(Root,"Data"),test,SteamLaunchOptions.Unavailable,name=>new Process[0]);
    Id=mode+"/Owned-"+Guid.NewGuid().ToString("N").Substring(0,8);Path=Owned(System.IO.Path.Combine(E.User,"Saves",Id.Replace('/',System.IO.Path.DirectorySeparatorChar)));
    Marker=Owned(System.IO.Path.Combine(Path,"map","marker.bin"));Other=Owned(System.IO.Path.Combine(E.User,"Saves",mode,"Adjacent","marker.bin"));
    Put(E.Config,"{\"vmArgs\":[]}");Put(E.Mods,"VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\n");Put(Marker,"owned deletion fixture "+Id);Put(Other,"adjacent save stays here");Put(System.IO.Path.Combine(E.User,"Server","settings.ini"),"server stays here");
   }
   internal string Stamp(){return new JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(new JavaScriptSerializer().Serialize(SaveDeletion.Preview(E,Id)))["stamp"].ToString();}
   internal void Delete(string stamp,bool confirmed=true){Owned(Path);SaveDeletion.Delete(E,Id,stamp,confirmed);}
   internal void Preserved(){Check(File.ReadAllText(Other)=="adjacent save stays here"&&File.ReadAllText(System.IO.Path.Combine(E.User,"Server","settings.ini"))=="server stays here"&&File.ReadAllText(E.Mods)=="VERSION = 1,\nmods\n{\n}\nmaps\n{\n}\n","adjacent save, server and default mods retain original bytes");}
  }
  public static int Main(){Console.OutputEncoding=new UTF8Encoding(false);try{
   string basePath=System.IO.Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);LauncherMaintenance.NoReparse(basePath);
   ownedRoot=System.IO.Path.Combine(basePath,"TestFixtures","d-"+Guid.NewGuid().ToString("N"));LauncherMaintenance.NoReparse(ownedRoot);Directory.CreateDirectory(ownedRoot);Console.WriteLine("OWNED ROOT: "+ownedRoot);
   // Break caught: the confirmed operation enters an unsupported native path or
   // a test-mode prohibition, instead of the same managed filesystem boundary.
   var direct=new Fixture(true);string target=direct.Path;direct.Delete(direct.Stamp());Check(!Directory.Exists(target),"confirmed test fixture is permanently removed through the real production filesystem boundary");Check(!InRecycleBin(target),"permanently deleted fixture has no recycle-bin entry");direct.Preserved();
   foreach(string mode in new[]{"Survivor","Multiplayer"}){var f=new Fixture(false,mode);f.Delete(f.Stamp());Check(!Directory.Exists(f.Path),"normal-mode "+mode+" fixture directory is permanently removed");Check(!InRecycleBin(f.Path),mode+" deletion does not move fixture into recycle bin");f.Preserved();}
   ConfirmationAndStamps();Failures();
   Console.WriteLine("FILESYSTEM SAVE DELETION PASSED: "+checks+" assertions");return 0;
  }catch(Exception x){Console.WriteLine(x);return 1;}}
  static Exception Refuses(Action action,string label){Exception error=null;try{action();}catch(Exception x){error=x;}Check(error!=null,label);Console.WriteLine("  refused: "+error.GetType().Name+" 0x"+error.HResult.ToString("X8"));return error;}
  static void ConfirmationAndStamps(){
   var f=new Fixture();string original=File.ReadAllText(f.Marker),stamp=f.Stamp();Refuses(()=>f.Delete(stamp,false),"cancelling existing confirmation refuses deletion");Check(File.ReadAllText(f.Marker)==original,"cancelled confirmation retains source bytes");
   using(var cancel=new CancellationTokenSource()){cancel.Cancel();Check(Refuses(()=>SaveDeletion.Delete(f.E,f.Id,stamp,true,cancel.Token),"pre-cancelled production deletion refuses") is OperationCanceledException,"cancellation remains distinguishable from filesystem errors");}
   Put(f.Marker,"changed since preview");Refuses(()=>f.Delete(stamp),"stale preview refuses before filesystem deletion");Check(File.ReadAllText(f.Marker)=="changed since preview","stale preview retains changed source bytes");f.Preserved();
  }
  static void Failures(){
   // Break caught: OS file failures terminate the process, are reported as
   // completion, or cause retries/fallback deletion outside the exact target.
   var locked=new Fixture();string original=File.ReadAllText(locked.Marker),stamp=locked.Stamp();
   using(var handle=new FileStream(Owned(locked.Marker),FileMode.Open,FileAccess.Read,FileShare.None)){Check(Refuses(()=>locked.Delete(stamp),"locked-file deletion returns a managed error") is IOException,"locked-file failure is displayable IOException");Check(Directory.Exists(locked.Path),"locked-file failure keeps source directory");}
   Check(File.ReadAllText(locked.Marker)==original,"locked marker retains original bytes");locked.Preserved();
   var readOnly=new Fixture();original=File.ReadAllText(readOnly.Marker);string marker=Owned(readOnly.Marker);File.SetAttributes(marker,FileAttributes.ReadOnly);
   try{stamp=readOnly.Stamp();Exception error=Refuses(()=>readOnly.Delete(stamp),"read-only-file deletion returns a managed error");Check(error is UnauthorizedAccessException||error is IOException,"read-only failure is displayable filesystem error");Check(File.ReadAllText(marker)==original,"read-only marker retains original bytes");readOnly.Preserved();}
   finally{Owned(marker);if(File.Exists(marker))File.SetAttributes(marker,FileAttributes.Normal);}
   // A successful subsequent real deletion in this same process proves both
   // filesystem errors were handled without corrupting native/process state.
   var after=new Fixture();after.Delete(after.Stamp());Check(!Directory.Exists(after.Path),"same process survives both errors and can complete the next confirmed deletion");after.Preserved();
  }
  static bool InRecycleBin(string original){
   // Read only the current user's metadata on the fixture volume. Never delete,
   // restore or enumerate the contents of any recycled user file/directory.
   string bin=System.IO.Path.Combine(System.IO.Path.GetPathRoot(original),"$Recycle.Bin",WindowsIdentity.GetCurrent().User.Value);LauncherMaintenance.NoReparse(bin);if(!Directory.Exists(bin))return false;
   foreach(string metadata in Directory.EnumerateFiles(bin,"$I*",SearchOption.TopDirectoryOnly)){LauncherMaintenance.NoReparse(metadata);var info=new FileInfo(metadata);if(info.Length<28||info.Length>65536)continue;byte[] bytes=File.ReadAllBytes(metadata);long version=BitConverter.ToInt64(bytes,0);if(version!=1&&version!=2)continue;int at=version==2?28:24;string path=Encoding.Unicode.GetString(bytes,at,bytes.Length-at).TrimEnd('\0');if(string.Equals(path,original,StringComparison.OrdinalIgnoreCase))return true;}return false;
  }
 }
}
