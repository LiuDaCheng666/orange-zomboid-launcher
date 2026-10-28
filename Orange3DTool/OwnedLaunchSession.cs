using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Threading;
namespace PZ3D {
 static partial class LaunchProfiles {
  static void StageOwned(Engine e,ProfileLaunchRequest request){
   request.Cancellation.ThrowIfCancellationRequested();e.GuardLaunch();if(Pending(e))throw new IOException("上一次启动会话尚未恢复。");string payload=Payload(request),nonce=payload.Split('\n')[1];var context=e.OwnedConfiguration;context.Initialize();
   string bridge=Path.Combine(context.RuntimeDirectory,"bridge-"+nonce.Substring(0,12)),requestPath=LuaFile(e,RequestName);foreach(string file in RequestFiles(e)){LauncherMaintenance.NoReparse(file);if(File.Exists(file))throw new IOException("存在上次启动请求，未覆盖。");}if(Directory.Exists(bridge))throw new IOException("启动桥接目录已存在。");
   byte[] original=File.ReadAllBytes(context.DefaultModsPath);var files=BridgeFiles(bridge,LegacyBuild(e));LiveModDiscovery.Session session=null;bool journaled=false;
   try{
    foreach(var file in files){request.Cancellation.ThrowIfCancellationRequested();LauncherComponents.Atomic(file.Key,file.Value);}
    var components=e.OwnedComponents();var catalog=SaveProfiles.Entries(e,new List<string>()).ToArray();string[] ordinary=context.ProfileId==null?null:components.OrdinaryIds();
    session=LiveModDiscovery.Prepare(components,BundledAssets.ReadOwnAsset("OrangeLive.jar"),catalog,request.Cancellation,bridge,ordinary);
    session.VmArguments=session.VmArguments.Concat(GameStartupComponent.PrepareOwnedSession(e,nonce,session.Directory)).ToArray();
    var record=new ProfileSessionRecord{Game=e.Game,User=e.User,Token=Key(e),Nonce=nonce,Kind=request.Kind,State="prepared",CreatedUtc=DateTime.UtcNow.ToString("o"),Original=Convert.ToBase64String(original),AppliedHash=DlssInstaller.Hash(original),CaptureEndpoint=request.Kind=="server"?request.Host+":"+request.Port:null,ConfigurationId=context.ProfileId,ConfigurationMode=context.Mode,ConfigurationOwner=context.OwnerUser??e.User,ConfigurationRoot=context.Root,SessionDirectory=session.Directory,BridgeDirectory=bridge,SessionAppliedHash=DlssInstaller.HashFile(Path.Combine(session.Directory,"mods","default.txt")),SessionVmArguments=session.VmArguments,ManagedIds=components.ManagedIds().Concat(new[]{ModId}).ToArray()};
    if(record.CaptureEndpoint!=null){try{record.CaptureRevision=ServerWorkshopCatalog.Revision(e.Data,record.CaptureEndpoint);}catch{record.CaptureRevision=null;}}request.Cancellation.ThrowIfCancellationRequested();
    foreach(var file in files)record.Files[file.Key]=DlssInstaller.Hash(file.Value);ValidateOwnedRecord(e,record);if(request.ValidateBeforeStage!=null)request.ValidateBeforeStage();
    if(!SameOriginal(context.DefaultModsPath,original))throw new IOException("准备期间此启动配置已变化，请重试。");Atomic(Journal(e),Encoding.UTF8.GetBytes(Json.Serialize(record)));journaled=true;
    Directory.CreateDirectory(Path.GetDirectoryName(requestPath));PrivateRequest(requestPath,payload);Atomic(LuaFile(e,StatusName),Encoding.UTF8.GetBytes("prepared\n"));e.LiveSession=session;
   }catch{if(journaled)Recover(e,true);else{if(session!=null)RemoveOwnedDirectory(context,session.Directory,"live-");RemoveOwnedDirectory(context,bridge,"bridge-");}throw;}
  }
  internal static LaunchConfigurationResolution RecoveryConfiguration(Engine source){if(!Pending(source))return null;var record=Load(source);if(record.ConfigurationMode!=null){var restored=OwnedRecoveryEngine(source,record);restored.LaunchConfiguration.Name="已启动配置";return restored.LaunchConfiguration;}if(record.SnapshotId!=null){var snapshot=SaveEnvironments.FindOwned(source.Data,source.Game,source.User);return new LaunchConfigurationResolution{Id="snapshot:"+snapshot.Id,Name=snapshot.Name,Kind="frozen",Available=true,Snapshot=snapshot};}return source.LaunchConfiguration;}
  internal static ProfileSessionRecord OwnedSession(Engine e){if(!Pending(e))return null;var record=Load(e);return record.ConfigurationMode!=null?record:null;}
  internal static ProfileSessionRecord OwnedSession(Engine e,string nonce){if(!Pending(e))return null;var record=Load(e);return record.ConfigurationMode!=null&&record.Nonce==nonce?record:null;}
  static void RequireSessionPath(LauncherConfiguration context,string path,string prefix){
   if(string.IsNullOrEmpty(path)||!VersionLibrary.Same(Path.GetDirectoryName(Path.GetFullPath(path)),context.RuntimeDirectory)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),"^"+prefix+"[a-f0-9]{"+12+"}$"))throw new IOException("启动会话目录不属于此配置。");LauncherMaintenance.NoReparse(path);
  }
  static void RemoveOwnedDirectory(LauncherConfiguration context,string path,string prefix){RequireSessionPath(context,path,prefix);if(Directory.Exists(path)){foreach(string child in Directory.EnumerateFileSystemEntries(path,"*",SearchOption.AllDirectories))LauncherMaintenance.NoReparse(child);Directory.Delete(path,true);}}
  static Engine OwnedRecoveryEngine(Engine source,ProfileSessionRecord record){var restored=new Engine(source.Game,source.User,source.Data,source.TestMode,source.ReadSteamOptions,source.ProcessInventory);RestoreOwnedContext(restored,record);return restored;}
  internal static void RestoreOwnedContext(Engine e,ProfileSessionRecord record){
   if(record.ConfigurationMode!="launcher"&&record.ConfigurationMode!="vanilla")throw new IOException("会话配置类型无效。");
   if(record.ConfigurationId!=null&&!System.Text.RegularExpressions.Regex.IsMatch(record.ConfigurationId,"^live:[a-f0-9]{32}$"))throw new IOException("会话配置身份无效。");
   var context=LauncherConfiguration.ResolveIndexed(e.Data,e.Game,e.User,record.ConfigurationId,record.ConfigurationOwner,3072).Bind(record.ConfigurationMode);
   if(!VersionLibrary.Same(context.Root,record.ConfigurationRoot))throw new IOException("会话配置目录不匹配。");
   e.BindConfiguration(new LaunchConfigurationResolution{Id=record.ConfigurationId??record.ConfigurationMode,Kind=record.ConfigurationId==null?"builtin":"live",Available=true,Context=context});
  }
  static void ValidateOwnedRecord(Engine e,ProfileSessionRecord record){
   var context=e.OwnedConfiguration;if(context==null||!VersionLibrary.Same(context.Root,record.ConfigurationRoot))throw new IOException("会话配置不匹配。");RequireSessionPath(context,record.SessionDirectory,"live-");RequireSessionPath(context,record.BridgeDirectory,"bridge-");
   if(!System.Text.RegularExpressions.Regex.IsMatch(record.Nonce??"","^[a-f0-9]{32}$")||record.ManagedIds==null||record.ManagedIds.Any(x=>!SaveProfiles.Id(x))||record.SessionVmArguments==null||record.Original==null||Convert.FromBase64String(record.Original).Length>1048576)throw new IOException("会话恢复内容无效。");
   foreach(string hash in new[]{record.AppliedHash,record.SessionAppliedHash})if(!System.Text.RegularExpressions.Regex.IsMatch(hash??"","^[A-F0-9]{64}$"))throw new IOException("会话恢复校验无效。");
   var expected=new[]{Path.Combine(record.BridgeDirectory,"common","mod.info"),Path.Combine(record.BridgeDirectory,"42","mod.info"),Path.Combine(record.BridgeDirectory,"42","media","lua","client","OrangeLaunchBridge.lua")};if(record.Files==null||record.Files.Count!=expected.Length||expected.Any(path=>!record.Files.ContainsKey(path)||!System.Text.RegularExpressions.Regex.IsMatch(record.Files[path]??"","^[A-F0-9]{64}$")))throw new IOException("会话桥接校验无效。");
  }
  static string RecoverOwned(Engine e,ProfileSessionRecord record){
   ValidateOwnedRecord(e,record);var context=e.OwnedConfiguration;string choices=Path.Combine(record.SessionDirectory,"mods","default.txt");LauncherMaintenance.NoReparse(choices);bool conflict=false;
   if(context.Mode!="vanilla"&&File.Exists(choices)&&DlssInstaller.HashFile(choices)!=record.SessionAppliedHash){
    if(new FileInfo(choices).Length>1048576)throw new IOException("会话模组清单过大，已保留待核对。");
    string[] ordinary=SaveProfiles.DefaultMods(File.ReadAllBytes(choices)).Where(id=>!record.ManagedIds.Contains(id.TrimStart('\\'))).ToArray();
    // Use the same per-profile ordinary writer lease so checking and projection cannot race a later edit.
    string name="Local\\OrangeOrdinary-"+LauncherComponents.Hash(Encoding.UTF8.GetBytes(context.Root.ToUpperInvariant()));using(var mutex=new Mutex(false,name)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("此配置正在保存，请稍后恢复会话。");if(!SameOriginal(context.DefaultModsPath,Convert.FromBase64String(record.Original)))conflict=true;else e.OwnedComponents().SaveOrdinary(ordinary,CancellationToken.None);}finally{if(held)mutex.ReleaseMutex();}}
   }
   ClearRequest(e);RemoveOwnedDirectory(context,record.BridgeDirectory,"bridge-");if(!conflict)RemoveOwnedDirectory(context,record.SessionDirectory,"live-");File.Delete(Journal(e));e.LiveSession=null;
   return conflict?"已保留此配置的新编辑；游戏期间的清单保留在会话目录供核对。":"已保存此配置的游戏内普通模组选择并清理启动会话。";
  }
 }
}
