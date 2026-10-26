using System;
using System.IO;
using System.Linq;
using System.Text;
namespace PZ3D {
 static class ServerTranslationBoundsTests {
  static int checks;
  static void Check(bool ok,string why){if(!ok)throw new Exception("FAIL: "+why);checks++;Console.WriteLine("PASS: "+why);}
  static void Put(string file,byte[] bytes){Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,bytes);}
  static void Text(string file,string text){Put(file,new UTF8Encoding(false).GetBytes(text));}
  static int ReadOnly(){
   string root=Path.Combine(Path.GetTempPath(),"ozl-translation-readonly");
   var e=new Engine(@"E:\Steam\steamapps\common\ProjectZomboid",Path.Combine(root,"user"),Path.Combine(root,"data"),true);
   string source=Path.Combine(e.Workshop,"3556540080","mods","B42ModTrans_CN_As1","42.21","media","lua","shared","Translate","CN","Sandbox.json");
   string before=DlssInstaller.HashFile(source);var output=ServerLaunchTranslations.Build(e,ServerLaunchTranslations.Ids);
   string target=Path.Combine(ServerLaunchTranslations.Root(e,"B42ModTrans_CN"),"42","media","lua","shared","Translate","CN","Sandbox.json");
   Check(output.ContainsKey(target)&&DlssInstaller.Hash(output[target])==before,"actual installed 4.74 MB Sandbox.json is copied exactly");
   Check(ServerLaunchTranslations.Ids.All(id=>output.ContainsKey(Path.Combine(ServerLaunchTranslations.Root(e,id),"42","mod.info"))),"both actually installed translation packs are prepared");
   Check(DlssInstaller.HashFile(source)==before&&!Directory.Exists(root),"actual-source verification creates no target files or player changes");
   Console.WriteLine("READONLY files="+output.Count+" bytes="+output.Sum(p=>(long)p.Value.Length)+" largest="+output.Max(p=>p.Value.Length));return 0;
  }
  public static int Main(string[] args){string root=Path.Combine(Path.GetTempPath(),"ozl-tb-"+Guid.NewGuid().ToString("N").Substring(0,8));try{
   if(args.Contains("--actual-readonly"))return ReadOnly();
   var e=new Engine(Path.Combine(root,"steamapps","common","ProjectZomboid"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);
   Text(e.Config,"{\"vmArgs\":[]}");
   string mod=Path.Combine(e.Workshop,"3556540080","mods","B42ModTrans_CN_As1"),version=Path.Combine(mod,"42"),file=Path.Combine(version,"media","lua","shared","Translate","CN","Sandbox.json");
   Text(Path.Combine(mod,"mod.info"),"id=B42ModTrans_CN\n");Text(Path.Combine(version,"mod.info"),"id=B42ModTrans_CN\n");
   byte[] content=Encoding.UTF8.GetBytes("{\"translation\":\""+new string('x',4737772)+"\"}");Put(file,content);
   byte[] original=Encoding.UTF8.GetBytes("VERSION = 1,\nmods\n{\n mod = B42ModTrans_CN,\n mod = OtherLocal,\n}\nmaps\n{\n}\n");Put(e.Mods,original);
   var request=new ProfileLaunchRequest{Kind="server",Host="127.0.0.1",Port=1,Username="synthetic"};
   LaunchProfiles.Stage(e,request);
   string overlay=Path.Combine(ServerLaunchTranslations.Root(e,"B42ModTrans_CN"),"42","media","lua","shared","Translate","CN","Sandbox.json");
   Check(File.ReadAllBytes(overlay).SequenceEqual(content),"normal current-sized translation does not block server staging and keeps every byte");
   Check(LaunchProfiles.Pending(e)&&File.ReadAllText(e.Mods).Contains("OZLServerTranslations_B42ModTrans_CN"),"server request and full text overlay are staged together");
   LaunchProfiles.Recover(e,true);
   Check(File.ReadAllBytes(e.Mods).SequenceEqual(original)&&!File.Exists(overlay)&&!LaunchProfiles.Pending(e),"recovery restores original selections and clears only owned translation files");
   Check(File.ReadAllBytes(file).SequenceEqual(content),"source translation pack remains unchanged");
   byte[] larger=new byte[16*1024*1024+1];Put(file,larger);var built=ServerLaunchTranslations.Build(e,new[]{"B42ModTrans_CN"});
   Check(built[overlay].Length==larger.Length,"a source larger than 16 MiB is still copied completely");
   using(var huge=new FileStream(file,FileMode.Create,FileAccess.Write)){huge.SetLength(64L*1024*1024+1);}
   LaunchProfiles.Stage(e,request);
   Check(LaunchProfiles.Pending(e)&&SaveProfiles.DefaultMods(File.ReadAllBytes(e.Mods)).Contains("B42ModTrans_CN"),"large optional translation still stages the server request with its original enabled mod");
   Check(!Directory.Exists(ServerLaunchTranslations.Root(e,"B42ModTrans_CN"))&&!File.ReadAllText(e.Mods).Contains("OZLServerTranslations_"),"capacity fallback never creates an incomplete derived translation mod");
   LaunchProfiles.Recover(e,true);Check(File.ReadAllBytes(e.Mods).SequenceEqual(original)&&!LaunchProfiles.Pending(e),"native-mod fallback recovers the exact original defaults");
   Put(file,content);
   string second=Path.Combine(e.Workshop,"3556544454","mods","B42Trans_CN_As1","42");Text(Path.Combine(second,"mod.info"),"id=B42Trans_CN\n");
   string hugeFile=Path.Combine(second,"media","lua","shared","Translate","CN","UI.json");Directory.CreateDirectory(Path.GetDirectoryName(hugeFile));using(var huge=new FileStream(hugeFile,FileMode.Create,FileAccess.Write)){huge.SetLength(64L*1024*1024+1);}
   Check(ServerLaunchTranslations.Build(e,ServerLaunchTranslations.Ids).Count==0,"a later large source cannot leave an earlier partial overlay set");
   Console.WriteLine("PASS "+checks+" translation size/staging/recovery checks");return 0;
  }catch(Exception error){Console.WriteLine(error);return 1;}finally{if(Directory.Exists(root))Directory.Delete(root,true);}}
 }
}
