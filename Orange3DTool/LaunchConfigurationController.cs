using System;using System.IO;using System.Linq;using System.Collections.Generic;
namespace PZ3D {sealed partial class ModernController {
 // A selected configuration owns an entire controller binding, including its operation
 // snapshots and callbacks. Publishing the next binding never retargets an in-flight call.
 readonly bool configurationDispatcher;
 readonly Func<Engine,string,Dictionary<string,object>,Action<int,string>,System.Threading.CancellationToken,Action<object>,object> configurationNetworkFetch;
 ModernController lastConfigurationRuntime,lastConfigurationWorkshop;
 readonly object configurationSelectionGate=new object();long configurationSelectionReceipt,configurationSelectionApplied;
 readonly Dictionary<string,ModernController> configurationWorkshopOwners=new Dictionary<string,ModernController>();
 readonly Dictionary<ModernController,int> configurationProducers=new Dictionary<ModernController,int>();
 readonly Dictionary<string,ModernController> configurationControllers=new Dictionary<string,ModernController>();
 sealed class ConfigurationReader {internal string Owner,ReadId;}
 readonly Dictionary<ModernController,ConfigurationReader> configurationReaders=new Dictionary<ModernController,ConfigurationReader>();
 internal static readonly HashSet<string> ConfigurationPureReads=new HashSet<string>{"status","modCatalog","modDetail","modCover","saveGet","modProfiles","memoryGet","javaList","javaComponentsList","javaComponentsRecovery","modSort","savesList","serverList"};
 // These reads produce values or immutable write snapshots, not live preview
 // handles. Give unwrapped page/thumbnail requests the same private engine and
 // cancellation source already used by configuration-editor reads.
 static readonly HashSet<string> ConfigurationSnapshotReads=new HashSet<string>(ConfigurationPureReads.Concat(new[]{"settingsGet","shadersGet","reticleGet","saveGet","saveThumb","saveDeletePreview","serverThumb","serverFavorites","modCover","modProfileGet","backupInventory","backupList","backupSettings","worldEditRecovery","javaComponentsInspectDownloaded"}));
 void CancelConfigurationReaders(string id=null,string readId=null){ModernController[] readers;lock(gate)readers=configurationReaders.Where(r=>(id==null||r.Value.Owner==id)&&(readId==null||r.Value.ReadId==readId)).Select(r=>r.Key).ToArray();foreach(var reader in readers)reader.Cancel();}
 internal static bool ConfigurationAction(string action){return new[]{"launchConfigurations","launchConfigurationSelect","configurationTasks","configurationCancel","configurationRead","configurationCreate","configurationRename","configurationVersion","configurationDelete","configurationSaveOrdinary","configurationRequest","configurationReadCancel"}.Contains(action);}
 internal static readonly HashSet<string> ConfigurationRequests=new HashSet<string>{"saveGet","saveSave","modCover","savesList","serverList","status","modCatalog","modDetail","modProfiles","memoryGet","memorySave","javaList","javaSave","javaComponentsList","javaComponentsInstall","javaComponentsEnable","javaComponentsPreview","javaComponentsApply","javaComponentsRecovery","javaComponentsRecover","javaComponentsReconcile","javaComponentsInspectDownloaded","componentsAction","modSort","steamDownload","forceTakeoverPreview","forceTakeoverApply","buddyPreview","buddyTakeover"};
 internal static void ValidateConfigurationPayload(string action,Dictionary<string,object> p){
  string[] keys=action=="launchConfigurations"||action=="configurationTasks"?new string[0]:action=="configurationReadCancel"?new[]{"id","readId"}:action=="configurationCreate"?new[]{"name","versionId"}:action=="configurationRename"?new[]{"id","name"}:action=="configurationVersion"?new[]{"id","versionId"}:action=="configurationSaveOrdinary"?new[]{"id","ordinaryIds"}:action=="configurationRequest"?new[]{"id","action","payload"}:new[]{"id"};
  if(new[]{"configurationRequest","configurationRead","configurationSaveOrdinary"}.Contains(action)&&p!=null&&p.ContainsKey("readId"))keys=keys.Concat(new[]{"readId"}).ToArray();
  if(p==null||p.Count!=keys.Length||keys.Any(k=>!p.ContainsKey(k)))throw new IOException("启动配置操作只接受本机配置标识和明确选项。");
  foreach(string key in keys.Where(k=>k!="payload"&&k!="ordinaryIds"))if(!(p[key] is string)||((string)p[key]).Length>(key=="name"?100:64)||((string)p[key]).Any(char.IsControl))throw new IOException("启动配置参数无效。");
  if(p.ContainsKey("id")&&!System.Text.RegularExpressions.Regex.IsMatch((string)p["id"],@"\A(launcher|steam|vanilla|live:[a-f0-9]{32}|snapshot:[a-f0-9]{32})\z"))throw new IOException("启动配置标识无效。");
  if(p.ContainsKey("readId")&&!(p["readId"] is string&&System.Text.RegularExpressions.Regex.IsMatch((string)p["readId"],@"\A[a-f0-9]{32}\z")))throw new IOException("配置读取标识无效。");
  if(p.ContainsKey("ordinaryIds"))FrozenIds(p["ordinaryIds"]);
  if(action!="configurationRequest")return;
  string inner=(string)p["action"];var body=p["payload"] as Dictionary<string,object>;
  if(!ConfigurationRequests.Contains(inner)||body==null||p.ContainsKey("readId")&&!ConfigurationPureReads.Contains(inner))throw new IOException("此操作不能用于配置编辑。");
  // Validate through the same host policy, with an additional exact field boundary
  // for older actions whose legacy policy accepts extra fields.
  string[] fields=inner=="memorySave"?new[]{"stamp","value"}:inner=="javaSave"?(body.ContainsKey("saveId")?new[]{"stamp","hash","decision","saveId"}:new[]{"stamp","hash","decision"}):inner=="componentsAction"?new[]{"component","operation","confirmed"}:inner=="modSort"?(body.ContainsKey("saveId")?new[]{"mods","saveId"}:new[]{"mods"}):inner=="steamDownload"?new[]{"id","confirmed"}:null;
  if(fields!=null&&(body.Count!=fields.Length||fields.Any(k=>!body.ContainsKey(k))))throw new IOException("此配置操作的参数无效。");
  if(new[]{"status","modProfiles","memoryGet","javaComponentsList","javaComponentsRecovery","forceTakeoverPreview","buddyPreview"}.Contains(inner)&&body.Count!=0)throw new IOException("配置读取不接受文件路径。");
  if(new[]{"modCatalog","modCover","modDetail","modSort","saveGet","saveSave"}.Contains(inner))ModernHostPolicy.ValidateSaveEditorPayload(inner,body);
  var json=new System.Web.Script.Serialization.JavaScriptSerializer();ModernHostPolicy.Validate("https://orange.local/index.html",json.Serialize(new{id="configuration-inner",action=inner,payload=body}));
 }
 internal static bool ConfigurationMutation(string action,Dictionary<string,object> p){if(action=="configurationRequest")action=(string)p["action"];return !NativeReads.Contains(action)&&!NativeNetworkReads.Contains(action)&&!new[]{"cancel","configurationCancel","configurationTasks","launchConfigurations","configurationRead","configurationReadCancel"}.Contains(action);}
 LaunchConfigurationProfiles ConfigurationProfiles(){return new LaunchConfigurationProfiles(data,legacyGame,defaultUser);}
 T ConfigurationMetadata<T>(Func<LaunchConfigurationProfiles,T> read){lock(configurationSelectionGate)return read(ConfigurationProfiles());}
 LaunchConfigurationResolution ResolveConfiguration(string id){return ConfigurationMetadata(profiles=>profiles.Resolve(id));}
 LaunchConfigurationResolution SelectedConfiguration(){return ConfigurationMetadata(profiles=>profiles.Selected());}
 ModernController[] ConfigurationControllers(){lock(gate)return configurationControllers.Values.ToArray();}
 void DisposeConfigurationControllers(){CancelConfigurationReaders();foreach(var child in ConfigurationControllers())child.Dispose();lock(gate)configurationControllers.Clear();}
 ModernController ConfigurationController(){return BoundConfigurationController(SelectedConfiguration());}
 static bool ConfigurationIndependentAction(string action){return GuideAction(action)||FeedbackAction(action)||LogAnalysisAction(action)||(NativeNetworkReads.Contains(action)&&action!="updatesGet")||new[]{"versionList","versionImport","versionPreserve","versionInstallBegin","versionInstallState","versionInstallComplete","versionInstallCancel","versionOpenSteam","recommendedServers","recommendedQuery","recommendedAdmin","serverList","serverUpsert","serverDelete","taskGet","playtimeGet","playtimeSettings","theme","help","logs","openContact","openOfficial","openExternal"}.Contains(action);}
 ModernController RequestConfigurationController(string action){return RequestConfigurationController(SelectedConfiguration(),action);}
 ModernController RequestConfigurationController(LaunchConfigurationResolution selected,string action){return BoundConfigurationController(selected,new[]{"savesList","saveGet","saveThumb","saveDeletePreview","saveDelete","serverList","serverUpsert","serverDelete","serverThumb","serverFavorites","favoriteImport","serverQuery","serverHall","steamConnection","steamSubscriptions","steamSearch","steamDetails","settingsGet"}.Contains(action)||ServerWorkshopAction(action));}
 ModernController BoundConfigurationController(LaunchConfigurationResolution selected,bool userOnly=false){
  bool userFallback=!selected.Available&&userOnly&&selected.ErrorCode=="missing-version"&&selected.Version!=null;
  if(!selected.Available&&!userFallback)throw new IOException(selected.Message);
  string game=userFallback?selected.Version.GamePath:selected.Context==null?selected.Snapshot.GamePath:selected.Context.Game,userPath=userFallback?selected.Version.UserPath:selected.Context==null?selected.Snapshot.UserPath:selected.Context.User;
  string key=(userFallback?"user-only|":"")+selected.Id+"|"+game+"|"+userPath+"|"+(selected.Snapshot==null?"":selected.Snapshot.ManifestHash);lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");ModernController child;if(configurationControllers.TryGetValue(key,out child))return child;
   child=new ModernController(legacyGame,defaultUser,data,test,fetch,configurationNetworkFetch,buddyEnvironment,recycleSave,true,networkLane);child.SetEngine(game,userPath,selected.Version!=null&&selected.Version.Managed||selected.Snapshot!=null,selected.Snapshot);if(userFallback)child.engine.LaunchConfiguration=selected;else child.engine.BindConfiguration(selected);child.StartupPresentation=value=>{var callback=StartupPresentation;if(callback!=null)callback(value);};
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
   try{var known=new Engine(game,userPath,data,test);known.BindConfiguration(resolution);if(!LaunchProfiles.Pending(known))continue;
    var recovered=LaunchProfiles.RecoveryConfiguration(known);if(recovered==null)continue;var owner=BoundConfigurationController(recovered);lock(gate)lastConfigurationRuntime=owner;return owner;
   }catch(Exception error){string message="上次启动恢复未完成："+LauncherJournal.Sanitize(error.Message);bool added;lock(gate){added=!log.Contains(message);if(added)log.Add(message);}if(added)try{journal.Diagnostic(message);}catch{ /* A diagnostic failure cannot prevent the UI or later sessions from opening. */ }}
  }return null;
 }
 object ConfigurationRows(){return ConfigurationMetadata(profiles=>new{selectedId=profiles.SelectedId,configurations=profiles.List().Select(r=>new{id=r.Id,name=r.Name,kind=r.Kind,versionId=r.VersionId,version=r.Snapshot!=null?r.Snapshot.GameVersion:r.Version==null?"":r.Version.Version,available=r.Available,errorCode=r.ErrorCode,message=r.Message}).ToArray()});}
 object ConfigurationView(string id){var row=ResolveConfiguration(id);return ConfigurationView(row,row.Available?BoundConfigurationController(row):null,System.Threading.CancellationToken.None);}
 object ConfigurationView(LaunchConfigurationResolution row,ModernController target,System.Threading.CancellationToken token){return new{id=row.Id,name=row.Name,kind=row.Kind,versionId=row.VersionId,available=row.Available,message=row.Message,ordinaryIds=target==null?new string[0]:row.Snapshot!=null?FrozenConfigurationEditor.Ordinary(row.Snapshot):target.engine.OwnedLaunch?target.engine.OwnedComponents().OrdinaryIds(token):SaveProfiles.DefaultMods(SaveProfiles.DefaultBytes(target.engine)),frozen=row.Snapshot==null?null:target.FrozenView(row.Snapshot)};}
 object ConfigurationEdit(string action,Dictionary<string,object> p){
  ValidateConfigurationPayload(action,p);var profiles=ConfigurationProfiles();
  if(action=="configurationRead"||action=="configurationSaveOrdinary"){
   var selected=ResolveConfiguration(Text(p,"id"));var target=selected.Available?BoundConfigurationController(selected):null;
   if(target==null){if(action=="configurationSaveOrdinary")throw new IOException(selected.Message);return ConfigurationView(selected,null,System.Threading.CancellationToken.None);}
   return ReadConfiguration(selected,target,action,p,p.ContainsKey("readId")?Text(p,"readId"):Guid.NewGuid().ToString("N"));
  }
  if(action=="configurationCreate"){var row=ConfigurationMetadata(store=>store.Create(Text(p,"name"),Text(p,"versionId"),new string[0]));return ConfigurationView(row.Id);}
  string id=Text(p,"id");
  if(action=="configurationRename")ConfigurationMetadata(store=>{store.Rename(id,Text(p,"name"));return true;});
  else if(action=="configurationVersion")ConfigurationMetadata(store=>{store.SetVersion(id,Text(p,"versionId"));return true;});
  else if(action=="configurationDelete"){ConfigurationMetadata(store=>{store.Delete(id);return true;});return ConfigurationRows();}
  return ConfigurationView(id);
 }
 Func<object> CaptureConfigurationRequest(Dictionary<string,object> p){
  ValidateConfigurationPayload("configurationRequest",p);string id=Text(p,"id"),action=Text(p,"action");var row=ResolveConfiguration(id);var target=BoundConfigurationController(row);var body=(Dictionary<string,object>)p["payload"];
  return ()=>new{configurationId=id,data=ConfigurationPureReads.Contains(action)?ReadConfiguration(row,target,action,body,p.ContainsKey("readId")?Text(p,"readId"):""):ExecuteConfiguration(target,action,body)};
 }
 Func<object> CaptureSelectedConfigurationRead(string action,Dictionary<string,object> payload){
  var row=SelectedConfiguration();var target=RequestConfigurationController(row,action);var body=payload??new Dictionary<string,object>();
  if(LibraryActions.Contains(action))ValidateLibraryPayload(action,body);
  if(action=="saveDeletePreview")ModernHostPolicy.ValidateSaveDeletionPayload(action,body);
  return ()=>ReadConfiguration(row,target,action,body,Guid.NewGuid().ToString("N"));
 }
 object ReadConfiguration(LaunchConfigurationResolution row,ModernController target,string action,Dictionary<string,object> body,string readId){
  // Each scoped read/ordinary save has an immutable engine and its own CTS.
  // It never replaces another writer's CTS or recovers a runtime; ordinary
  // writes still use the component service's existing per-list mutex.
  using(var reader=new ModernController(legacyGame,defaultUser,data,test,fetch,configurationNetworkFetch,buddyEnvironment,recycleSave,true,networkLane,true)){
   reader.SetEngine(target.engine.Game,target.engine.User,target.engine.ManagedVersion,row.Snapshot);if(row.Available)reader.engine.BindConfiguration(row);else reader.engine.LaunchConfiguration=row;reader.Progress=(value,text)=>{var callback=Event;if(callback!=null)callback("configurationProgress",new{configurationId=row.Id,configurationName=row.Name,readId,value,text});};
   lock(gate){if(disposed)throw new ObjectDisposedException("ModernController");configurationReaders.Add(reader,new ConfigurationReader{Owner=row.Id,ReadId=readId});}
   try{
    reader.cancel.Token.ThrowIfCancellationRequested();
    if(action=="configurationSaveOrdinary"){
     ConfigurationProfiles().SaveOrdinary(row.Id,FrozenIds(body["ordinaryIds"]),reader.cancel.Token,reader.Progress);
     // The writer's return is its committed/no-op boundary. A later cancel must
     // not turn a completed save into an error or trigger another metadata scan.
     return new{id=row.Id,saved=true};
    }
    if(action=="modCatalog"&&Flag(body,"refresh"))SaveProfiles.InvalidateDisplay(reader.SaveEditorEngine(body));object result=action=="modCatalog"?SaveProfiles.Catalog(reader.SaveEditorEngine(body),reader.cancel.Token,(value,text)=>{var callback=Event;if(callback!=null)callback("configurationProgress",new{configurationId=row.Id,configurationName=row.Name,readId,value,text});}):action=="configurationRead"?ConfigurationView(row,reader,reader.cancel.Token):reader.Dispatch(action,body);
    reader.cancel.Token.ThrowIfCancellationRequested();
    if(action=="memoryGet"){var json=new System.Web.Script.Serialization.JavaScriptSerializer();var value=json.Deserialize<Dictionary<string,object>>(json.Serialize(result));lock(target.gate)target.memorySnapshots.Add((string)value["stamp"]);}
    // A later write still validates the original bytes/stamp on its bound owner.
    // Publishing a read receipt never replaces that owner's cancellation source.
    if(action=="settingsGet")lock(target.gate)foreach(var snapshot in reader.settingSnapshots)target.settingSnapshots[snapshot.Key]=snapshot.Value;
    if(action=="shadersGet")lock(target.gate)foreach(var snapshot in reader.shaderSnapshots)target.shaderSnapshots[snapshot.Key]=snapshot.Value;
    if(action=="status"){
     var json=new System.Web.Script.Serialization.JavaScriptSerializer();var value=json.Deserialize<Dictionary<string,object>>(json.Serialize(result));value["configurationId"]=row.Id;value["configurationName"]=row.Name;
     lock(target.gate)value["task"]=new{active=target.localMutationBusy,value=target.progressValue,text=target.progressText};
     if(row.Snapshot!=null){
      var active=row.Snapshot.DefaultModIds;var catalog=SaveProfiles.SnapshotEntries(reader.engine,active,new List<string>(),new LaunchReadBudget(67108864,100000));
      var cards=((System.Collections.IEnumerable)value["components"]).Cast<Dictionary<string,object>>().ToList();
      foreach(var card in cards){string component=OwnedComponentId(Convert.ToString(card["id"]));card["configured"]=active.Contains(component);}
      foreach(string component in active.Where(id=>!LauncherComponents.KnownIds.Contains(id))){var entry=catalog.Entries.FirstOrDefault(item=>item.id==component);cards.Add(new Dictionary<string,object>{{"id",component},{"title",entry==null?component:entry.name},{"configured",true},{"downloaded",true},{"item",""}});}
      value["components"]=cards.ToArray();
     }

     ModernController runtime;lock(gate)runtime=lastConfigurationRuntime;
     if(runtime!=null){value["startup"]=runtime.StartupStatus();value["running"]=runtime.ClientRunning();value["runtimeConfigurationId"]=runtime.engine.LaunchConfiguration.Id;value["runtimeConfigurationName"]=runtime.engine.LaunchConfiguration.Name;}
     result=value;
    }return result;
   }finally{lock(gate)configurationReaders.Remove(reader);}
  }
 }
 object ConfigurationTasks(){return new{configurations=ConfigurationControllers().Select(c=>new{id=c.engine.LaunchConfiguration.Id,name=c.engine.LaunchConfiguration.Name,active=c.Busy,networkActive=c.NetworkBusy}).ToArray()};}
 Func<object> CaptureConfigurationSelection(Dictionary<string,object> payload){ValidateConfigurationPayload("launchConfigurationSelect",payload);string id=Text(payload,"id");long receipt=System.Threading.Interlocked.Increment(ref configurationSelectionReceipt);return ()=>{lock(configurationSelectionGate){if(receipt>configurationSelectionApplied){configurationSelectionApplied=receipt;ConfigurationProfiles().Select(id);}return ConfigurationRows();}};}
 internal Func<object> CaptureRequest(string action,Dictionary<string,object> payload){
  if(action=="launchConfigurationSelect")return CaptureConfigurationSelection(payload);
  if(action=="configurationRequest")return CaptureConfigurationRequest(payload);
  if(configurationDispatcher&&!ConfigurationIndependentAction(action)&&ConfigurationSnapshotReads.Contains(action))return CaptureSelectedConfigurationRead(action,payload);
  if(!configurationDispatcher||ConfigurationIndependentAction(action)||ConfigurationAction(action)||(action=="frozenConfigurationCancel"||action=="frozenConfigurationReadCancel")||new[]{"setGame","versionSelect","cancel","startupCancel","startupYield","startupResume","startupStatus","forceCloseGame","gameProcesses","profileRecover","serverWorkshopStatus","serverWorkshopCancel"}.Contains(action))return ()=>Execute(action,payload);
  var target=(FrozenConfigurationAction(action)?FrozenTarget(action,payload):StartupOwner(payload)??RequestConfigurationController(action));return ()=>ExecuteConfiguration(target,action,payload);
 }
 ModernController StartupOwner(Dictionary<string,object> payload){string startupId=payload==null||!payload.ContainsKey("startupId")?"":Text(payload,"startupId");if(startupId!="")foreach(var child in ConfigurationControllers())lock(child.startupGate)if(child.startupAttempt!=null&&child.startupAttempt.Launch.SessionId==startupId)return child;return null;}

