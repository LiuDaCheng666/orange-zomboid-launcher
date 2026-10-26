using System;using System.Diagnostics;using System.IO;using System.Linq;
namespace PZ3D {
 static class LaunchPathValidationTests {
  static void Check(bool value,string why){if(!value)throw new Exception("FAIL: "+why);Console.WriteLine("PASS: "+why);}
  static bool Refuses(string path){try{LauncherMaintenance.NoReparse(path);return false;}catch{return true;}}
  public static int Main(){string root=Path.Combine(Path.GetTempPath(),"ozl-launch-path-"+Guid.NewGuid().ToString("N"));string link=Path.Combine(root,"link");try{
   Directory.CreateDirectory(root);string target=Path.Combine(root,"target");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"marker.txt"),"owned fixture");
   LauncherMaintenance.NoReparse(Path.Combine(target,"missing","leaf.txt"));LauncherMaintenance.NoReparse(Path.Combine(target,"marker.txt"));Check(true,"ordinary existing and missing paths remain accepted");

   using(var process=Process.Start(new ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){process.WaitForExit();Check(process.ExitCode==0,"disposable junction created");}
   Check(Refuses(Path.Combine(link,"missing","leaf.txt")),"existing junction ancestor rejected for a missing leaf");
   File.Delete(Path.Combine(target,"marker.txt"));Directory.Delete(target);
   Check(Refuses(Path.Combine(link,"missing","leaf.txt")),"dangling junction ancestor rejected before a target can reappear");
   Directory.Delete(link);Directory.CreateDirectory(link);LauncherMaintenance.NoReparse(Path.Combine(link,"missing.txt"));Check(true,"new ordinary path accepted without a stale cache");Directory.Delete(link);Directory.CreateDirectory(target);
   using(var process=Process.Start(new ProcessStartInfo("cmd.exe","/c mklink /J \""+link+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){process.WaitForExit();Check(process.ExitCode==0,"replacement fixture junction created");}
   Check(Refuses(Path.Combine(link,"missing.txt")),"replaced ancestor rechecked on every invocation");return 0;
  }catch(Exception error){Console.WriteLine(error);return 1;}finally{try{if((File.GetAttributes(link)&FileAttributes.ReparsePoint)!=0)Directory.Delete(link);}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}if(Directory.Exists(root))Directory.Delete(root,true);}}
 }
}


