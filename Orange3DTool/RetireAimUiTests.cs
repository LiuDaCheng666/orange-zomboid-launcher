using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace PZ3D {
 static class RetireAimUiTests {
  static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
  static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  [STAThread] public static int Main(){try{Run();return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}
  static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
  static object Call(MainForm form,string method){return typeof(MainForm).GetMethod(method,Flags).Invoke(form,null);}
  static void Set(MainForm form,string field,object value){typeof(MainForm).GetField(field,Flags).SetValue(form,value);}
  public static void Run(){
   bool preview=Program.PreviewMode;Program.PreviewMode=true;
   try{
    string root=Path.Combine(LauncherTestPaths.Root,"retire-aim-ui-"+Guid.NewGuid().ToString("N"));
    using(var form=new MainForm(Path.Combine(root,"preferences"))){
     var buttons=(Control)typeof(MainForm).GetField("buttons",Flags).GetValue(form);var choices=buttons.Parent.Controls.Cast<Control>().OfType<FlowLayoutPanel>().First(c=>c!=buttons);
     Check(!Descendants(choices).OfType<ComboBox>().Any()&&!Descendants(choices).OfType<TextBox>().Any(),"classic component choices expose no local aiming mode or fallback shortcut");
     Check(Descendants(choices).Any(c=>c.Text.Contains("Ctrl+V")&&c.Text.Contains("右键")),"classic component choice explains author-native gun controls");
     var tabs=Descendants(form).OfType<TabControl>().Single();typeof(MainForm).GetMethod("AddShootClientPage",Flags).Invoke(form,new object[]{tabs});var gunPage=tabs.TabPages[tabs.TabPages.Count-1];
     Check(!Descendants(gunPage).OfType<Button>().Any(b=>b.Text.Contains("桥接")),"classic gun page has no launcher bridge installation or toggle buttons");
     Check(!Descendants(gunPage).Any(c=>c.Text.Contains("原版/扩展")||c.Text.Contains("orange.ads")),"classic gun page does not advertise the retired local patch");
     var engine=Fixture(root);Set(form,"operationEngine",engine);Set(form,"componentShoot",true);Set(form,"operationModels",false);
     string author=Path.Combine(InstallWorkflow.Source(engine,ShootClient.Item),"mods",ShootClient.Id,"42","media","java","client","ISeeThem.jar"),calibration=Path.Combine(engine.User,"iseethem.properties");byte[] authorBytes=File.ReadAllBytes(author),calibrationBytes=File.ReadAllBytes(calibration);
     string result=Convert.ToString(Call(form,"InstallComponents"));
     Check(!result.Contains("[失败]")&&ShootClient.Enabled(engine),"classic install enables the author gun component");
     Check(!File.Exists(Path.Combine(engine.Data,"aim-fallback.json"))&&!File.Exists(PresentationBridge.PropertiesPath(engine))&&!File.Exists(Path.Combine(Path.GetDirectoryName(PresentationBridge.PropertiesPath(engine)),PresentationBridge.Name))&&!File.ReadAllText(engine.Mods).Contains("mod = OrangeShootPresentation,"),"classic install retires local aim artifacts and preferences without recreating them");
     Check(File.ReadAllBytes(author).SequenceEqual(authorBytes)&&File.ReadAllBytes(calibration).SequenceEqual(calibrationBytes)&&File.ReadAllText(engine.Mods).Contains("mod = Other,"),"classic install preserves author JAR, calibration and unrelated mod membership");
     Call(form,"EnableShootClient");Check(!File.Exists(Path.Combine(engine.Data,"aim-fallback.json"))&&!File.Exists(PresentationBridge.PropertiesPath(engine)),"classic direct author enable never recreates local aim settings or bridge");
    }
   }finally{Program.PreviewMode=preview;}
  }
  static Engine Fixture(string root){
   var engine=new Engine(Path.Combine(root,"steamapps","common","ProjectZomboid"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(engine.Game);Directory.CreateDirectory(Path.GetDirectoryName(engine.Mods));Directory.CreateDirectory(engine.Data);
   File.WriteAllText(engine.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Xmx8192m\"]}");File.WriteAllText(engine.Mods,"VERSION = 1,\nmods\n{\n mod = ZombieBuddy,\n mod = Viewpoint,\n mod = Other,\n mod = OrangeShootPresentation,\n}\nmaps\n{\n map = KeepMap,\n}\n");
   File.WriteAllText(Path.Combine(engine.Game,"ZombieBuddy.jar"),"fixture");File.WriteAllText(Path.Combine(engine.Game,"zbNative.dll"),"fixture");
   string view=Path.Combine(engine.Workshop,"3809306528","mod.info");Directory.CreateDirectory(Path.GetDirectoryName(view));File.WriteAllText(view,"id=Viewpoint\n");
   string source=Path.Combine(engine.Workshop,ShootClient.Item.ToString()),info=Path.Combine(source,"mods",ShootClient.Id,"common","mod.info"),jar=Path.Combine(source,"mods",ShootClient.Id,"42","media","java","client","ISeeThem.jar");Directory.CreateDirectory(Path.GetDirectoryName(info));Directory.CreateDirectory(Path.GetDirectoryName(jar));File.WriteAllText(info,"id=ShootToThrillMultiplayer\n");File.WriteAllText(jar,"untouched author fixture");File.WriteAllText(Path.Combine(engine.User,"iseethem.properties"),"author.calibration=keep\n");
   string addon=Path.Combine(engine.User,"mods",PresentationBridge.Id,"42"),properties=PresentationBridge.PropertiesPath(engine);Directory.CreateDirectory(Path.GetDirectoryName(properties));File.WriteAllText(Path.Combine(addon,"mod.info"),"name=Orange local aim fallback\nid=OrangeShootPresentation\njavaPkgName=orange.ads\njavaJarFile=media/java/client/OrangeShootPresentation.jar\n");File.WriteAllText(Path.Combine(Path.GetDirectoryName(properties),PresentationBridge.Name),"retired fixture");File.WriteAllText(properties,"aim.mode=native\naim.key=66\n");File.WriteAllText(Path.Combine(engine.Data,"aim-fallback.json"),"{\"StartNative\":false,\"KeyText\":\"CTRL+F8\"}");return engine;
  }
 }
}
