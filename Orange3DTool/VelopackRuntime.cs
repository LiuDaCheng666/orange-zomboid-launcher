using System;
using Velopack;
using Velopack.Locators;
namespace PZ3D {
 internal static class VelopackRuntime {
  internal static void Run(string[] args){
   VelopackApp.Build().SetArgs(args).SetAutoApplyOnStartup(false).Run();
   var locator=VelopackLocator.Current;
   if(locator.CurrentlyInstalledVersion==null||locator.AppId!=VelopackFeed.AppId||locator.CurrentlyInstalledVersion.ToString()!=AppVersion.Current)throw new InvalidOperationException("此版本需要完整 Velopack 安装或便携分发包。");
   DistributionPaths.RuntimeRoot=DistributionPaths.Resolve(locator.RootAppDir,locator.AppContentDir,locator.IsPortable,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
   DistributionPaths.InstallationRoot=locator.RootAppDir;
   DistributionPaths.ExecutingRoot(locator.RootAppDir,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
  }
 }
}
