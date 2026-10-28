using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Text;using System.Threading;using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class FrozenJavaRule {public string ModId,JarPath,Package,Hash;public bool Approved,Allowed;}
 static class FrozenJavaPolicy {
  static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=4194304};}
  internal static string Token(FrozenJavaRule rule){return LauncherComponents.Hash(Encoding.UTF8.GetBytes(rule.ModId+"\n"+rule.JarPath.ToUpperInvariant()+"\n"+rule.Package+"\n"+rule.Hash));}
  static bool CapturedApproval(SaveEnvironmentRecord old,string id,string hash){if(old.ApprovalBytes==null)return false;var obj=Json().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(old.ApprovalBytes));object values;if(obj==null||!obj.TryGetValue("mods",out values)||!(values is IList))return false;return ((IList)values).Cast<object>().OfType<Dictionary<string,object>>().Any(row=>row.ContainsKey("id")&&row.ContainsKey("jar_hash")&&row.ContainsKey("decision")&&Equals(row["id"],id)&&Equals(row["jar_hash"],hash)&&Equals(row["decision"],true));}
  internal static void Prepare(Engine engine,SaveEnvironmentRecord old,SaveEnvironmentRecord revised,CancellationToken token){
   var inspector=new LauncherComponents(LauncherConfiguration.Resolve(engine.Data,revised.GamePath,revised.UserPath),GameVersionProbe.Read(revised.GamePath)??new Version(42,0));var rules=new List<FrozenJavaRule>();var preloads=new Dictionary<string,object>();
   foreach(var mapping in revised.ModRoots)foreach(string id in mapping.ModIds){token.ThrowIfCancellationRequested();LauncherComponents.Row item;
    try{item=inspector.Inspect(mapping.CopiedRoot,id,token);}catch(OperationCanceledException){throw;}catch(IOException){if(revised.ActiveComponentIds.Contains(id))throw;continue;}
    if(item.Jar==null||id=="ZombieBuddy")continue;
    var rule=new FrozenJavaRule{ModId=id,JarPath=item.Jar,Package=item.Package,Hash=item.Sha256};var previous=(old.JavaRules??new FrozenJavaRule[0]).SingleOrDefault(r=>Token(r)==Token(rule));rule.Approved=previous!=null?previous.Approved:old.JavaRules==null&&old.ModRoots.Any(m=>VersionLibrary.Same(m.CopiedRoot,mapping.CopiedRoot)&&m.ModIds.Contains(id))&&CapturedApproval(old,id,item.Sha256);rule.Allowed=rule.Approved&&revised.ActiveComponentIds.Contains(id);rules.Add(rule);
    if(rule.Allowed){if(!revised.ActiveComponentIds.Contains("ZombieBuddy"))throw new IOException(item.Name+"：启用 Java 文件还需要选择 ZombieBuddy。");var source=ViewpointAddons.ResolveSource(Path.GetDirectoryName(Path.GetDirectoryName(mapping.CopiedRoot)),id,inspector.GameVersion,null,mapping.CopiedRoot);Func<string,string> original=p=>Path.Combine(mapping.SourceRoot,p.Substring(mapping.CopiedRoot.Length).TrimStart('\\','/'));preloads.Add(id,new{infPath=original(source.Info),jarPath=original(item.Jar)});}
   }
   revised.JavaRules=rules.ToArray();revised.PreloadModIds=preloads.Keys.ToArray();revised.BuddyConfigBytes=Encoding.UTF8.GetBytes(Json().Serialize(new{preload_mods=preloads,trusted_authors=new object[0],auto_fix_mod_order=false,fix_approval_dialog_cursor=false}));
   // Buddy stores a single decision per hash. The pinned execution gate still
   // checks each copied path/package/hash rule, so an enabled twin cannot grant
   // execution to an unapproved or disabled source with identical bytes.
   revised.ApprovalBytes=Encoding.UTF8.GetBytes(Json().Serialize(new{
    formatVersion=2,
    mods=rules.GroupBy(r=>r.Hash,StringComparer.Ordinal).Select(group=>new{
     id=(group.FirstOrDefault(r=>r.Allowed)??group.First()).ModId,
     jar_hash=group.Key,
     decision=group.Any(r=>r.Allowed)
    }).ToArray()
   }));
  }
 }
}
