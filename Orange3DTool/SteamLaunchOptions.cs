using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
namespace PZ3D {
 internal enum SteamLaunchOptionsState { Ready, Unavailable, UnknownIdentity, Invalid }
 // Native-only evidence. Never pass this object to the web bridge or a logger.
 internal sealed class SteamLaunchOptionsSnapshot {
  internal string Root {get;private set;}
  internal uint Account {get;private set;}
  internal string ConfigPath {get;private set;}
  internal SteamLaunchOptionsState State {get;private set;}
  internal bool KnownIdentity {get{return Account!=0&&!string.IsNullOrEmpty(Root)&&State!=SteamLaunchOptionsState.UnknownIdentity;}}
  internal bool HasField {get{return State==SteamLaunchOptionsState.Ready&&FieldStart>=0;}}
  internal string LaunchOptions {get;private set;}
  internal int FieldStart {get;private set;}
  internal int FieldLength {get;private set;}
  internal Encoding Encoding {get;private set;}
  readonly byte[] raw;
  internal byte[] RawBytes {get{return raw==null?null:(byte[])raw.Clone();}}
  internal SteamLaunchOptionsSnapshot(string root,uint account,string path,SteamLaunchOptionsState state,byte[] bytes=null,Encoding encoding=null,int start=-1,int length=0,string value=null){Root=root;Account=account;ConfigPath=path;State=state;raw=bytes==null?null:(byte[])bytes.Clone();Encoding=encoding;FieldStart=start;FieldLength=length;LaunchOptions=value;}
  internal string ScopedStamp {get{string identity=(Root??"").ToUpperInvariant()+"\n"+Account+"\n"+State+"\n"+(HasField?"present":"absent");byte[] field=HasField?raw.Skip(FieldStart).Take(FieldLength).ToArray():new byte[0];return DlssInstaller.Hash(System.Text.Encoding.UTF8.GetBytes(identity).Concat(field).ToArray());}}
  internal void RequireReadable(){if(State==SteamLaunchOptionsState.UnknownIdentity)throw new Exception("未能核实当前 Steam 账号，未读取或修改账号启动配置。");if(State==SteamLaunchOptionsState.Invalid)throw new Exception("当前游戏 Steam 启动配置无法安全解析，请检查配置后重试。");}
 }
 internal sealed class SteamNativeEvidence {
  public string root,exe,steamId;public int pid;public long birth;public uint appId;internal uint registryAccount,registryPid;
  internal uint Account {get{ulong value;if(!ulong.TryParse(steamId,out value)||(value>>56)!=1||((value>>52)&15)!=1||((value>>32)&1048575)!=1||(uint)value==0)throw new InvalidOperationException("Steam account evidence invalid");return (uint)value;}}
  internal void Validate(){if((registryAccount!=0&&registryAccount!=Account)||(registryPid!=0&&registryPid!=(uint)pid)||appId!=108600||Account==0||pid<=0||birth<=0||!Path.IsPathRooted(root)||!string.Equals(Path.GetFullPath(exe),Path.Combine(Path.GetFullPath(root),"steam.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Steam client evidence invalid");LauncherMaintenance.NoReparse(root);LauncherMaintenance.NoReparse(exe);}
  internal bool Same(SteamNativeEvidence b){return b!=null&&pid==b.pid&&birth==b.birth&&Account==b.Account&&appId==b.appId&&string.Equals(root,b.root,StringComparison.OrdinalIgnoreCase)&&string.Equals(exe,b.exe,StringComparison.OrdinalIgnoreCase);}
  internal static SteamNativeEvidence Observe(){
   uint activeAccount=0,activePid=0;string root;using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){root=k==null?null:k.GetValue("SteamPath") as string;if(k!=null)using(var a=k.OpenSubKey("ActiveProcess")){if(a!=null){object account=a.GetValue("ActiveUser"),pid=a.GetValue("pid");if(account is int)activeAccount=unchecked((uint)(int)account);if(pid is int)activePid=unchecked((uint)(int)pid);}}}if(string.IsNullOrEmpty(root)||!Path.IsPathRooted(root))throw new InvalidOperationException();root=Path.GetFullPath(root).TrimEnd('\\','/');LauncherMaintenance.NoReparse(root);
   var processes=Process.GetProcessesByName("steam");try{if(processes.Length!=1)throw new InvalidOperationException();var p=processes[0];string exe=p.MainModule.FileName;if(p.HasExited||!string.Equals(Path.GetFullPath(exe),Path.Combine(root,"steam.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException();return new SteamNativeEvidence{root=root,exe=exe,pid=p.Id,birth=p.StartTime.ToUniversalTime().Ticks,registryAccount=activeAccount,registryPid=activePid};}finally{foreach(var p in processes)p.Dispose();}
  }
  internal static SteamNativeEvidence Probe(Engine e){var before=Observe();var json=new System.Web.Script.Serialization.JavaScriptSerializer();var result=json.Deserialize<SteamNativeEvidence>(json.Serialize(SteamLauncherServices.Execute(e,"identity",new Dictionary<string,object>(),null,System.Threading.CancellationToken.None)));result.Validate();var after=Observe();before.steamId=after.steamId=result.steamId;before.appId=after.appId=result.appId;before.Validate();after.Validate();if(!before.Same(result)||!after.Same(result))throw new InvalidOperationException();return result;}
 }
 internal static class SteamLaunchOptions {
  // Fresh native proof is requested only for explicit operations, never status polling.
  internal static BuddySteamIdentity NativeCurrent(Engine e){
   try{return NativeCurrent(()=>SteamNativeEvidence.Probe(e),Read);}catch{return new BuddySteamIdentity(new SteamLaunchOptionsSnapshot(null,0,null,SteamLaunchOptionsState.UnknownIdentity));}
  }
  internal static BuddySteamIdentity NativeCurrent(Func<SteamNativeEvidence> probe,Func<string,uint,SteamLaunchOptionsSnapshot> read){
   var first=probe();first.Validate();var options=read(first.root,first.Account);var last=probe();last.Validate();if(!first.Same(last))throw new InvalidOperationException("Steam identity changed");return new BuddySteamIdentity(options,first.pid,first.birth,first.exe){Native=true};
  }
  const int Limit=16777216;
  internal static SteamLaunchOptionsSnapshot Unavailable(){return new SteamLaunchOptionsSnapshot(null,0,null,SteamLaunchOptionsState.Unavailable);}
  internal static SteamLaunchOptionsSnapshot Current(){return Current(ClientIdentity,ProcessPath);}
  // One observation: Steam root, ActiveUser and nullable PID. Native read-only dependencies.
  internal static SteamLaunchOptionsSnapshot Current(Func<Tuple<string,uint,uint?>> discover,Func<uint,string> processPath){
   string root=null;
   try{
    var identity=discover();if(identity==null)return Unavailable();root=identity.Item1;
    if(string.IsNullOrWhiteSpace(root)||!Path.IsPathRooted(root))return new SteamLaunchOptionsSnapshot(null,0,null,SteamLaunchOptionsState.UnknownIdentity);
    root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);LauncherMaintenance.NoReparse(root);string exe=Path.Combine(root,"steam.exe");LauncherMaintenance.NoReparse(exe);
    if(!File.Exists(exe)||identity.Item2==0||!identity.Item3.HasValue||identity.Item3.Value==0)return new SteamLaunchOptionsSnapshot(root,0,null,SteamLaunchOptionsState.UnknownIdentity);
    uint? pid=identity.Item3;
    if(!string.Equals(Path.GetFullPath(processPath(pid.Value)),exe,StringComparison.OrdinalIgnoreCase))return new SteamLaunchOptionsSnapshot(root,0,null,SteamLaunchOptionsState.UnknownIdentity);
    var snapshot=Read(root,identity.Item2);var current=discover();
    if(current==null||current.Item2!=identity.Item2||current.Item3!=pid||!string.Equals(Path.GetFullPath(current.Item1).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar),root,StringComparison.OrdinalIgnoreCase))return new SteamLaunchOptionsSnapshot(root,0,null,SteamLaunchOptionsState.UnknownIdentity);
    if(!string.Equals(Path.GetFullPath(processPath(pid.Value)),exe,StringComparison.OrdinalIgnoreCase))return new SteamLaunchOptionsSnapshot(root,0,null,SteamLaunchOptionsState.UnknownIdentity);
    return snapshot;
   }catch{return new SteamLaunchOptionsSnapshot(root,0,null,SteamLaunchOptionsState.UnknownIdentity);}
  }
  static Tuple<string,uint,uint?> ClientIdentity(){using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(key==null)return null;string root=key.GetValue("SteamPath") as string;using(var active=key.OpenSubKey("ActiveProcess")){if(active==null)return Tuple.Create(root,(uint)0,(uint?)null);object pid=active.GetValue("pid");return Tuple.Create(root,Number(active.GetValue("ActiveUser")),pid==null?(uint?)null:Number(pid));}}}
  static string ProcessPath(uint pid){using(var process=Process.GetProcessById(checked((int)pid)))return process.MainModule.FileName;}
  static uint Number(object value){if(value is int)return unchecked((uint)(int)value);if(value is uint)return (uint)value;return 0;}
  internal static SteamLaunchOptionsSnapshot Read(string root,uint account){
   string path=null;
   if(account==0)return new SteamLaunchOptionsSnapshot(root,0,null,SteamLaunchOptionsState.UnknownIdentity);
   try{
    if(string.IsNullOrWhiteSpace(root)||!Path.IsPathRooted(root))throw new FormatException();root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);LauncherMaintenance.NoReparse(root);
    path=Path.Combine(root,"userdata",account.ToString(System.Globalization.CultureInfo.InvariantCulture),"config","localconfig.vdf");LauncherMaintenance.NoReparse(path);
    if(!File.Exists(path))throw new FormatException();byte[] raw;
    using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(stream.Length>Limit)throw new FormatException();raw=new byte[(int)stream.Length];int at=0,n;while(at<raw.Length&&(n=stream.Read(raw,at,raw.Length-at))>0)at+=n;if(at!=raw.Length)throw new FormatException();}
    int bom;Encoding encoding=Detect(raw,out bom);string text=encoding.GetString(raw,bom,raw.Length-bom);var parser=new Vdf(text);List<Node> nodes=parser.Parse();Node field=null;
    foreach(string part in new[]{"UserLocalConfigStore","Software","Valve","Steam","apps","108600","LaunchOptions"}){
     var matches=nodes.Where(n=>string.Equals(n.Key,part,StringComparison.OrdinalIgnoreCase)).ToArray();if(matches.Length>1||part=="UserLocalConfigStore"&&matches.Length==0)throw new FormatException();if(matches.Length==0){field=null;break;}field=matches[0];if(part!="LaunchOptions"){if(field.Children==null)throw new FormatException();nodes=field.Children;}else if(field.Children!=null)throw new FormatException();
    }
    int start=field==null?-1:bom+encoding.GetByteCount(text.Substring(0,field.Start));int length=field==null?0:encoding.GetByteCount(text.Substring(field.Start,field.End-field.Start));
    return new SteamLaunchOptionsSnapshot(root,account,path,SteamLaunchOptionsState.Ready,raw,encoding,start,length,field==null?null:field.Value);
   }catch{return new SteamLaunchOptionsSnapshot(root,account,path,SteamLaunchOptionsState.Invalid);}
  }
  internal static byte[] Replace(SteamLaunchOptionsSnapshot snapshot,string replacement){
   if(snapshot==null||!snapshot.KnownIdentity||!snapshot.HasField||replacement==null)throw new InvalidOperationException("没有可安全替换的当前游戏启动字段。");if(replacement.Length>Limit||replacement.IndexOf('\0')>=0)throw new FormatException("启动选项无效。");
   string escaped="\""+replacement.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\n","\\n").Replace("\r","\\r").Replace("\t","\\t")+"\"";byte[] raw=snapshot.RawBytes,value=snapshot.Encoding.GetBytes(escaped);if((long)raw.Length-snapshot.FieldLength+value.Length>Limit)throw new FormatException("Steam 配置过大。");
   return raw.Take(snapshot.FieldStart).Concat(value).Concat(raw.Skip(snapshot.FieldStart+snapshot.FieldLength)).ToArray();
  }
  static Encoding Detect(byte[] raw,out int bom){bom=0;if(raw.Length>=3&&raw[0]==239&&raw[1]==187&&raw[2]==191){bom=3;return new UTF8Encoding(false,true);}if(raw.Length>=2&&raw[0]==255&&raw[1]==254){bom=2;return new UnicodeEncoding(false,false,true);}if(raw.Length>=2&&raw[0]==254&&raw[1]==255){bom=2;return new UnicodeEncoding(true,false,true);}return new UTF8Encoding(false,true);}
  sealed class Node {internal string Key,Value;internal int Start,End;internal List<Node> Children;}
  sealed class Vdf {
   readonly string text;int at,count;
   internal Vdf(string source){text=source;}
   internal List<Node> Parse(){var result=Object(0,false);Space();if(at!=text.Length)throw new FormatException();return result;}
   void Space(){while(at<text.Length){if(char.IsWhiteSpace(text[at])){at++;continue;}if(at+1<text.Length&&text[at]=='/'&&text[at+1]=='/'){at+=2;while(at<text.Length&&text[at]!='\n')at++;continue;}if(at+1<text.Length&&text[at]=='/'&&text[at+1]=='*'){int end=text.IndexOf("*/",at+2,StringComparison.Ordinal);if(end<0)throw new FormatException();at=end+2;continue;}break;}}
   List<Node> Object(int depth,bool closing){if(depth>64)throw new FormatException();var list=new List<Node>();while(true){Space();if(at==text.Length){if(closing)throw new FormatException();return list;}if(text[at]=='}'){if(!closing)throw new FormatException();at++;return list;}if(++count>250000)throw new FormatException();var n=new Node{Key=Quoted()};Space();if(at<text.Length&&text[at]=='{'){at++;n.Children=Object(depth+1,true);}else{n.Start=at;n.Value=Quoted();n.End=at;}list.Add(n);}}
   string Quoted(){Space();if(at>=text.Length||text[at++]!='\"')throw new FormatException();var b=new StringBuilder();while(at<text.Length){char c=text[at++];if(c=='\"')return b.ToString();if(c<' '&&c!='\t'&&c!='\r'&&c!='\n')throw new FormatException();if(c=='\\'){if(at==text.Length)throw new FormatException();c=text[at++];switch(c){case '\\':case '\"':break;case 'n':c='\n';break;case 'r':c='\r';break;case 't':c='\t';break;default:throw new FormatException();}}b.Append(c);}throw new FormatException();}
  }
 }
}
