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
 static class DiagnosticProgram {
  [STAThread] internal static int Main(string[] args) {
   LauncherRuntime.Initialize();
   ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
   if(args.Length==2&&(args[0]=="--buddy-crash"||args[0]=="--buddy-steam-crash"))return BuddyTakeoverTests.Main(args);
   if(args.Length>=2&&args[0]=="--check-workshop"){try{var engine=new Engine(args[1],Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Zomboid"),LauncherTestPaths.Root);var plan=WorkshopUpdate.Check(engine,InstallWorkflow.Requested(true),System.Threading.CancellationToken.None);Console.WriteLine("READ_ONLY_CHECK: installed="+plan.Paths.Count+" needed="+plan.Needed.Count+" nativeSteamLoaded="+System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Any(m=>m.ModuleName.Equals("steam_api64.dll",StringComparison.OrdinalIgnoreCase)));return plan.Needed.Count==0?0:2;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
   if(args.Length>=2 && args[0]=="--subscribe-workshop"){try{var paths=SteamWorkshop.Fetch(args[1],InstallWorkflow.Requested(args.Contains("--models")),(v,t)=>Console.WriteLine(t),System.Threading.CancellationToken.None,420,true);foreach(var p in paths)Console.WriteLine("Subscribed: "+p.Key+"="+p.Value);return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
   if(args.Contains("--verify-bundle")){try{PortableTests.Bundle();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
   if(args.Length>=2 && args[0]=="--verify-cache"){try{FeatureTests.Real(args[1]);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
   if(args.Length>=2 && args[0]=="--prepare-dlss"){try{Console.WriteLine(ComponentCache.Prepare(args[1],(v,t)=>Console.WriteLine(v+"% "+t),System.Threading.CancellationToken.None));return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
   if(args.Length>=2 && args[0]=="--fetch-workshop"){try{var paths=SteamWorkshop.Fetch(args[1],InstallWorkflow.Requested(args.Contains("--models")),(v,t)=>Console.WriteLine(v+"% "+t),System.Threading.CancellationToken.None,420);foreach(var p in paths)Console.WriteLine(p.Key+"="+p.Value);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
if(args.Contains("--self-test")) { try { if(ServerWorkshopTests.Main()!=0)throw new Exception("服务器工坊回归失败"); CoreComponentsTests.Run(); CatalogCacheTests.Run(); ServerLaunchPolicyTests.Run(); LaunchTransportTests.Run(); AimBridgeRetirementTests.Run(); ConfigurationRuntimeTests.Run(); BuddyTakeoverTests.Run(); SteamLaunchOptionsTests.Run(); BuddyLaunchArgumentsTests.Run(); LauncherNativeServicesTests.Run(); GameFavoritesTests.Run(); FullLauncherIntegrationTests.Run(); ModConfigurationsTests.Run(); SteamLauncherServicesTests.RunUnit(); ConcurrentSnapshotTests.Run(); JournalPrivacyTests.Run(); PhaseProgressTests.Run(); SteamUnavailableTests.Run(); PlaytimeDayTests.Run(); RoofRemovalTests.Run(); ServerRulesTests.Run(); AddonTests.Run(); AddonTests.Resolver(); ComponentEvidenceTests.Run(); SettingsChineseTests.Run(); GameVersionProbeTests.Run(); JavaApprovalsTests.Run(); SaveProfilesTests.Run(); if(ServerProfilesTests.Main()!=0)throw new Exception("服务器配置回归失败"); LaunchProfilesTests.Run(); PortableDeploymentTests.Run(); GameProcessGuardTests.Run(); HardwareProbeTests.Run(); ModernHostTests.Run(); ModernHostTests.QueuedCancel(); ModernControllerTests.Run(); LocalAddonTests.Run(); LauncherFeaturesTests.Run(); MemoryPolicyTests.Run(); LauncherMaintenanceTests.Run(); LauncherWorkshopRebuildTests.Run(); LauncherUiTests.Run(); ComponentBatchTests.Run(); ReticleSettingsTests.Run(); Test.Run(); Test.Extras(); Test.Audit(); FeatureTests.Run(); ChinesePatchTests.Run(); UiUpdateTests.Run(); PortableTests.Run(); WorkshopSubscriptionTests.Run(); SteamSubscriptionWaitTests.Run(); StatusTests.Run(); SettingsTests.Run(); ShaderTests.Run(); UpdateTests.Run(); DiscoveryTests.Run(); ShootClientTests.Run(); PresentationBridgeTests.Run(); return 0; } catch(Exception e) { Console.WriteLine(e); return 1; } }
   if(args.Length>=2 && (args[0]=="--preview" || args[0]=="--preview-help")) {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>=2 && args[0]=="--preview"){Program.PreviewMode=true;int index=0;if(args.Length>2)int.TryParse(args[2],out index);Func<string,string> option=key=>{int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:null;};using(var f=new MainForm()){f.PreviewLauncher(args[1],index,args.Contains("--busy"),option("--theme"),option("--size"),option("--page"));}return 0;}
   if(args.Length>=2&&args[0]=="--preview-help"){Program.PreviewMode=true;using(var form=new MainForm())form.PreviewHelp(args[1]);return 0;}return 0;
   }
   return Program.Main(args);
  }
 }
}
#endif
