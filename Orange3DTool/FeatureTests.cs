using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Compatibility tests seed the OLD journal directly: production no longer has an install path.
 static class FeatureTests {
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
  internal static string Seed(string game,string data){
   Directory.CreateDirectory(game);Directory.CreateDirectory(data);
   File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx28672m\"]}");
   File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"inert fixture, never executed");
   File.WriteAllText(Path.Combine(game,"UserNotes.txt"),"keep me");
   string backup=Path.Combine(data,"dlss-backups","legacy-fixture"),original=Path.Combine(backup,"original-header.txt");
   Directory.CreateDirectory(backup);File.WriteAllText(original,"original header preserved");
   var files=new[]{new DlssFile{Relative="opengl32.dll"},new DlssFile{Relative="nvngx_dlssnr.dll"},new DlssFile{Relative="ReShade.ini"},new DlssFile{Relative="dlss5-feed.cfg"},new DlssFile{Relative="reshade-shaders/Shaders/ReShade.fxh",Backup=original,BeforeHash=DlssInstaller.HashFile(original)}};
   foreach(var file in files){string p=DlssInstaller.SafePath(game,file.Relative);Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllText(p,"inert old deployment fixture");file.InstalledHash=DlssInstaller.HashFile(p);}
   var record=new DlssRecord{Game=Path.GetFullPath(game),Backup=backup,State="组件已部署，等待游戏内验证",Time=DateTime.UtcNow.ToString("o"),Files=files};
   string json=new JavaScriptSerializer().Serialize(record),state=Path.Combine(data,"dlss-state",DlssInstaller.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToLowerInvariant())).Substring(0,16)+".json");
   Directory.CreateDirectory(Path.GetDirectoryName(state));File.WriteAllText(state,json);File.WriteAllText(Path.Combine(backup,"changes.json"),json);File.WriteAllText(Path.Combine(backup,"人工关闭DLSS5.txt"),"Legacy manual guide retained");return backup;
  }
  // Break caught: retiring installation also makes real old deployment journals impossible to restore.
  public static void Run(){
   string root=Path.Combine(LauncherTestPaths.Root,"retired-dlss-"+Guid.NewGuid().ToString("N")),game=Path.Combine(root,"中文游戏"),data=Path.Combine(root,"data");
   string backup=Seed(game,data);var installer=new DlssInstaller(game,data,true);
   Check(installer.Enabled&&DlssInstaller.Installed(game,data),"old deployment record remains discoverable");
   using(var locked=new FileStream(Path.Combine(game,"ReShade.ini"),FileMode.Open,FileAccess.Read,FileShare.None)){
    bool rejected=false;try{installer.Disable();}catch(IOException){rejected=true;}
    Check(rejected&&installer.Enabled&&File.Exists(Path.Combine(game,"opengl32.dll")),"locked old file refuses recovery without partial removal");
   }
   File.WriteAllText(Path.Combine(game,"dlss5-feed.cfg"),"user edit after deployment");
   installer.Disable();
   Check(!installer.Enabled&&!DlssInstaller.Installed(game,data)&&!File.Exists(Path.Combine(game,"opengl32.dll"))&&!File.Exists(Path.Combine(game,"nvngx_dlssnr.dll")),"recovery removes only journaled old injection files and state");
   Check(File.ReadAllText(Path.Combine(game,"reshade-shaders/Shaders/ReShade.fxh"))=="original header preserved","exact original shader file restored from old backup");
   Check(File.ReadAllText(Path.Combine(backup,"user-edits/dlss5-feed.cfg"))=="user edit after deployment","user changes retained before removal");
   Check(File.ReadAllText(Path.Combine(game,"UserNotes.txt"))=="keep me"&&File.ReadAllText(Path.Combine(game,"ProjectZomboid64.json")).Contains("28672m"),"unrelated game files and memory settings preserved");
   Check(File.ReadAllText(Path.Combine(backup,"人工关闭DLSS5.txt"))=="Legacy manual guide retained","legacy manual recovery evidence retained");
   installer.Disable();Check(!installer.Enabled,"repeat recovery is harmless");
   var callback=typeof(SteamWorkshop).GetMethod("SuccessResult");Check((bool)callback.Invoke(null,new object[]{(uint)108600,(ulong)3809306528,(uint)1,(ulong)3809306528}),"successful requested workshop callback accepted");
   Check(!(bool)callback.Invoke(null,new object[]{(uint)999,(ulong)3809306528,(uint)1,(ulong)3809306528})&&!(bool)callback.Invoke(null,new object[]{(uint)108600,(ulong)99,(uint)1,(ulong)3809306528})&&!(bool)callback.Invoke(null,new object[]{(uint)108600,(ulong)3809306528,(uint)15,(ulong)3809306528}),"wrong app item or error callback never marks download complete");
   Console.WriteLine("PASS retired DLSS filesystem recovery: "+root);
  }
 }
}
