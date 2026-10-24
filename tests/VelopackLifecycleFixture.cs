using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
namespace PZ3D {
 // Compiled only by the isolated lifecycle test, never by a product build.
 internal static class VelopackLifecycleFixture {
  [STAThread]static int Main(string[] args){
   if(args.Length==3&&args[0]=="--velopack-lease")return ApplyLeaseWorker.Run(args[1],int.Parse(args[2]));
   if(args.Length==3&&args[0]=="--playtime-watch")return PlaytimeTracker.Watch(args[1],args[2]);
   if(args.Length==3&&args[0]=="--fixture-hold"){File.WriteAllText(args[1],Process.GetCurrentProcess().Id.ToString());Until(()=>File.Exists(args[2]),180);return 0;}
   return RunApplication(args);
  }
  static void Until(Func<bool> done,int seconds=30){DateTime end=DateTime.UtcNow.AddSeconds(seconds);while(!done()){if(DateTime.UtcNow>end)throw new Exception("Lifecycle fixture timeout");Thread.Sleep(50);}}
  [MethodImpl(MethodImplOptions.NoInlining)]static int RunApplication(string[] args){
   string root=Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));string state=Path.Combine(root,"OrangeLauncherRuntime");
   try{
    DistributionPaths.Bootstrap(args); // actual SDK handles install/obsolete/updated hooks and exits
    using(NativeApplyGuard.Instance(root)){
     if(AppVersion.Current=="1.0.1"){Directory.CreateDirectory(state);File.WriteAllText(Path.Combine(state,"restarted.txt"),AppVersion.Current);return 0;}
     if(args.Length<3||args[0]!="--fixture-update")throw new Exception("Unexpected fixture activation");
     string feed=args[1],game=args[2];bool reject=args.Length>3&&args[3]=="reject";
     var source=new AuthenticatedVelopackSource(CoreUpdateTrust.PublicKeyXml,AppVersion.Current,(url,t)=>File.OpenRead(Path.Combine(feed,url.EndsWith(".sig")?"velopack-manifest.sig":url.EndsWith(".json")?"velopack-manifest.json":VelopackFeed.AppId+"-1.0.1-full.nupkg")),DistributionPaths.Child("accepted-release.bin"));
     using(var updater=new VelopackUpdateBackend(source,Velopack.Locators.VelopackLocator.Current,true)){
      updater.Check();Until(()=>updater.Status().state!="Checking");if(updater.Status().state!="Available")throw new Exception(updater.Status().message);
      updater.Download();Until(()=>updater.Status().state!="Downloading");if(updater.Status().state!="Ready")throw new Exception(updater.Status().message);
      if(reject){try{updater.Apply();}catch(IOException e){Directory.CreateDirectory(state);File.WriteAllText(Path.Combine(state,"rejected.txt"),e.Message);return 0;}throw new Exception("Nested live process was not rejected");}
      string data=Path.Combine(state,"player"),nonce=Guid.NewGuid().ToString("N");LauncherNativeState.Write(Path.Combine(data,"playtime","launch-"+nonce+".json"),new PlaytimeRequest{game=game,nonce=nonce,created=DateTime.UtcNow.AddSeconds(-30).Ticks});
      string monitorImage=ManagedBackgroundWorker.PrepareDistribution(typeof(VelopackLifecycleFixture).Assembly.Location,CoreUpdateTrust.PublicKeyXml,DistributionPaths.ExecutingRoot(root,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
      using(var monitor=ManagedBackgroundWorker.Start("--playtime-watch",data,nonce)){File.WriteAllText(Path.Combine(state,"monitor-pid.txt"),monitor.Id.ToString());File.WriteAllText(Path.Combine(state,"monitor-image.txt"),monitorImage);}
      Until(()=>File.Exists(Path.Combine(data,"playtime","time.json")));Thread.Sleep(1100);
      updater.Apply();if(updater.Status().state!="Restarting")throw new Exception("No native handoff");File.WriteAllText(Path.Combine(state,"armed.txt"),AppVersion.Current);return 0;
     }
    }
   }catch(Exception e){Directory.CreateDirectory(state);File.WriteAllText(Path.Combine(state,"fixture-error.txt"),e.ToString());return 1;}
  }
 }
}
