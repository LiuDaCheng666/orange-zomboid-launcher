using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
namespace PZ3D {
 static class LauncherUiTests {
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  [STAThread] public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   string prefs=Path.Combine(LauncherTestPaths.Root,"orange-launcher-ui-"+Guid.NewGuid().ToString("N"));
   Program.PreviewMode=true;
   using(var f=new MainForm(prefs)){f.Show();Application.DoEvents();
    var tabs=f.Controls[0].Controls.OfType<TabControl>().Single();
    Check(tabs.TabPages.Cast<TabPage>().Any(p=>p.Name=="launcher-home"),"launcher opens on dedicated homepage");
    Check(tabs.SelectedTab.Name=="launcher-home","homepage selected at first open");
    var install=tabs.TabPages.Cast<TabPage>().Single(p=>p.Name=="launcher-install");
    Check(install.Controls.Count>0,"installation page retains original controls");
    foreach(string label in new[]{"Viewpoint 中文设置","光影列表","组件安装"}){
     var nav=f.Controls.Find("nav-"+label,true).Single() as Button;Check(nav!=null,"sidebar preserves "+label);nav.PerformClick();Check(tabs.SelectedTab.Text==label,"sidebar navigates to "+label);
    }
    var theme=f.Controls.Find("launcher-theme",true).Single() as Button;theme.PerformClick();Check(f.BackColor.GetBrightness()>0.7f,"theme toggles to pale blue");
    f.Size=new Size(1100,760);f.Show();Application.DoEvents();Check(!((bool)typeof(MainForm).GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f)),"startup has no automatic installation task");
    Check(f.Controls.Find("launcher-launch",true).Single().Width>=200,"launch action remains usable at 1100x760");Check(f.Controls.Find("nav-启动首页",true).Single().Parent.Controls.Cast<Control>().Count(c=>c.Name.StartsWith("nav-"))<=8,"sidebar keeps primary navigation compact");f.Close();
   }
   using(var f=new MainForm(prefs)){f.Show();Application.DoEvents();Check(f.BackColor.GetBrightness()>0.7f,"pale blue selection persisted across reopen");((Button)f.Controls.Find("launcher-theme",true).Single()).PerformClick();Check(f.BackColor.GetBrightness()<0.3f,"theme toggles back to graphite");f.Close();}
   using(var f=new MainForm(prefs))Check(f.BackColor.GetBrightness()<0.3f,"graphite selection persisted across reopen");
   string game=Path.Combine(prefs,"fixture-game");Directory.CreateDirectory(game);File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture, never executed");string json="{\"vmArgs\":[\"-Xmx8g\"]}";File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),json);File.WriteAllText(Path.Combine(prefs,"game-path.txt"),game);File.WriteAllText(Path.Combine(prefs,"startup-help-hidden.txt"),"hidden");Program.PreviewMode=false;
   using(var f=new MainForm(prefs)){f.Show();Application.DoEvents();Application.DoEvents();Check(!((bool)typeof(MainForm).GetField("busy",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f)),"normal opening with valid game does not start workshop download");Check(File.ReadAllText(Path.Combine(game,"ProjectZomboid64.json"))==json&&!File.Exists(Path.Combine(prefs,"workshop-paths.json")),"normal startup leaves game config and workshop cache unchanged");f.Close();}Program.PreviewMode=true;
   using(var c=new GoldCheckBox{Text="Disabled light theme",Size=new Size(260,32),Enabled=false,BackColor=Color.FromArgb(232,240,250),ForeColor=Color.FromArgb(33,54,78)})using(var bitmap=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(bitmap,c.ClientRectangle);int dark=0;for(int x=30;x<bitmap.Width;x++)for(int y=0;y<bitmap.Height;y++)if(bitmap.GetPixel(x,y).R<120)dark++;Check(dark>20,"disabled choices remain readable in pale blue theme");}
   Console.WriteLine("LAUNCHER UI TESTS PASSED");
  }
 }
}
