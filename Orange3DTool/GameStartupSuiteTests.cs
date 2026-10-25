using System;
using System.Linq;
using System.Windows.Forms;
namespace PZ3D {
 static class GameStartupSuiteTests {
  [STAThread] public static int Main(string[] args){
   if(args.Contains("--bridge-fixture"))return GameStartupBridgeTests.Main(args);
   if(args.Contains("--sentinel"))return GameStartupWindowTests.Main(args);
   if(args.Length>0&&args[0]=="--workshop-worker")return ModernControllerTests.Main(args);
   try{
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    GameStartupPresentationTests.Run();GameStartupPresenterTests.Run();
    if(GameStartupMonitorTests.Main()!=0)throw new Exception("Startup monitor regression failed");
    if(GameStartupBridgeTests.Main(new string[0])!=0)throw new Exception("Startup bridge regression failed");
    if(GameStartupWindowTests.Main(new string[0])!=0)throw new Exception("Startup full-screen view regression failed");
    LaunchProfilesTests.Run();LaunchTransportTests.Run();LaunchPreflightTests.Run();MemoryPolicyTests.Run();ModernControllerTests.Run();
    Console.WriteLine("ALL GAME STARTUP AND RELATED REGRESSIONS PASSED");return 0;
   }catch(Exception error){Console.WriteLine(error);return 1;}
  }
 }
}
