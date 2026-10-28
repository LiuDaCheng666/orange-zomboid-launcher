using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class LaunchConfigurationResolution {
  public string Id,Name,Kind,VersionId,ErrorCode="",Message="";
  public bool Available;
  public LauncherConfiguration Context;
  public LocalGameVersion Version;
  public SaveEnvironmentRecord Snapshot;
 }
 // A per-original-user inventory. Only native catalog IDs cross this boundary;
 // no caller-supplied game/mod/user path is interpreted by Resolve or mutations.
 sealed class LaunchConfigurationProfiles {
  sealed class Catalog {internal string SelectedId;internal IList<LocalGameVersion> Rows;}
  sealed class Entry {public string Id,Name,VersionId;}
  sealed class Document {public int Schema=1;public string SelectedId="launcher";public List<Entry> Entries=new List<Entry>();}
  static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
  readonly string data,game,user;
  public string IndexPath {get;private set;}
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1048576};}
  static bool Opaque(string value){return Regex.IsMatch(value??"",@"\A[a-f0-9]{32}\z");}
  static bool Builtin(string id){return id=="launcher"||id=="steam"||id=="vanilla";}
  static void Id(string id){if(!Builtin(id)&&!(id!=null&&id.StartsWith("live:",StringComparison.Ordinal)&&Opaque(id.Substring(5)))&&!(id!=null&&id.StartsWith("snapshot:",StringComparison.Ordinal)&&Opaque(id.Substring(9))))throw new ArgumentException("启动配置标识无效。","id");}
  static void VersionId(string id){if(id!="steam"&&!Opaque(id))throw new ArgumentException("版本标识无效。","id");}
  static string Name(string value){value=(value??"").Trim();if(value.Length==0||value.Length>100||value.Any(char.IsControl))throw new ArgumentException("配置名称需要 1 至 100 个普通字符。");return value;}
  public LaunchConfigurationProfiles(string dataRoot,string originalGame,string originalUser){data=VersionLibrary.Full(dataRoot);game=VersionLibrary.Full(originalGame);user=VersionLibrary.Full(originalUser);VersionLibrary.Safe(data);IndexPath=Path.Combine(data,"launch-configuration-index",DlssInstaller.Hash(Utf8.GetBytes(user.ToUpperInvariant())).ToLowerInvariant()+".json");}
  byte[] Read(){VersionLibrary.Safe(IndexPath);if(!File.Exists(IndexPath))return null;using(var f=new FileStream(IndexPath,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){if(f.Length>1048576)throw new IOException("启动配置目录过大。");byte[] b=new byte[(int)f.Length];int n=0,c;while(n<b.Length&&(c=f.Read(b,n,b.Length-n))>0)n+=c;if(n!=b.Length)throw new IOException("启动配置目录读取不完整。");return b;}}
  Document Load(){byte[] bytes=Read();Document doc;try{doc=bytes==null?new Document():Json().Deserialize<Document>(Utf8.GetString(bytes));}catch(Exception error){throw new IOException("启动配置目录无法读取，原文件已保留。",error);}if(doc==null||doc.Schema!=1||doc.Entries==null||doc.Entries.Count>128)throw new IOException("启动配置目录格式无效。");try{Id(doc.SelectedId);var ids=new HashSet<string>(StringComparer.Ordinal);foreach(var e in doc.Entries){if(e==null||!e.Id.StartsWith("live:",StringComparison.Ordinal)||!Opaque(e.Id.Substring(5))||!ids.Add(e.Id)||Name(e.Name)!=e.Name)throw new ArgumentException();VersionId(e.VersionId);}if(doc.SelectedId.StartsWith("live:",StringComparison.Ordinal)&&!ids.Contains(doc.SelectedId))throw new ArgumentException();}catch(Exception e){throw new IOException("启动配置目录包含无效记录。",e);}return doc;}
  void Persist(Document doc){VersionLibrary.Safe(IndexPath);if(VersionLibrary.Within(IndexPath,game)||VersionLibrary.Within(IndexPath,user))throw new IOException("启动配置目录不能位于游戏或存档目录中。");Directory.CreateDirectory(Path.GetDirectoryName(IndexPath));string temp=IndexPath+".new-"+Guid.NewGuid().ToString("N");try{byte[] bytes=Utf8.GetBytes(Json().Serialize(doc));using(var f=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){f.Write(bytes,0,bytes.Length);f.Flush(true);}VersionLibrary.Safe(IndexPath);if(File.Exists(IndexPath))File.Replace(temp,IndexPath,null);else File.Move(temp,IndexPath);}finally{if(File.Exists(temp))File.Delete(temp);}}
  T Write<T>(Func<Document,T> change){using(var mutex=new Mutex(false,"Local\\OrangeLaunchConfigurationIndex-"+DlssInstaller.Hash(Utf8.GetBytes(IndexPath.ToUpperInvariant())))){bool held=false;try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("此用户的启动配置目录正在修改，请重试。");var doc=Load();T result=change(doc);Persist(doc);return result;}finally{if(held)mutex.ReleaseMutex();}}}
  public string SelectedId {get{return Load().SelectedId;}}
  Catalog Versions(){string selected;var rows=new VersionLibrary(data,game,user).ListReadOnly(out selected);return new Catalog{Rows=rows,SelectedId=selected};}
  SaveEnvironmentRegistration[] Snapshots(Catalog versions){return SaveEnvironments.RegisteredForUsers(data,new[]{user}.Concat(versions.Rows.Select(v=>v.UserPath)));}
  static int Recommendation(){var info=LauncherMaintenance.MemoryInfo();return Math.Max(512,Math.Min(262144,info.RecommendedHeapGiB*1024));}
  // Match the stock Windows branch precedence, extracting only complete Xmx tokens.
  internal static int? SafeHeap(string json){return SafeHeap(json,LaunchProfiles.CurrentWindowsVersion());}
  internal static int? SafeHeap(string json,Version windowsVersion){
   try{
    var doc=Json().Deserialize<Dictionary<string,object>>(json);object args;
    if(doc==null||windowsVersion==null||!doc.TryGetValue("vmArgs",out args)||args is string||!(args is IEnumerable))return null;
    var effective=((IEnumerable)args).Cast<object>().ToList();object rawWindows;
    if(doc.TryGetValue("windows",out rawWindows)){
     var windows=rawWindows as Dictionary<string,object>;if(windows==null)return null;
     string branch=windows.Keys.Where(key=>{Version version;return Version.TryParse(key,out version)&&version<=windowsVersion;}).OrderByDescending(key=>new Version(key)).FirstOrDefault();
     if(branch!=null){var selected=windows[branch] as Dictionary<string,object>;if(selected==null)return null;if(selected.TryGetValue("vmArgs",out args)){if(args is string||!(args is IEnumerable))return null;effective.AddRange(((IEnumerable)args).Cast<object>());}}
    }
    var heap=effective.OfType<string>().Select(token=>Regex.Match(token,@"\A-Xmx([0-9]+)([kKmMgG]?)\z")).LastOrDefault(match=>match.Success);if(heap==null)return null;
    long n;if(!long.TryParse(heap.Groups[1].Value,out n))return null;string unit=heap.Groups[2].Value.ToLowerInvariant();long multiplier=unit=="g"?1073741824L:unit=="m"?1048576L:unit=="k"?1024L:1L;
    if(n>long.MaxValue/multiplier)return null;long bytes=n*multiplier;if(bytes%1048576!=0||bytes/1048576<512||bytes/1048576>262144)return null;return (int)(bytes/1048576);
   }catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}
  }
  int InitialHeap(string sourceGame,bool preserve){if(preserve){string config=Path.Combine(sourceGame,"ProjectZomboid64.json");VersionLibrary.Safe(config);if(File.Exists(config)&&new FileInfo(config).Length<=2097152){int? value=SafeHeap(File.ReadAllText(config,Utf8));if(value.HasValue)return value.Value;}}return Recommendation();}
  LaunchConfigurationResolution Live(string id,string name,string versionId,Catalog versions){var version=versions.Rows.SingleOrDefault(v=>v.Id==versionId);var row=new LaunchConfigurationResolution{Id=id,Name=name,Kind=Builtin(id)?"builtin":"live",VersionId=versionId,Version=version};if(version==null||!version.Installed){row.ErrorCode="missing-version";row.Message="此启动配置的游戏版本不可用："+versionId+"。请重新导入该版本或为此配置选择已安装版本。";return row;}row.Context=LauncherConfiguration.ResolveIndexed(data,version.GamePath,version.UserPath,Builtin(id)?null:id,user,3072);if(!File.Exists(row.Context.PreferencesPath))row.Context=LauncherConfiguration.ResolveIndexed(data,version.GamePath,version.UserPath,Builtin(id)?null:id,user,InitialHeap(version.GamePath,Builtin(id)));row.Context=row.Context.Bind(Builtin(id)?id:"launcher");row.Available=true;return row;}
  LaunchConfigurationResolution Resolve(string id,Document doc,Catalog versions,SaveEnvironmentRegistration[] snapshots){Id(id);if(Builtin(id))return Live(id,id=="launcher"?"启动器配置":id=="steam"?"Steam 配置":"原版配置",id=="steam"?"steam":versions.SelectedId,versions);if(id.StartsWith("live:",StringComparison.Ordinal)){var e=doc.Entries.SingleOrDefault(x=>x.Id==id);if(e==null)throw new IOException("此启动配置未登记："+id);return Live(id,e.Name,e.VersionId,versions);}var record=snapshots.SingleOrDefault(r=>"snapshot:"+r.Id==id);if(record==null)return new LaunchConfigurationResolution{Id=id,Kind="frozen",Name="快照配置",ErrorCode="missing-snapshot",Message="此快照未在当前用户的版本库中登记："+id};if(record.Record==null)return new LaunchConfigurationResolution{Id=id,Name="快照配置",Kind="frozen",ErrorCode="invalid-snapshot",Message="快照 "+record.Id+" 无法使用："+record.Error};var frozen=record.Record;return new LaunchConfigurationResolution{Id=id,Name=frozen.Name,Kind="frozen",VersionId=frozen.GameVersion,Snapshot=frozen,Available=true};}
  public LaunchConfigurationResolution[] List(){var doc=Load();var versions=Versions();var snapshots=Snapshots(versions);return new[]{"launcher","steam","vanilla"}.Concat(doc.Entries.Select(e=>e.Id)).Concat(snapshots.Select(r=>"snapshot:"+r.Id)).Select(id=>Resolve(id,doc,versions,snapshots)).ToArray();}
  public LaunchConfigurationResolution Resolve(string id){Id(id);var doc=Load();var versions=Versions();return Resolve(id,doc,versions,id.StartsWith("snapshot:",StringComparison.Ordinal)?Snapshots(versions):new SaveEnvironmentRegistration[0]);}
  public LaunchConfigurationResolution Selected(){return Resolve(SelectedId);}
  public LaunchConfigurationResolution Select(string id){Id(id);return Write(doc=>{var versions=Versions();var result=Resolve(id,doc,versions,id.StartsWith("snapshot:",StringComparison.Ordinal)?Snapshots(versions):new SaveEnvironmentRegistration[0]);if(result.ErrorCode=="missing-snapshot")throw new IOException(result.Message);doc.SelectedId=id;return result;});}
  static Entry Named(Document doc,string id){Id(id);var entry=doc.Entries.SingleOrDefault(e=>e.Id==id);if(entry==null)throw new IOException("请选择已登记的普通配置。");return entry;}
  public LaunchConfigurationResolution Create(string name,string versionId,string[] ordinaryIds){name=Name(name);VersionId(versionId);if(ordinaryIds==null||ordinaryIds.Length>2000||ordinaryIds.Any(x=>!SaveProfiles.Id(x))||ordinaryIds.Distinct().Count()!=ordinaryIds.Length)throw new ArgumentException("普通模组清单无效。");return Write(doc=>{if(doc.Entries.Count>=128)throw new IOException("普通启动配置最多 128 个。");var e=new Entry{Id="live:"+Guid.NewGuid().ToString("N"),Name=name,VersionId=versionId};var result=Live(e.Id,e.Name,e.VersionId,Versions());if(!result.Available)throw new IOException(result.Message);new LauncherComponents(result.Context,GameVersionProbe.Read(result.Context.Game)??new Version(42,0)).SaveOrdinary(ordinaryIds,CancellationToken.None);doc.Entries.Add(e);return result;});}
  public void Rename(string id,string name){name=Name(name);Write(doc=>{Named(doc,id).Name=name;return true;});}
  public void SetVersion(string id,string versionId){VersionId(versionId);Write(doc=>{var e=Named(doc,id);var version=Versions().Rows.SingleOrDefault(v=>v.Id==versionId);if(version==null||!version.Installed)throw new IOException("所选游戏版本不可用："+versionId);e.VersionId=versionId;return true;});}
  public void Delete(string id){Write(doc=>{var e=Named(doc,id);doc.Entries.Remove(e);if(doc.SelectedId==id)doc.SelectedId="launcher";return true;});}
  public void SaveOrdinary(string id,string[] ids,CancellationToken token,Action<int,string> progress=null){token.ThrowIfCancellationRequested();Id(id);if(Builtin(id)&&id!="launcher")throw new IOException("请在普通启动配置中修改模组清单。");var result=Resolve(id);if(result.Kind=="frozen")throw new IOException("请在快照配置编辑器中修改此快照的模组清单。");if(result.Context==null||!result.Available)throw new IOException(result.Message);new LauncherComponents(result.Context,GameVersionProbe.Read(result.Context.Game)??new Version(42,0),progress).SaveOrdinary(ids,token);}
 }
}
