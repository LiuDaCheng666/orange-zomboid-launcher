using System;using System.IO;using System.Linq;using System.Text;using System.Text.RegularExpressions;using System.Collections.Generic;
namespace PZ3D {
 sealed class ServerWorkshopManifest {public string endpoint,stamp,source,capturedUtc;public string[] ids;}
 sealed class ServerWorkshopCatalog {
  readonly Engine engine;readonly ServerProfiles servers;
  public ServerWorkshopCatalog(Engine engine,ServerProfiles servers){this.engine=engine;this.servers=servers;}
  internal static string Endpoint(ServerLaunchTarget target){return target.Host+":"+target.Port;}
  static string FilePath(string data,string endpoint){return Path.Combine(data,"server-workshop",DlssInstaller.Hash(Encoding.UTF8.GetBytes(endpoint))+".json");}
  public static string[] Parse(string text){
   if(text==null||text.Length>65536)throw new Exception("清单为空或超过 65536 字符。");text=text.Trim();if(text.StartsWith("WorkshopItems=",StringComparison.Ordinal))text=text.Substring(14).Trim();
   if(text.Length==0||!Regex.IsMatch(text,@"^[0-9\s;,]+$"))throw new Exception("仅接受 WorkshopItems 一行或正整数工坊 ID 列表。");
   var parts=Regex.Split(text,@"\r\n|[;,\r\n]");var result=new List<string>();var seen=new HashSet<string>();foreach(string part in parts){string id=part.Trim();if(!Regex.IsMatch(id,@"^[1-9][0-9]{0,19}$"))throw new Exception("工坊 ID 格式无效。");ulong number;if(!ulong.TryParse(id,out number)||number==0)throw new Exception("工坊 ID 超出范围。");if(seen.Add(id))result.Add(id);if(result.Count>2000)throw new Exception("清单最多包含 2000 个项目。");}return result.ToArray();
  }
  static ServerWorkshopManifest Read(string file,string endpoint){var r=LauncherNativeState.Read<ServerWorkshopManifest>(file,()=>null);if(r!=null&&(r.endpoint!=endpoint||r.ids==null||r.ids.Length==0||r.ids.Length>2000||r.stamp==null||r.stamp.Length!=32||!new[]{"connection","imported"}.Contains(r.source)||string.IsNullOrEmpty(r.capturedUtc)||!Parse(string.Join(";",r.ids)).SequenceEqual(r.ids)))throw new Exception("服务器清单记录无效，未覆盖。");return r;}
  string Stamp(string id,string endpoint,ServerWorkshopManifest r){return DlssInstaller.Hash(Encoding.UTF8.GetBytes(endpoint+"\n"+servers.TargetStamp(id)+"\n"+(r==null?"none":r.stamp)));}
  public object Get(string id){var target=servers.PublicTarget(id);string endpoint=Endpoint(target),file=FilePath(engine.Data,endpoint);return LauncherNativeState.Locked(file,()=>View(id,endpoint,Read(file,endpoint)));}
  object View(string id,string endpoint,ServerWorkshopManifest r){var titles=r==null?new Dictionary<string,string>():SaveProfiles.DisplaySnapshot(engine).Entries.Where(e=>!string.IsNullOrEmpty(e.workshopId)).GroupBy(e=>e.workshopId).ToDictionary(g=>g.Key,g=>string.Join(" / ",g.Select(e=>e.title).Distinct().Take(3)));return new{id=id,endpoint=endpoint,stamp=Stamp(id,endpoint,r),source=r==null?"none":r.source,capturedUtc=r==null?null:r.capturedUtc,items=(r==null?new string[0]:r.ids).Select(item=>new{id=item,title=titles.ContainsKey(item)?titles[item]:"",localPresent=Directory.Exists(InstallWorkflow.Source(engine,ulong.Parse(item)))}).ToArray(),summary=r==null?"尚无完整清单，可导入服主提供的工坊 ID。":"上次记录的工坊清单；服务器可能已更新，不代表模组加载顺序。"};}
  public object Import(string id,string stamp,string text){
   string[] ids=Parse(text);
   var target=servers.PublicTarget(id);
   string endpoint=Endpoint(target),file=FilePath(engine.Data,endpoint);
   return LauncherNativeState.Locked(file,()=>{
    var previous=Read(file,endpoint);
    if(stamp!=Stamp(id,endpoint,previous))throw new Exception("服务器或清单已变化，请刷新后重试。");
    var record=New(endpoint,ids,"imported");
    LauncherNativeState.Write(file,record);
    return View(id,endpoint,record);
   });
  }
  public ServerWorkshopManifest Resolve(string id,string stamp){var target=servers.PublicTarget(id);string endpoint=Endpoint(target),file=FilePath(engine.Data,endpoint);return LauncherNativeState.Locked(file,()=>{var r=Read(file,endpoint);if(r==null||stamp!=Stamp(id,endpoint,r))throw new Exception("完整清单不可用或已变化，请刷新后重试。");return new ServerWorkshopManifest{endpoint=endpoint,stamp=stamp,source=r.source,capturedUtc=r.capturedUtc,ids=r.ids.ToArray()};});}
  static ServerWorkshopManifest New(string endpoint,string[] ids,string source){return new ServerWorkshopManifest{endpoint=endpoint,stamp=Guid.NewGuid().ToString("N"),source=source,capturedUtc=DateTime.UtcNow.ToString("o"),ids=ids};}
  internal static string Revision(string data,string endpoint){
   string file=FilePath(data,endpoint);
   return LauncherNativeState.Locked(file,()=>{var current=Read(file,endpoint);return current==null?"none":current.stamp;});
  }
  public static bool Capture(string data,string endpoint,string nonce,string path,string expectedRevision=null){
   LauncherMaintenance.NoReparse(path);
   if(!File.Exists(path))return false;
   string text;
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
    var bytes=new byte[65537];
    int countRead=0,n;
    while(countRead<bytes.Length&&(n=stream.Read(bytes,countRead,bytes.Length-countRead))>0)countRead+=n;
    if(countRead>65536)return false;
    text=new UTF8Encoding(false,true).GetString(bytes,0,countRead);
   }
   string[] lines=text.Replace("\r\n","\n").TrimEnd('\n').Split('\n');
   int count;
   if(lines.Length<6||lines[0]!="ORANGE_WORKSHOP_1"||lines[1]!=nonce||lines[2]!=endpoint
    ||!int.TryParse(lines[3],out count)||count<1||count>2000
    ||lines.Length!=count+5||lines[lines.Length-1]!="END")return false;
   string[] ids;
   try{ids=Parse(string.Join(";",lines.Skip(4).Take(count)));}catch{return false;}
   if(ids.Length!=count)return false;
   string file=FilePath(data,endpoint);
   return LauncherNativeState.Locked(file,()=>{
    var current=Read(file,endpoint);
    // Legacy journals have no revision authority. They may seed an absent record only.
    if(expectedRevision==null?current!=null:expectedRevision!=(current==null?"none":current.stamp))return false;
    LauncherNativeState.Write(file,New(endpoint,ids,"connection"));
    return true;});
  }
 }
}
