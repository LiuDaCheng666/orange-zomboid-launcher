using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Text.RegularExpressions;
namespace PZ3D {
 enum JavaProcessRole {Unknown,Client,Server}
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
  // Windows command-line quoting, bounded before allocating tokens. No command text is logged.
  static List<string> Arguments(string command){
   if(string.IsNullOrWhiteSpace(command)||command.Length>32768)return null;
   // Windows argument separators are space/tab. Refuse other whitespace instead of
   // splitting a JVM property and promoting its contents to the main class.
   foreach(char c in command)if(c=='\0'||char.IsWhiteSpace(c)&&c!=' '&&c!='\t')return null;
   var result=new List<string>();int i=0;
   while(i<command.Length){while(i<command.Length&&char.IsWhiteSpace(command[i]))i++;if(i==command.Length)break;if(result.Count>=1024)return null;
    var token=new StringBuilder();bool quoted=false;
    while(i<command.Length&&(quoted||!char.IsWhiteSpace(command[i]))){int slashes=0;while(i<command.Length&&command[i]=='\\'){slashes++;i++;}
     if(i<command.Length&&command[i]=='"'){token.Append('\\',slashes/2);if(slashes%2==1)token.Append('"');else quoted=!quoted;i++;}
     else {token.Append('\\',slashes);if(i<command.Length&&(quoted||!char.IsWhiteSpace(command[i])))token.Append(command[i++]);}
    }
    if(quoted)return null;result.Add(token.ToString());
   }return result;
  }
  public static JavaProcessRole ClassifyJavaCommandLine(string command){
   var args=Arguments(command);if(args==null||args.Count<2)return JavaProcessRole.Unknown;
   string exe;try{exe=Path.GetFileName(args[0]);}catch{return JavaProcessRole.Unknown;}
   if(!exe.Equals("java.exe",StringComparison.OrdinalIgnoreCase)&&!exe.Equals("javaw.exe",StringComparison.OrdinalIgnoreCase)&&!exe.Equals("java",StringComparison.OrdinalIgnoreCase)&&!exe.Equals("javaw",StringComparison.OrdinalIgnoreCase))return JavaProcessRole.Unknown;
   for(int i=1;i<args.Count;i++){
    string a=args[i];if(a.Length==0||a[0]=='@')return JavaProcessRole.Unknown;
    if(a=="-cp"||a=="-classpath"||a=="--class-path"||a=="-p"||a=="--module-path"||a=="--upgrade-module-path"||a=="--add-modules"||a=="--limit-modules"||a=="--add-opens"||a=="--add-exports"||a=="--add-reads"||a=="--patch-module"){if(++i>=args.Count||string.IsNullOrEmpty(args[i])||args[i][0]=='@')return JavaProcessRole.Unknown;continue;}
    if(a.StartsWith("--class-path=",StringComparison.Ordinal)||a.StartsWith("--module-path=",StringComparison.Ordinal)||a.StartsWith("--add-opens=",StringComparison.Ordinal)||a.StartsWith("--add-exports=",StringComparison.Ordinal)||a.StartsWith("--add-modules=",StringComparison.Ordinal)||a.StartsWith("--enable-native-access=",StringComparison.Ordinal))continue;
    if(a.StartsWith("-D",StringComparison.Ordinal)&&a.Length>2||a.StartsWith("-XX:",StringComparison.Ordinal)&&a.Length>4||Regex.IsMatch(a,@"^-X(?:m[sx]|ss)[0-9]+[kKmMgG]?$",RegexOptions.CultureInvariant)||a=="-Xint"||a=="-Xcomp"||a=="-Xmixed"||a=="-Xcheck:jni"||a.StartsWith("-agentlib:",StringComparison.Ordinal)||a.StartsWith("-agentpath:",StringComparison.Ordinal)||a.StartsWith("-javaagent:",StringComparison.Ordinal)||a=="-server"||a=="-client"||a=="-ea"||a=="-da"||a=="-esa"||a=="-dsa"||a=="-verbose"||a=="--enable-preview"||a.StartsWith("-ea:",StringComparison.Ordinal)||a.StartsWith("-da:",StringComparison.Ordinal))continue;
    if(a=="zombie.network.GameServer")return JavaProcessRole.Server;
    if(a=="zombie.gameStates.MainScreenState")return JavaProcessRole.Client;
    return JavaProcessRole.Unknown;
   }return JavaProcessRole.Unknown;
  }
  internal static JavaProcessRole ReadJavaRole(string game,string name,string image,Process process){
   if(!MatchesJava(game,name,image))return JavaProcessRole.Unknown;
   try{
    long started=process.StartTime.ToUniversalTime().Ticks;int pid=process.Id;string command=null;long created=0;
    using(var query=new ManagementObjectSearcher("SELECT CommandLine, CreationDate FROM Win32_Process WHERE ProcessId = "+pid))using(var rows=query.Get())foreach(ManagementObject row in rows)using(row){command=row["CommandLine"] as string;string date=row["CreationDate"] as string;if(date!=null)created=ManagementDateTimeConverter.ToDateTime(date).ToUniversalTime().Ticks;}
    // WMI creation timestamps have microsecond precision; process start ticks can be finer.
    if(created==0||Math.Abs(created-started)>=TimeSpan.TicksPerMillisecond)return JavaProcessRole.Unknown;
    JavaProcessRole role=ClassifyJavaCommandLine(command);
    using(var current=Process.GetProcessById(pid)){
     if(current.HasExited||current.StartTime.ToUniversalTime().Ticks!=started||!MatchesJava(game,name,current.MainModule.FileName))return JavaProcessRole.Unknown;
    }
    if(process.HasExited)return JavaProcessRole.Unknown;return role;
   }catch{return JavaProcessRole.Unknown;}
  }
  public static bool IsClientRunning(string game){return Running(game,true);}
  public static bool IsRunning(string game){
   return Running(game,false);
  }
  static bool Running(string game,bool clientOnly){
   foreach(string name in new[]{"ProjectZomboid64","ProjectZomboid32"}){Process[] native=Process.GetProcessesByName(name);try{foreach(var process in native)if(!Exited(process))return true;}finally{foreach(var process in native)process.Dispose();}}
   foreach(string name in new[]{"java","javaw"}){Process[] java=Process.GetProcessesByName(name);try{foreach(var process in java){if(Exited(process))continue;string image;try{var module=process.MainModule;image=module==null?null:module.FileName;}catch(Exception ex){if(Exited(process))continue;throw new Exception("无法核实运行中的 Java 进程路径；请停止客户端或服务端占用，或使用可读取该进程的权限后重试。未修改配置。",ex);}if(image==null){if(Exited(process))continue;throw new Exception("运行中的 Java 进程路径不可读取，暂不能安全确认占用已结束；未修改配置。");}if(MatchesJava(game,name,image)&&(!clientOnly||ReadJavaRole(game,name,image,process)!=JavaProcessRole.Server))return true;}}finally{foreach(var process in java)process.Dispose();}}
   return false;
  }
 }
}
