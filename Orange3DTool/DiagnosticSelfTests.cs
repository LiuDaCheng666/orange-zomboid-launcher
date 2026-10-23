#if OZL_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using ProgressBar = PZ3D.OrangeProgress;
using Button = PZ3D.GoldButton;
using CheckBox = PZ3D.GoldCheckBox;

namespace PZ3D {
 static class Test {
  public static void Audit(){
   string root=Path.Combine(LauncherTestPaths.Root,"pz3d-audit-"+Guid.NewGuid().ToString("N"));string game=Path.Combine(root,"steamapps","common","ProjectZomboid");Directory.CreateDirectory(game);var e=new Engine(game,Path.Combine(root,"User"),Path.Combine(root,"Data"),true);e.PresetSource=Path.Combine(root,"Presets");Directory.CreateDirectory(e.PresetSource);
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx28672m\"]}");Directory.CreateDirectory(e.BuddyLib);File.WriteAllText(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"),"fixture");File.WriteAllText(Path.Combine(e.BuddyLib,"zbNative.dll"),"fixture");string vp=Path.Combine(e.Workshop,"3809306528","mods","Viewpoint");Directory.CreateDirectory(vp);File.WriteAllText(Path.Combine(vp,"mod.info"),"id=Viewpoint\n");
   string preset=Path.Combine(e.PresetSource,"Preset.zip");using(var a=ZipFile.Open(preset,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("shaders/final.fsh").Open()))w.Write("fixture");}
   var steps=new List<int>();e.Progress=(v,t)=>steps.Add(v);e.Switch(true,false);Assert(steps.Count==9&&steps.Take(8).All(v=>v==-1)&&steps.Last()==100,"phase labels have unknown progress until actual installation completes");Assert(File.Exists(Path.Combine(e.ShaderDir,"Preset.zip")),"preset deployed into actual Viewpoint directory");
   var report=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(e.LastBackup,"changes.json")));var entries=((System.Collections.IEnumerable)report["files"]).Cast<Dictionary<string,object>>().ToArray();Assert(entries.Length==6&&(string)report["state"]=="已完成","audit includes shader and completed state");Assert(entries[0]["backup"]!=null&&File.Exists((string)entries[0]["backup"])&&((string)entries[0]["beforeSHA256"]).Length==64&&((string)entries[0]["afterSHA256"]).Length==64,"audit backup mapping and SHA256 evidence");Assert(File.ReadAllText(Path.Combine(e.LastBackup,"人工关闭3D.txt")).Contains(e.Config),"manual guide contains exact game paths");
   byte[] original=File.ReadAllBytes(Path.Combine(e.ShaderDir,"Preset.zip"));e.Switch(false,false);Assert(File.ReadAllBytes(Path.Combine(e.ShaderDir,"Preset.zip")).SequenceEqual(original),"disable retains shader packages");e.Switch(true,false);Assert(File.ReadAllBytes(Path.Combine(e.ShaderDir,"Preset.zip")).SequenceEqual(original),"reinstall preserves existing preset");
   string invalid=Path.Combine(root,"invalid.zip");using(var a=ZipFile.Open(invalid,ZipArchiveMode.Create))a.CreateEntry("nested/shaders/final.fsh");bool rejected=false;try{e.Import(invalid);}catch{rejected=true;}Assert(rejected&&!File.Exists(Path.Combine(e.ShaderDir,"invalid.zip")),"invalid shader layout rejected without copying");
   string failureGame=Path.Combine(root,"FailureGame");Directory.CreateDirectory(failureGame);var failureEngine=new Engine(failureGame,Path.Combine(root,"FailureUser"),Path.Combine(root,"FailureData"),true);string before="{\"vmArgs\":[\"-Xmx28672m\",\"-agentlib:zbNative\"]}";File.WriteAllText(failureEngine.Config,before);string loader=Path.Combine(failureGame,"ZombieBuddy.jar");File.WriteAllText(loader,"restore-me");Directory.CreateDirectory(Path.GetDirectoryName(failureEngine.Mods));File.WriteAllText(failureEngine.Mods,"mods\r\n{\r\n mod = ZombieBuddy,\r\n mod = Viewpoint,\r\n mod = Ordinary,\r\n}\r\nmaps\r\n{\r\n}\r\n",new UTF8Encoding(true));byte[] configBefore=File.ReadAllBytes(failureEngine.Config),loaderBefore=File.ReadAllBytes(loader),modsBefore=File.ReadAllBytes(failureEngine.Mods);FileStream heldMods=null;bool reachedWrite=false;failureEngine.Progress=(v,text)=>{if(text=="更新客户端默认模组列表"){heldMods=new FileStream(failureEngine.Mods,FileMode.Open,FileAccess.Read,FileShare.None);reachedWrite=true;}else if(text=="操作失败，正在恢复本阶段原文件"&&heldMods!=null){heldMods.Dispose();heldMods=null;}};bool failed=false;try{failureEngine.Switch(false,false);}catch{failed=true;}finally{if(heldMods!=null)heldMods.Dispose();}Assert(failed&&reachedWrite&&File.ReadAllBytes(failureEngine.Config).SequenceEqual(configBefore)&&File.ReadAllBytes(loader).SequenceEqual(loaderBefore)&&File.ReadAllBytes(failureEngine.Mods).SequenceEqual(modsBefore),"post-backup mod write failure rolls back exact config loader and mod bytes");Assert(failureEngine.LastBackup!=null&&File.ReadAllText(Path.Combine(failureEngine.LastBackup,"changes.json")).Contains("失败，已回滚")&&File.Exists(Path.Combine(failureEngine.LastBackup,"人工关闭3D.txt")),"failed operation retains report and manual guide");Console.WriteLine("AUDIT TESTS PASSED; fixture retained at "+root);
  }
  public static void Extras(){
   string root=Path.Combine(LauncherTestPaths.Root,"pz3d-extra-"+Guid.NewGuid().ToString("N"));
   string game=Path.Combine(root,"steamapps","common","ProjectZomboid"),user=Path.Combine(root,"User");Directory.CreateDirectory(game);
   var e=new Engine(game,user,Path.Combine(root,"Data"),true);
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Xmx12g\"],\"windows\":{\"10\":{\"vmArgs\":[\"-javaagent:ZombieBuddy.jar\",\"-XX:+UseZGC\"]}}}");
   e.Switch(false,false);string disabled=File.ReadAllText(e.Config);Assert(!disabled.Contains("zbNative")&&!disabled.Contains("ZombieBuddy.jar")&&disabled.Contains("UseZGC"),"disable OS-specific agent too");
   e.Switch(false,false);Assert(File.ReadAllText(e.Config)==disabled,"idempotent disable");
   string bat=Path.Combine(game,"ProjectZomboid64.bat");File.WriteAllText(bat,"SET _JAVA_OPTIONS=-agentlib:zbNative");bool refused=false;try{e.Switch(false,false);}catch{refused=true;}Assert(refused,"detect independent BAT loader");
   string zip=Path.Combine(root,"example.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("shaders/final.fsh").Open())){w.Write("void main() {}");}}
   e.Import(zip);Assert(File.Exists(Path.Combine(user,"viewpoint-shaderpacks","example.zip")),"shader ZIP import");bool duplicate=false;try{e.Import(zip);}catch{duplicate=true;}Assert(duplicate,"shader import does not overwrite");
   Console.WriteLine("EXTRA TESTS PASSED");
  }
  static void Assert(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
  public static void Run(){string root=Path.Combine(LauncherTestPaths.Root,"pz3d-test-"+Guid.NewGuid().ToString("N"));string game=Path.Combine(root,"steamapps","common","ProjectZomboid"),user=Path.Combine(root,"User"),data=Path.Combine(root,"Data");Directory.CreateDirectory(game);Directory.CreateDirectory(Path.Combine(user,"mods"));var e=new Engine(game,user,data,true);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx28672m\",\"-Dexample=true\"],\"windows\":{\"10\":{\"vmArgs\":[\"-XX:+UseZGC\"]}},\"custom\":42}");File.WriteAllText(e.Mods,"VERSION = 1,\nmods\n{\n    mod = OtherMod,\n}\nmaps\n{\n}\n");string before=File.ReadAllText(e.Config);bool rejected=false;try{e.Switch(true,false);}catch{rejected=true;}Assert(rejected&&File.ReadAllText(e.Config)==before,"missing dependencies do not mutate");Directory.CreateDirectory(e.BuddyLib);File.WriteAllText(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"),"fake-jar");File.WriteAllText(Path.Combine(e.BuddyLib,"zbNative.dll"),"fake-dll");string vp=Path.Combine(e.Workshop,"3809306528","mods","Viewpoint","42");Directory.CreateDirectory(vp);File.WriteAllText(Path.Combine(vp,"mod.info"),"id=Viewpoint\n");e.Switch(true,false);Assert(e.Enabled,"enable");e.Switch(true,false);Assert(Regex.Matches(File.ReadAllText(e.Mods),"mod = Viewpoint,").Count==1,"idempotent enable");e.Switch(false,false);Assert(!e.Enabled && !File.Exists(Path.Combine(game,"ZombieBuddy.jar"))&&!File.Exists(Path.Combine(game,"zbNative.dll")),"disable both and stash loader");Assert(File.ReadAllText(e.Mods).Contains("OtherMod")&&!File.ReadAllText(e.Mods).Contains("Viewpoint"),"preserve other mods");Assert(File.ReadAllText(e.Config).Contains("-Xmx28672m")&&File.ReadAllText(e.Config).Contains("-XX:+UseZGC")&&File.ReadAllText(e.Config).Contains("custom"),"preserve memory GC and extra settings");Assert(Directory.GetDirectories(Path.Combine(data,"backups")).Length==3,"backups created");Console.WriteLine("ALL PASSED; fixture retained at "+root);}
 }
}
#endif
