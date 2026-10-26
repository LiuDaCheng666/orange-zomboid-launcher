using System;
namespace PZ3D {
 static class SnapshotAffectedTests {
  [STAThread] public static int Main(string[] args){
   if(args.Length>0&&args[0]=="--workshop-worker")return ModernControllerTests.Main(args);
   if(args.Length!=0)return 2;
   try{
    GameStartupComponentTests.Run();GameStartupPresentationTests.Run();
    LaunchProfilesTests.Run();LaunchTransportTests.Run();LaunchPreflightTests.Run();
    if(LaunchPendingTests.Main()!=0)return 1;
    Console.WriteLine("PASS affected startup ownership, save launch, preflight and pending guards");return 0;
   }catch(Exception error){Console.WriteLine(error);return 1;}
  }
 }
}
