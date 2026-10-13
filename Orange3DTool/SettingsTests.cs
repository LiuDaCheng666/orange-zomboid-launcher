using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
namespace PZ3D {
 static class SettingsTests {
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
  public static void Run(){
   Type type=typeof(Engine).Assembly.GetType("PZ3D.ViewpointSettings");Check(type!=null,"safe Viewpoint settings store exists");
   string root=Path.Combine(LauncherTestPaths.Root,"orange-settings-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string file=Path.Combine(root,"viewpoint-live.properties");string original="# author comment\r\nmouse.sensitivity=1.0\r\nkeys.firstPerson=O\r\nminecraft.some\\:pack.OPTION=unchanged\\u4E2D\r\n";File.WriteAllText(file,original);
   Func<object> load=()=>Activator.CreateInstance(type,new object[]{root,Path.Combine(root,"backups")});object store=load();
   var entries=(Dictionary<string,string>)type.GetProperty("Values").GetValue(store,null);Check(entries["viewpoint-live.properties|minecraft.some:pack.OPTION"]=="unchanged中","Java property escaped keys and Unicode values decoded correctly");
   var updates=new Dictionary<string,string>{{"viewpoint-live.properties|mouse.sensitivity","1.5"}};
   type.GetMethod("Save").Invoke(store,new object[]{updates,false});string saved=File.ReadAllText(file);Check(saved.Contains("mouse.sensitivity=1.5")&&saved.Contains("minecraft.some\\:pack.OPTION=unchanged\\u4E2D")&&saved.Contains("# author comment"),"save changes only selected parameter while preserving unknown source");
   Check(Directory.GetFiles(Path.Combine(root,"backups"),"viewpoint-live.properties",SearchOption.AllDirectories).Length==1,"original file backed up before editing");
   bool encodingRejected=false;try{new PropertyDocument(new byte[]{35,32,255,10});}catch(System.Text.DecoderFallbackException){encodingRejected=true;}Check(encodingRejected,"unsupported encoding rejected instead of silently corrupting original bytes");
   var continued=new PropertyDocument(System.Text.Encoding.UTF8.GetBytes("# keep\nkeys.firstPerson=SHI\\\n FT+O\nunknown=untouched\n"));Check(continued.Values["keys.firstPerson"]=="SHIFT+O","Java property continuation decoded correctly");
   var commentSlash=new PropertyDocument(System.Text.Encoding.UTF8.GetBytes("# comment with slash \\\nmouse.sensitivity=1.0\n"));Check(commentSlash.Values.ContainsKey("mouse.sensitivity"),"comment ending with backslash never consumes next setting");
   store=load();File.AppendAllText(file,"\nexternal.setting=true\n");bool stale=false;try{type.GetMethod("Save").Invoke(store,new object[]{updates,false});}catch(TargetInvocationException){stale=true;}Check(stale&&File.ReadAllText(file).Contains("external.setting=true"),"external changes block stale overwrite");
   store=load();string before=File.ReadAllText(file);bool running=false;try{type.GetMethod("Save").Invoke(store,new object[]{updates,true});}catch(TargetInvocationException){running=true;}Check(running&&File.ReadAllText(file)==before,"running game blocks all writes");
   bool invalid=false;try{type.GetMethod("Save").Invoke(store,new object[]{new Dictionary<string,string>{{"viewpoint-live.properties|mouse.sensitivity","NaN"}},false});}catch(TargetInvocationException){invalid=true;}Check(invalid&&File.ReadAllText(file)==before,"invalid numeric input rejected without mutation");
   bool unknown=false;try{type.GetMethod("Save").Invoke(store,new object[]{new Dictionary<string,string>{{"viewpoint-live.properties|minecraft.some:pack.OPTION","changed"}},false});}catch(TargetInvocationException){unknown=true;}Check(unknown,"unverified shader option is read-only");
   Program.PreviewMode=true;using(var form=new MainForm(Path.Combine(root,"preferences"))){var flags=BindingFlags.NonPublic|BindingFlags.Instance;var field=typeof(MainForm).GetField("settingsGrid",flags);Check(field!=null&&field.GetValue(form)!=null,"Chinese settings page provides an editable grid");
    store=load();typeof(MainForm).GetField("settingsStore",flags).SetValue(form,store);var edits=(Dictionary<string,string>)typeof(MainForm).GetField("settingsEdits",flags).GetValue(form);edits["viewpoint-live.properties|mouse.sensitivity"]="2.0";File.AppendAllText(file,"another.external=true\n");typeof(MainForm).GetMethod("PollSettings",flags).Invoke(form,null);Check((bool)typeof(MainForm).GetField("settingsConflict",flags).GetValue(form)&&edits.Count==1,"automatic sync keeps unsaved edits and signals external conflict");
   }
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
 }
}
