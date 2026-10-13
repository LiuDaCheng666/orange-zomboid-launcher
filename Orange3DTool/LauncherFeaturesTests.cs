using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace PZ3D {
 static class LauncherFeaturesTests {
  static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  public static void Run(){
   var type=typeof(Engine).Assembly.GetType("PZ3D.LauncherLaunch");Check(type!=null,"Steam launcher preflight policy exists");
   string root=Path.Combine(LauncherTestPaths.Root,"orange-launch-preflight-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string game=Path.Combine(root,"game");Directory.CreateDirectory(game);var e=new Engine(game,Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Data);
   File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx8g\"]}");
   var reason=type.GetMethod("Reason");
   Check((string)reason.Invoke(null,new object[]{e,false,false})=="","valid vanilla game can launch without forcing 3D");
   Check(((string)reason.Invoke(null,new object[]{e,true,false})).Contains("任务"),"busy installation blocks game launch");
   Check(((string)reason.Invoke(null,new object[]{e,false,true})).Contains("运行"),"running game prevents duplicate launch");
   File.WriteAllText(Path.Combine(e.Data,"workshop-rebuild.pending"),"failed");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("工坊"),"unfinished destructive redownload cannot be launched as ready");File.Delete(Path.Combine(e.Data,"workshop-rebuild.pending"));
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("Buddy"),"configured native loader requires both deployed files");
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-javaagent:OrangeShootPresentation.jar\"]}");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("旧"),"known instrument-breaking legacy agent blocks launch with repair hint");
   File.WriteAllText(e.Config,"broken");Check(((string)reason.Invoke(null,new object[]{e,false,false})).Contains("配置"),"malformed launch config is diagnosed before Steam launch");
   foreach(string bad in new[]{"{\"vmArgs\":[123]}","{\"vmArgs\":[\"-Xmxgarbage\"]}","{\"vmArgs\":[],\"windows\":{\"10\":7}}","{\"vmArgs\":[\"-javaagent:unknown.jar\"]}","{\"vmArgs\":[\"-javaagent:OtherZombieBuddy.jar\"]}","{\"vmArgs\":[\"-javaagent:ZombieBuddy.jar\"]}","{\"vmArgs\":[\"-Xmx999999999999999999g\"]}"}){File.WriteAllText(e.Config,bad);Check((string)reason.Invoke(null,new object[]{e,false,false})!="","unsafe or malformed launch arguments block Steam launch: "+bad);}
   string original="{\"vmArgs\":[\"-Xmx4096g\",\"-Dcustom=keep\"]}";File.WriteAllText(e.Config,original);string prefs=Path.Combine(root,"legacy-prefs");Directory.CreateDirectory(prefs);File.WriteAllText(Path.Combine(prefs,"game-path.txt"),game);bool preview=Program.PreviewMode;Program.PreviewMode=false;
   try{using(var form=new MainForm(prefs)){var input=form.Controls.Find("launcher-memory-number",true).SingleOrDefault() as NumericUpDown;Check(input!=null&&input.Value==4096,"legacy page automatically reads actual existing heap without truncation");Check(File.ReadAllText(e.Config)==original,"legacy automatic memory read does not apply or rewrite configuration");}}finally{Program.PreviewMode=preview;}
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
