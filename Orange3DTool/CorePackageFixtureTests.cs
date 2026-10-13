using System;
using System.Diagnostics;
using System.IO;
namespace PZ3D {
 // A real executable used only by Test-CorePackageBuild.ps1, never a release helper.
 static class CorePackageFixtureTests {
  public static int Main(string[] args){try{
   string self=Process.GetCurrentProcess().MainModule.FileName,root=Path.GetDirectoryName(self);
   if(root.IndexOf(Path.DirectorySeparatorChar+"TestFixtures"+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)<0)throw new IOException("Fixture entry requires isolated test root.");
   if(args.Length>0&&args[0]=="--complete-core-uninstall")return UninstallerProgram.Main(args);
   if(args.Length==1&&args[0]=="--fixture-uninstall"){UninstallerProgram.UninstallCore(root,self);return 0;}
   string web=ModernAssets.Prepare();if(!File.Exists(Path.Combine(web,"app.js")))throw new IOException("Sidecar Web missing.");
   if(BundledAssets.ReadOwnAsset("OrangeLaunchBridge.lua").Length==0||BundledAssets.ReadOwnAsset("OrangeShootPresentation.jar").Length==0)throw new IOException("Sidecar bridge missing.");
   Console.WriteLine("PASS: extracted fixture adopts signed root and loads owned sidecars");return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
 }
}
