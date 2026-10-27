using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class SaveEnvironmentMissing {
  public string modId,name,requiredBy,kind,workshopId,reason,replacementId="",replacementName="";public bool canDownload,canSearch;
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
  static void Replacement(SaveEnvironmentMissing missing,SaveProfiles.PreflightCatalog catalog,CancellationToken token){
   var matches=new List<SaveProfiles.Candidate>();string baseId=System.Text.RegularExpressions.Regex.Replace(missing.modId,@"\.\d+$","");
   foreach(var candidate in catalog.Candidates){token.ThrowIfCancellationRequested();if(candidate.Id==missing.modId)continue;bool found=baseId!=missing.modId&&candidate.Id==baseId;if(!found){LauncherMaintenance.NoReparse(candidate.Root);foreach(string directory in Directory.EnumerateDirectories(candidate.Root)){token.ThrowIfCancellationRequested();Version version;if(!Version.TryParse(Path.GetFileName(directory),out version)&&Path.GetFileName(directory)!="42")continue;string file=Path.Combine(directory,"mod.info");LauncherMaintenance.NoReparse(file);if(File.Exists(file)&&new FileInfo(file).Length<=262144){string previousId;if(Info(File.ReadAllBytes(file)).TryGetValue("id",out previousId)&&previousId==missing.modId){found=true;break;}}}}if(found)matches.Add(candidate);}
   if(matches.Count!=1)return;var selected=matches[0];var metadata=Info(selected.Common);foreach(var pair in Info(selected.Active))metadata[pair.Key]=pair.Value;string name;missing.replacementId=selected.Id;missing.replacementName=metadata.TryGetValue("name",out name)?name:selected.Id;missing.workshopId=selected.Workshop;missing.canSearch=missing.canDownload=false;missing.kind="changed-mod-id";missing.reason="当前模组使用新标识 "+selected.Id+"，可选择使用新版或跳过此项。";
  }
  internal static Dependencies Inspect(Engine engine,string[] selected,SaveProfiles.PreflightCatalog catalog,IEnumerable<SaveEnvironmentModRoot> history,CancellationToken token){
   var ids=new List<string>(selected);var seen=new HashSet<string>(ids.Select(Canonical),StringComparer.Ordinal);var missing=new List<SaveEnvironmentMissing>();var parents=new Dictionary<string,string>(StringComparer.Ordinal);var mappings=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
   Action<string,string> mapping=(id,item)=>{if(!Numeric(item))return;HashSet<string> values;if(!mappings.TryGetValue(Canonical(id),out values))mappings[Canonical(id)]=values=new HashSet<string>(StringComparer.Ordinal);values.Add(item);};
   foreach(var candidate in catalog.Candidates)mapping(candidate.Id,candidate.Workshop);foreach(var row in history??new SaveEnvironmentModRoot[0])foreach(string id in row.ModIds??new string[0])mapping(id,row.WorkshopId);
   for(int at=0;at<ids.Count;at++){
    token.ThrowIfCancellationRequested();string requested=ids[at],id=Canonical(requested),parent;parents.TryGetValue(id,out parent);var matches=catalog.Candidates.Where(row=>row.Id==id).ToArray();var entries=catalog.Entries.Where(row=>row.id==id).ToArray();string known=KnownItem(id);if(known!=null)mapping(id,known);HashSet<string> workshops;mappings.TryGetValue(id,out workshops);string item=workshops!=null&&workshops.Count==1?workshops.Single():"";
    if(matches.Length!=1||entries.Length!=1){
     bool conflict=matches.Length>1||entries.Length>1||workshops!=null&&workshops.Count>1;bool variant=!conflict&&item.Length>0&&catalog.Candidates.Any(row=>row.Workshop==item);
     missing.Add(new SaveEnvironmentMissing{modId=id,name=KnownName(id),requiredBy=parent??"",kind=conflict?"ambiguous-source":variant?"mod-variant":"workshop-mod",workshopId=conflict?"":item,canDownload=!conflict&&item.Length>0,canSearch=!conflict&&item.Length==0,reason=conflict?"发现多个可用来源，请选择要使用的工坊项目。":variant?"工坊项目已找到，但当前游戏版本没有这个子模组；创建时重新下载并读取。":item.Length>0?"创建时自动从 Steam 工坊补齐。":"创建时搜索 Steam 工坊并补齐。"});continue;
    }
    var entry=entries[0];foreach(string dependency in entry.requires){string dep=Canonical(dependency);if(!SaveProfiles.Id(dep))throw new IOException("模组声明无效依赖："+entry.name+" ("+id+") → "+dependency);if(seen.Add(dep)){if(ids.Count>=2000)throw new IOException("递归依赖数量超过快照安全上限。");parents[dep]=entry.name+" ("+id+")";ids.Add(dependency);}}
    var metadata=Info(matches[0].Common);foreach(var pair in Info(matches[0].Active))metadata[pair.Key]=pair.Value;string jar;if(metadata.TryGetValue("javaJarFile",out jar)&&!string.IsNullOrWhiteSpace(jar)){
     string root=VersionLibrary.Full(matches[0].Root),path=VersionLibrary.Full(Path.Combine(matches[0].Layout,jar.Replace('/',Path.DirectorySeparatorChar)));LauncherMaintenance.NoReparse(path);if(!VersionLibrary.Within(path,root)||VersionLibrary.Same(path,root))throw new IOException("Java 组件路径超出模组来源："+entry.name+" ("+id+")");if(!File.Exists(path)&&matches[0].CommonRoot!=null){string common=VersionLibrary.Full(Path.Combine(matches[0].CommonRoot,jar.Replace('/',Path.DirectorySeparatorChar)));LauncherMaintenance.NoReparse(common);if(!VersionLibrary.Within(common,root)||VersionLibrary.Same(common,root))throw new IOException("Java 组件路径超出模组来源："+entry.name+" ("+id+")");if(File.Exists(common))path=common;}
     if(!File.Exists(path))missing.Add(new SaveEnvironmentMissing{modId=id,name=entry.name,requiredBy=parent??"",kind="java-component",workshopId=item,canDownload=item.Length>0,reason="模组元数据声明的 Java 组件缺失："+jar+(item.Length>0?"；将重新下载所属工坊项目并验证文件。":"；此本地模组没有唯一的工坊映射，无法自动补齐。")});
    }
   }
   foreach(var item in missing.Where(row=>row.canSearch))Replacement(item,catalog,token);return new Dependencies{Ids=ids.ToArray(),Missing=missing.ToArray()};
  }
  internal static string Describe(SaveEnvironmentMissing item){return item.name+" ("+item.modId+")"+(string.IsNullOrEmpty(item.requiredBy)?"":"，依赖来源："+item.requiredBy)+"："+item.reason;}
  // Implicit require dependencies load before their parent in the game itself.
  // Keep the relative order of every explicitly captured mod identity.
  internal static string[] Expand(string[] selected,SaveProfiles.PreflightCatalog catalog){
   var explicitIds=new HashSet<string>(selected.Select(Canonical),StringComparer.Ordinal);var output=new List<string>();var emitted=new HashSet<string>(StringComparer.Ordinal);var visiting=new HashSet<string>(StringComparer.Ordinal);
   Action<string> add=null;add=id=>{string canonical=Canonical(id);if(emitted.Contains(canonical)||!visiting.Add(canonical))return;var entries=catalog.Entries.Where(row=>row.id==canonical).ToArray();if(entries.Length==1)foreach(string dep in entries[0].requires)if(!explicitIds.Contains(Canonical(dep)))add(dep);visiting.Remove(canonical);if(emitted.Add(canonical))output.Add(id);};foreach(string id in selected)add(id);return output.ToArray();
  }
  internal static SaveEnvironmentPlan Prepare(SaveEnvironments library,string saveId,string expectedStamp,CancellationToken token,Action<SaveEnvironmentProgress> progress,Func<string,Action<int,string>,CancellationToken,object> download,Func<SaveEnvironmentMissing,Action<int,string>,CancellationToken,string> resolve=null){
   // Preparation deliberately refreshes the current files. A previous download
   // may have succeeded before a later one failed; its old UI stamp is no lock.
   // Create still checks the fresh capture throughout copying and publication.
   if(library==null||download==null)throw new ArgumentNullException(library==null?"library":"download");var plan=library.Preview(saveId,token,progress);string initialBinding=plan.activeSnapshotId;var attempted=new HashSet<string>(StringComparer.Ordinal);
   while(!plan.ready){
    token.ThrowIfCancellationRequested();if(!plan.canPrepare||plan.missing.Length==0)throw new IOException(string.Join("\n",plan.warnings));foreach(var missing in plan.missing.Where(row=>row.canSearch)){if(resolve==null)throw new IOException("请为 "+missing.modId+" 选择对应的工坊项目。");Action<int,string> searching=(value,text)=>{token.ThrowIfCancellationRequested();if(progress!=null)progress(new SaveEnvironmentProgress{stage="dependency-search",text=text,value=-1});};string found=resolve(missing,searching,token);if(!Numeric(found))throw new IOException("没有找到 "+missing.modId+" 对应的工坊项目，请填写它的工坊链接后重试。");missing.workshopId=found;missing.canDownload=true;}var items=plan.missing.GroupBy(row=>row.workshopId,StringComparer.Ordinal).ToArray();
    foreach(var group in items){token.ThrowIfCancellationRequested();if(!attempted.Add(group.Key))throw new IOException("工坊下载完成后仍缺少所需内容，未创建快照：\n"+string.Join("\n",group.Select(Describe)));string names=string.Join("、",group.Select(row=>row.name+" ("+row.modId+")"));Action<int,string> report=(value,text)=>{token.ThrowIfCancellationRequested();if(progress!=null)progress(new SaveEnvironmentProgress{stage="dependency-download",text="正在下载所需模组 · "+names+" · "+text,value=value<0?-1:Math.Min(value,100)});};report(-1,"工坊 "+group.Key);object result;try{result=download(group.Key,report,token);}catch(OperationCanceledException){throw;}catch(Exception error){throw new IOException("所需模组下载失败："+names+"，工坊 "+group.Key+"。"+error.Message,error);}token.ThrowIfCancellationRequested();var serializer=new JavaScriptSerializer();var response=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(result));object resultId;if(response==null||!response.TryGetValue("id",out resultId)||!(resultId is string)||(string)resultId!=group.Key)throw new IOException("Steam 未返回所需工坊项目的已核实结果："+names+" ("+group.Key+")。");}
    plan=library.Preview(saveId,token,progress);if(plan.activeSnapshotId!=initialBinding)throw new IOException("此存档的快照绑定在依赖下载期间改变，已保留最新选择，未自动创建或启用新快照。");
   }
   token.ThrowIfCancellationRequested();return plan;
  }
 }
}
