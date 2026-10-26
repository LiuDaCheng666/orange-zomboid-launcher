using System;
namespace PZ3D {
 static class VersionLibrarySuiteTests {
  [STAThread] public static int Main(){try{
   VersionLibraryTests.Run();SteamVersionWorkflowTests.Run();VersionLaunchTests.Run();GameVersionProbeTests.Run();
   LaunchProfilesTests.Run();LaunchTransportTests.Run();SaveProfilesTests.Run();ModConfigurationsTests.Run();
   if(LaunchPathValidationTests.Main()!=0)throw new Exception("Launch path validation failed");
   Console.WriteLine("VERSION LIBRARY AND RELATED REGRESSIONS PASSED");return 0;
  }catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
