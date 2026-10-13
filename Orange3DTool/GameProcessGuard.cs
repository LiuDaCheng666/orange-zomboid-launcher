using System;
using System.Diagnostics;
using System.IO;
namespace PZ3D {
 // Read-only process evidence shared by launch, status and every Engine mutation.
 static class GameProcessGuard {
  public static bool MatchesJava(string game,string processName,string executablePath){
   if(string.IsNullOrWhiteSpace(game)||string.IsNullOrWhiteSpace(processName)||string.IsNullOrWhiteSpace(executablePath))return false;
   string name=processName.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?processName.Substring(0,processName.Length-4):processName;
   if(!name.Equals("java",StringComparison.OrdinalIgnoreCase)&&!name.Equals("javaw",StringComparison.OrdinalIgnoreCase))return false;
   try{string root=Path.GetFullPath(game),image=Path.GetFullPath(executablePath);foreach(string runtime in new[]{"jre64","jre"})if(string.Equals(image,Path.Combine(root,runtime,"bin",name+".exe"),StringComparison.OrdinalIgnoreCase))return true;}catch(ArgumentException){}catch(NotSupportedException){}catch(PathTooLongException){}
   return false;
  }
  static bool Exited(Process process){try{return process.HasExited;}catch{return false;}}
  public static bool IsRunning(string game){
   foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32"}){Process[] native=Process.GetProcessesByName(name);try{foreach(var process in native)if(!Exited(process))return true;}finally{foreach(var process in native)process.Dispose();}}
   foreach(string name in new[]{"java","javaw"}){Process[] java=Process.GetProcessesByName(name);try{foreach(var process in java){if(Exited(process))continue;string image;try{var module=process.MainModule;image=module==null?null:module.FileName;}catch(Exception ex){if(Exited(process))continue;throw new Exception("无法核实运行中的 Java 进程路径；请完全退出游戏，或使用可读取该进程的权限后重试。未修改配置。",ex);}if(image==null){if(Exited(process))continue;throw new Exception("运行中的 Java 进程路径不可读取，暂不能安全确认游戏已关闭；未修改配置。");}if(MatchesJava(game,name,image))return true;}}finally{foreach(var process in java)process.Dispose();}}
   return false;
  }
 }
}
