using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace PZ3D {
 sealed partial class ModernController {
  VersionLibrary versions;SteamVersionWorkflow versionInstall;RecommendedServers recommendations;SafehouseTransfer houseTransfer;JavaComponents javaComponents;ForceTakeover forceTakeover;
  sealed class AutomaticHouseBinding {internal string SourceId,CatalogStamp,HouseId,NativeStamp;}
  readonly Dictionary<string,AutomaticHouseBinding> automaticHousePreviews=new Dictionary<string,AutomaticHouseBinding>();
  void ResetContentLibrary(){DisposeContentLibrary();lock(automaticHousePreviews)automaticHousePreviews.Clear();houseTransfer=new SafehouseTransfer(engine);javaComponents=new JavaComponents(engine,Report);forceTakeover=new ForceTakeover(engine,Report);}
  JavaComponentWorkflow JavaWorkflow(){return new JavaComponentWorkflow(engine,javaComponents,(id,report,token)=>{DownloadSnapshotWorkshop(engine,id,report,token);},(id,report,token)=>ResolveSnapshotWorkshop(engine,new SaveEnvironmentMissing{modId=id},report,token),key=>Components(key,"install",new Dictionary<string,object>{{"confirmed",true}}),Report);}
  void DisposeContentLibrary(){if(houseTransfer!=null)houseTransfer.Dispose();if(javaComponents!=null)javaComponents.Dispose();if(forceTakeover!=null)forceTakeover.Dispose();}
  string legacyGame,defaultUser;
  internal static bool LibraryWebAction(string action){return LibraryActions.Contains(action)&&action!="versionImport"&&action!="versionPreserve"&&action!="versionInstallBegin";}
  internal static void ValidateLibraryWebPayload(string action,Dictionary<string,object> payload){
   if(LibraryWebAction(action)){if(action=="backupSettingsSave"&&payload.ContainsKey("directory"))throw new IOException("请使用文件夹选择更换备份位置。");ValidateLibraryPayload(action,payload);return;}
   string mapped,key;switch(action){case "versionChooseImport":mapped="versionImport";key="path";break;case "versionChoosePreserve":mapped="versionPreserve";key="destination";break;case "versionChooseInstall":mapped="versionInstallBegin";key="destination";break;case "backupChooseDirectory":mapped="backupSettingsSave";key="directory";break;default:return;}
   if(payload.ContainsKey(key))throw new IOException("请使用文件夹选择器。");var selected=new Dictionary<string,object>(payload);selected[key]="preview";ValidateLibraryPayload(mapped,selected);
  }
  internal static readonly HashSet<string> LibraryActions=new HashSet<string>{"javaComponentsInstall","javaComponentsEnable","javaComponentsReconcile","versionList","versionSelect","versionImport","versionPreserve","versionInstallBegin","versionInstallState","versionInstallComplete","versionInstallCancel","versionOpenSteam","recommendedServers","recommendedQuery","recommendedAdmin","backupInventory","backupList","backupCreate","backupRestore","backupDelete","backupSettings","backupSettingsSave","houseCatalogList","forceTakeoverPreview","forceTakeoverApply","forceTakeoverRecovery","forceTakeoverRecover","houseTransferPreview","houseTransferApply","javaComponentsList","javaComponentsPreview","javaComponentsApply","javaComponentsRecovery","javaComponentsRecover","javaComponentsInspectDownloaded","worldEditRecovery","worldEditRecover"};
  static readonly string[] LibraryReads={"versionList","versionInstallState","backupInventory","backupList","backupSettings","houseCatalogList","forceTakeoverPreview","forceTakeoverRecovery","houseTransferPreview","javaComponentsList","javaComponentsPreview","javaComponentsRecovery","javaComponentsInspectDownloaded","worldEditRecovery"};
  void InitializeLibrary(string game,string originalUser){
   legacyGame=string.IsNullOrWhiteSpace(game)?Path.Combine(data,"unselected-game"):game;defaultUser=originalUser;versions=new VersionLibrary(data,legacyGame,defaultUser);versionInstall=new SteamVersionWorkflow(data,legacyGame,defaultUser);
   try{var selected=versions.Selected();SetEngine(selected.GamePath,selected.UserPath,selected.Managed);}catch(Exception error){journal.Diagnostic("版本库读取失败："+error.Message);SetEngine(legacyGame,defaultUser,false);}
  }
  static object VersionRow(LocalGameVersion row,string selected){return new{id=row.Id,name=row.Name,version=row.Version,gamePath=row.GamePath,userPath=row.UserPath,managed=row.Managed,installed=row.Installed,selected=row.Id==selected};}
  object VersionRows(){var selected=versions.Selected();return new{selectedId=selected.Id,versions=versions.List().Select(row=>VersionRow(row,selected.Id)).ToArray(),localPreview=LocalLibraryPreview};}
  static object InstallRow(SteamVersionInstall row){if(row==null)return new{stage="none"};return (object)new{stage=row.Stage,message=row.Message,major=row.ExpectedMajor,destination=row.Destination,branch=row.Branch,buildId=row.BuildId,detectedVersion=row.DetectedVersion,bytesDone=row.BytesDone,bytesTotal=row.BytesTotal,installedVersionId=row.InstalledVersionId};}
  internal static bool LocalLibraryPreview {get{
#if OZL_LOCAL_PREVIEW
   return true;
#else
   return false;
#endif
  }}
  void RequireVersionIdle(){cancel.Token.ThrowIfCancellationRequested();if(ClientRunning()||(!test&&GameProcessGuard.IsClientRunning(legacyGame))||LaunchProfiles.Pending(engine)||PlayProfileTransaction.Pending(engine))throw new IOException("请先退出游戏并结束当前启动任务，再切换或安装版本。");}
  object ReadRecommendations(Dictionary<string,object> payload,CancellationToken token){if(recommendations==null)Interlocked.CompareExchange(ref recommendations,new RecommendedServers(data),null);return recommendations.Read(Flag(payload,"refresh"),token);}
  object QueryRecommendation(Dictionary<string,object> payload,CancellationToken token){if(recommendations==null)Interlocked.CompareExchange(ref recommendations,new RecommendedServers(data),null);return recommendations.Query(Text(payload,"id"),token);}
  object DispatchLibrary(string action,Dictionary<string,object> p){
   if(engine.OwnedLaunch&&action.StartsWith("javaComponents",StringComparison.Ordinal))return OwnedJavaAction(action,p);
   switch(action){
    case "versionList":return VersionRows();
    case "versionSelect":cancel.Token.ThrowIfCancellationRequested();var previous=versions.Selected();var selected=versions.Select(Text(p,"id"));try{SetEngine(selected.GamePath,selected.UserPath,selected.Managed);}catch{versions.Select(previous.Id);throw;}return new{message="已切换到 "+selected.Name,version=VersionRow(selected,selected.Id)};
    case "versionImport":cancel.Token.ThrowIfCancellationRequested();var imported=versions.Import(Text(p,"path"),Text(p,"name"));return new{message="版本已添加",version=VersionRow(imported,versions.Selected().Id)};
    case "versionPreserve":RequireVersionIdle();var copied=versions.Preserve(engine.Game,Text(p,"destination"),Text(p,"name"),cancel.Token,CopyProgress);return new{message="独立版本已保存",version=VersionRow(copied,versions.Selected().Id)};
    case "versionInstallBegin":RequireVersionIdle();var install=versionInstall.BeginInteractive(Convert.ToInt32(p["major"]),Text(p,"destination"),Text(p,"name"),cancel.Token,CopyProgress);if(!test)Links.Open("steam://nav/games/details/108600");return InstallRow(install);
    case "versionInstallState":return InstallRow(versionInstall.State());
    case "versionInstallComplete":RequireVersionIdle();var completed=versionInstall.Complete(cancel.Token,CopyProgress);return new{message="独立版本已安装",version=VersionRow(completed,versions.Selected().Id)};
    case "versionInstallCancel":versionInstall.Cancel();return Message("已取消安装任务");
    case "versionOpenSteam":if(!test)Links.Open("steam://nav/games/details/108600");return Message("已打开 Steam 游戏页面");
    case "recommendedAdmin":if(!LocalLibraryPreview)throw new IOException("管理页请在浏览器打开。");if(!test)Links.Open("http://127.0.0.1:18083/admin");return Message("已打开推荐服务器管理页");
    case "backupInventory":return SaveBackups.Inventory(engine);
    case "backupList":return SaveBackups.List(engine,Text(p,"id"));
    case "backupCreate":return SaveBackups.Create(engine,Text(p,"id"),Text(p,"label"),cancel.Token);
    case "backupRestore":Confirm(p);return SaveBackups.Restore(engine,Text(p,"id"),Text(p,"backupId"),true,cancel.Token);
    case "backupDelete":Confirm(p);return SaveBackups.Delete(engine,Text(p,"id"),Text(p,"backupId"),true);
    case "backupSettings":return SaveBackups.Settings(engine);
    case "backupSettingsSave":return SaveBackups.SaveSettings(engine,p.ContainsKey("directory")?Text(p,"directory"):SaveBackups.Settings(engine).directory,Convert.ToInt32(p["retention"]),Flag(p,"afterExit"));
    case "houseCatalogList":return SafehouseCatalog.List(engine,Text(p,"sourceId"));
    case "forceTakeoverPreview":return forceTakeover.Preview();
    case "forceTakeoverApply":Confirm(p);return forceTakeover.Apply(Text(p,"stamp"),true,cancel.Token);
    case "forceTakeoverRecovery":return forceTakeover.Recovery();
    case "forceTakeoverRecover":Confirm(p);return forceTakeover.Recover(Text(p,"stamp"),true,cancel.Token);
    case "houseTransferPreview":return PreviewHouse(p);
    case "houseTransferApply":Confirm(p);return ApplyHouse(Text(p,"previewStamp"));
    case "javaComponentsList":return javaComponents.List();
    case "javaComponentsInstall":Confirm(p);return JavaWorkflow().Install(Text(p,"id"),true,cancel.Token);
    case "javaComponentsEnable":Confirm(p);return JavaWorkflow().Enable(Text(p,"token"),true,cancel.Token);
    case "javaComponentsReconcile":Confirm(p);return JavaWorkflow().Reconcile(true,cancel.Token);
    case "javaComponentsPreview":return javaComponents.Preview(Text(p,"token"),Text(p,"operation"),Text(p,"stamp"),Text(p,"receiptId"));
    case "javaComponentsApply":Confirm(p);return javaComponents.Apply(Text(p,"previewStamp"),true,cancel.Token);
    case "javaComponentsRecovery":return javaComponents.Recovery();
    case "javaComponentsRecover":Confirm(p);return javaComponents.Recover(Text(p,"stamp"),true,cancel.Token);
    case "javaComponentsInspectDownloaded":ulong workshopId;if(!UInt64.TryParse(Text(p,"id"),out workshopId))throw new IOException("无效工坊条目。");return javaComponents.InspectDownloaded(workshopId);
    case "worldEditRecovery":return SaveBackups.EditRecovery(engine);
    case "worldEditRecover":Confirm(p);return SaveBackups.RecoverEdit(engine,Text(p,"id"),Text(p,"stamp"),true,cancel.Token);
    default:throw new IOException("未知版本或备份操作。");
   }
  }
  object PreviewHouse(Dictionary<string,object> p){
   string source=Text(p,"sourceId"),target=Text(p,"targetId");bool automatic=p.ContainsKey("houseId");
   var bounds=automatic?SafehouseCatalog.Resolve(engine,source,Text(p,"catalogStamp"),Text(p,"houseId")):new SafehouseTransferBounds{minX=(int)p["minX"],minY=(int)p["minY"],maxX=(int)p["maxX"],maxY=(int)p["maxY"]};
   var view=houseTransfer.Preview(source,target,bounds.minX,bounds.minY,bounds.maxX,bounds.maxY,(int)p["marginChunks"],cancel.Token);
   if(automatic&&view.canTransfer){string issued="autoHouse:"+Guid.NewGuid().ToString("N");lock(automaticHousePreviews){if(automaticHousePreviews.Count>=16)automaticHousePreviews.Clear();automaticHousePreviews[issued]=new AutomaticHouseBinding{SourceId=source,CatalogStamp=Text(p,"catalogStamp"),HouseId=Text(p,"houseId"),NativeStamp=view.previewStamp};}view.previewStamp=issued;}
   return view;
  }
  object ApplyHouse(string stamp){
   if((stamp??"").StartsWith("autoHouse:",StringComparison.Ordinal)){AutomaticHouseBinding binding;lock(automaticHousePreviews){if(!automaticHousePreviews.TryGetValue(stamp,out binding))throw new IOException("安全屋预览已失效，请重新预览。");automaticHousePreviews.Remove(stamp);}using(SafehouseCatalog.WithSelectionLease(engine,binding.SourceId,binding.CatalogStamp,binding.HouseId))return houseTransfer.Apply(binding.NativeStamp,true,cancel.Token);}
   return houseTransfer.Apply(stamp,true,cancel.Token);
  }
  void CopyProgress(VersionCopyProgress value){Report(value.TotalBytes<=0?-1:(int)Math.Min(99,value.BytesCopied*100d/value.TotalBytes),"正在保存版本 · "+Path.GetFileName(value.File));}
  internal static void ValidateLibraryPayload(string action,Dictionary<string,object> p){
   if(!LibraryActions.Contains(action))return;string[] keys;
   switch(action){
    case "versionSelect":case "backupList":case "recommendedQuery":keys=new[]{"id"};break;
    case "versionImport":keys=new[]{"path","name"};break;
    case "versionPreserve":keys=new[]{"destination","name"};break;
    case "versionInstallBegin":keys=new[]{"major","destination","name"};break;
    case "recommendedServers":keys=new[]{"refresh","requestId"};break;
    case "backupCreate":keys=new[]{"id","label"};break;
    case "backupRestore":case "backupDelete":keys=new[]{"id","backupId","confirmed"};break;
    case "backupSettingsSave":keys=new[]{"directory","retention","afterExit"};break;
    case "houseCatalogList":keys=new[]{"sourceId"};break;
    case "houseTransferPreview":if(p.ContainsKey("houseId")||p.ContainsKey("catalogStamp"))keys=new[]{"sourceId","targetId","houseId","catalogStamp","marginChunks"};else keys=new[]{"sourceId","targetId","minX","minY","maxX","maxY","marginChunks"};break;
    case "houseTransferApply":keys=new[]{"previewStamp","confirmed"};break;
    case "javaComponentsPreview":keys=new[]{"token","operation","stamp","receiptId"};break;
    case "javaComponentsApply":keys=new[]{"previewStamp","confirmed"};break;
    case "javaComponentsInstall":keys=new[]{"id","confirmed"};break;
    case "javaComponentsEnable":keys=new[]{"token","confirmed"};break;
    case "javaComponentsReconcile":keys=new[]{"confirmed"};break;
    case "javaComponentsRecover":keys=new[]{"stamp","confirmed"};break;
    case "forceTakeoverApply":case "forceTakeoverRecover":keys=new[]{"stamp","confirmed"};break;
    case "javaComponentsInspectDownloaded":keys=new[]{"id"};break;
    case "worldEditRecover":keys=new[]{"id","stamp","confirmed"};break;
    default:keys=new string[0];break;
   }
   if(p.Keys.Except(keys).Any())throw new IOException("请求包含无关参数。");
   foreach(var key in keys){object value;if(!p.TryGetValue(key,out value)){if(key=="refresh"||key=="requestId"||key=="receiptId"&&action=="javaComponentsPreview"||key=="directory"&&action=="backupSettingsSave")continue;throw new IOException("缺少参数："+key);}if(key=="confirmed"||key=="refresh"||key=="afterExit"){if(!(value is bool))throw new IOException("选项无效。");}else if(key=="major"||key=="retention"||key=="minX"||key=="minY"||key=="maxX"||key=="maxY"||key=="marginChunks"){if(!(value is int))throw new IOException("数值无效。");if(key=="major"&&(int)value!=41&&(int)value!=42)throw new IOException("请选择 B41 或 B42。");}else if(!(value is string)||((string)value).Any(char.IsControl)||((string)value).Length>4096)throw new IOException("参数无效："+key);}
  }
 }
}