 public object Execute(string action,Dictionary<string,object> payload){
  if(action=="launchConfigurationSelect")return CaptureConfigurationSelection(payload)();
  if(action=="configurationReadCancel"){ValidateConfigurationPayload(action,payload);CancelConfigurationReaders(Text(payload,"id"),Text(payload,"readId"));return Message("已取消此配置读取。");}
  if(action=="configurationRequest")return CaptureConfigurationRequest(payload)();
  if(new[]{"configurationRead","configurationCreate","configurationRename","configurationVersion","configurationDelete","configurationSaveOrdinary"}.Contains(action))return ConfigurationEdit(action,payload);
  if(configurationDispatcher&&(action=="frozenConfigurationCancel"||action=="frozenConfigurationReadCancel")){ValidateFrozenConfigurationPayload(action,payload);if(action=="frozenConfigurationCancel")CancelConfigurationReaders("snapshot:"+Text(payload,"snapshotId"));foreach(var child in ConfigurationControllers().Where(c=>c.engine.FrozenEnvironment!=null&&c.engine.FrozenEnvironment.Id==Text(payload,"snapshotId")))child.FrozenRead(action,payload);return Message("已请求取消此快照操作。");}
  if(ConfigurationAction(action)){ValidateConfigurationPayload(action,payload??new Dictionary<string,object>());if(action=="configurationTasks")return ConfigurationTasks();if(action=="configurationCancel"){CancelConfigurationReaders(Text(payload,"id"));var matches=ConfigurationControllers().Where(c=>c.engine.LaunchConfiguration.Id==Text(payload,"id")).ToArray();foreach(var child in matches)child.Cancel();return Message("已请求取消此配置的任务。");}if(action=="launchConfigurationSelect")ConfigurationProfiles().Select(Text(payload,"id"));return ConfigurationRows();}
  if(!configurationDispatcher||ConfigurationIndependentAction(action))return ExecuteLocal(action,payload);
  if(action=="setGame"||action=="versionSelect"){var result=ExecuteLocal(action,payload);ConfigurationProfiles().Select("launcher");return result;}
  if(action=="cancel"){if(IsHallCancel(action,payload)){foreach(var child in ConfigurationControllers())child.CancelHall(Text(payload,"requestId"));CancelHall(Text(payload,"requestId"));return Message("已请求取消指定查询。");}ModernController[] producers;lock(gate)producers=configurationProducers.Keys.ToArray();if(producers.Length==1)return producers[0].ExecuteLocal(action,payload);if(producers.Length>1)throw new IOException("多个配置正在处理任务，请取消指定配置。");}
  if(new[]{"startupCancel","startupYield","startupResume","startupStatus","forceCloseGame","gameProcesses","profileRecover"}.Contains(action)){var runtime=StartupOwner(payload)??RuntimeConfigurationController();if(runtime!=null)return runtime.ExecuteLocal(action,payload);}
  if(action=="serverWorkshopStatus"||action=="serverWorkshopCancel"){
   string requested=payload!=null&&payload.ContainsKey("jobId")?Text(payload,"jobId"):null;ModernController registered;lock(gate){registered=lastConfigurationWorkshop;if(requested!=null)configurationWorkshopOwners.TryGetValue(requested,out registered);}if(registered!=null)return registered.ExecuteLocal(action,payload);var owners=ConfigurationControllers().Where(c=>c.workshopPreparation!=null).Where(c=>{var state=c.workshopPreparation.Status() as Dictionary<string,object>;return state!=null&&(requested!=null?Convert.ToString(state["jobId"])==requested:c.workshopPreparation.Active);}).ToArray();
   if(owners.Length==1)return owners[0].ExecuteLocal(action,payload);if(requested!=null&&owners.Length==0)throw new IOException("未找到此配置的准备任务。");
  }
  if(ConfigurationSnapshotReads.Contains(action))return CaptureSelectedConfigurationRead(action,payload)();
  // Capture once, before any user work or asynchronous download callback begins.
  return ExecuteConfiguration((FrozenConfigurationAction(action)?FrozenTarget(action,payload):StartupOwner(payload)??RequestConfigurationController(action)),action,payload);
 }
 object ExecuteConfiguration(ModernController target,string action,Dictionary<string,object> payload){bool producerAction=!NativeReads.Contains(action)&&!NativeNetworkReads.Contains(action);
  if(producerAction)lock(gate){int active;configurationProducers.TryGetValue(target,out active);configurationProducers[target]=active+1;}
  if(action=="launch"||action=="profileLaunch"||action=="startupBegin")lock(gate)lastConfigurationRuntime=target;
  try{var result=target.ExecuteLocal(action,payload);if(action=="serverWorkshopPrepare"){var row=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(result));lock(gate){lastConfigurationWorkshop=target;configurationWorkshopOwners[Convert.ToString(row["jobId"])]=target;}}return result;}finally{if(producerAction)lock(gate){int active=configurationProducers[target]-1;if(active==0)configurationProducers.Remove(target);else configurationProducers[target]=active;}}

 }
}}
