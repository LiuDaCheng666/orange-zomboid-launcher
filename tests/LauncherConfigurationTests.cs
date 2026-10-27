using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
namespace PZ3D {
static class LauncherConfigurationTests {
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Main(){try{Run();}catch(Exception e){Console.Error.WriteLine("FAIL: "+e.Message);Environment.Exit(1);}}
 static void Run(){
  // This reflection assertion also makes the initial RED executable without the new source.
  Check(typeof(LauncherConfigurationTests).Assembly.GetType("PZ3D.LauncherConfiguration")!=null,"Missing explicit launcher configuration context");
#if CONTEXT_IMPLEMENTED
  string root=Path.Combine(Path.GetTempPath(),"ozl-context-中文-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  string game=Path.Combine(root,"游戏"),user=Path.Combine(root,"存档"),data=Path.Combine(root,"数据");Directory.CreateDirectory(game);Directory.CreateDirectory(Path.Combine(user,"mods"));
  byte[] original=new byte[]{239,187,191,109,111,100,115,13,10};File.WriteAllBytes(Path.Combine(user,"mods","default.txt"),original);
  File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{malformed -javaagent:unknown.jar");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.bat"),"-agentpath:foreign.dll");
  var c=LauncherConfiguration.Resolve(data,game,user);Check(c.Mode=="launcher","default mode");Check(!Directory.Exists(data),"resolve must be read-only");
  foreach(string mode in new[]{"launcher","steam","vanilla"})Check(c.Bind(mode).Mode==mode,"exact mode "+mode);
  try{c.Bind("other");throw new Exception("invalid mode accepted");}catch(ArgumentException){}
  Check(c.Key==LauncherConfiguration.Resolve(data,game+"\\",user).Key,"trailing slash identity");
  Check(c.Key!=LauncherConfiguration.Resolve(data,game,user+"2").Key,"per user identity");
  c=c.Select("vanilla");Check(LauncherConfiguration.Resolve(data,game,user).Mode=="vanilla","selection persists");c=c.Select("launcher");c.Initialize();
  Check(File.ReadAllBytes(c.DefaultModsPath).SequenceEqual(original),"ordinary list byte-preserving import");File.WriteAllText(Path.Combine(user,"mods","default.txt"),"later");c.Initialize();Check(File.ReadAllBytes(c.DefaultModsPath).SequenceEqual(original),"import only once");
  Check(File.ReadAllText(Path.Combine(c.ConfigDirectory,"config.json")).Contains("\"auto_fix_mod_order\":false"),"owned Buddy order policy");
  c.SetMemoryMb(6144);Check(LauncherConfiguration.Resolve(data,game,user).MemoryMb==6144,"owned memory persists");
  foreach(string p in new[]{"jre64/bin/java.exe","jre64/bin/server/jvm.dll","projectzomboid.jar","steam_api64.dll","ZNetJNI64.dll","Lighting64.dll"})Touch(game,p);
  var args=c.StockArguments(42);Check(args.Contains("-Xmx6144m")&&args.Contains("-Xms1024m"),"owned heap");Check(!args.Any(a=>a.Contains("AlwaysPreTouch")||a.Contains("agent")||a.Contains("foreign")),"stock has no imported agents/pre-touch");
  Check(args.Contains("--enable-native-access=ALL-UNNAMED")&&args.Contains("zombie.gameStates.MainScreenState"),"B42 main and native access");Check(args[Array.IndexOf(args,"-cp")+1]==Path.Combine(game,"projectzomboid.jar"),"B42 no loose class override");
  foreach(string key in new[]{"JAVA_TOOL_OPTIONS","_JAVA_OPTIONS","JDK_JAVA_OPTIONS"}){string old=Environment.GetEnvironmentVariable(key);try{Environment.SetEnvironmentVariable(key,"-javaagent:host.jar");var psi=c.BuildStartInfo(42);Check(!psi.EnvironmentVariables.ContainsKey(key),"child strips "+key);Check(Environment.GetEnvironmentVariable(key)=="-javaagent:host.jar","parent unchanged "+key);Check(psi.WorkingDirectory==game&&psi.EnvironmentVariables["SteamAppId"]=="108600"&&psi.EnvironmentVariables["SteamGameId"]=="108600","game cwd and Steam IDs");Check(psi.Arguments.Contains("-cachedir="+user),"real Chinese cachedir");}finally{Environment.SetEnvironmentVariable(key,old);}}
  foreach(string text in new[]{"中文 路径\\","a\\\"b","","C:\\a b\\"})Check(Split(LauncherConfiguration.QuoteArgument(text))[0]==text,"Windows argument round trip");
  foreach(string jar in LauncherConfiguration.Build41Jars)Touch(game,jar);Touch(game,"zombie/gameStates/MainScreenState.class");args=c.StockArguments(41);Check(!args.Contains("--enable-native-access=ALL-UNNAMED"),"B41 no Java25 option");Check(args[Array.IndexOf(args,"-cp")+1].Contains("lwjgl-natives-windows.jar")&&args[Array.IndexOf(args,"-cp")+1].EndsWith(game),"B41 concrete jars and loose classes");
  File.Delete(Path.Combine(game,"lwjgl.jar"));try{c.StockArguments(41);throw new Exception("missing jar accepted");}catch(IOException e){Check(e.Message.Contains("lwjgl.jar"),"missing asset names file");}
  Check(File.ReadAllText(Path.Combine(game,"ProjectZomboid64.json"))=="{malformed -javaagent:unknown.jar","original malformed JSON untouched");
  Check(c.Bind("vanilla").StockArguments(42).All(a=>!a.Contains("javaagent")),"vanilla stock no Java components");
  try{c.Bind("steam").BuildStartInfo(42);throw new Exception("Steam direct transport accepted");}catch(InvalidOperationException){}
  Console.WriteLine("PASS "+checks+" context/stock assertions; fixture="+root);
#endif
 }
#if CONTEXT_IMPLEMENTED
 static void Touch(string root,string relative){string file=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"fixture");}
 [DllImport("shell32.dll",SetLastError=true)]static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)]string command,out int count);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
 static string[] Split(string command){int count;var p=CommandLineToArgvW("fixture.exe "+command,out count);try{return Enumerable.Range(1,count-1).Select(i=>Marshal.PtrToStringUni(Marshal.ReadIntPtr(p,i*IntPtr.Size))).ToArray();}finally{LocalFree(p);}}
#endif
}}
