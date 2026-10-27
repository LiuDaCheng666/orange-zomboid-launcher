using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 // A live context is deliberately separate from frozen snapshot manifests and Engine.Game/User.
 sealed class LauncherConfiguration {
  static readonly System.Collections.Concurrent.ConcurrentDictionary<string,object> Gates=new System.Collections.Concurrent.ConcurrentDictionary<string,object>(StringComparer.OrdinalIgnoreCase);
  object Gate {get{return Gates.GetOrAdd(Root,ignored=>new object());}}
  int initialMemoryMb=3072;
  bool indexed;
  public string ProfileId {get;private set;}
  static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  public string Mode {get;private set;} public string Key {get;private set;}
  public string Game {get;private set;} public string User {get;private set;} public string Root {get;private set;}
  public string ConfigDirectory {get{return Path.Combine(Root,"buddy");}}
  public string RuntimeDirectory {get{return Path.Combine(Root,"runtime");}}
  public string ComponentsDirectory {get{return Path.Combine(Root,"components");}}
  public string DefaultModsPath {get{return Path.Combine(Root,"mods","default.txt");}}
  public string PreferencesPath {get{return Path.Combine(Root,"preferences.json");}}
  public string SelectionPath {get{return Path.Combine(Root,"selection.txt");}}
  public int MemoryMb {get{if(!File.Exists(PreferencesPath))return initialMemoryMb;var p=ReadPreferences();object value;int mb;if(!p.TryGetValue("memoryMb",out value)||!int.TryParse(Convert.ToString(value),out mb)||mb<512||mb>262144)throw new IOException("启动器内存设置无效，请重新设置内存。");return mb;}}
  public static string ParseMode(string value){if(string.IsNullOrEmpty(value))return "launcher";if(value=="launcher"||value=="steam"||value=="vanilla")return value;throw new ArgumentException("无法识别启动配置："+value);}
  static string Full(string path){if(string.IsNullOrWhiteSpace(path)||path.Any(char.IsControl))throw new ArgumentException("目录无效。");string full=Path.GetFullPath(path);return full.Length>Path.GetPathRoot(full).Length?full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar):full;}
  public static LauncherConfiguration Resolve(string data,string game,string user){
   var c=new LauncherConfiguration{Game=Full(game),User=Full(user)};
   using(var sha=SHA256.Create())c.Key=BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(c.Game.ToUpperInvariant()+"\n"+c.User.ToUpperInvariant()))).Replace("-","").ToLowerInvariant();
   c.Root=Path.Combine(Full(data),"launcher-configurations",c.Key);
   c.Mode=File.Exists(c.SelectionPath)?ParseMode(File.ReadAllText(c.SelectionPath,Utf8).Trim()):"launcher";return c;
  }
  // Indexed inventory owns selection; resolution never reads or writes the legacy selection.txt.
  internal static LauncherConfiguration ResolveIndexed(string data,string game,string user,string profileId,string ownerUser,int initialMemory){
   if(initialMemory<512||initialMemory>262144)throw new ArgumentOutOfRangeException("initialMemory");
   var c=new LauncherConfiguration{Game=Full(game),User=Full(user),Mode="launcher",ProfileId=profileId,indexed=true,initialMemoryMb=initialMemory};
   string identity=profileId==null?c.Game.ToUpperInvariant()+"\n"+c.User.ToUpperInvariant():"profile\n"+Full(ownerUser).ToUpperInvariant()+"\n"+profileId;
   using(var sha=SHA256.Create())c.Key=BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(identity))).Replace("-","").ToLowerInvariant();
   c.Root=Path.Combine(Full(data),"launcher-configurations",c.Key);return c;
  }
  public LauncherConfiguration Bind(string mode){return new LauncherConfiguration{Game=Game,User=User,Root=Root,Key=Key,Mode=ParseMode(mode),ProfileId=ProfileId,indexed=indexed,initialMemoryMb=initialMemoryMb};}
  public LauncherConfiguration Select(string mode){if(indexed)throw new InvalidOperationException("请使用启动配置目录选择配置。");var selected=Bind(mode);lock(Gate){OwnWrites();Atomic(SelectionPath,Utf8.GetBytes(selected.Mode));}return selected;}
  static bool Beneath(string file,string directory){return file.StartsWith(directory.TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||string.Equals(file,directory,StringComparison.OrdinalIgnoreCase);}
  static void NoLinks(string path){for(string p=Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p)){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("启动配置目录不能使用链接："+p);}}
  void OwnWrites(){if(Beneath(Root,Game)||Beneath(Root,User))throw new IOException("启动器配置应保存在游戏和存档目录之外。");NoLinks(Root);}
  static void Atomic(string file,byte[] bytes){NoLinks(file);Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public void Initialize(){lock(Gate){OwnWrites();foreach(string dir in new[]{Root,ConfigDirectory,RuntimeDirectory,ComponentsDirectory,Path.GetDirectoryName(DefaultModsPath)}){NoLinks(dir);Directory.CreateDirectory(dir);}
   if(!File.Exists(PreferencesPath))Atomic(PreferencesPath,Utf8.GetBytes("{\"memoryMb\":"+initialMemoryMb+"}"));
   string buddy=Path.Combine(ConfigDirectory,"config.json");if(!File.Exists(buddy))Atomic(buddy,Utf8.GetBytes("{\"trusted_authors\":[],\"preload_mods\":{},\"auto_fix_mod_order\":false,\"fix_approval_dialog_cursor\":false}"));
   if(!File.Exists(DefaultModsPath)){string original=Path.Combine(User,"mods","default.txt");Atomic(DefaultModsPath,File.Exists(original)?File.ReadAllBytes(original):Utf8.GetBytes("VERSION = 1,\r\nmods\r\n{\r\n}\r\nmaps\r\n{\r\n}\r\n"));}
  }}
  Dictionary<string,object> ReadPreferences(){NoLinks(PreferencesPath);if(new FileInfo(PreferencesPath).Length>65536)throw new IOException("启动器内存设置文件过大。");try{return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(PreferencesPath,Utf8))??new Dictionary<string,object>();}catch(Exception e){throw new IOException("无法读取启动器内存设置，请重新设置内存。",e);}}
  public void SetMemoryMb(int mb){if(mb<512||mb>262144)throw new ArgumentOutOfRangeException("mb","内存应在 512 至 262144 MB 之间。");lock(Gate){OwnWrites();var p=File.Exists(PreferencesPath)?ReadPreferences():new Dictionary<string,object>();p["memoryMb"]=mb;Atomic(PreferencesPath,Utf8.GetBytes(new JavaScriptSerializer().Serialize(p)));}}
  // Windows B41.78 stock alternate launcher inventory, not an original vmArgs import.
  internal static readonly string[] Build41Jars={"commons-compress-1.18.jar","istack-commons-runtime.jar","jassimp.jar","javacord-2.0.17-shaded.jar","javax.activation-api.jar","jaxb-api.jar","jaxb-runtime.jar","lwjgl.jar","lwjgl-natives-windows.jar","lwjgl-glfw.jar","lwjgl-glfw-natives-windows.jar","lwjgl-jemalloc.jar","lwjgl-jemalloc-natives-windows.jar","lwjgl-opengl.jar","lwjgl-opengl-natives-windows.jar","lwjgl_util.jar","sqlite-jdbc-3.27.2.1.jar","trove-3.0.3.jar","uncommons-maths-1.2.3.jar"};
  string Required(string relative){string file=Path.Combine(Game,relative);if(!File.Exists(file))throw new IOException("游戏运行文件缺失："+file);return file;}
  void RequiredNative(string name){if(!File.Exists(Path.Combine(Game,name))&&!File.Exists(Path.Combine(Game,"win64",name)))throw new IOException("游戏运行文件缺失："+name+"（"+Game+" 或 win64）");}
  public string[] StockArguments(int buildMajor){
   if(Mode=="steam")throw new InvalidOperationException("Steam 配置使用 Steam 启动流程。");
   if(buildMajor!=41&&buildMajor!=42)throw new IOException("暂不支持此游戏版本的独立启动："+buildMajor);
   Required("jre64/bin/java.exe");Required("jre64/bin/server/jvm.dll");foreach(string name in new[]{"steam_api64.dll","ZNetJNI64.dll","Lighting64.dll"})RequiredNative(name);
   string cp;if(buildMajor==42)cp=Required("projectzomboid.jar");else{Required("zombie/gameStates/MainScreenState.class");cp=string.Join(";",Build41Jars.Select(Required).Concat(new[]{Game}));}
   int memory=MemoryMb;var args=new List<string>{"-Djava.awt.headless=true","-Dzomboid.steam=1","-Dzomboid.znetlog=1","-XX:-CreateCoredumpOnCrash","-XX:-OmitStackTraceInFastThrow","-XX:+UseG1GC","-Xms"+Math.Min(1024,memory)+"m","-Xmx"+memory+"m","-Djava.library.path="+Path.Combine(Game,"win64")+";"+Game};
   if(buildMajor==42){args.Add("--enable-native-access=ALL-UNNAMED");args.Add("--add-exports=java.base/jdk.internal.misc=ALL-UNNAMED");}
   args.Add("-cp");args.Add(cp);args.Add("zombie.gameStates.MainScreenState");args.Add("-cachedir="+User);return args.ToArray();
  }
  public ProcessStartInfo BuildStartInfo(int buildMajor){var start=new ProcessStartInfo(Path.Combine(Game,"jre64","bin","java.exe")){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Game,Arguments=string.Join(" ",StockArguments(buildMajor).Select(QuoteArgument))};
   foreach(string key in new[]{"JAVA_TOOL_OPTIONS","_JAVA_OPTIONS","JDK_JAVA_OPTIONS","ZB_VERBOSITY"})start.EnvironmentVariables.Remove(key);
   start.EnvironmentVariables["PATH"]=Path.Combine(Game,"jre64","bin")+";"+Game+";"+(start.EnvironmentVariables["PATH"]??"");start.EnvironmentVariables["SteamAppId"]="108600";start.EnvironmentVariables["SteamGameId"]="108600";return start;
  }
  public static string QuoteArgument(string value){if(value==null||value.IndexOf('\0')>=0)throw new ArgumentException("启动参数无效。");var b=new StringBuilder("\"");int slashes=0;foreach(char ch in value){if(ch=='\\'){slashes++;continue;}b.Append('\\',ch=='"'?slashes*2+1:slashes);b.Append(ch);slashes=0;}return b.Append('\\',slashes*2).Append('"').ToString();}
 }
}
