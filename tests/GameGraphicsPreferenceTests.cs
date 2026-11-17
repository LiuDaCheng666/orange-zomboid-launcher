using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;

namespace PZ3D {
 static class GameGraphicsPreferenceTests {
  const string KeyPath=@"Software\Microsoft\DirectX\UserGpuPreferences";
  static int checks;
  static void Check(bool ok,string reason){if(!ok)throw new Exception("FAIL "+reason);checks++;Console.WriteLine("PASS "+reason);}
  static void Put(string path,string value){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,value);}
  static MethodInfo Submit;
  static Type Preference;
  static void RunTransport(string root,string kind){
   string game=Path.Combine(root,kind,"游戏 中文"),user=Path.Combine(root,kind,"存档 中文"),data=Path.Combine(root,kind,"data");
   foreach(string name in new[]{"jre64/bin/server/jvm.dll","steam_api64.dll","ZNetJNI64.dll","Lighting64.dll","projectzomboid.jar"})Put(Path.Combine(game,name),"isolated fixture, never loaded");
   foreach(string name in new[]{"jre64/bin/java.exe","ProjectZomboid64.exe"}){string target=Path.Combine(game,name);Directory.CreateDirectory(Path.GetDirectoryName(target));File.Copy(Environment.GetEnvironmentVariable("OZL_GPU_PROCESS_FIXTURE"),target);}
   Put(Path.Combine(game,"game-version.txt"),"42.22.2");
   Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"mainClass\":\"zombie/gameStates/MainScreenState\",\"classpath\":[\"projectzomboid.jar\"],\"vmArgs\":[\"-Xmx8g\"]}");
   var engine=new Engine(game,user,data,true,null,_=>new Process[0]);
   if(kind=="owned"||kind=="server")engine.BindConfiguration(new LaunchConfigurationProfiles(data,game,user).Resolve("launcher"));
   if(kind=="managed")engine.ManagedVersion=true;
   if(kind=="frozen")engine.FrozenEnvironment=new SaveEnvironmentRecord();
   if(kind=="server")LaunchProfiles.Stage(engine,new ProfileLaunchRequest{Kind="server",Host="example.invalid",Port=16261,Username="isolated",ServerComponents="follow-server"});
   var start=LaunchProfiles.BuildStartInfo(engine);string exe=start.FileName,arguments=start.Arguments;
   using(var registry=Registry.CurrentUser.CreateSubKey(KeyPath)){
    try{
     registry.SetValue(exe,"SwapEffectUpgradeEnable=1;GpuPreference=1;VRROptimizeEnable=0;",RegistryValueKind.String);
     start.RedirectStandardOutput=true;start.RedirectStandardError=true;start.CreateNoWindow=true;
     using(var process=Submit==null?Process.Start(start):(Process)Submit.Invoke(null,new object[]{start})){
      var outputTask=process.StandardOutput.ReadToEndAsync();var errorTask=process.StandardError.ReadToEndAsync();
      if(!process.WaitForExit(10000)){process.Kill();process.WaitForExit();throw new Exception("isolated GPU fixture timed out and was terminated: "+kind);}
      string output=outputTask.GetAwaiter().GetResult(),error=errorTask.GetAwaiter().GetResult();
      Check(process.ExitCode==0,kind+" launches real isolated child: "+output+error);
      Check(output.Contains("GpuPreference=2;")&&!output.Contains("GpuPreference=1;"),kind+" child sees high-performance preference at process entry");
      Check(output.Contains("SwapEffectUpgradeEnable=1;")&&output.Contains("VRROptimizeEnable=0;"),kind+" preserves unrelated per-app graphics settings");
     }
     Check(start.Arguments==arguments&&start.WorkingDirectory==game,kind+" keeps production arguments and working directory unchanged");
     string before=(string)registry.GetValue(exe);
     Check((bool)Preference.GetMethod("PreferHighPerformance",new[]{typeof(string)}).Invoke(null,new object[]{exe}),kind+" accepts repeat preference preparation");
     Check((string)registry.GetValue(exe)==before,kind+" repeated preparation preserves exact stored value");
     Check(registry.GetValueKind(exe)==RegistryValueKind.String,kind+" stores Windows REG_SZ preference");
    }finally{registry.DeleteValue(exe,false);if(LaunchProfiles.Pending(engine))LaunchProfiles.Recover(engine,true);}
   }
  }
  static void PreferenceCases(string root){
   string exe=Path.Combine(root,"registry 中文.exe"),temporary=@"Software\OrangeLauncherTests\Gpu-"+Guid.NewGuid().ToString("N");
   var apply=Preference.GetMethod("PreferHighPerformance",new[]{typeof(string),typeof(RegistryKey)});
   Check(apply!=null,"preference write has an isolated registry integration seam");
   try{
    using(var key=Registry.CurrentUser.CreateSubKey(temporary)){
     Check((bool)apply.Invoke(null,new object[]{exe,key})&&(string)key.GetValue(exe)=="GpuPreference=2;","missing preference defaults to high performance");
     key.SetValue(exe,"GpuPreference=0;Other=keep; gPuPreference =1;",RegistryValueKind.String);
     Check((bool)apply.Invoke(null,new object[]{exe,key})&&(string)key.GetValue(exe)=="Other=keep;GpuPreference=2;","replaces all old GPU preferences without losing other options");
    }
    using(var key=Registry.CurrentUser.OpenSubKey(temporary,false)){
     Check(!(bool)apply.Invoke(null,new object[]{Path.Combine(root,"read-only.exe"),key}),"unwritable preference returns failure without throwing or blocking launch");
    }
   }finally{Registry.CurrentUser.DeleteSubKeyTree(temporary,false);}
  }
  static void SteamTargets(string root){
   string game=Path.Combine(root,"steam 游戏");string[] targets={Path.Combine(game,"ProjectZomboid64.exe"),Path.Combine(game,"jre64","bin","java.exe")};
   foreach(string path in targets)Put(path,"never execute");
   using(var key=Registry.CurrentUser.CreateSubKey(KeyPath)){
    try{
     Preference.GetMethod("PrepareSteamGame").Invoke(null,new object[]{game});
     Check(targets.All(path=>(string)key.GetValue(path)=="GpuPreference=2;"),"Steam preparation covers selected game's native EXE and its Java EXE");
     foreach(string path in targets)key.SetValue(path,"GpuPreference=1;",RegistryValueKind.String);
     string user=Path.Combine(root,"steam user"),data=Path.Combine(root,"steam data");
     Put(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[]}");
     Put(Path.Combine(game,"game-version.txt"),"42.22.2");
     Put(Path.Combine(user,"mods/default.txt"),"mods\n{\n}\nmaps\n{\n}\n");
     var engine=new Engine(game,user,data,false,null,_=>new Process[0]);bool observed=false;
     try{LaunchProfiles.LaunchNormal(engine,Guid.NewGuid().ToString("N"),()=>{
      observed=true;
      Check(targets.All(path=>(string)key.GetValue(path)=="GpuPreference=2;"),"actual production Steam submission sees high-performance preferences before its callback");
      throw new OperationCanceledException("isolated Steam submission; never contact Steam");
     });}catch(OperationCanceledException){}
     Check(observed&&!LaunchProfiles.Pending(engine),"isolated Steam callback runs without contacting Steam and failure recovery releases its session");
    }finally{foreach(string path in targets)key.DeleteValue(path,false);}
   }
  }
  public static int Main(string[] args){
   LauncherRuntime.Initialize();
   string root=Path.Combine(Path.GetFullPath(args[0]),"fixtures");
   try{
    var production=args.Length>1?Assembly.LoadFrom(Path.GetFullPath(args[1])):typeof(LaunchProfiles).Assembly;
    Submit=production.GetType("PZ3D.LaunchProfiles").GetMethod("StartGameProcess",BindingFlags.Static|BindingFlags.NonPublic);
    Preference=production.GetType("PZ3D.GameGraphicsPreference");
    Console.WriteLine("GPU implementation assembly="+production.Location);
    foreach(string kind in new[]{"source","owned","managed","frozen","server"})RunTransport(root,kind);
    Check(Submit!=null,"production process submission prepares the actual game's GPU preference");
    Check(Preference!=null,"shared game graphics preference implementation exists");
    PreferenceCases(root);SteamTargets(root);
    Console.WriteLine("GAME GPU PREFERENCE PASS: "+checks+" assertions");return 0;
   }catch(Exception error){Console.Error.WriteLine(error);return 1;}
   finally{if(Directory.Exists(root))Directory.Delete(root,true);}
  }
 }
}
