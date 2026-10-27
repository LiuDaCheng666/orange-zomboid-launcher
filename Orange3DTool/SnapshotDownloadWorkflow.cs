using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed partial class ModernController {
  static string SnapshotSteamRoot(string game){
   if(!GameDiscovery.IsGameFolder(game))return null;string common=Path.GetDirectoryName(Path.GetFullPath(game)),apps=common==null?null:Path.GetDirectoryName(common);
   return common!=null&&apps!=null&&Path.GetFileName(common).Equals("common",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(apps).Equals("steamapps",StringComparison.OrdinalIgnoreCase)?Path.GetDirectoryName(apps):null;
  }
  Engine SnapshotDownloadEngine(Engine selected){
   // Production uses registered Steam libraries. A game backup may retain the
   // steamapps/common layout and must never become a download installation.
   var roots=new List<string>();if(test){string fixtureRoot=SnapshotSteamRoot(legacyGame);if(fixtureRoot!=null)roots.Add(fixtureRoot);}else roots.AddRange(GameDiscovery.SteamRoots());
   string installation=GameDiscovery.Find(roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
   if(string.IsNullOrEmpty(installation))throw new IOException("未找到 Steam 安装的僵尸毁灭工程，无法下载缺失模组。请先在 Steam 安装游戏后重试。");
   LauncherMaintenance.NoReparse(installation);if(VersionLibrary.Same(installation,selected.Game))return selected;
   return new Engine(installation,selected.User,selected.Data,selected.TestMode);
  }
  object DownloadSnapshotWorkshop(Engine selected,string item,Action<int,string> progress,CancellationToken token){
   return WithSteamLane(token,()=>{
    token.ThrowIfCancellationRequested();var download=SnapshotDownloadEngine(selected);var result=networkFetch(download,"download",new Dictionary<string,object>{{"id",item},{"confirmed",true}},progress,token,null);token.ThrowIfCancellationRequested();
    var json=new JavaScriptSerializer{MaxJsonLength=4194304};var response=json.Deserialize<Dictionary<string,object>>(json.Serialize(result));object returned;
    if(response==null||!response.TryGetValue("id",out returned)||!(returned is string)||(string)returned!=item)throw new IOException("Steam 未返回所需的工坊项目："+item+"。");
    // Native download already checks Steam's GetItemInstallInfo against this exact
    // App108600 library path. Record it for independent versions' source discovery.
    string installed=Path.GetFullPath(Path.Combine(download.Workshop,item));LauncherMaintenance.NoReparse(installed);if(!Directory.Exists(installed))throw new IOException("Steam 下载结果的模组目录不存在："+item+"。");
    if(!VersionLibrary.Same(download.Game,selected.Game)){
     string record=Path.Combine(selected.Data,"workshop-paths.json");LauncherMaintenance.NoReparse(record);Directory.CreateDirectory(selected.Data);
     var paths=File.Exists(record)?json.Deserialize<Dictionary<string,string>>(File.ReadAllText(record,Encoding.UTF8)):new Dictionary<string,string>();if(paths==null)throw new IOException("工坊来源记录无法读取。");paths[item]=installed;
     string temporary=record+".new-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temporary,json.Serialize(paths),new UTF8Encoding(false));if(File.Exists(record))File.Replace(temporary,record,null);else File.Move(temporary,record);}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    return result;
   });
  }
  string ResolveSnapshotWorkshop(Engine selected,SaveEnvironmentMissing missing,Action<int,string> progress,CancellationToken token){
   var result=WithSteamLane(token,()=>{var download=SnapshotDownloadEngine(selected);return SnapshotWorkshopResolver.Resolve(missing.modId,new HashSet<string>(StringComparer.Ordinal),query=>networkFetch(download,"search",new Dictionary<string,object>{{"query",query},{"page",1}},progress,token,null),token,progress);});
   if(result.candidates.Length>1){var previous=SnapshotWorkshopResolver.HistoricalItems(selected.User,token,progress);result=SnapshotWorkshopResolver.Select(result.candidates,previous);}
   if(result.workshopId.Length>0)return result.workshopId;
   if(result.candidates.Length>1)throw new IOException(missing.modId+" 找到了多个工坊项目："+string.Join("；",result.candidates.Select(row=>row.name+"（"+row.workshopId+"）"))+"。请在此模组的工坊来源中选择一个。");
   throw new IOException(missing.modId+" 没有搜到仍提供此标识的工坊项目。可填写原工坊链接重试；作者改名或删除的旧模组需要选择新版或原文件。");
  }
 }
}
