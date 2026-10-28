using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;
using Directory=PZ3D.SnapshotFileSystem.Directories;using File=PZ3D.SnapshotFileSystem.Files;using FileStream=PZ3D.SnapshotFileSystem.Stream;
namespace PZ3D {
 sealed partial class SaveEnvironments {
  internal sealed class RevisionSource {public string id,name,token,sourceKind,workshopId;public long? bytes,files;public string coverToken;public string[] requires;public bool requiresBuddy;internal string RootIdentity;internal SaveProfiles.Candidate Candidate;internal SaveEnvironmentFile[] Inventory;}
  internal static RevisionSource[] RevisionSources(Engine frozen,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){
   var row=FindOwned(frozen.Data,frozen.Game,frozen.User);if(row==null)throw new IOException("请选择已登记的快照。");
   // This engine is constructed from registered provenance, never browser paths. It
   // deliberately does not inherit frozen discovery, which cannot see new sources.
   var current=new Engine(row.SourceGame,row.UserPath,frozen.Data,frozen.TestMode,frozen.ReadSteamOptions,frozen.ProcessInventory){ModCatalogVersion=GameVersionProbe.Read(row.GamePath)};
   PreparationProgress(progress,cancellation,"sources","正在读取当前模组来源");var warnings=new List<string>();var budget=new LaunchReadBudget(67108864,100000);budget.Checkpoint=(path,size)=>cancellation.ThrowIfCancellationRequested();var catalog=SaveProfiles.SnapshotEntries(current,new string[0],warnings,budget);if(budget.Incomplete)throw new IOException("当前模组来源尚未读取完整，请重试。");
   var result=new List<RevisionSource>();
   foreach(var candidate in catalog.Candidates){cancellation.ThrowIfCancellationRequested();var entry=SaveProfiles.DescribeCandidate(candidate);string identity=Identity(candidate.Root);result.Add(new RevisionSource{coverToken=entry.coverToken,requires=entry.requires,requiresBuddy=entry.requiresBuddy,id=candidate.Id,name=entry.name,sourceKind=candidate.Source,workshopId=candidate.Workshop,bytes=null,files=null,Candidate=candidate,RootIdentity=identity,token=Hash(Utf8.GetBytes(Json().Serialize(new{root=Full(candidate.Root),identity,id=candidate.Id,layout=candidate.Layout,commonRoot=candidate.CommonRoot,common=candidate.Common,active=candidate.Active})))});PreparationProgress(progress,cancellation,"sources","正在读取可添加的模组",result.Count,0,candidate.Id);}
   return result.ToArray();
  }
  internal static SaveEnvironmentModRoot CopyRevisionSource(SaveEnvironmentRecord owner,RevisionSource source,string[] ids,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){
   cancellation.ThrowIfCancellationRequested();var c=source.Candidate;if(Identity(c.Root)!=source.RootIdentity)throw new IOException("所选来源目录已改变。");if(!EqualBytes(Read(System.IO.Path.Combine(c.Layout,"mod.info"),262144),c.Active)||c.CommonRoot!=null&&!EqualBytes(Read(System.IO.Path.Combine(c.CommonRoot,"mod.info"),262144),c.Common))throw new IOException(source.name+" ("+source.id+")：来源信息已变化，请重新选择。");long scanned=0,scannedBytes=0;source.Inventory=Scan(c.Root,true,cancellation,null,f=>{if(!f.Directory)scanned++;PreparationProgress(progress,cancellation,"scan","正在核对 "+source.name,scanned,scannedBytes,f.Path);},(n,path)=>{scannedBytes+=n;PreparationProgress(progress,cancellation,"scan","正在核对 "+source.name,scanned,scannedBytes,path);});source.files=source.Inventory.LongCount(f=>!f.Directory);source.bytes=Size(source.Inventory);string slot=Child(owner.RootPath,"mods/edit-"+Guid.NewGuid().ToString("N")),target=System.IO.Path.Combine(slot,"mods",System.IO.Path.GetFileName(c.Root));Directory.CreateDirectory(slot);RevisionWrite(System.IO.Path.Combine(slot,".owner"),Utf8.GetBytes(owner.Id));var stage=new RevisionStage{Id=owner.Id,RootIdentity=owner.RootIdentity,SlotIdentity=Identity(slot),SourceRoot=c.Root,SourceIdentity=source.RootIdentity,Target=target,Files=source.Inventory};RevisionWrite(System.IO.Path.Combine(slot,".stage.json"),Utf8.GetBytes(Json().Serialize(stage)));
   var copy=source.Inventory.Select(f=>ProjectedFile(f,f.Path)).ToArray();try{var transfer=new SaveEnvironmentTransferProgress("copy","正在复制 "+source.name,source.files.Value,source.bytes.Value,cancellation,progress);Copy(c.Root,target,copy,cancellation,transfer.Bytes,transfer.File);if(Identity(c.Root)!=source.RootIdentity||!Match(source.Inventory,Scan(c.Root,true,cancellation),true)||!SameContent(source.Inventory,Scan(target,true,cancellation)))throw new IOException("来源在复制期间变化："+source.name+" ("+source.id+")");transfer.Finish();return new SaveEnvironmentModRoot{SourceRoot=c.Root,CopiedRoot=target,Layout=System.IO.Path.Combine(target,c.Layout.Substring(c.Root.Length).TrimStart('\\','/')),CommonRoot=c.CommonRoot==null?null:System.IO.Path.Combine(target,c.CommonRoot.Substring(c.Root.Length).TrimStart('\\','/')),WorkshopId=c.Workshop,ModIds=ids,SourceFiles=source.Inventory};}catch(Exception error){try{RemoveRevisionStage(owner,slot,copy);}catch(IOException cleanup){error.Data["cleanupWarning"]=cleanup.Message;}catch(UnauthorizedAccessException cleanup){error.Data["cleanupWarning"]=cleanup.Message;}throw;}
  }
  sealed class RevisionStage {public string Id,RootIdentity,SlotIdentity,SourceRoot,SourceIdentity,Target;public SaveEnvironmentFile[] Files;}
  static void RemoveRevisionStage(SaveEnvironmentRecord owner,string slot,SaveEnvironmentFile[] written=null){
   if(!Directory.Exists(slot))return;Safe(slot);if(!Within(slot,Child(owner.RootPath,"mods"))||!System.IO.Path.GetFileName(slot).StartsWith("edit-",StringComparison.Ordinal))throw new IOException("新增副本的清理位置无效。");
   byte[] bytes=Read(System.IO.Path.Combine(slot,".stage.json"),MaxManifest);RevisionStage stage;try{stage=bytes==null?null:Json().Deserialize<RevisionStage>(Utf8.GetString(bytes));}catch(Exception error){throw new IOException("新增副本的复制记录无法读取，已保留。",error);}
   if(stage==null||stage.Id!=owner.Id||stage.RootIdentity!=owner.RootIdentity||Identity(slot)!=stage.SlotIdentity||!EqualBytes(Read(System.IO.Path.Combine(slot,".owner"),64),Utf8.GetBytes(owner.Id))||stage.Files==null||!Within(stage.Target,slot))throw new IOException("新增副本目录已改变，未自动清理。");
   string prefix=stage.Target.Substring(slot.Length+1).Replace('\\','/');var expected=(written??stage.Files).Select(f=>ProjectedFile(f,prefix+"/"+f.Path)).ToList();expected.Add(new SaveEnvironmentFile{Path=".owner",Bytes=Utf8.GetBytes(owner.Id).Length,Hash=Hash(Utf8.GetBytes(owner.Id))});expected.Add(new SaveEnvironmentFile{Path=".stage.json",Bytes=bytes.Length,Hash=Hash(bytes)});string parent=prefix;while(parent.Length>0){expected.Add(new SaveEnvironmentFile{Path=parent,Directory=true});int separator=parent.LastIndexOf('/');parent=separator<0?"":parent.Substring(0,separator);}
   // Incomplete copies are removable only when every existing byte still matches
   // the recorded source. Unknown or partially written bytes remain for recovery.
   var actual=Scan(slot,true,CancellationToken.None);if(actual.Any(file=>!expected.Any(e=>e.Path==file.Path&&e.Directory==file.Directory&&(file.Directory||e.Bytes==file.Bytes&&e.Hash==file.Hash||PartialRevisionFile(stage,prefix,e,file)))))throw new IOException("新增副本中存在已改变的文件，已保留，未自动清理。");foreach(string child in Directory.EnumerateFileSystemEntries(slot).Where(path=>System.IO.Path.GetFileName(path)!=".owner"&&System.IO.Path.GetFileName(path)!=".stage.json")){if(Directory.Exists(child))DeleteTree(child);else File.Delete(child);}File.Delete(System.IO.Path.Combine(slot,".stage.json"));File.Delete(System.IO.Path.Combine(slot,".owner"));Directory.Delete(slot);
  }
  static bool PartialRevisionFile(RevisionStage stage,string prefix,SaveEnvironmentFile expected,SaveEnvironmentFile actual){
   if(expected.Directory||actual.Directory||actual.Bytes>=expected.Bytes||!actual.Path.StartsWith(prefix+"/",StringComparison.Ordinal)||string.IsNullOrEmpty(stage.SourceRoot)||!Directory.Exists(stage.SourceRoot)||Identity(stage.SourceRoot)!=stage.SourceIdentity)return false;
   string source=Child(stage.SourceRoot,actual.Path.Substring(prefix.Length+1));if(!File.Exists(source)||FileHash(source,CancellationToken.None)!=expected.Hash)return false;
   using(var stream=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=System.Security.Cryptography.SHA256.Create()){byte[] buffer=new byte[1048576];long left=actual.Bytes;while(left>0){int count=stream.Read(buffer,0,(int)Math.Min(buffer.Length,left));if(count==0)return false;hash.TransformBlock(buffer,0,count,null,0);left-=count;}hash.TransformFinalBlock(new byte[0],0,0);return BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant()==actual.Hash;}
  }
  internal static void RemoveUncommittedAdditions(Engine engine,SaveEnvironmentRecord old,IEnumerable<SaveEnvironmentModRoot> additions){
   SaveEnvironmentRecord current;try{current=FindOwned(engine.Data,engine.Game,engine.User);}catch{return;}if(current==null)return;
   foreach(var root in additions){if(current.ModRoots.Any(r=>Same(r.CopiedRoot,root.CopiedRoot)))continue;try{RemoveRevisionAddition(old,root);}catch(IOException){}catch(UnauthorizedAccessException){}}
  }
  internal static void RemoveRevisionAddition(SaveEnvironmentRecord owner,SaveEnvironmentModRoot root){string slot=System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(root.CopiedRoot));RemoveRevisionStage(owner,slot);}
  internal static string[] RecoverRevisionAdditions(SaveEnvironmentRecord owner){var warnings=new List<string>();string mods=Child(owner.RootPath,"mods");Safe(mods);if(!Directory.Exists(mods))return warnings.ToArray();foreach(string slot in Directory.EnumerateFileSystemEntries(mods)){if(!Directory.Exists(slot)||!System.IO.Path.GetFileName(slot).StartsWith("edit-",StringComparison.Ordinal)||owner.ModRoots.Any(r=>Within(r.CopiedRoot,slot)))continue;try{RemoveRevisionStage(owner,slot);}catch(IOException error){warnings.Add(error.Message);}catch(UnauthorizedAccessException error){warnings.Add(error.Message);}}return warnings.ToArray();}
  static SaveEnvironmentUpdate CheckRevisedSources(SaveEnvironmentRecord record,CancellationToken token){var result=new SaveEnvironmentUpdate{snapshotId=record.Id};try{result.gameChanged=!Match(record.GameSourceFiles.Where(f=>!GameSourceExcluded(f.Path)).ToArray(),Scan(record.SourceGame,true,token,GameSourceExcluded),true);foreach(var root in record.ModRoots){token.ThrowIfCancellationRequested();if(!Directory.Exists(root.SourceRoot))throw new IOException("当前来源无法读取："+string.Join(", ",root.ModIds)+" ("+root.SourceRoot+")");if(!Match(root.SourceFiles,Scan(root.SourceRoot,true,token),true))result.modsChanged=true;}result.available=true;result.changed=result.gameChanged||result.modsChanged;result.message=result.changed?"当前来源已变化；此快照继续使用已复制的版本。":"当前来源与此快照的复制内容一致。";}catch(OperationCanceledException){throw;}catch(IOException error){result.message="当前来源无法完整核对；此快照继续保留。 "+error.Message;}return result;}
  sealed class RevisionOutput {public string Path,BeforeHash,AfterHash;public byte[] Before,After;}
  sealed class RevisionRoot {public string Path,Identity;public SaveEnvironmentFile[] Files;}
  sealed class RevisionCleanup {public string Id,RootIdentity;public RevisionRoot[] Roots;}
  sealed class RevisionJournal {public string Id,RootIdentity,BeforeHash,AfterHash;public RevisionOutput[] Outputs;public RevisionRoot[] Added,Retired;}
  const string RevisionJournalPath="runtime/revision.json";
  static void RevisionWrite(string path,byte[] bytes){Safe(path);if(bytes==null){if(File.Exists(path))File.Delete(path);return;}Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));string temporary=path+".revision-"+Guid.NewGuid().ToString("N");try{using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  static string OptionalHash(byte[] bytes){return bytes==null?null:Hash(bytes);}
  // Called only inside the short catalog boundary. A terminated writer cannot leave
  // readers with a new manifest and old pointer (or the reverse).
  string[] RecoverRevision(Pointer pointer){return RecoverRevision(pointer,null,null);}
  string[] RecoverRevision(Pointer pointer,RevisionJournal trusted,byte[] expectedBytes){
   string path=Child(pointer.RootPath,RevisionJournalPath);byte[] bytes=Read(path,MaxManifest);if(bytes==null){if(trusted!=null)throw new IOException("快照配置恢复记录在保存后变化，已保留现场。");return new string[0];}
   // A successful writer already owns the exact parsed journal. Reuse it only
   // while disk bytes still match; crash/uncertain recovery keeps the disk parser.
   RevisionJournal journal=trusted;
   if(trusted!=null){if(!EqualBytes(bytes,expectedBytes))throw new IOException("快照配置恢复记录在保存后变化，已保留现场。");}
   else try{journal=Json().Deserialize<RevisionJournal>(Utf8.GetString(bytes));}catch(Exception error){throw new IOException("快照配置恢复记录无法读取，已保留现场。",error);}
   if(journal==null||journal.Id!=pointer.Id||journal.RootIdentity!=Identity(pointer.RootPath)||!EqualBytes(Read(Child(pointer.RootPath,".owner"),64),Utf8.GetBytes(pointer.Id))||!Digest(journal.BeforeHash)||!Digest(journal.AfterHash)||journal.Outputs==null||journal.Outputs.Length>32||journal.Outputs.Select(o=>o.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=journal.Outputs.Length)throw new IOException("快照配置恢复身份不一致，已保留现场。");
   var manifestOutput=journal.Outputs.SingleOrDefault(o=>o.Path==ManifestName);if(manifestOutput==null||manifestOutput.BeforeHash!=journal.BeforeHash||manifestOutput.AfterHash!=journal.AfterHash||Hash(manifestOutput.Before)!=journal.BeforeHash||Hash(manifestOutput.After)!=journal.AfterHash)throw new IOException("快照恢复记录与配置版本不一致，已保留。");var beforeManifest=DecodeManifest(manifestOutput.Before);var afterManifest=DecodeManifest(manifestOutput.After);foreach(var manifest in new[]{beforeManifest,afterManifest})if(manifest.Record.Id!=pointer.Id||manifest.Record.RootIdentity!=journal.RootIdentity||!Same(manifest.Record.RootPath,pointer.RootPath))throw new IOException("快照恢复记录不属于此副本，已保留。");foreach(var output in journal.Outputs.Where(o=>o.Path!=ManifestName&&!Mutable(o.Path))){var beforeFile=beforeManifest.Files.SingleOrDefault(f=>f.Path==output.Path&&!f.Directory);var afterFile=afterManifest.Files.SingleOrDefault(f=>f.Path==output.Path&&!f.Directory);if((beforeFile==null?null:beforeFile.Hash)!=output.BeforeHash||(afterFile==null?null:afterFile.Hash)!=output.AfterHash)throw new IOException("快照恢复文件与登记内容不一致，已保留。");}
   bool committed=pointer.ManifestHash==journal.AfterHash;if(!committed&&pointer.ManifestHash!=journal.BeforeHash)throw new IOException("快照配置已存在另一版本，未覆盖恢复。");
   foreach(var output in journal.Outputs){Relative(output.Path);if(!RevisionOutputPath(output.Path)||OptionalHash(output.Before)!=output.BeforeHash||OptionalHash(output.After)!=output.AfterHash)throw new IOException("快照配置恢复文件无效。");if(committed&&output.Path!=ManifestName&&Mutable(output.Path))continue;byte[] current=Read(Child(pointer.RootPath,output.Path),MaxManifest);if(!EqualBytes(current,output.Before)&&!EqualBytes(current,output.After))throw new IOException("快照文件已由其他程序修改，已保留："+output.Path);if(committed&&!EqualBytes(current,output.After))throw new IOException("已提交的快照配置文件缺失或改变："+output.Path);}
   if(!committed)foreach(var output in journal.Outputs)if(!EqualBytes(Read(Child(pointer.RootPath,output.Path),MaxManifest),output.Before))RevisionWrite(Child(pointer.RootPath,output.Path),output.Before);
   var roots=(committed?journal.Retired:journal.Added)??new RevisionRoot[0];if(roots.Length>0){string cleanupPath=Child(pointer.RootPath,"runtime/revision-cleanup/"+journal.BeforeHash+"-"+journal.AfterHash+".json");byte[] cleanup=Utf8.GetBytes(Json().Serialize(new RevisionCleanup{Id=pointer.Id,RootIdentity=journal.RootIdentity,Roots=roots}));var existing=Read(cleanupPath,MaxManifest);if(existing!=null&&!EqualBytes(existing,cleanup))throw new IOException("快照清理记录已改变，已保留。");if(existing==null)RevisionWrite(cleanupPath,cleanup);}File.Delete(path);return roots.Length==0?new string[0]:new[]{"配置已恢复，旧副本等待清理。"};
  }
  static bool HasRevisionCleanup(string root){string folder=Child(root,"runtime/revision-cleanup");return Directory.Exists(folder)&&Directory.EnumerateFileSystemEntries(folder).Any();}
  static bool MatchesRemainingFiles(SaveEnvironmentFile[] expected,SaveEnvironmentFile[] remaining){
   var recorded=expected.ToDictionary(file=>file.Path,StringComparer.OrdinalIgnoreCase);
   foreach(var file in remaining){
    SaveEnvironmentFile original;
    if(!recorded.TryGetValue(file.Path,out original)||file.Directory!=original.Directory)return false;
    if(!file.Directory&&(file.Bytes!=original.Bytes||file.Hash!=original.Hash))return false;
   }
   return true;
  }
  internal static string[] RetryRevisionCleanup(SaveEnvironmentRecord owner){
   var warnings=new List<string>();
   string folder=Child(owner.RootPath,"runtime/revision-cleanup");
   if(Directory.Exists(folder))foreach(string path in Directory.EnumerateFileSystemEntries(folder))try{
    Safe(path);
    var cleanup=Json().Deserialize<RevisionCleanup>(Utf8.GetString(Read(path,MaxManifest)));
    if(cleanup==null||cleanup.Id!=owner.Id||cleanup.RootIdentity!=owner.RootIdentity||Identity(owner.RootPath)!=owner.RootIdentity||cleanup.Roots==null)
     throw new IOException("快照清理记录身份不一致，已保留。");
    foreach(var root in cleanup.Roots){
     if(root==null||!Within(root.Path,Child(owner.RootPath,"mods"))||Same(root.Path,Child(owner.RootPath,"mods"))||root.Files==null||owner.ModRoots.Any(r=>Within(r.CopiedRoot,root.Path)||Within(root.Path,r.CopiedRoot)))
      throw new IOException("旧副本仍被当前配置引用，未删除。");
     if(!Directory.Exists(root.Path))continue;
     // A previous attempt may have removed some entries before encountering a
     // lock. Every survivor must still match; missing entries need no deletion.
     if(Identity(root.Path)!=root.Identity||!MatchesRemainingFiles(root.Files,Scan(root.Path,true,CancellationToken.None)))
      throw new IOException("旧副本文件已改变，未删除。");
     DeleteTree(root.Path);
    }
    File.Delete(path);
   }catch(IOException error){warnings.Add(error.Message);}
    catch(UnauthorizedAccessException error){warnings.Add(error.Message);}
    catch(ArgumentException){warnings.Add("快照清理记录无法读取，已保留。");}
   warnings.AddRange(RecoverRevisionAdditions(owner));
   return warnings.ToArray();
  }
  static bool RevisionOutputPath(string path){return path=="game/ZombieBuddy.jar"||path=="game/OrangeLauncherRuntime/environment/OrangeEnvironment.jar"||path==ManifestName||path=="game/ProjectZomboid64.json"||path=="game/OrangeLauncherRuntime/environment/environment.properties"||path=="runtime/mods/default.txt"||path=="runtime/mods/loaded.txt"||path=="runtime/home/.zombie_buddy/config.json"||path=="runtime/home/.zombie_buddy/mod_approvals.json";}
  internal static SaveEnvironmentRecord PublishRevision(Engine engine,SaveEnvironmentRecord previous,SaveEnvironmentRecord revised,Dictionary<string,byte[]> outputs,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){
   var reader=new SaveEnvironments(engine.Data);if(previous.Id!=revised.Id||previous.RootIdentity!=revised.RootIdentity||!Same(previous.RootPath,revised.RootPath))throw new IOException("快照修改目标不一致。");
   var removed=previous.ModRoots.Where(m=>!revised.ModRoots.Any(r=>Same(r.CopiedRoot,m.CopiedRoot))).ToArray();var files=previous.Files.Where(f=>!removed.Any(m=>f.Path==MappingPrefix(previous,m)||f.Path.StartsWith(MappingPrefix(previous,m)+"/",StringComparison.OrdinalIgnoreCase))).Select(f=>ProjectedFile(f,f.Path)).ToList();foreach(var mapping in revised.ModRoots.Where(m=>!previous.ModRoots.Any(p=>Same(p.CopiedRoot,m.CopiedRoot))))foreach(var file in mapping.SourceFiles)files.Add(ProjectedFile(file,MappingPrefix(revised,mapping)+"/"+file.Path));var writes=new List<RevisionOutput>();
   foreach(var pair in outputs){if(!RevisionOutputPath(pair.Key)||pair.Key==ManifestName)throw new IOException("快照修改输出不受支持。");byte[] before=Read(Child(previous.RootPath,pair.Key),MaxManifest);if(EqualBytes(before,pair.Value))continue;writes.Add(new RevisionOutput{Path=pair.Key,Before=before,After=pair.Value,BeforeHash=OptionalHash(before),AfterHash=OptionalHash(pair.Value)});if(!Mutable(pair.Key)){files.RemoveAll(f=>f.Path.Equals(pair.Key,StringComparison.OrdinalIgnoreCase));if(pair.Value!=null)files.Add(new SaveEnvironmentFile{Path=pair.Key,Bytes=pair.Value.LongLength,Hash=Hash(pair.Value),WriteTicks=DateTime.UtcNow.Ticks});}}
   revised.Files=files.OrderBy(f=>f.Path,StringComparer.OrdinalIgnoreCase).ToArray();byte[] manifest=EncodeManifest(revised,revised.Files);revised.ManifestHash=Hash(manifest);writes.Add(new RevisionOutput{Path=ManifestName,Before=Read(previous.ManifestPath,MaxManifest),After=manifest,BeforeHash=previous.ManifestHash,AfterHash=revised.ManifestHash});
   var journal=new RevisionJournal{Id=previous.Id,RootIdentity=previous.RootIdentity,BeforeHash=previous.ManifestHash,AfterHash=revised.ManifestHash,Outputs=writes.ToArray(),Added=revised.ModRoots.Where(m=>!previous.ModRoots.Any(p=>Same(p.CopiedRoot,m.CopiedRoot))).Select(m=>new RevisionRoot{Path=m.CopiedRoot,Identity=Identity(m.CopiedRoot),Files=m.SourceFiles}).ToArray(),Retired=removed.Select(m=>new RevisionRoot{Path=m.CopiedRoot,Identity=Identity(m.CopiedRoot),Files=m.SourceFiles}).ToArray()};byte[] journalBytes=Utf8.GetBytes(Json().Serialize(journal));if(journalBytes.Length>MaxManifest)throw new IOException("本次快照配置恢复记录过大，未开始保存。");
   cancellation.ThrowIfCancellationRequested();if(progress!=null)progress(new SaveEnvironmentProgress{stage="publish",text="正在保存快照配置",totalFiles=writes.Count});cancellation.ThrowIfCancellationRequested();
   var published=reader.Locked(()=>{
    var catalog=reader.Load();var pointer=catalog.Document.Records.SingleOrDefault(p=>p.Id==previous.Id);if(pointer==null||pointer.ManifestHash!=previous.ManifestHash)throw new IOException("此快照配置已变化，请重新读取后保存。");reader.ReadRecord(pointer,catalog.Stamp);
    foreach(var output in writes)if(!EqualBytes(Read(Child(previous.RootPath,output.Path),MaxManifest),output.Before))throw new IOException("快照文件在保存前变化："+output.Path);
    cancellation.ThrowIfCancellationRequested();string journalPath=Child(previous.RootPath,RevisionJournalPath);RevisionWrite(journalPath,journalBytes);
    try{foreach(var output in writes)RevisionWrite(Child(previous.RootPath,output.Path),output.After);pointer.ManifestHash=revised.ManifestHash;reader.Persist(catalog);}catch{var actual=reader.Load().Document.Records.Single(p=>p.Id==previous.Id);reader.RecoverRevision(actual);throw;}
    // The index is the commit point: late cancellation must not report rollback.
    try{if(progress!=null)progress(new SaveEnvironmentProgress{stage="committed",text="快照配置已保存",completedFiles=writes.Count,totalFiles=writes.Count});revised.CleanupWarnings=reader.RecoverRevision(pointer,journal,journalBytes);revised.Stamp=reader.Load().Stamp;}catch(Exception error){if(error is OutOfMemoryException||error is StackOverflowException)throw;revised.CleanupWarnings=new[]{"配置已保存，后续清理尚未完成："+error.Message};}return revised;
   });
   try{var cleanup=RetryRevisionCleanup(published);published.CleanupWarnings=File.Exists(Child(published.RootPath,RevisionJournalPath))?published.CleanupWarnings.Concat(cleanup).ToArray():cleanup;}catch(Exception error){if(error is OutOfMemoryException||error is StackOverflowException)throw;published.CleanupWarnings=published.CleanupWarnings.Concat(new[]{"配置已保存，后续清理尚未完成："+error.Message}).ToArray();}return published;
  }
 }
}
