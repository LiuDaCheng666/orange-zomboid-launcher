using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace PZ3D {
 static class PortableTests {
  static void Check(bool result,string text){if(!result)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);}
  static System.Collections.Generic.IEnumerable<Control> Descendants(Control root){yield return root;foreach(Control c in root.Controls)foreach(Control d in Descendants(c))yield return d;}
  public static void Run(){
   Program.PreviewMode=true;using(var form=new MainForm(Path.Combine(LauncherTestPaths.Root,"orange-help-test-"+Guid.NewGuid().ToString("N")))){var tabs=Descendants(form).OfType<TabControl>().Single();var help=tabs.TabPages.Cast<TabPage>().SingleOrDefault(p=>p.Text=="使用帮助");Check(help!=null,"offline player help is built into application tab");var chapters=Descendants(help).OfType<ListBox>().Single();var body=Descendants(help).OfType<TextBox>().Single();Check(chapters.Items.Count>=7&&body.Text.Length>100,"help shows usable first-run instructions immediately");chapters.SelectedItem=HelpContent.Titles[2];string shader=body.Text;chapters.SelectedItem=HelpContent.Titles[4];Check(body.Text!=shader&&body.Text.Contains("Home")&&body.Text.Contains("尚未验证"),"chapter selection shows actual DLSS instructions and limitations");}
   var bundle=typeof(Engine).Assembly.GetType("PZ3D.BundledAssets");Check(bundle!=null,"standalone assets manager exists");
  }
  public static void Bundle(){var type=typeof(Engine).Assembly.GetType("PZ3D.BundledAssets");Check(type!=null,"embedded bundle manager available");string data=Path.Combine(LauncherTestPaths.Root,"orange-portable-bundle-"+Guid.NewGuid().ToString("N"));string folder=(string)type.GetMethod("Ensure").Invoke(null,new object[]{data});string presets=Path.Combine(folder,"PresetShaders");string[] packs=Directory.GetFiles(presets,"*.zip");Check(packs.Length==0||packs.Length==11,"public build omits third-party packs; private build keeps eleven provided packs");foreach(string p in packs)Engine.ValidateShader(p);Check(File.Exists(Path.Combine(folder,"ThirdPartyNotices","NOTICE.txt")),"copyright notices available without adjacent files");if(packs.Length>0){string originalHash=DlssInstaller.HashFile(packs[0]);DateTime originalStamp=File.GetLastWriteTimeUtc(packs[0]);type.GetMethod("Ensure").Invoke(null,new object[]{data});Check(File.GetLastWriteTimeUtc(packs[0])==originalStamp,"valid cached preset not rewritten on subsequent launch");File.WriteAllText(packs[0],"corrupt fixture");type.GetMethod("Ensure").Invoke(null,new object[]{data});Check(DlssInstaller.HashFile(packs[0])==originalHash,"damaged owned cache restored from embedded resource");}Console.WriteLine("PORTABLE EMBEDDED BUNDLE VERIFIED: "+folder+" · packs="+packs.Length);}
 }
}
