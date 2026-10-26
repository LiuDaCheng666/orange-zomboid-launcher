using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class LocalGameVersion {
  public string Id,Name,Version,GamePath,UserPath,DataPath;
  public bool Managed,Installed;
 }
 sealed class VersionCopyProgress {public long BytesCopied,TotalBytes;public string File;}
 // Records refer only to user-owned local installations. Game content is never downloaded here.
 sealed class VersionLibrary {
  sealed class Document {public int Schema=1;public string SelectedId="steam";public List<LocalGameVersion> Versions=new List<LocalGameVersion>();}
  sealed class Entry {public string Relative,Hash;public long Length,Write;public bool Directory;}
  readonly string data,game,user,state;
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1048576};}
  internal static string Full(string path){if(string.IsNullOrWhiteSpace(path)||path.Any(char.IsControl))throw new IOException("目录无效。");string full=Path.GetFullPath(path);return string.Equals(full,Path.GetPathRoot(full),StringComparison.OrdinalIgnoreCase)?full:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
  internal static bool Same(string a,string b){return string.Equals(Full(a),Full(b),StringComparison.OrdinalIgnoreCase);}
  internal static bool Within(string child,string parent){return Same(child,parent)||Full(child).StartsWith(Full(parent).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  internal static void Safe(string path){LauncherMaintenance.NoReparse(Full(path));}
  public VersionLibrary(string dataRoot,string legacyGame,string legacyUser){data=Full(dataRoot);game=Full(legacyGame);user=Full(legacyUser);state=Path.Combine(data,"version-library.json");Safe(data);}
  T Locked<T>(Func<T> action){string name="Local\\OrangeVersionLibrary-"+DlssInstaller.Hash(Encoding.UTF8.GetBytes(data.ToUpperInvariant()));using(var mutex=new Mutex(false,name)){bool held=false;try{try{held=mutex.WaitOne(5000);}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("另一个版本管理任务仍在进行。");return action();}finally{if(held)mutex.ReleaseMutex();}}}
  string UserPath(string id){return Path.Combine(data,"version-users",id);}
  static string Detect(string path){var version=GameVersionProbe.Read(path);return version==null?"未知":version.ToString();}
  LocalGameVersion Legacy(){return new LocalGameVersion{Id="steam",Name="当前 Steam 安装",Version=Detect(game),GamePath=game,UserPath=user,DataPath=data,Managed=false,Installed=GameDiscovery.IsGameFolder(game)};}
  static LocalGameVersion Clone(LocalGameVersion row){return new LocalGameVersion{Id=row.Id,Name=row.Name,Version=row.Version,GamePath=row.GamePath,UserPath=row.UserPath,DataPath=row.DataPath,Managed=row.Managed,Installed=row.Installed};}
  Document Load(){
   Safe(state);if(!File.Exists(state))return new Document();if(new FileInfo(state).Length>1048576)throw new IOException("版本库记录过大，请检查备份。");Document doc;
   try{doc=Json().Deserialize<Document>(File.ReadAllText(state,Encoding.UTF8));}catch(Exception x){throw new IOException("版本库记录损坏，已保留原文件。",x);}
   if(doc==null||doc.Schema!=1||doc.Versions==null||doc.Versions.Count>128)throw new IOException("版本库格式无效。");
   var ids=new HashSet<string>(StringComparer.Ordinal);var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var row in doc.Versions){if(row==null||!Regex.IsMatch(row.Id??"",@"^[a-f0-9]{32}$")||!ids.Add(row.Id)||!row.Managed||!Same(row.UserPath,UserPath(row.Id))||!Same(row.DataPath,data)||!paths.Add(Full(row.GamePath))||string.IsNullOrWhiteSpace(row.Name)||row.Name.Length>80||row.Name.Any(char.IsControl)||Within(row.GamePath,data)||Within(data,row.GamePath)||Within(row.GamePath,user)||Within(user,row.GamePath))throw new IOException("版本记录包含无效或交叉的目录。");Safe(row.GamePath);Safe(row.UserPath);}
   if(doc.SelectedId!="steam"&&!ids.Contains(doc.SelectedId??""))throw new IOException("已选版本不存在。");return doc;
  }
  void Save(Document doc){Safe(data);Directory.CreateDirectory(data);Safe(state);string temporary=state+".new-"+Guid.NewGuid().ToString("N");try{byte[] bytes=Encoding.UTF8.GetBytes(Json().Serialize(doc));using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}Safe(state);if(File.Exists(state))File.Replace(temporary,state,null);else File.Move(temporary,state);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  LocalGameVersion Refresh(LocalGameVersion row){var copy=Clone(row);copy.Installed=GameDiscovery.IsGameFolder(copy.GamePath);copy.Version=Detect(copy.GamePath);return copy;}
  public IList<LocalGameVersion> List(){return Locked(()=>{var doc=Load();return (IList<LocalGameVersion>)new[]{Legacy()}.Concat(doc.Versions.Select(Refresh)).ToList();});}
  public LocalGameVersion Selected(){return Locked(()=>{var doc=Load();return doc.SelectedId=="steam"?Legacy():Refresh(doc.Versions.Single(row=>row.Id==doc.SelectedId));});}
  public LocalGameVersion Select(string id){return Locked(()=>{var doc=Load();var row=id=="steam"?Legacy():doc.Versions.FirstOrDefault(item=>item.Id==id);if(row==null)throw new IOException("所选版本不在本地版本库。");Safe(row.GamePath);Safe(row.UserPath);if(!GameDiscovery.IsGameFolder(row.GamePath))throw new IOException("此版本的游戏文件已移动或不完整，请重新导入。");doc.SelectedId=id;Save(doc);return Refresh(row);});}
  void ValidateGame(string path){Safe(path);if(!GameDiscovery.IsGameFolder(path))throw new IOException("请选择包含 ProjectZomboid64.exe 和启动配置的游戏目录。");foreach(string name in new[]{"ProjectZomboid64.exe","ProjectZomboid64.json"})Safe(Path.Combine(path,name));if(Within(path,data)||Within(data,path)||Within(path,user)||Within(user,path))throw new IOException("游戏目录不能与启动器数据或原存档目录交叉。");}
  LocalGameVersion NewRow(string path,string name){name=(name??"").Trim();if(name.Length==0)name="Project Zomboid "+Detect(path);if(name.Length>80||name.Any(char.IsControl))throw new IOException("版本名称最多 80 个字符。");string id=Guid.NewGuid().ToString("N");return new LocalGameVersion{Id=id,Name=name,Version=Detect(path),GamePath=path,UserPath=UserPath(id),DataPath=data,Managed=true,Installed=true};}
  LocalGameVersion Add(Document doc,LocalGameVersion row){if(doc.Versions.Count>=128)throw new IOException("本地版本已达到上限。");Safe(row.UserPath);if(Directory.Exists(row.UserPath)||File.Exists(row.UserPath))throw new IOException("独立用户目录已存在，未覆盖。");Directory.CreateDirectory(row.UserPath);try{doc.Versions.Add(row);Save(doc);return Clone(row);}catch{if(!Directory.EnumerateFileSystemEntries(row.UserPath).Any())Directory.Delete(row.UserPath);throw;}}
  public LocalGameVersion Import(string gamePath,string name){return Locked(()=>{string path=Full(gamePath);ValidateGame(path);if(Same(path,game))throw new IOException("当前 Steam 安装会随分支更新，请使用“保存为独立版本”。");var doc=Load();if(doc.Versions.Any(row=>Same(row.GamePath,path)))throw new IOException("此游戏目录已在版本库中。");return Add(doc,NewRow(path,name));});}
  static string HashFile(string path,CancellationToken cancel){Safe(path);using(var sha=SHA256.Create())using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){byte[] buffer=new byte[1048576];int count;while((count=file.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,null,0);}sha.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(sha.Hash).Replace("-","");}}
  static List<Entry> Snapshot(string root,CancellationToken cancel){var entries=new List<Entry>();var pending=new Stack<string>();pending.Push(root);while(pending.Count>0){string directory=pending.Pop();Safe(directory);foreach(string path in Directory.EnumerateFileSystemEntries(directory)){cancel.ThrowIfCancellationRequested();Safe(path);string relative=path.Substring(root.Length+1);bool isDirectory=(File.GetAttributes(path)&FileAttributes.Directory)!=0;var entry=new Entry{Relative=relative,Directory=isDirectory};if(isDirectory)pending.Push(path);else{var info=new FileInfo(path);entry.Length=info.Length;entry.Write=info.LastWriteTimeUtc.Ticks;entry.Hash=HashFile(path,cancel);}entries.Add(entry);}}return entries.OrderBy(e=>e.Relative,StringComparer.OrdinalIgnoreCase).ToList();}
  static bool Matches(List<Entry> before,List<Entry> after){return before.Count==after.Count&&before.Zip(after,(a,b)=>a.Relative==b.Relative&&a.Directory==b.Directory&&a.Length==b.Length&&a.Write==b.Write&&a.Hash==b.Hash).All(equal=>equal);}
  // Clean only the exact staging tree owned by this call, never recursively follow a link.
  static void RemoveOwned(string path){Safe(path);if(!Directory.Exists(path))return;foreach(string entry in Directory.EnumerateFileSystemEntries(path)){Safe(entry);if(Directory.Exists(entry))RemoveOwned(entry);else File.Delete(entry);}Directory.Delete(path);}
  public LocalGameVersion Preserve(string sourceGame,string destination,string name,CancellationToken cancel,Action<VersionCopyProgress> progress=null){return PreserveChecked(sourceGame,destination,name,cancel,progress,null);}
  internal LocalGameVersion PreserveChecked(string sourceGame,string destination,string name,CancellationToken cancel,Action<VersionCopyProgress> progress,Action validateBeforePublish){return Locked(()=>{
   cancel.ThrowIfCancellationRequested();string source=Full(sourceGame),target=Full(destination);ValidateGame(source);Safe(target);if(Within(target,source)||Within(source,target)||Within(target,data)||Within(data,target)||Within(target,user)||Within(user,target))throw new IOException("保留目录不能与原游戏、存档或启动器数据目录交叉。");if(Directory.Exists(target)||File.Exists(target))throw new IOException("目标目录已存在，请选择一个新的版本目录。");
   var doc=Load();if(doc.Versions.Any(item=>Within(target,item.GamePath)||Within(item.GamePath,target)))throw new IOException("目标目录与已有版本交叉。");var row=NewRow(target,name);string parent=Path.GetDirectoryName(target),staging=target+".partial-"+Guid.NewGuid().ToString("N");bool published=false;
   try{
    var entries=Snapshot(source,cancel);long total=entries.Where(entry=>!entry.Directory).Sum(entry=>entry.Length),done=0;Safe(parent);Directory.CreateDirectory(parent);Directory.CreateDirectory(staging);
    foreach(var entry in entries){cancel.ThrowIfCancellationRequested();string input=Path.Combine(source,entry.Relative),output=Path.Combine(staging,entry.Relative);Safe(input);Safe(output);if(entry.Directory){Directory.CreateDirectory(output);continue;}Directory.CreateDirectory(Path.GetDirectoryName(output));using(var from=new FileStream(input,FileMode.Open,FileAccess.Read,FileShare.Read))using(var to=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] buffer=new byte[1048576];int count;while((count=from.Read(buffer,0,buffer.Length))>0){cancel.ThrowIfCancellationRequested();to.Write(buffer,0,count);done+=count;if(progress!=null)progress(new VersionCopyProgress{BytesCopied=done,TotalBytes=total,File=entry.Relative});}to.Flush(true);}if(HashFile(output,cancel)!=entry.Hash)throw new IOException("复制校验失败，未加入版本库。");}
    cancel.ThrowIfCancellationRequested();if(!Matches(entries,Snapshot(source,cancel)))throw new IOException("复制期间原游戏文件发生变化，请等待 Steam 下载和游戏退出后重试。");ValidateGame(staging);if(validateBeforePublish!=null)validateBeforePublish();cancel.ThrowIfCancellationRequested();Safe(target);if(Directory.Exists(target)||File.Exists(target))throw new IOException("复制期间目标目录已被占用。");Directory.Move(staging,target);published=true;row.Version=Detect(target);return Add(doc,row);
   }catch{if(Directory.Exists(staging))RemoveOwned(staging);if(published&&Directory.Exists(target))RemoveOwned(target);throw;}
  });}
  // A worker may use only the legacy user identity or an exact registered game/user pair.
  internal static bool OwnsUser(string dataRoot,string gamePath,string userPath){try{string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Zomboid");if(Same(legacy,userPath))return true;var library=new VersionLibrary(dataRoot,gamePath,legacy);return library.List().Any(row=>row.Managed&&Same(row.GamePath,gamePath)&&Same(row.UserPath,userPath));}catch{return false;}}
 }
}
