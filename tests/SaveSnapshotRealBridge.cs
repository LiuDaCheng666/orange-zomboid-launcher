using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using System.Web.Script.Serialization;

// This adapter invokes the unmodified release candidate in its own ApplicationBase.
// It never substitutes process inventory, TestMode, snapshot copying, or game entry.
public sealed class SaveSnapshotRealBridge : MarshalByRefObject {
 static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=67108864};
 Assembly app;object controller;Dictionary<string,object> config;string evidence;
 public override object InitializeLifetimeService(){return null;}
 string Get(string key){return (string)config[key];}
 void Write(string name,object value){lock(json){File.WriteAllText(Path.Combine(evidence,name),json.Serialize(value),new UTF8Encoding(false));}}
 void Event(string name,object value){lock(json){File.AppendAllText(Path.Combine(evidence,"events.jsonl"),json.Serialize(new{utc=DateTime.UtcNow.ToString("o"),name=name,data=value})+"\n",new UTF8Encoding(false));}}
 object Call(string name,Dictionary<string,object> payload){return app.GetType("PZ3D.ModernController",true).GetMethod("Execute",Flags).Invoke(controller,new object[]{name,payload});}
 Dictionary<string,object> Object(object value){lock(json){return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}}
 object Static(string type,string method,params object[] args){return app.GetType("PZ3D."+type,true).GetMethod(method,Flags).Invoke(null,args);}
 object Field(object target,string field){return target.GetType().GetField(field,Flags).GetValue(target);}
 public string Run(string configPath,string action){
  AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
  config=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(configPath));evidence=Get("evidence");Directory.CreateDirectory(evidence);
  try{
   app=Assembly.LoadFrom(Get("assembly"));Static("LauncherRuntime","Initialize");
   string key=(string)app.GetType("PZ3D.CoreUpdateTrust",true).GetProperty("PublicKeyXml",Flags).GetValue(null,null);
   object files=Static("DistributionBootstrap","Read",AppDomain.CurrentDomain.BaseDirectory,key);Write("bootstrap-"+action+".json",new{applicationBase=AppDomain.CurrentDomain.BaseDirectory,verifiedFiles=((System.Collections.IEnumerable)files).Cast<object>().Count(),fileVersion=FileVersionInfo.GetVersionInfo(Get("assembly")).FileVersion,versionFile=File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"VERSION")).Trim()});
   controller=Activator.CreateInstance(app.GetType("PZ3D.ModernController",true),Flags,null,new object[]{Get("game"),Get("user"),Get("data"),false},null);
   controller.GetType().GetField("Event",Flags).SetValue(controller,new Action<string,object>(Event));
   controller.GetType().GetField("Progress",Flags).SetValue(controller,new Action<int,string>((value,text)=>Event("progress",new{value=value,text=text})));
   Write("engine-"+action+".json",new{testMode=Field(Field(controller,"engine"),"TestMode"),game=Get("game"),user=Get("user"),data=Get("data")});
   object result;
   if(action=="manifestcapacity")result=ManifestCapacity();
   else if(action=="catalogsources"){
    object engine=Field(controller,"engine");var warnings=new List<string>();object roots=Static("GameDiscovery","WorkshopSources",engine,warnings);var save=Object(Static("SaveProfiles","Read",engine,Get("saveId")));
    string[] saved=((System.Collections.IEnumerable)save["enabled"]).Cast<object>().Select(value=>(string)value).ToArray();string[] defaults=(string[])Static("SaveProfiles","DefaultMods",File.ReadAllBytes(Path.Combine(Get("user"),"mods","default.txt")));var choices=json.Deserialize<Dictionary<string,string>>(Get("modChoices"));
    string[] selected=(string[])Static("SnapshotModChoices","Apply",saved.Concat(defaults).ToArray(),choices);object catalog=Static("SaveProfiles","SnapshotEntries",engine,selected,warnings,null,choices);
    var candidates=((System.Collections.IEnumerable)Field(catalog,"Candidates")).Cast<object>().Where(value=>selected.Contains((string)Field(value,"Id"))).Select(value=>new{id=Field(value,"Id"),root=Field(value,"Root"),workshop=Field(value,"Workshop"),source=Field(value,"Source")}).ToArray();
    result=new{roots=Field(roots,"Roots"),inputs=Field(roots,"Inputs"),selectedCount=selected.Length,candidates=candidates,warnings=warnings,scope="Actual candidate metadata discovery only, no full game inventory or data hashing."};
   }else if(action=="nativepathprobe"){
    string relative="steamapps/workshop/content/108600/3470426196/mods/KATTAJ1 Military Pack/42.12/media/clothing/clothingItems";
    string input=Path.Combine(Get("game"),relative.Replace('/',Path.DirectorySeparatorChar));if(!Directory.Exists(input))throw new IOException("Expected read-only actual source directory is absent: "+input);
    string destination=Path.Combine(Get("fixture"),"native-copy-long-path-probe.partial-"+Guid.NewGuid().ToString("N"),"game");string output=Path.Combine(destination,relative.Replace('/',Path.DirectorySeparatorChar));
    var original=new FileInfo(Directory.GetFiles(input).OrderBy(value=>value,StringComparer.OrdinalIgnoreCase).First());string copiedFile=Path.Combine(output,original.Name);
    Write("nativepathprobe-path.json",new{input=input,inputLength=input.Length,output=output,outputLength=output.Length,fileInput=original.FullName,fileOutput=copiedFile,fileOutputLength=copiedFile.Length,productionMethod="SaveEnvironments.Copy",sourceReadOnly=true});
    var type=app.GetType("PZ3D.SaveEnvironmentFile",true);var entry=Activator.CreateInstance(type);type.GetField("Path",Flags).SetValue(entry,relative);type.GetField("Directory",Flags).SetValue(entry,true);var file=Activator.CreateInstance(type);type.GetField("Path",Flags).SetValue(file,relative+"/"+original.Name);type.GetField("Directory",Flags).SetValue(file,false);type.GetField("Bytes",Flags).SetValue(file,original.Length);type.GetField("WriteTicks",Flags).SetValue(file,original.LastWriteTimeUtc.Ticks);var entries=Array.CreateInstance(type,2);entries.SetValue(entry,0);entries.SetValue(file,1);
    Static("SaveEnvironments","Copy",Get("game"),destination,entries,CancellationToken.None,null,null);string sourceHash=(string)Static("SaveEnvironments","FileHash",original.FullName,CancellationToken.None,null),copiedHash=(string)Static("SaveEnvironments","FileHash",copiedFile,CancellationToken.None,null);if(sourceHash!=copiedHash)throw new IOException("Long-path actual source file copy differs");result=new{input=original.FullName,output=copiedFile,outputLength=copiedFile.Length,bytes=original.Length,sha256=copiedHash,note="One actual source directory/file through production Copy+FileHash+timestamp; original source remains read-only."};
   }else if(action=="pathprobe"){
    string path=Path.Combine(Get("fixture"),"long-path-host-probe",new string('a',100),new string('b',100),new string('c',100));Directory.CreateDirectory(path);string file=Path.Combine(path,"probe.txt");File.WriteAllText(file,"host path support");bool legacy,blocked;AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling",out legacy);AppContext.TryGetSwitch("Switch.System.IO.BlockLongPaths",out blocked);result=new{path=file,length=file.Length,read=File.ReadAllText(file),useLegacyPathHandling=legacy,blockLongPaths=blocked};
   }else if(action=="create"){
    Write("storage-before.json",Call("environmentStorageGet",new Dictionary<string,object>()));
    Write("storage-save.json",Call("environmentStorageSave",new Dictionary<string,object>{{"directory",Get("snapshots")}}));
    var preview=Object(Call("environmentPreview",new Dictionary<string,object>{{"saveId",Get("saveId")},{"jobId",Guid.NewGuid().ToString("N")}}));Write("preview.json",preview);if(string.IsNullOrEmpty((string)preview["stamp"]))throw new IOException("Actual preview refused creation: "+json.Serialize(preview["warnings"]));
    result=Call("environmentCreateDefault",new Dictionary<string,object>{{"saveId",Get("saveId")},{"name","真实存档独立验收"},{"stamp",(string)preview["stamp"]},{"modChoices",Get("modChoices")}});
   }else if(action=="preview")result=Call("environmentPreview",new Dictionary<string,object>{{"saveId",Get("saveId")},{"jobId",Guid.NewGuid().ToString("N")}});
   else if(action=="check"){
    var created=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(evidence,"create.json")));var snapshot=(Dictionary<string,object>)created["snapshot"];
    result=Call("environmentCheck",new Dictionary<string,object>{{"snapshotId",(string)snapshot["id"]}});
   }else if(action=="validate"){
    object frozen=Static("SaveEnvironmentRuntime","ForSave",Field(controller,"engine"),Get("saveId"));
    Static("SaveEnvironmentRuntime","Validate",frozen,Get("saveId"));Static("SaveEnvironmentRuntime","CheckSource",frozen,CancellationToken.None);
    var start=(ProcessStartInfo)Static("LaunchProfiles","BuildStartInfo",frozen);object record=Field(frozen,"FrozenEnvironment");var captured=((System.Collections.IEnumerable)Field(record,"Files")).Cast<object>().Where(value=>!(bool)Field(value,"Directory")).ToArray();
    result=new{game=Field(frozen,"Game"),user=Field(frozen,"User"),testMode=Field(frozen,"TestMode"),executable=start.FileName,arguments=start.Arguments,gameVersion=Field(record,"GameVersion"),modChoices=Field(record,"ModChoices"),modRoots=((Array)Field(record,"ModRoots")).Length,manifestBytes=new FileInfo((string)Field(record,"ManifestPath")).Length,capturedFiles=captured.Length,capturedBytes=captured.Sum(value=>(long)Field(value,"Bytes")),nestedWorkshopCachePresent=Directory.Exists(Path.Combine((string)Field(frozen,"Game"),"steamapps","workshop")),note="Registered frozen manifest validated and real game-JRE compatibility probe executed; this action does not enter a world."};
   }else if(action=="running"){
    object frozen=Static("SaveEnvironmentRuntime","ForSave",Field(controller,"engine"),Get("saveId"));string game=(string)Field(frozen,"Game");
    var start=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(evidence,"launch-start.json")));int pid=Convert.ToInt32(start["pid"]);
    var clients=((System.Collections.IEnumerable)Static("GameProcessGuard","Clients",game,null)).Cast<object>().Select(value=>new{pid=(int)Field(value,"Pid"),image=(string)Field(value,"Image"),java=(bool)Field(value,"Java")}).ToArray();
    bool client=(bool)frozen.GetType().GetMethod("RuntimeRunning",Flags).Invoke(frozen,new object[]{true}),any=(bool)frozen.GetType().GetMethod("RuntimeRunning",Flags).Invoke(frozen,new object[]{false});string role;
    using(var process=Process.GetProcessById(pid)){string image=process.MainModule.FileName;if(!image.StartsWith(game.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("Runtime classification probe refuses a non-owned process");role=Static("GameProcessGuard","ReadJavaRole",game,process.ProcessName,image,process).ToString();}
    if(!client||!any||role!="Client"||!clients.Any(value=>value.pid==pid))throw new IOException("Actual production guard did not identify the owned JVM as Client");
    result=new{runtimeRunningClient=client,runtimeRunningAny=any,role=role,clients=clients,note="Read-only actual production process classification; no process close/termination method was invoked."};
   }else if(action=="resave"||action=="resavecontroller"){
    object frozen=Static("SaveEnvironmentRuntime","ForSave",Field(controller,"engine"),Get("saveId"));
    if(Field(frozen,"FrozenEnvironment")==null)throw new IOException("Resave requires the registered isolated snapshot");
    var before=Object(action=="resavecontroller"?Call("saveGet",new Dictionary<string,object>{{"id",Get("saveId")}}):Static("SaveProfiles","Read",frozen,Get("saveId")));Write(action+"-before.json",before);string save= (string)Static("SaveProfiles","ResolveSave",frozen,Get("saveId"));File.WriteAllBytes(Path.Combine(evidence,action+"-before-mods.txt"),File.ReadAllBytes(Path.Combine(save,"mods.txt")));
    string[] enabled=((System.Collections.IEnumerable)before["enabled"]).Cast<object>().Select(value=>(string)value).ToArray();
    result=action=="resavecontroller"?Call("saveSave",new Dictionary<string,object>{{"id",Get("saveId")},{"stamp",(string)before["stamp"]},{"mods",enabled},{"confirmed",true}}):Static("SaveProfiles","Save",frozen,Get("saveId"),(string)before["stamp"],enabled,true);Write(action+"-written.json",result);File.WriteAllBytes(Path.Combine(evidence,action+"-after-mods.txt"),File.ReadAllBytes(Path.Combine(save,"mods.txt")));
    Static("SaveEnvironmentRuntime","Validate",frozen,Get("saveId"));
   }else if(action=="launch"||action=="launchjava"||action=="launchnativeprobe")result=Launch(action=="launchjava",action=="launchnativeprobe");else throw new IOException("Unknown real acceptance action");
   Write(action+".json",result);return json.Serialize(new{ok=true,action=action,result=result});
  }catch(Exception error){while(error is TargetInvocationException&&error.InnerException!=null)error=error.InnerException;var failure=new{ok=false,action=action,type=error.GetType().FullName,error=error.Message,details=error.ToString()};Write(action+"-failure.json",failure);return json.Serialize(failure);}
  finally{var disposable=controller as IDisposable;if(disposable!=null)disposable.Dispose();}
 }
 object ManifestCapacity(){
  // Capacity preflight only: actual metadata and equal-length placeholder hashes.
  // This action does not copy payload files, hash their contents, or publish a snapshot.
  object engine=Field(controller,"engine");var warnings=new List<string>();var view=Object(Static("SaveProfiles","Read",engine,Get("saveId")));var choices=json.Deserialize<Dictionary<string,string>>(Get("modChoices"));
  string[] saved=((System.Collections.IEnumerable)view["enabled"]).Cast<object>().Select(value=>(string)value).ToArray();byte[] defaults=File.ReadAllBytes(Path.Combine(Get("user"),"mods","default.txt"));string[] defaultIds=(string[])Static("SaveProfiles","DefaultMods",defaults);string[] selected=(string[])Static("SnapshotModChoices","Apply",saved.Concat(defaultIds).ToArray(),choices);
  object catalog=Static("SaveProfiles","SnapshotEntries",engine,selected,warnings,null,choices);var candidates=((System.Collections.IEnumerable)Field(catalog,"Candidates")).Cast<object>().Where(value=>selected.Contains((string)Field(value,"Id"))).GroupBy(value=>(string)Field(value,"Root"),StringComparer.OrdinalIgnoreCase).ToArray();
  Type entryType=app.GetType("PZ3D.SaveEnvironmentFile",true),rootType=app.GetType("PZ3D.SaveEnvironmentModRoot",true),recordType=app.GetType("PZ3D.SaveEnvironmentRecord",true);var all=new List<object>();long sourceRows=0;
  Func<string,Array> scan=path=>(Array)Static("SaveEnvironments","Scan",path,false,CancellationToken.None,null,null,null);
  var exclusion=(Func<string,bool>)Delegate.CreateDelegate(typeof(Func<string,bool>),app.GetType("PZ3D.SaveEnvironments",true).GetMethod("GameSourceExcluded",Flags));
  var game=(Array)Static("SaveEnvironments","Scan",Get("game"),false,CancellationToken.None,exclusion,null,null);
  Action<Array,string> add=(files,prefix)=>{sourceRows+=files.Length;foreach(object original in files){bool directory=(bool)Field(original,"Directory");if(!directory)original.GetType().GetField("Hash",Flags).SetValue(original,new string('0',64));object copy=Activator.CreateInstance(entryType);foreach(string field in new[]{"Hash","Bytes","WriteTicks","Directory"})entryType.GetField(field,Flags).SetValue(copy,Field(original,field));entryType.GetField("Path",Flags).SetValue(copy,prefix+(string)Field(original,"Path"));all.Add(copy);}};add(game,"game/");
  string target=Path.Combine(Get("snapshots"),"OZL-快照-20261027-110000-aaaaaa");var roots=Array.CreateInstance(rootType,candidates.Length);
  for(int i=0;i<candidates.Length;i++){
   object candidate=candidates[i].First();string source=(string)Field(candidate,"Root"),prefix="mods/"+i.ToString("D4")+"/mods/"+Path.GetFileName(source)+"/";var files=scan(source);add(files,prefix);object root=Activator.CreateInstance(rootType);rootType.GetField("SourceRoot",Flags).SetValue(root,source);string copied=Path.Combine(target,prefix.TrimEnd('/').Replace('/',Path.DirectorySeparatorChar));rootType.GetField("CopiedRoot",Flags).SetValue(root,copied);rootType.GetField("Layout",Flags).SetValue(root,Path.Combine(copied,((string)Field(candidate,"Layout")).Substring(source.Length).TrimStart('\\','/')));string common=(string)Field(candidate,"CommonRoot");rootType.GetField("CommonRoot",Flags).SetValue(root,common==null?null:Path.Combine(copied,common.Substring(source.Length).TrimStart('\\','/')));rootType.GetField("WorkshopId",Flags).SetValue(root,Field(candidate,"Workshop"));rootType.GetField("ModIds",Flags).SetValue(root,candidates[i].Select(value=>(string)Field(value,"Id")).ToArray());rootType.GetField("SourceFiles",Flags).SetValue(root,files);roots.SetValue(root,i);Event("manifest-capacity-inventory",new{roots=i+1,total=candidates.Length,rows=sourceRows});
  }
  object record=Activator.CreateInstance(recordType);foreach(var pair in new Dictionary<string,object>{{"Id",new string('a',32)},{"Name","真实存档独立验收"},{"SaveId",Get("saveId")},{"SourceGame",Get("game")},{"UserPath",Get("user")},{"RootPath",target},{"GamePath",Path.Combine(target,"game")},{"RuntimeHome",Path.Combine(target,"runtime","home")},{"GameVersion","42.21"},{"CreatedUtc",DateTime.UtcNow.ToString("o")},{"RootIdentity","1:2:3"},{"SourceGameIdentity","1:2:3"},{"UserIdentity","1:2:3"},{"ManifestPath",Path.Combine(target,"environment-manifest.json")},{"SaveConfigHash",new string('0',64)},{"ModIds",saved},{"ModChoices",choices},{"Maps",new string[0]},{"DefaultModIds",defaultIds},{"PreloadModIds",new string[0]},{"DefaultBytes",defaults},{"SaveModsBytes",File.ReadAllBytes(Path.Combine(Get("user"),"Saves",Get("saveId").Replace('/',Path.DirectorySeparatorChar),"mods.txt"))},{"ModRoots",roots},{"GameSourceFiles",game}})recordType.GetField(pair.Key,Flags).SetValue(record,pair.Value);
  object buddy=Static("SaveEnvironments","BuddyConfiguration",engine);recordType.GetField("BuddyConfigBytes",Flags).SetValue(record,Field(buddy,"ConfigBytes"));recordType.GetField("ApprovalBytes",Flags).SetValue(record,Field(buddy,"ApprovalBytes"));
  var targetFiles=Array.CreateInstance(entryType,all.Count);for(int i=0;i<all.Count;i++)targetFiles.SetValue(all[i],i);
  // One synthetic tick difference exercises lossless cross-filesystem timestamp recovery.
  var expectedRoots=roots.Cast<object>().Select(value=>(Array)Field(value,"SourceFiles")).ToArray();object tickEntry=expectedRoots.SelectMany(value=>value.Cast<object>()).First(value=>!(bool)Field(value,"Directory"));entryType.GetField("WriteTicks",Flags).SetValue(tickEntry,(long)Field(tickEntry,"WriteTicks")+1);
  var measure=new JavaScriptSerializer{MaxJsonLength=268435456,RecursionLimit=32};string text=measure.Serialize(new{Schema=1,Record=record,Files=targetFiles});long oldBytes=Encoding.UTF8.GetByteCount(text);int oldCharacters=text.Length;text=null;
  Write("manifest-capacity-size.json",new{scope="Metadata-only capacity preflight; equal-length placeholder hashes and one synthetic source/target mtime delta; no payload copy/hash/publication",sourceRows=sourceRows,targetRows=all.Count,legacyCharacters=oldCharacters,legacyUtf8Bytes=oldBytes,warnings=warnings});
  var timing=Stopwatch.StartNew();byte[] encoded=(byte[])Static("SaveEnvironments","EncodeManifest",record,targetFiles);long encodeMs=timing.ElapsedMilliseconds;timing.Restart();object decoded=Static("SaveEnvironments","DecodeManifest",encoded),decodedRecord=Field(decoded,"Record");long decodeMs=timing.ElapsedMilliseconds;var decodedRoots=(Array)Field(decodedRecord,"ModRoots");if(decodedRoots.Length!=expectedRoots.Length)throw new IOException("Production codec lost a captured mod root");for(int i=0;i<expectedRoots.Length;i++)SameRows(expectedRoots[i],(Array)Field(decodedRoots.GetValue(i),"SourceFiles"));SameRows(game,(Array)Field(decodedRecord,"GameSourceFiles"));SameRows(targetFiles,(Array)Field(decoded,"Files"));
  return new{scope="Capacity preflight only; not a real snapshot creation. Production EncodeManifest/DecodeManifest, actual metadata with placeholder hashes.",sourceRows=sourceRows,targetRows=all.Count,legacyCharacters=oldCharacters,legacyUtf8Bytes=oldBytes,encodedCharacters=Encoding.UTF8.GetCharCount(encoded),encodedUtf8Bytes=encoded.LongLength,encodeMilliseconds=encodeMs,decodeMilliseconds=decodeMs,roundtripFiles=((Array)Field(decoded,"Files")).Length,allSourceFileFieldsPreserved=true,syntheticMtimeDeltaPreserved=true};
 }
 void SameRows(Array expected,Array actual){if(expected.Length!=actual.Length)throw new IOException("Manifest codec row count changed");for(int i=0;i<expected.Length;i++)foreach(string field in new[]{"Path","Hash","Bytes","WriteTicks","Directory"})if(!object.Equals(Field(expected.GetValue(i),field),Field(actual.GetValue(i),field)))throw new IOException("Manifest codec changed source metadata: "+field+" row "+i);}
 object Launch(bool directJava,bool argumentProbe){
  object engine=Field(controller,"engine");object frozen=Static("SaveEnvironmentRuntime","ForSave",engine,Get("saveId"));if(frozen==null)throw new IOException("Save did not resolve to its registered snapshot");
  Static("SaveEnvironmentRuntime","PrepareForLaunch",frozen,Get("saveId"));
  object request=Activator.CreateInstance(app.GetType("PZ3D.ProfileLaunchRequest",true));var parts=Get("saveId").Split('/');
  foreach(var pair in new Dictionary<string,string>{{"Kind","save"},{"Id",Get("saveId")},{"Mode",parts[0]},{"Save",parts[1]}})request.GetType().GetField(pair.Key,Flags).SetValue(request,pair.Value);
  string session=Guid.NewGuid().ToString("N");request.GetType().GetField("StartupSessionId",Flags).SetValue(request,session);Static("GameStartupComponent","Prepare",frozen,session);
  var start=(ProcessStartInfo)Static("LaunchProfiles","BuildStartInfo",frozen);
  if(argumentProbe)start.EnvironmentVariables["JAVA_TOOL_OPTIONS"]="-javaagent:"+Get("argumentProbeJar")+"="+Get("argumentProbeOutput");
  if(directJava){
   // Diagnostic only: exact frozen JSON, actual bundled JVM, same private desktop.
   // This does not change the release candidate's native-executable launch policy.
   var native=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(start.WorkingDirectory,"ProjectZomboid64.json")));var arguments=((System.Collections.IEnumerable)native["vmArgs"]).Cast<object>().Select(value=>(string)value).ToList();
   if(native.ContainsKey("windows")){var windows=(Dictionary<string,object>)native["windows"];var branch=windows.Keys.Where(value=>new Version(value)<=Environment.OSVersion.Version).OrderByDescending(value=>new Version(value)).FirstOrDefault();if(branch!=null){var values=(Dictionary<string,object>)windows[branch];if(values.ContainsKey("vmArgs"))arguments.AddRange(((System.Collections.IEnumerable)values["vmArgs"]).Cast<object>().Select(value=>(string)value));}}
   arguments.Add("-cp");arguments.Add(string.Join(";",((System.Collections.IEnumerable)native["classpath"]).Cast<object>().Select(value=>(string)value)));arguments.Add(((string)native["mainClass"]).Replace('/','.'));
   start.FileName=Path.Combine(start.WorkingDirectory,"jre64","bin","java.exe");start.Arguments=string.Join(" ",arguments.Select(Quote))+" "+start.Arguments;
  }
  string desktop="OzlSnapshotReal-"+Guid.NewGuid().ToString("N");IntPtr handle=Native.CreateDesktop(desktop,IntPtr.Zero,IntPtr.Zero,0,0x1ff,IntPtr.Zero);if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"CreateDesktop");
  Native.PROCESS_INFORMATION process=new Native.PROCESS_INFORMATION();bool started=false;string recovery="";int? code=null;DateTime launchedAt=DateTime.UtcNow;var stopped=new List<object>();
  try{
   // The official stage still precedes native process creation, inside recovery coverage.
   Static("LaunchProfiles","Stage",frozen,request);
   var startup=new Native.STARTUPINFO{cb=Marshal.SizeOf(typeof(Native.STARTUPINFO)),lpDesktop="WinSta0\\"+desktop,dwFlags=0x100};
   string environment=string.Join("\0",start.EnvironmentVariables.Keys.Cast<string>().OrderBy(value=>value,StringComparer.OrdinalIgnoreCase).Select(name=>name+"="+start.EnvironmentVariables[name]))+"\0\0";
   string emptyInput=Path.Combine(evidence,"launch-empty-stdin.bin");File.WriteAllBytes(emptyInput,new byte[0]);
   using(var output=new FileStream(Path.Combine(evidence,"launch-native-output.log"),FileMode.Create,FileAccess.Write,FileShare.ReadWrite))using(var input=new FileStream(emptyInput,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){
    startup.hStdOutput=startup.hStdError=output.SafeFileHandle.DangerousGetHandle();startup.hStdInput=input.SafeFileHandle.DangerousGetHandle();
    if(!Native.SetHandleInformation(startup.hStdOutput,1,1)||!Native.SetHandleInformation(startup.hStdInput,1,1))throw new Win32Exception(Marshal.GetLastWin32Error(),"Owned acceptance log handle inheritance");
    var env=Marshal.StringToHGlobalUni(environment);try{if(!Native.CreateProcess(start.FileName,new StringBuilder("\""+start.FileName+"\" "+start.Arguments),IntPtr.Zero,IntPtr.Zero,true,0x400|(start.CreateNoWindow?0x08000000:0),env,start.WorkingDirectory,ref startup,out process))throw new Win32Exception(Marshal.GetLastWin32Error(),"CreateProcess on private desktop");}finally{Marshal.FreeHGlobal(env);Native.SetHandleInformation(startup.hStdOutput,1,0);Native.SetHandleInformation(startup.hStdInput,1,0);}
   }
   started=true;Native.CloseHandle(process.hThread);
   var threadDesktop=new StringBuilder(256);int desktopNeeded;IntPtr threadDesktopHandle=Native.GetThreadDesktop(process.dwThreadId);bool desktopObserved=threadDesktopHandle!=IntPtr.Zero&&Native.GetUserObjectInformation(threadDesktopHandle,2,threadDesktop,threadDesktop.Capacity*2,out desktopNeeded);
   Write("launch-start.json",new{pid=process.dwProcessId,launchedUtc=launchedAt.ToString("o"),directJavaDiagnostic=directJava,desktop=desktop,observedThreadDesktop=desktopObserved?threadDesktop.ToString():"unavailable",inputDesktop=InputDesktopName(),switchDesktopCalled=false,executable=start.FileName,arguments=start.Arguments,workingDirectory=start.WorkingDirectory,createNoWindow=start.CreateNoWindow,pathFirstEntry=(start.EnvironmentVariables["Path"]??"").Split(';').FirstOrDefault(),engineGame=Field(frozen,"Game"),engineUser=Field(frozen,"User"),testMode=Field(frozen,"TestMode")});
   int seconds=config.ContainsKey("launchSeconds")?Convert.ToInt32(config["launchSeconds"]):240;string stopRequest=Path.Combine(Get("fixture"),"stop-owned-game.request");if(File.Exists(stopRequest))File.Delete(stopRequest);
   for(int i=0;i<seconds;i++){
    var owned=OwnedGameProcesses(start.WorkingDirectory,launchedAt);try{if(owned.Count==0&&i>=5)break;if(i%15==0)Event("owned-game-alive",new{pids=owned.Select(value=>value.Id).ToArray(),seconds=i,inputDesktop=InputDesktopName()});}finally{foreach(var value in owned)value.Dispose();}Thread.Sleep(1000);
    if(File.Exists(stopRequest)){Event("owned-game-stop-request",new{path=stopRequest,seconds=i+1});break;}
   }
   foreach(var owned in OwnedGameProcesses(start.WorkingDirectory,launchedAt))using(owned){int id=owned.Id;string image=owned.MainModule.FileName;long birth=owned.StartTime.ToUniversalTime().Ticks;bool closeRequested=owned.CloseMainWindow(),forced=false;if(!owned.WaitForExit(10000)){forced=true;owned.Kill();owned.WaitForExit(10000);}stopped.Add(new{pid=id,image=image,birth=birth,closeMainWindowRequested=closeRequested,forcedStop=forced,exited=owned.HasExited});}
   uint nativeExit;if(Native.GetExitCodeProcess(process.hProcess,out nativeExit)&&nativeExit!=259)code=unchecked((int)nativeExit);
   recovery=(string)Static("LaunchProfiles","Recover",frozen,true,null);var outcome=new{started=started,desktop=desktop,inputDesktop=InputDesktopName(),executable=start.FileName,exitCode=code,stopped=stopped,recovery=recovery,note="Private desktop; no SwitchDesktop or foreground UI operations. Only fresh processes whose image is inside this unique snapshot are owned. Inspect native logs for actual world entry."};Write("launch-process-result.json",outcome);if(code.HasValue&&code.Value!=0)throw new IOException("Actual game process exited with nonzero code "+code.Value+"; retained native output and process result");return outcome;
  }finally{if(process.hProcess!=IntPtr.Zero)Native.CloseHandle(process.hProcess);Native.CloseDesktop(handle);try{Write("launch-recovery.json",new{message=Static("LaunchProfiles","Recover",frozen,true,null),pending=Static("LaunchProfiles","Pending",frozen)});}catch(Exception error){Write("launch-recovery.json",new{error=error.ToString()});}}
 }
 static string Quote(string value){if(value.IndexOf('"')>=0||value.IndexOf('\0')>=0||value.IndexOf('\r')>=0||value.IndexOf('\n')>=0)throw new IOException("Diagnostic JVM argument contains unsupported control/quote");return "\""+value+new string('\\',value.Reverse().TakeWhile(c=>c=='\\').Count())+"\"";}
 static List<Process> OwnedGameProcesses(string game,DateTime since){var found=new List<Process>();string prefix=Path.GetFullPath(game).TrimEnd('\\')+"\\";foreach(string name in new[]{"ProjectZomboid64","java","javaw"})foreach(var process in Process.GetProcessesByName(name)){bool owned=false;try{owned=!process.HasExited&&process.StartTime.ToUniversalTime()>=since.AddSeconds(-2)&&process.MainModule.FileName.StartsWith(prefix,StringComparison.OrdinalIgnoreCase);if(owned)found.Add(process);}catch{}finally{if(!owned)process.Dispose();}}return found;}
 static string InputDesktopName(){IntPtr handle=Native.OpenInputDesktop(0,false,1);if(handle==IntPtr.Zero)return "unavailable";try{var name=new StringBuilder(256);int needed;return Native.GetUserObjectInformation(handle,2,name,name.Capacity*2,out needed)?name.ToString():"unavailable";}finally{Native.CloseDesktop(handle);}}
 static class Native {
  [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]internal struct STARTUPINFO{public int cb;public string lpReserved,lpDesktop,lpTitle;public int dwX,dwY,dwXSize,dwYSize,dwXCountChars,dwYCountChars,dwFillAttribute,dwFlags;public short wShowWindow,cbReserved2;public IntPtr lpReserved2,hStdInput,hStdOutput,hStdError;}
  [StructLayout(LayoutKind.Sequential)]internal struct PROCESS_INFORMATION{public IntPtr hProcess,hThread;public int dwProcessId,dwThreadId;}
  [DllImport("user32",SetLastError=true,CharSet=CharSet.Unicode)]internal static extern IntPtr CreateDesktop(string name,IntPtr device,IntPtr mode,int flags,int access,IntPtr attributes);
  [DllImport("user32",SetLastError=true)]internal static extern bool CloseDesktop(IntPtr desktop);
  [DllImport("user32",SetLastError=true)]internal static extern IntPtr GetThreadDesktop(int thread);
  [DllImport("user32",SetLastError=true)]internal static extern IntPtr OpenInputDesktop(int flags,bool inherit,int access);
  [DllImport("user32",SetLastError=true,CharSet=CharSet.Unicode)]internal static extern bool GetUserObjectInformation(IntPtr handle,int index,StringBuilder data,int length,out int needed);
  [DllImport("kernel32",SetLastError=true)]internal static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32",SetLastError=true)]internal static extern bool SetHandleInformation(IntPtr handle,int mask,int flags);
  [DllImport("kernel32",SetLastError=true)]internal static extern bool GetExitCodeProcess(IntPtr handle,out uint code);
  [DllImport("kernel32",SetLastError=true,CharSet=CharSet.Unicode)]internal static extern bool CreateProcess(string app,StringBuilder command,IntPtr pa,IntPtr ta,bool inherit,int flags,IntPtr environment,string directory,ref STARTUPINFO startup,out PROCESS_INFORMATION process);
 }
 public static int Main(string[] args){try{
  AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
  if(args.Length!=2)throw new IOException("Expected config and action");
  if(args[0]=="--formal-entry-path-probe"){
   string path=Path.Combine(args[1],"formal-entry-long-path-probe",new string('a',100),new string('b',100),new string('c',100));
   bool legacy,blocked;AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling",out legacy);AppContext.TryGetSwitch("Switch.System.IO.BlockLongPaths",out blocked);
   Console.WriteLine("Formal first-entry same two switches; path="+path+"; length="+path.Length+"; legacy="+legacy+"; blocked="+blocked+"; no child AppDomain involved");Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"entry.txt"),"same entry switches and manifest");Console.WriteLine("PASS formal entry long path");return 0;
  }
  var serializer=new JavaScriptSerializer();var config=serializer.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[0]));
  var domain=AppDomain.CreateDomain("SaveSnapshotReal-"+Guid.NewGuid().ToString("N"),null,new AppDomainSetup{ApplicationBase=Path.GetDirectoryName((string)config["assembly"]),ConfigurationFile=Assembly.GetExecutingAssembly().Location+".config"});
  try{var bridge=(SaveSnapshotRealBridge)domain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().Location,typeof(SaveSnapshotRealBridge).FullName);string result=bridge.Run(Path.GetFullPath(args[0]),args[1]);Console.OutputEncoding=new UTF8Encoding(false);Console.WriteLine(result);return result.Contains("\"ok\":true")?0:1;}finally{AppDomain.Unload(domain);}
 }catch(Exception error){Console.Error.WriteLine(error);return 2;}}
}
