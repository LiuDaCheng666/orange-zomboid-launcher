using System;
using System.IO;
using System.Linq;
using System.Text;
namespace PZ3D {
 static class SteamLaunchOptionsTests {
  internal static void Check(bool value,string message){if(!value)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  internal static void Reject(Action action,string message){bool failed=false;try{action();}catch{failed=true;}Check(failed,message);}
  internal static string Wrap(string apps){return "\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { "+apps+" } } } } }";}
  internal static string Fixture(string root,uint account,string text,Encoding encoding=null){string file=Path.Combine(root,"userdata",account.ToString(),"config","localconfig.vdf");Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,text,encoding??new UTF8Encoding(false));return file;}
  public static int Main(string[] args){try{if(Array.IndexOf(args,"--discovery")>=0){Discovery();return 0;}Run();BuddyLaunchArgumentsTests.Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   Discovery();
   string root=Path.Combine(LauncherTestPaths.Root,"steam-options-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   string original=Wrap("\"999\" { \"LaunchOptions\" \"-javaagent:ZombieBuddy.jar\" } \"108600\" { \"LaunchOptions\" \"-Xmx28g -javaagent:ZombieBuddy.jar -Dkeep=yes\" }");
   string expected="\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { \"999\" { \"LaunchOptions\" \"-javaagent:ZombieBuddy.jar\" } \"108600\" { \"LaunchOptions\" \"-Xmx28g -Dkeep=yes\" } } } } } }";
   string file=Fixture(root,42,original);Fixture(root,7,Wrap("\"108600\" { \"LaunchOptions\" \"other account secret\" }"));
   var s=SteamLaunchOptions.Read(root,42);Check(s.KnownIdentity&&s.HasField&&s.LaunchOptions=="-Xmx28g -javaagent:ZombieBuddy.jar -Dkeep=yes","selected account and app only");
   Check(SteamLaunchOptions.Replace(s,"-Xmx28g -Dkeep=yes").SequenceEqual(new UTF8Encoding(false).GetBytes(expected)),"replacement preserves every unrelated literal byte");
   Check(File.ReadAllText(file)==original,"parser and replacement never write fixture");
   string stamp=s.ScopedStamp;Fixture(root,42,original.Replace("-javaagent:ZombieBuddy.jar\" } \"108600", "unrelated refresh\" } \"108600"));Check(SteamLaunchOptions.Read(root,42).ScopedStamp==stamp,"other app refresh does not stale current-game stamp");
   Fixture(root,42,original.Replace("-Xmx28g","-Xmx29g"));Check(SteamLaunchOptions.Read(root,42).ScopedStamp!=stamp,"own launch change stales current-game stamp");
   string escaped=Wrap("// comment ZombieBuddy\r\n\"108600\" { /* layout */ \"launchoptions\" \"-javaagent:\\\"C:\\\\My Mods\\\\ZombieBuddy.jar\\\" -Dnote=\\\"hi there\\\"\" }");
   foreach(Encoding encoding in new Encoding[]{new UTF8Encoding(true,true),new UnicodeEncoding(false,true,true),new UnicodeEncoding(true,true,true)}){Fixture(root,42,escaped,encoding);s=SteamLaunchOptions.Read(root,42);Check(s.LaunchOptions=="-javaagent:\"C:\\My Mods\\ZombieBuddy.jar\" -Dnote=\"hi there\"","comments escaped quotes and paths decoded");byte[] wanted=encoding.GetPreamble().Concat(encoding.GetBytes("\"UserLocalConfigStore\" { \"Software\" { \"Valve\" { \"Steam\" { \"apps\" { // comment ZombieBuddy\r\n\"108600\" { /* layout */ \"launchoptions\" \"-Dnote=\\\"hi there\\\"\" } } } } } }")).ToArray();Check(SteamLaunchOptions.Replace(s,"-Dnote=\"hi there\"").SequenceEqual(wanted),"encoding BOM and unedited text preserved");}
   Fixture(root,42,Wrap("\"108600\" { \"name\" \"ZombieBuddy is ordinary text\" }"));s=SteamLaunchOptions.Read(root,42);Check(s.KnownIdentity&&s.State==SteamLaunchOptionsState.Ready&&!s.HasField&&s.LaunchOptions==null,"absent field is known identity, not empty fabricated options");Reject(()=>SteamLaunchOptions.Replace(s,""),"missing field cannot be fabricated by replacement");
   foreach(string malformed in new[]{Wrap("\"108600\" { \"LaunchOptions\" \"a\" \"launchoptions\" \"b\" }"),Wrap("\"108600\" {} \"108600\" {}"),"\"UserLocalConfigStore\" \"Software\"",original+"}",original.Replace("-Xmx28g","\\q"),"UserLocalConfigStore { }","","\"WrongRoot\" {}"}){Fixture(root,42,malformed);Check(SteamLaunchOptions.Read(root,42).State==SteamLaunchOptionsState.Invalid,"ambiguous or malformed VDF rejected");}
   File.WriteAllBytes(file,new byte[16777217]);Check(SteamLaunchOptions.Read(root,42).State==SteamLaunchOptionsState.Invalid,"oversized VDF rejected before parse");Check(SteamLaunchOptions.Read(root,0).State==SteamLaunchOptionsState.UnknownIdentity,"zero account never guesses another userdata directory");
   string junction=Path.Combine(root,"linked"),target=Path.Combine(root,"target");Directory.CreateDirectory(target);var start=new System.Diagnostics.ProcessStartInfo("cmd.exe","/c mklink /J \""+junction+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var process=System.Diagnostics.Process.Start(start)){process.WaitForExit();Check(process.ExitCode==0,"synthetic junction fixture created");}Check(SteamLaunchOptions.Read(junction,42).State==SteamLaunchOptionsState.Invalid,"reparse Steam root refused");
   Console.WriteLine("STEAM LAUNCH OPTIONS PASSED");
  }
  static void Discovery(){
   string root=Path.Combine(LauncherTestPaths.Root,"steam-discovery-"+Guid.NewGuid().ToString("N"));Fixture(root,42,Wrap("\"108600\" { \"LaunchOptions\" \"-agentlib:zbNative\" }"));File.WriteAllText(Path.Combine(root,"steam.exe"),"synthetic executable identity; never launched");
   foreach(uint? pid in new uint?[]{null,0}){int calls=0;Func<Tuple<string,uint,uint?>> discover=()=>Tuple.Create(root,(uint)42,pid);Func<uint,string> process=id=>{calls++;return Path.Combine(root,"steam.exe");};var result=SteamLaunchOptions.Current(discover,process);Check(result.State==SteamLaunchOptionsState.UnknownIdentity&&!result.KnownIdentity&&calls==0,"nonzero account with missing/zero PID never becomes verified identity");}
   Func<Tuple<string,uint,uint?>> valid=()=>Tuple.Create(root,(uint)42,(uint?)123);Func<uint,string> matching=id=>{Check(id==123,"process verification uses captured PID");return Path.Combine(root,"steam.exe");};var known=SteamLaunchOptions.Current(valid,matching);Check(known.KnownIdentity&&known.State==SteamLaunchOptionsState.Ready&&known.LaunchOptions=="-agentlib:zbNative","verified client and account select own launch field");
   Check(SteamLaunchOptions.Current(valid,id=>Path.Combine(root,"other.exe")).State==SteamLaunchOptionsState.UnknownIdentity,"different executable cannot verify selected Steam root");int observations=0;Check(SteamLaunchOptions.Current(()=>Tuple.Create(root,++observations==1?(uint)42:43,(uint?)123),matching).State==SteamLaunchOptionsState.UnknownIdentity,"account changed during capture refuses identity");observations=0;Check(SteamLaunchOptions.Current(()=>Tuple.Create(root,(uint)42,++observations==1?(uint?)123:0),matching).State==SteamLaunchOptionsState.UnknownIdentity,"process disappeared during capture refuses identity");
  }
 }
}
