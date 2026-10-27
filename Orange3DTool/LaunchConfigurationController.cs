using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace PZ3D {sealed partial class ModernController {
 // A selected configuration owns an entire controller binding, including its operation
 // snapshots and callbacks. Publishing the next binding never retargets an in-flight call.
 readonly bool configurationDispatcher;
 readonly Func<Engine,string,Dictionary<string,object>,Action<int,string>,System.Threading.CancellationToken,Action<object>,object> configurationNetworkFetch;
 ModernController lastConfigurationRuntime,lastConfigurationWorkshop;
 readonly Dictionary<string,ModernController> configurationWorkshopOwners=new Dictionary<string,ModernController>();
 readonly Dictionary<ModernController,int> configurationProducers=new Dictionary<ModernController,int>();
 readonly Dictionary<string,ModernController> configurationControllers=new Dictionary<string,ModernController>();
 internal static bool ConfigurationAction(string action){return new[]{"launchConfigurations","launchConfigurationSelect","configurationTasks","configurationCancel"}.Contains(action);}
 internal static void ValidateConfigurationPayload(string action,Dictionary<string,object> p){object id;if(p==null||p.Count!=((action=="launchConfigurations"||action=="configurationTasks")?0:1)||(action=="launchConfigurationSelect"||action=="configurationCancel")&&(!p.TryGetValue("id",out id)||!(id is string)||((string)id).Length>64))throw new IOException("启动配置操作只接受本机配置标识。");}
 LaunchConfigurationProfiles ConfigurationProfiles(){return new LaunchConfigurationProfiles(data,legacyGame,defaultUser);}
 ModernController[] ConfigurationControllers(){lock(gate)return configurationControllers.Values.ToArray();}
 void DisposeConfigurationControllers(){foreach(var child in ConfigurationControllers())child.Dispose();lock(gate)configurationControllers.Clear();}
 ModernController ConfigurationController(){return BoundConfigurationController(ConfigurationProfiles().Selected());}
 static bool ConfigurationIndependentAction(string action){return GuideAction(action)||FeedbackAction(action)||LogAnalysisAction(action)||(NativeNetworkReads.Contains(action)&&action!="updatesGet")||new[]{"versionList","versionImport","versionPreserve","versionInstallBegin","versionInstallState","versionInstallComplete","versionInstallCancel","versionOpenSteam","recommendedServers","recommendedQuery","recommendedAdmin","serverList","serverUpsert","serverDelete","taskGet","playtimeGet","playtimeSettings","theme","help","logs","openContact","openOfficial"}.Contains(action);}
 ModernController RequestConfigurationController(string action){var selected=ConfigurationProfiles().Selected();return BoundConfigurationController(selected,new[]{"savesList","saveGet","saveThumb","saveDeletePreview","saveDelete","serverList","serverUpsert","serverDelete","serverThumb","serverFavorites","favoriteImport","serverQuery","serverHall","steamConnection","steamSubscriptions","steamSearch","steamDetails","settingsGet"}.Contains(action)||ServerWorkshopAction(action));}
 ModernController BoundConfigurationController(LaunchConfigurationResolution selected,bool userOnly=false){
  bool userFallback=!selected.Available&&userOnly&&selected.ErrorCode=="missing-version"&&selected.Version!=null;
  if(!selected.Available&&!userFallback)throw new IOException(selected.Message);
  string game=userFallback?selected.Version.GamePath:selected.Context==null?selected.Snapshot.GamePath:selected.Context.Game,userPath=userFallback?selected.Version.UserPath:selected.Context==null?selected.Snapshot.UserPath:selected.Context.User;
  string key=(userFallback?"user-only|":"")+selected.Id+"|"+game+"|"+userPath+"|"+(selected.Snapshot==null?"":selected.Snapshot.ManifestHash);lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");ModernController child;if(configurationControllers.TryGetValue(key,out child))return child;
   child=new ModernController(legacyGame,defaultUser,data,test,fetch,configurationNetworkFetch,buddyEnvironment,recycleSave,true,networkLane);child.SetEngine(game,userPath,selected.Version!=null&&selected.Version.Managed||selected.Snapshot!=null);if(userFallback)child.engine.LaunchConfiguration=selected;else child.engine.BindConfiguration(selected);child.StartupPresentation=value=>{var callback=StartupPresentation;if(callback!=null)callback(value);};
   child.Progress=(value,message)=>{var callback=Progress;if(callback!=null)callback(value,message);var notification=Event;if(notification!=null)notification("configurationProgress",new{configurationId=selected.Id,configurationName=selected.Name,value=value,text=message});};child.Event=(name,value)=>{var callback=Event;if(callback!=null){var serializer=new System.Web.Script.Serialization.JavaScriptSerializer();var node=serializer.Deserialize<Dictionary<string,object>>(serializer.Serialize(value));node["configurationId"]=selected.Id;node["configurationName"]=selected.Name;callback(name,node);}};configurationControllers.Add(key,child);return child;
  }
 }
 ModernController RuntimeConfigurationController(){
  ModernController remembered;lock(gate)remembered=lastConfigurationRuntime;if(remembered!=null){if(LaunchProfiles.Pending(remembered.engine))return remembered;lock(remembered.startupGate)if(remembered.startupAttempt!=null&&ActiveStartup(remembered.startupAttempt))return remembered;}
  // Runtime journals belong to catalog identities, not to surviving profile rows.
  // A selected built-in can move to another version and named rows can be deleted.
  var catalog=new VersionLibrary(data,legacyGame,defaultUser).List();
  var trusted=new List<LaunchConfigurationResolution>();
  foreach(var version in catalog.Where(v=>v.Installed))trusted.Add(new LaunchConfigurationResolution{Id="steam",Name=version.Name,Kind="builtin",VersionId=version.Id,Version=version,Available=true,Context=LauncherConfiguration.ResolveIndexed(data,version.GamePath,version.UserPath,null,defaultUser,3072).Bind("steam")});
  foreach(var registered in SaveEnvironments.RegisteredForUsers(data,catalog.Select(v=>v.UserPath).Concat(new[]{defaultUser})).Where(r=>r.Record!=null)){var row=registered.Record;trusted.Add(new LaunchConfigurationResolution{Id="snapshot:"+row.Id,Name=row.Name,Kind="frozen",Available=true,Snapshot=row});}
  foreach(var resolution in trusted){
   string game=resolution.Context==null?resolution.Snapshot.GamePath:resolution.Context.Game,userPath=resolution.Context==null?resolution.Snapshot.UserPath:resolution.Context.User;
   var known=new Engine(game,userPath,data,test);known.BindConfiguration(resolution);if(!LaunchProfiles.Pending(known))continue;
   var recovered=LaunchProfiles.RecoveryConfiguration(known);if(recovered==null)continue;var owner=BoundConfigurationController(recovered);lock(gate)lastConfigurationRuntime=owner;return owner;
  }return null;
 }
 object ConfigurationRows(){var profiles=ConfigurationProfiles();return new{selectedId=profiles.SelectedId,configurations=profiles.List().Select(r=>new{id=r.Id,name=r.Name,kind=r.Kind,versionId=r.VersionId,available=r.Available,errorCode=r.ErrorCode,message=r.Message}).ToArray()};}
 object ConfigurationTasks(){return new{configurations=ConfigurationControllers().Select(c=>new{id=c.engine.LaunchConfiguration.Id,name=c.engine.LaunchConfiguration.Name,active=c.Busy,networkActive=c.NetworkBusy}).ToArray()};}
 internal Func<object> CaptureRequest(string action,Dictionary<string,object> payload){
  if(!configurationDispatcher||ConfigurationIndependentAction(action)||ConfigurationAction(action)||new[]{"setGame","versionSelect","cancel","startupCancel","startupYield","startupResume","startupStatus","forceCloseGame","gameProcesses","profileRecover","serverWorkshopStatus","serverWorkshopCancel"}.Contains(action))return ()=>Execute(action,payload);
  var target=StartupOwner(payload)??RequestConfigurationController(action);return ()=>ExecuteConfiguration(target,action,payload);
 }
 ModernController StartupOwner(Dictionary<string,object> payload){string startupId=payload==null||!payload.ContainsKey("startupId")?"":Text(payload,"startupId");if(startupId!="")foreach(var child in ConfigurationControllers())lock(child.startupGate)if(child.startupAttempt!=null&&child.startupAttempt.Launch.SessionId==startupId)return child;return null;}

 public object Execute(string action,Dictionary<string,object> payload){
  if(ConfigurationAction(action)){ValidateConfigurationPayload(action,payload??new Dictionary<string,object>());if(action=="configurationTasks")return ConfigurationTasks();if(action=="configurationCancel"){var matches=ConfigurationControllers().Where(c=>c.engine.LaunchConfiguration.Id==Text(payload,"id")).ToArray();foreach(var child in matches)child.Cancel();return Message("已请求取消此配置的任务。");}if(action=="launchConfigurationSelect")ConfigurationProfiles().Select(Text(payload,"id"));return ConfigurationRows();}
  if(!configurationDispatcher||ConfigurationIndependentAction(action))return ExecuteLocal(action,payload);
  if(action=="setGame"||action=="versionSelect"){var result=ExecuteLocal(action,payload);ConfigurationProfiles().Select("launcher");return result;}
  if(action=="cancel"){if(IsHallCancel(action,payload)){foreach(var child in ConfigurationControllers())child.CancelHall(Text(payload,"requestId"));CancelHall(Text(payload,"requestId"));return Message("已请求取消指定查询。");}ModernController[] producers;lock(gate)producers=configurationProducers.Keys.ToArray();if(producers.Length==1)return producers[0].ExecuteLocal(action,payload);if(producers.Length>1)throw new IOException("多个配置正在处理任务，请取消指定配置。");}
  if(new[]{"startupCancel","startupYield","startupResume","startupStatus","forceCloseGame","gameProcesses","profileRecover"}.Contains(action)){var runtime=StartupOwner(payload)??RuntimeConfigurationController();if(runtime!=null)return runtime.ExecuteLocal(action,payload);}
  if(action=="serverWorkshopStatus"||action=="serverWorkshopCancel"){
   string requested=payload!=null&&payload.ContainsKey("jobId")?Text(payload,"jobId"):null;ModernController registered;lock(gate){registered=lastConfigurationWorkshop;if(requested!=null)configurationWorkshopOwners.TryGetValue(requested,out registered);}if(registered!=null)return registered.ExecuteLocal(action,payload);var owners=ConfigurationControllers().Where(c=>c.workshopPreparation!=null).Where(c=>{var state=c.workshopPreparation.Status() as Dictionary<string,object>;return state!=null&&(requested!=null?Convert.ToString(state["jobId"])==requested:c.workshopPreparation.Active);}).ToArray();
   if(owners.Length==1)return owners[0].ExecuteLocal(action,payload);if(requested!=null&&owners.Length==0)throw new IOException("未找到此配置的准备任务。");
  }
  // Capture once, before any user work or asynchronous download callback begins.
  return ExecuteConfiguration(StartupOwner(payload)??RequestConfigurationController(action),action,payload);
 }
 object ExecuteConfiguration(ModernController target,string action,Dictionary<string,object> payload){bool producerAction=!NativeReads.Contains(action)&&!NativeNetworkReads.Contains(action);
  if(producerAction)lock(gate){int active;configurationProducers.TryGetValue(target,out active);configurationProducers[target]=active+1;}
  if(action=="launch"||action=="profileLaunch"||action=="startupBegin")lock(gate)lastConfigurationRuntime=target;
  try{var result=target.ExecuteLocal(action,payload);if(action=="serverWorkshopPrepare"){var row=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));lock(gate){lastConfigurationWorkshop=target;configurationWorkshopOwners[Convert.ToString(row["jobId"])]=target;}}return result;}finally{if(producerAction)lock(gate){int active=configurationProducers[target]-1;if(active==0)configurationProducers.Remove(target);else configurationProducers[target]=active;}}

 }
}}
