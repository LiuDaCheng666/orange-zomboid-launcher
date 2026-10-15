using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace PZ3D {
 static class BrandingTests {
  static int passed;
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
  [STAThread] public static int Main(){try{
   LauncherRuntime.Initialize();
   using(var form=new ModernForm(Path.Combine(LauncherTestPaths.Root,"Web"),new[]{"--modern-preview",Path.Combine(LauncherTestPaths.Root,"unused.png")}))Check(form.Text=="OZL","native preview window identifies OZL without accessing real user data");
   using(var form=new InstallerForm(new InstallerDestination{Root=Path.Combine(LauncherTestPaths.Root,"brand-install")},(root,p)=>{throw new Exception("read-only branding fixture must not install");},r=>{throw new Exception("must not launch");})){
    Check(form.Text=="安装 OZL · "+AppVersion.Current,"installer title identifies OZL and current version");
    Check(form.Controls.Cast<Control>().SelectMany(All).Any(c=>c.Text=="OZL"),"installer visible heading identifies OZL");
   }
   var info=System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(BrandingTests).Assembly.Location);
   Check(info.ProductName=="Orange zombiod launcher"&&info.FileDescription=="OZL","executable Explorer metadata exposes exact requested full name and display name");
   Console.WriteLine("BRANDING: "+passed+" PASS");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
  static System.Collections.Generic.IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in All(child))yield return nested;}
 }
}
