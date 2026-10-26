using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class SaveEnvironmentSummary {
  public string id,name,saveId,gameVersion,createdUtc,rootPath;public int modCount;public long bytes;public bool active;
 }
 sealed class SaveEnvironmentSaveSummary {public string id,name;}
 sealed class SaveEnvironmentListView {
  public string stamp="",selectedSaveId="",selectedSaveName="",activeSnapshotId="";
  public bool saveBackupSeparate=true;
  public SaveEnvironmentSaveSummary[] saves=new SaveEnvironmentSaveSummary[0];
  public SaveEnvironmentSummary[] snapshots=new SaveEnvironmentSummary[0];
  public string[] warnings=new string[0];
 }
 sealed partial class ModernController {
  internal static readonly string[] EnvironmentReads={"environmentList","environmentPreview","environmentCheck"};
  static readonly HashSet<string> EnvironmentActions=new HashSet<string>{"environmentList","environmentPreview","environmentCreate","environmentChooseCreate","environmentBind","environmentUnbind","environmentCheck"};
  internal static bool EnvironmentAction(string action){return action!=null&&EnvironmentActions.Contains(action);}
  internal static bool EnvironmentWebAction(string action){return EnvironmentAction(action)&&action!="environmentCreate";}
  internal static void ValidateEnvironmentWebPayload(string action,Dictionary<string,object> payload){ValidateEnvironmentPayload(action,payload,true);}
  internal static void ValidateEnvironmentPayload(string action,Dictionary<string,object> payload,bool fromWeb=false){
   if(!EnvironmentAction(action))return;if(payload==null)throw new IOException("快照请求参数无效。");if(fromWeb&&action=="environmentCreate")throw new IOException("请使用文件夹选择器创建快照。");string[] keys;
   switch(action){case "environmentList":keys=new[]{"saveId"};break;case "environmentPreview":keys=new[]{"saveId"};break;case "environmentCreate":keys=new[]{"saveId","destination","name","stamp"};break;case "environmentChooseCreate":keys=new[]{"saveId","name","stamp"};break;case "environmentBind":keys=new[]{"saveId","snapshotId","stamp"};break;case "environmentUnbind":keys=new[]{"saveId","stamp"};break;case "environmentCheck":keys=new[]{"snapshotId"};break;default:throw new IOException("未知快照操作。");}
   if(payload.Keys.Except(keys).Any())throw new IOException("快照请求包含无关参数。");foreach(string key in keys){object raw;if(!payload.TryGetValue(key,out raw)){if(action=="environmentList"&&key=="saveId")continue;throw new IOException("缺少快照参数："+key);}string value=raw as string;if(value==null||value.Any(char.IsControl)||value.Length>4096)throw new IOException("快照参数无效："+key);if(key=="saveId"&&(!SaveDeletion.ValidId(value)||value.Split('/')[0].Equals("Multiplayer",StringComparison.OrdinalIgnoreCase)))throw new IOException("请选择有效的单人存档。");if(key=="snapshotId"&&!Regex.IsMatch(value,@"\A[a-f0-9]{32}\z"))throw new IOException("快照标识无效。");if(key=="stamp"&&!Regex.IsMatch(value,@"\A[a-f0-9]{64}\z"))throw new IOException("快照预览或绑定凭据无效，请重新读取。");if(key=="name"&&value.Length>100)throw new IOException("快照名称最多 100 个字符。");if(key=="destination"&&(!Path.IsPathRooted(value)||string.IsNullOrWhiteSpace(value)))throw new IOException("快照位置必须通过本机文件夹选择确定。");}
  }
  static SaveEnvironmentSummary EnvironmentSummary(SaveEnvironmentRecord row,string active){return new SaveEnvironmentSummary{id=row.Id,name=row.Name,saveId=row.SaveId,gameVersion=row.GameVersion,modCount=row.ModIds.Concat(row.DefaultModIds).Concat(row.PreloadModIds).Select(id=>id.TrimStart('\\')).Distinct(StringComparer.Ordinal).Count(),bytes=row.Bytes,createdUtc=row.CreatedUtc,rootPath=row.RootPath,active=row.Id==active};}
  SaveEnvironmentListView ReadEnvironments(string requestedSaveId=null,bool cancellable=true){
   if(cancellable)cancel.Token.ThrowIfCancellationRequested();var serializer=new JavaScriptSerializer{MaxJsonLength=4194304};var saveList=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(SaveProfiles.List(engine)));var rows=((IEnumerable)saveList["saves"]).Cast<Dictionary<string,object>>().Where(row=>row.ContainsKey("editable")&&row["editable"] is bool&&(bool)row["editable"]&&!((string)row["mode"]).Equals("Multiplayer",StringComparison.OrdinalIgnoreCase)).Select(row=>new SaveEnvironmentSaveSummary{id=(string)row["id"],name=(string)row["name"]}).OrderBy(row=>row.name,StringComparer.OrdinalIgnoreCase).ThenBy(row=>row.id,StringComparer.Ordinal).ToArray();
   var result=new SaveEnvironmentListView{saves=rows,warnings=((IEnumerable)saveList["warnings"]).Cast<string>().ToArray()};if(requestedSaveId!=null&&!rows.Any(row=>row.id==requestedSaveId))throw new IOException("所选单人存档不存在，请重新读取列表。");var selected=requestedSaveId==null?rows.FirstOrDefault():rows.Single(row=>row.id==requestedSaveId);if(selected==null)return result;result.selectedSaveId=selected.id;result.selectedSaveName=selected.name;
   var library=new SaveEnvironments(engine);result.stamp=library.BindingStamp(selected.id);var snapshots=library.List(selected.id);var active=library.Bound(selected.id);result.activeSnapshotId=active==null?"":active.Id;if(cancellable)cancel.Token.ThrowIfCancellationRequested();result.snapshots=snapshots.Select(row=>EnvironmentSummary(row,result.activeSnapshotId)).ToArray();if(library.BindingStamp(selected.id)!=result.stamp)throw new IOException("快照绑定在读取期间变化，请重新读取。");return result;
  }
  object DispatchEnvironment(string action,Dictionary<string,object> payload){
   if(!EnvironmentAction(action))throw new IOException("未知快照操作。");ValidateEnvironmentPayload(action,payload);cancel.Token.ThrowIfCancellationRequested();var library=new SaveEnvironments(engine);
   switch(action){
    case "environmentList":return ReadEnvironments(payload.ContainsKey("saveId")?Text(payload,"saveId"):null);
    case "environmentPreview":Report(-1,"正在读取游戏与模组快照大小");return library.Preview(Text(payload,"saveId"),cancel.Token);
    case "environmentCheck":Report(-1,"正在核对当前游戏与模组来源");return library.CheckUpdates(Text(payload,"snapshotId"),cancel.Token);
    case "environmentChooseCreate":throw new IOException("请使用本机文件夹选择器创建快照。");
    case "environmentCreate":RequireVersionIdle();Report(-1,"正在核对游戏快照兼容性");SaveEnvironmentRuntime.CheckSource(engine,cancel.Token);cancel.Token.ThrowIfCancellationRequested();Report(-1,"正在创建游戏与模组快照");var created=library.Create(Text(payload,"saveId"),Text(payload,"destination"),Text(payload,"name"),Text(payload,"stamp"),cancel.Token,Report,row=>SaveEnvironmentRuntime.Prepare(engine,row));var createdList=ReadEnvironments(created.SaveId,false);return new{message="游戏与模组快照已创建，此存档将自动使用。",snapshot=EnvironmentSummary(created,createdList.activeSnapshotId),list=createdList};
    case "environmentBind":RequireVersionIdle();var selected=library.Bind(Text(payload,"saveId"),Text(payload,"snapshotId"),Text(payload,"stamp"));var selectedList=ReadEnvironments(selected.SaveId,false);return new{message="此存档已改用所选游戏与模组快照。",snapshot=EnvironmentSummary(selected,selectedList.activeSnapshotId),list=selectedList};
    case "environmentUnbind":RequireVersionIdle();string saveId=Text(payload,"saveId");library.Unbind(saveId,Text(payload,"stamp"));return new{message="此存档将使用当前环境，已有快照继续保留。",list=ReadEnvironments(saveId,false)};
    default:throw new IOException("未知快照操作。");
   }
  }
 }
}
