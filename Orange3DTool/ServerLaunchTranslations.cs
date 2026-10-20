using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace PZ3D {
 // Derive separate text-only mods so the game merges keys, not whole files.
 static class ServerLaunchTranslations {
  internal static readonly string[] Ids={"B42ModTrans_CN","B42Trans_CN"};
  internal static string OverlayId(string id){return "OZLServerTranslations_"+id;}
  internal static string Root(Engine e,string id){return Path.Combine(e.User,"mods",OverlayId(id));}
  internal static IEnumerable<string> GeneratedIds(Engine e,IEnumerable<string> selected,Dictionary<string,byte[]> files){return selected.Where(Ids.Contains).Distinct().Where(id=>files.ContainsKey(Path.Combine(Root(e,id),"42","mod.info"))).Select(OverlayId);}
  internal static string[] OwnedIds(Engine e,IEnumerable<string> files){var paths=new HashSet<string>(files.Select(Path.GetFullPath),StringComparer.OrdinalIgnoreCase);return Ids.Where(id=>paths.Contains(Path.Combine(Root(e,id),"42","mod.info"))).Select(OverlayId).ToArray();}
  internal static bool IsOverlay(string id){return Ids.Any(source=>OverlayId(source)==id);}
  const int MaxFiles=4089,MaxFileBytes=4194304,MaxBytes=67108864;
  static Version Normal(Version v){return new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));}
  internal static bool OwnedPath(Engine e,string file){
   string path=Path.GetFullPath(file),ext=Path.GetExtension(path);
   foreach(string id in Ids)foreach(string part in new[]{"common","42"}){
    string root=Path.GetFullPath(Path.Combine(Root(e,id),part));
    if(string.Equals(path,Path.Combine(root,"mod.info"),StringComparison.OrdinalIgnoreCase))return true;
    string texts=Path.Combine(root,"media","lua","shared","Translate")+Path.DirectorySeparatorChar;
    if(path.StartsWith(texts,StringComparison.OrdinalIgnoreCase)&&(ext.Equals(".txt",StringComparison.OrdinalIgnoreCase)||ext.Equals(".json",StringComparison.OrdinalIgnoreCase)))return true;
   }
   return false;
  }
  static void CopyTexts(string source,string target,Dictionary<string,byte[]> output,ref long bytes,int depth){
   LauncherMaintenance.NoReparse(source);if(!Directory.Exists(source))return;
   if(depth>16)throw new IOException("汉化目录层级异常。");
   foreach(string file in Directory.GetFiles(source)){
    LauncherMaintenance.NoReparse(file);string ext=Path.GetExtension(file);
    if(!ext.Equals(".txt",StringComparison.OrdinalIgnoreCase)&&!ext.Equals(".json",StringComparison.OrdinalIgnoreCase))continue;
    byte[] content;
    using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     long size=stream.Length;
     if(size>MaxFileBytes||bytes+size>MaxBytes||output.Count>=MaxFiles)throw new IOException("联机汉化文本超出读取范围。");
     content=new byte[(int)size];int offset=0;
     while(offset<content.Length){int read=stream.Read(content,offset,content.Length-offset);if(read==0)throw new IOException("联机汉化文本在读取期间改变。");offset+=read;}
     if(stream.ReadByte()!=-1)throw new IOException("联机汉化文本在读取期间改变。");
    }
    bytes+=content.Length;output.Add(Path.Combine(target,Path.GetFileName(file)),content);
   }
   foreach(string child in Directory.GetDirectories(source))CopyTexts(child,Path.Combine(target,Path.GetFileName(child)),output,ref bytes,depth+1);
  }
  internal static Dictionary<string,byte[]> Build(Engine e,IEnumerable<string> selected){
   var output=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);long bytes=0;
   Version current=Normal(GameVersionProbe.Read(e.Game)??new Version(42,0));
   foreach(string id in selected.Where(Ids.Contains).Distinct()){
    ulong item=id==Ids[0]?3556540080UL:3556544454UL;AddonFiles files;
    try{files=ViewpointAddons.Resolve(e,new ViewpointAddon("translation",item,id,id,""),current);}
    catch(Exception){if(e.Progress!=null)e.Progress(-1,"未找到统一汉化来源："+id+"；保留原模组，由游戏处理加载。");continue;}
    string active=files.Folder;Version best=null;
    foreach(string dir in Directory.GetDirectories(files.Folder)){
     LauncherMaintenance.NoReparse(dir);Version candidate;string name=Path.GetFileName(dir);
     if(!Version.TryParse(name.Contains(".")?name:name+".0",out candidate))continue;
     candidate=Normal(candidate);
     if(candidate.Major==current.Major&&candidate<=current&&(best==null||candidate>best)){best=candidate;active=dir;}
    }
    int before=output.Count;string root=Root(e,id),common=Path.Combine(files.Folder,"common");
    CopyTexts(Path.Combine(common,"media","lua","shared","Translate"),Path.Combine(root,"common","media","lua","shared","Translate"),output,ref bytes,0);
    if(!string.Equals(common,active,StringComparison.OrdinalIgnoreCase))CopyTexts(Path.Combine(active,"media","lua","shared","Translate"),Path.Combine(root,"42","media","lua","shared","Translate"),output,ref bytes,0);
    if(output.Count>before){
     byte[] info=Encoding.UTF8.GetBytes("name=OZL 联机中文文本 · "+id+"\nid="+OverlayId(id)+"\ndescription=仅本次联机使用已启用汉化的翻译文本，不含脚本或地图。\n");
     output.Add(Path.Combine(root,"common","mod.info"),info);output.Add(Path.Combine(root,"42","mod.info"),info);
    }
   }
   return output;
  }
  internal static void RemoveEmptyParents(Engine e,IEnumerable<string> files){
   var parents=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(string file in files){
    if(!OwnedPath(e,file))continue;
    string path=Path.GetFullPath(file),root=Ids.Select(id=>Path.GetFullPath(Root(e,id))).First(p=>path.StartsWith(p+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
    string dir=Path.GetDirectoryName(path);
    while(dir==root||dir.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){parents.Add(dir);if(dir==root)break;dir=Path.GetDirectoryName(dir);}
   }
   foreach(string dir in parents.OrderByDescending(p=>p.Length)){LauncherMaintenance.NoReparse(dir);if(Directory.Exists(dir)&&!Directory.EnumerateFileSystemEntries(dir).Any())Directory.Delete(dir,false);}
  }
 }
}
