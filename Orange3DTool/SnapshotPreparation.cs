using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class SaveEnvironmentMissing {
  public string modId,name,requiredBy,kind,workshopId,reason;public bool canDownload;
 }
 // Only exact identities from author metadata, owned historical manifests, or
 // launcher-owned component definitions may select a Workshop download.
 static class SnapshotPreparation {
  internal sealed class Dependencies {internal string[] Ids;internal SaveEnvironmentMissing[] Missing;}
  static string Canonical(string id){return id.TrimStart('\\');}
  static Dictionary<string,string> Info(byte[] bytes){var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(string line in Encoding.UTF8.GetString(bytes??new byte[0]).Split('\n')){int at=line.IndexOf('=');if(at>0)result[line.Substring(0,at).Trim()]=line.Substring(at+1).Trim();}return result;}
  static string KnownItem(string id){
   if(id=="ZombieBuddy")return CoreComponents.Buddy.ToString();if(id=="Viewpoint")return CoreComponents.Viewpoint.ToString();if(id=="PZVoxelStudioViewpoint")return "3810302175";if(id==ShootClient.Id)return ShootClient.Item.ToString();if(id==ChinesePatch.Id)return ChinesePatch.Item.ToString();var addon=ViewpointAddons.All.SingleOrDefault(row=>row.Id==id);return addon==null?null:addon.Item.ToString();
  }
  static string KnownName(string id){if(id=="ZombieBuddy")return "ZombieBuddy 框架";if(id=="Viewpoint")return "Viewpoint 基础视角";if(id=="PZVoxelStudioViewpoint")return "额外 3D 物件包";var addon=ViewpointAddons.All.SingleOrDefault(row=>row.Id==id);return addon==null?id:addon.Title;}
  static bool Numeric(string id){ulong value;return ulong.TryParse(id,out value)&&value>0&&value.ToString()==id;}
  internal static Dependencies Inspect(Engine engine,string[] selected,SaveProfiles.PreflightCatalog catalog,IEnumerable<SaveEnvironmentModRoot> history,CancellationToken token){
   var ids=new List<string>(selected);var seen=new HashSet<string>(ids.Select(Canonical),StringComparer.Ordinal);var missing=new List<SaveEnvironmentMissing>();var parents=new Dictionary<string,string>(StringComparer.Ordinal);var mappings=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
   Action<string,string> mapping=(id,item)=>{if(!Numeric(item))return;HashSet<string> values;if(!mappings.TryGetValue(Canonical(id),out values))mappings[Canonical(id)]=values=new HashSet<string>(StringComparer.Ordinal);values.Add(item);};
   foreach(var candidate in catalog.Candidates)mapping(candidate.Id,candidate.Workshop);foreach(var row in history??new SaveEnvironmentModRoot[0])foreach(string id in row.ModIds??new string[0])mapping(id,row.WorkshopId);
   for(int at=0;at<ids.Count;at++){
    token.ThrowIfCancellationRequested();string requested=ids[at],id=Canonical(requested),parent;parents.TryGetValue(id,out parent);var matches=catalog.Candidates.Where(row=>row.Id==id).ToArray();var entries=catalog.Entries.Where(row=>row.id==id).ToArray();string known=KnownItem(id);if(known!=null)mapping(id,known);HashSet<string> workshops;mappings.TryGetValue(id,out workshops);string item=workshops!=null&&workshops.Count==1?workshops.Single():"";
    if(matches.Length!=1||entries.Length!=1){
     bool conflict=matches.Length>1||entries.Length>1||workshops!=null&&workshops.Count>1;bool variant=!conflict&&item.Length>0&&catalog.Candidates.Any(row=>row.Workshop==item);
     missing.Add(new SaveEnvironmentMissing{modId=id,name=KnownName(id),requiredBy=parent??"",kind=conflict?"ambiguous-source":variant?"mod-variant":"workshop-mod",workshopId=conflict?"":item,canDownload=!conflict&&item.Length>0,reason=conflict?"同一模组标识存在多个本地来源或工坊映射，无法安全选择。":variant?"已发现对应工坊项目，但当前游戏版本没有该标识的可用子模组；将重新下载并核实该变体。":item.Length>0?"本地缺少此模组；已核实对应工坊项目，可自动下载。":"存档只记录模组标识，本地目录与已有快照没有唯一的工坊 ID 映射，无法安全自动下载。"});continue;
    }
    var entry=entries[0];foreach(string dependency in entry.requires){string dep=Canonical(dependency);if(!SaveProfiles.Id(dep))throw new IOException("模组声明无效依赖："+entry.name+" ("+id+") → "+dependency);if(seen.Add(dep)){if(ids.Count>=2000)throw new IOException("递归依赖数量超过快照安全上限。");parents[dep]=entry.name+" ("+id+")";ids.Add(dependency);}}
    var metadata=Info(matches[0].Common);foreach(var pair in Info(matches[0].Active))metadata[pair.Key]=pair.Value;string jar;if(metadata.TryGetValue("javaJarFile",out jar)&&!string.IsNullOrWhiteSpace(jar)){
     string root=VersionLibrary.Full(matches[0].Root),path=VersionLibrary.Full(Path.Combine(matches[0].Layout,jar.Replace('/',Path.DirectorySeparatorChar)));LauncherMaintenance.NoReparse(path);if(!VersionLibrary.Within(path,root)||VersionLibrary.Same(path,root))throw new IOException("Java 组件路径超出模组来源："+entry.name+" ("+id+")");if(!File.Exists(path)&&matches[0].CommonRoot!=null){string common=VersionLibrary.Full(Path.Combine(matches[0].CommonRoot,jar.Replace('/',Path.DirectorySeparatorChar)));LauncherMaintenance.NoReparse(common);if(!VersionLibrary.Within(common,root)||VersionLibrary.Same(common,root))throw new IOException("Java 组件路径超出模组来源："+entry.name+" ("+id+")");if(File.Exists(common))path=common;}
     if(!File.Exists(path))missing.Add(new SaveEnvironmentMissing{modId=id,name=entry.name,requiredBy=parent??"",kind="java-component",workshopId=item,canDownload=item.Length>0,reason="模组元数据声明的 Java 组件缺失："+jar+(item.Length>0?"；将重新下载所属工坊项目并验证文件。":"；此本地模组没有唯一的工坊映射，无法自动补齐。")});
    }
   }
   return new Dependencies{Ids=ids.ToArray(),Missing=missing.ToArray()};
  }
  internal static string Describe(SaveEnvironmentMissing item){return item.name+" ("+item.modId+")"+(string.IsNullOrEmpty(item.requiredBy)?"":"，依赖来源："+item.requiredBy)+"："+item.reason;}
  // Implicit require dependencies load before their parent in the game itself.
  // Keep the relative order of every explicitly captured mod identity.
  internal static string[] Expand(string[] selected,SaveProfiles.PreflightCatalog catalog){
   var explicitIds=new HashSet<string>(selected.Select(Canonical),StringComparer.Ordinal);var output=new List<string>();var emitted=new HashSet<string>(StringComparer.Ordinal);var visiting=new HashSet<string>(StringComparer.Ordinal);
   Action<string> add=null;add=id=>{string canonical=Canonical(id);if(emitted.Contains(canonical)||!visiting.Add(canonical))return;var entries=catalog.Entries.Where(row=>row.id==canonical).ToArray();if(entries.Length==1)foreach(string dep in entries[0].requires)if(!explicitIds.Contains(Canonical(dep)))add(dep);visiting.Remove(canonical);if(emitted.Add(canonical))output.Add(id);};foreach(string id in selected)add(id);return output.ToArray();
  }
  internal static SaveEnvironmentPlan Prepare(SaveEnvironments library,string saveId,string expectedStamp,CancellationToken token,Action<SaveEnvironmentProgress> progress,Func<string,Action<int,string>,CancellationToken,object> download){
   if(library==null||download==null)throw new ArgumentNullException(library==null?"library":"download");var plan=library.Preview(saveId,token,progress);if(plan.stamp!=expectedStamp)throw new IOException("快照预览已变化，请重新读取后创建。");string initialBinding=plan.activeSnapshotId;var attempted=new HashSet<string>(StringComparer.Ordinal);
   while(!plan.ready){
    token.ThrowIfCancellationRequested();if(!plan.canPrepare||plan.missing.Length==0)throw new IOException(string.Join("\n",plan.warnings));var items=plan.missing.GroupBy(row=>row.workshopId,StringComparer.Ordinal).ToArray();
    foreach(var group in items){token.ThrowIfCancellationRequested();if(!attempted.Add(group.Key))throw new IOException("工坊下载完成后仍缺少所需内容，未创建快照：\n"+string.Join("\n",group.Select(Describe)));string names=string.Join("、",group.Select(row=>row.name+" ("+row.modId+")"));Action<int,string> report=(value,text)=>{token.ThrowIfCancellationRequested();if(progress!=null)progress(new SaveEnvironmentProgress{stage="dependency-download",text="正在下载所需模组 · "+names+" · "+text,value=value<0?-1:Math.Min(value,100)});};report(-1,"工坊 "+group.Key);object result;try{result=download(group.Key,report,token);}catch(OperationCanceledException){throw;}catch(Exception error){throw new IOException("所需模组下载失败："+names+"，工坊 "+group.Key+"。"+error.Message,error);}token.ThrowIfCancellationRequested();var serializer=new JavaScriptSerializer();var response=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(result));object resultId;if(response==null||!response.TryGetValue("id",out resultId)||!(resultId is string)||(string)resultId!=group.Key)throw new IOException("Steam 未返回所需工坊项目的已核实结果："+names+" ("+group.Key+")。");}
    plan=library.Preview(saveId,token,progress);if(plan.activeSnapshotId!=initialBinding)throw new IOException("此存档的快照绑定在依赖下载期间改变，已保留最新选择，未自动创建或启用新快照。");
   }
   token.ThrowIfCancellationRequested();return plan;
  }
 }
}
