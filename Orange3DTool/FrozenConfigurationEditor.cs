using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading;
namespace PZ3D {
 // All calls are pinned to a native-resolved Engine and a selected manifest generation.
 static class FrozenConfigurationEditor {
  internal static string[] Components(SaveEnvironmentRecord row){return row.ActiveComponentIds??ModConfigurations.Managed(row.DefaultModIds).Concat(row.PreloadModIds).Distinct(StringComparer.Ordinal).ToArray();}
  internal static string[] Managed(SaveEnvironmentRecord row){return row.ActiveManagedIds??Components(row);}
  internal static SaveEnvironmentRecord Approve(Engine engine,string expected,string approvalToken,bool decision,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){
   using(SaveEnvironmentRuntime.AcquireRevisionEdit(engine)){var old=SaveEnvironments.FindOwned(engine.Data,engine.Game,engine.User);if(old.ManifestHash!=expected)throw new IOException("此快照配置已变化，请重新读取后授权。");SaveEnvironmentRuntime.ValidateOwnedRuntime(engine);var selected=(old.JavaRules??new FrozenJavaRule[0]).SingleOrDefault(r=>FrozenJavaPolicy.Token(r)==approvalToken);if(selected==null)throw new IOException("此 Java 文件尚未复制或来源已变化，请重新读取。");if(selected.Approved==decision)return old;var decisionSource=old.ShallowCopy();decisionSource.JavaRules=old.JavaRules.Select(r=>new FrozenJavaRule{ModId=r.ModId,JarPath=r.JarPath,Package=r.Package,Hash=r.Hash,Allowed=r.Allowed,Approved=r==selected?decision:r.Approved}).ToArray();var revised=old.ShallowCopy();FrozenJavaPolicy.Prepare(engine,decisionSource,revised,cancellation);return SaveEnvironments.PublishRevision(engine,old,revised,SaveEnvironmentRuntime.RevisionOutputs(old,revised),cancellation,progress);}
  }
  internal static SaveEnvironmentRecord SetMemory(Engine engine,string expected,int gib,Func<LauncherMemoryInfo> memoryProvider){
   using(SaveEnvironmentRuntime.AcquireRevisionEdit(engine)){var old=SaveEnvironments.FindOwned(engine.Data,engine.Game,engine.User);if(old.ManifestHash!=expected)throw new IOException("此快照配置已变化，请重新读取后保存。");SaveEnvironmentRuntime.ValidateOwnedRuntime(engine);byte[] before=SaveEnvironmentRuntime.PinnedFile(engine,"game/ProjectZomboid64.json",4194304),after=LauncherMaintenance.PrepareProfileHeap(before,gib,memoryProvider);if(before.SequenceEqual(after))return old;return SaveEnvironments.PublishRevision(engine,old,old.ShallowCopy(),new Dictionary<string,byte[]>{{"game/ProjectZomboid64.json",after}},CancellationToken.None,null);}
  }
  internal static SaveEnvironments.RevisionSource[] Sources(Engine engine,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){return SaveEnvironments.RevisionSources(engine,cancellation,progress);}
  internal static SaveEnvironmentRecord SaveOrdinary(Engine engine,string expected,string[] ordinary,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){return Save(engine,expected,ordinary,Components(engine.FrozenEnvironment),new string[0],cancellation,progress);}
  internal static SaveEnvironmentRecord Save(Engine engine,string expected,string[] ordinary,string[] selectedComponents,string[] tokens,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){return SavePrepared(engine,expected,ordinary,selectedComponents,tokens,cancellation,progress,null);}
  internal static SaveEnvironmentRecord Recover(Engine engine,string expected){using(SaveEnvironmentRuntime.AcquireRevisionEdit(engine)){var row=SaveEnvironments.FindOwned(engine.Data,engine.Game,engine.User);if(row.ManifestHash!=expected)throw new IOException("快照配置已变化，请重新读取。");row.CleanupWarnings=SaveEnvironments.RetryRevisionCleanup(row);return row;}}
  internal static SaveEnvironmentRecord Uninstall(Engine engine,string expected,string[] ids,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress){var row=engine.FrozenEnvironment;ids=ModConfigurations.ValidatePreflightMods(ids);if(ids.Any(id=>!(Managed(row)).Contains(id)))throw new IOException("只能卸载此快照中已管理的组件。");var components=Components(row);return SavePrepared(engine,expected,row.DefaultModIds.Except(components).ToArray(),components.Except(ids).ToArray(),new string[0],cancellation,progress,null,ids);}
  static SaveEnvironmentMissing MissingPayload(Engine engine,SaveEnvironmentRecord owner,SaveEnvironments.RevisionSource source,CancellationToken cancellation){
   var catalog=new SaveProfiles.PreflightCatalog();
   catalog.Candidates.Add(source.Candidate);
   catalog.Select(new[]{source.id});
   return SnapshotPreparation.Inspect(engine,new[]{source.id},catalog,owner.ModRoots,cancellation).Missing
    .SingleOrDefault(missing=>missing.modId==source.id&&missing.kind=="java-component");
  }
  static SaveEnvironments.RevisionSource PrepareNewSource(Engine engine,SaveEnvironmentRecord owner,SaveEnvironments.RevisionSource source,CancellationToken cancellation,Func<SaveEnvironmentMissing,CancellationToken,SaveEnvironments.RevisionSource[]> resolve){
   var missing=MissingPayload(engine,owner,source,cancellation);
   if(missing==null)return source;
   if(resolve==null||!missing.canDownload)throw new IOException(SnapshotPreparation.Describe(missing));
   // Only a not-yet-copied source is eligible. Preserve the explicit mod and
   // reliable Workshop identities while refreshing metadata after download.
   var refreshed=resolve(missing,cancellation).Where(candidate=>candidate.id==source.id&&candidate.Candidate.Workshop==missing.workshopId).ToArray();
   cancellation.ThrowIfCancellationRequested();
   if(refreshed.Length!=1)throw new IOException(SnapshotPreparation.Describe(missing));
   var remaining=MissingPayload(engine,owner,refreshed[0],cancellation);
   if(remaining!=null)throw new IOException(SnapshotPreparation.Describe(remaining));
   return refreshed[0];
  }
  internal static SaveEnvironmentRecord SavePrepared(Engine engine,string expected,string[] ordinary,string[] selectedComponents,string[] tokens,CancellationToken cancellation,Action<SaveEnvironmentProgress> progress,Func<SaveEnvironmentMissing,CancellationToken,SaveEnvironments.RevisionSource[]> resolve,string[] uninstall=null){
   if(engine==null||engine.FrozenEnvironment==null)throw new IOException("请选择快照配置。");
   using(SaveEnvironmentRuntime.AcquireRevisionEdit(engine)){
    cancellation.ThrowIfCancellationRequested();var old=SaveEnvironments.FindOwned(engine.Data,engine.Game,engine.User);
    if(old.ManifestHash!=expected)throw new IOException("此快照配置已变化，请重新读取后保存。");
    old.CleanupWarnings=SaveEnvironments.RetryRevisionCleanup(old);SaveEnvironmentRuntime.ValidateOwnedRuntime(engine);ordinary=ModConfigurations.ValidatePreflightMods(ordinary);
    if(ordinary.Any(id=>id=="OrangeLauncherSession"))throw new IOException("普通模组清单不能包含会话组件。");
    var managed=(Managed(old)).Except(uninstall??new string[0]).ToArray();var components=Components(old);
    if(ordinary.Intersect(managed,StringComparer.Ordinal).Any())throw new IOException("组件启用请在组件选择中修改。");
    components=ModConfigurations.ValidatePreflightMods(selectedComponents);managed=managed.Concat(components).Distinct().ToArray();if(ordinary.Intersect(components).Any())throw new IOException("普通模组与所选组件不能重复。");
    var effective=ordinary.Concat(components).Distinct(StringComparer.Ordinal).ToArray();
    if(ordinary.SequenceEqual(old.DefaultModIds.Except(Components(old)),StringComparer.Ordinal)&&components.SequenceEqual(Components(old))&&tokens.Length==0&&(uninstall==null||uninstall.Length==0))return old;
    var added=new List<SaveEnvironmentModRoot>();bool committed=false;
    try{
     var available=tokens.Length==0?new SaveEnvironments.RevisionSource[0]:Sources(engine,cancellation,progress);var chosen=tokens.Select(t=>{var source=available.SingleOrDefault(c=>c.token==t);if(source==null)throw new IOException("所选来源已变化或无法读取，请重新选择。");return source;}).ToArray();
     if(chosen.Any(c=>old.ModRoots.Any(r=>r.ModIds.Contains(c.id))))throw new IOException("此模组已有独立副本，不能用当前来源覆盖。");
     foreach(var group in chosen.GroupBy(c=>c.Candidate.Root,StringComparer.OrdinalIgnoreCase)){var source=PrepareNewSource(engine,old,group.First(),cancellation,resolve);added.Add(SaveEnvironments.CopyRevisionSource(old,source,group.Select(c=>c.id).Distinct().ToArray(),cancellation,progress));}
     var roots=old.ModRoots.Concat(added).ToArray();foreach(string id in effective)if(!roots.Any(root=>root.ModIds.Contains(id,StringComparer.Ordinal)))throw new IOException("尚未复制所选模组："+id);
     var revised=old.ShallowCopy();revised.ModRoots=roots;revised.DefaultModIds=effective;revised.ActiveManagedIds=managed.ToArray();revised.ActiveComponentIds=components.ToArray();
     SnapshotPreparation.Dependencies dependencies=null;var attempted=new HashSet<string>();
     for(int pass=0;pass<2000;pass++){
      var copied=new Engine(engine.Game,engine.User,engine.Data,engine.TestMode,engine.ReadSteamOptions,engine.ProcessInventory){FrozenEnvironment=revised};var budget=new LaunchReadBudget(67108864,100000);budget.Checkpoint=(path,size)=>cancellation.ThrowIfCancellationRequested();var warnings=new List<string>();var catalog=SaveProfiles.SnapshotEntries(copied,effective,warnings,budget);if(budget.Incomplete)throw new IOException("复制的模组信息尚未读取完整，请重试。");dependencies=SnapshotPreparation.Inspect(copied,effective,catalog,revised.ModRoots,cancellation);if(dependencies.Missing.Length==0)break;
      bool progressed=false;
      foreach(var missing in dependencies.Missing){
       if(!attempted.Add(missing.modId))throw new IOException(SnapshotPreparation.Describe(missing));
       // Never repair a committed copy by downloading current source bytes.
       if(old.ModRoots.Any(root=>root.ModIds.Contains(missing.modId)))
        throw new IOException(SnapshotPreparation.Describe(missing));
       if(available.Length==0)available=Sources(engine,cancellation,progress);
       var candidates=available.Where(candidate=>candidate.id==missing.modId).ToArray();
       if(candidates.Length==0&&resolve!=null&&missing.canDownload){
        available=resolve(missing,cancellation);
        candidates=available.Where(candidate=>candidate.id==missing.modId).ToArray();
       }
       if(candidates.Length!=1)throw new IOException(SnapshotPreparation.Describe(missing));
       var source=PrepareNewSource(engine,old,candidates[0],cancellation,resolve);
       var existing=revised.ModRoots.FirstOrDefault(root=>VersionLibrary.Same(root.SourceRoot,source.Candidate.Root));
       if(existing!=null)throw new IOException("已有副本不包含所需模组："+missing.modId);
       added.Add(SaveEnvironments.CopyRevisionSource(old,source,new[]{source.id},cancellation,progress));
       revised.ModRoots=old.ModRoots.Concat(added).ToArray();
       progressed=true;
      }
      if(!progressed)throw new IOException("无法取得所需的模组副本。");
     }
     if(dependencies==null||dependencies.Missing.Length>0)throw new IOException("依赖数量过多，未保存此次修改。");var retained=new HashSet<string>(dependencies.Ids.Concat(managed),StringComparer.Ordinal);revised.ModRoots=revised.ModRoots.Where(r=>r.ModIds.Any(retained.Contains)).ToArray();
     FrozenJavaPolicy.Prepare(engine,old,revised,cancellation);
     var outputs=SaveEnvironmentRuntime.RevisionOutputs(old,revised);var result=SaveEnvironments.PublishRevision(engine,old,revised,outputs,cancellation,progress);committed=true;return result;
    }finally{if(!committed)SaveEnvironments.RemoveUncommittedAdditions(engine,old,added);}

   }
  }
 }
}
