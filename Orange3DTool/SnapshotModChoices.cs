using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Web.Script.Serialization;
namespace PZ3D {static class SnapshotModChoices {
 internal static string Canonical(string id){return id.TrimStart('\\');}
 internal static Dictionary<string,string> Parse(string text){
  Dictionary<string,string> values;try{values=string.IsNullOrEmpty(text)?new Dictionary<string,string>():new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(text);}catch(Exception error){throw new IOException("模组选择无法读取，请重新选择。",error);}return Validate(values);
 }
 internal static Dictionary<string,string> Validate(IDictionary<string,string> values){
  var result=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var pair in values??new Dictionary<string,string>()){if(!SaveProfiles.Id(pair.Key)||pair.Value==null||pair.Value.Length>0&&!SaveProfiles.Id(pair.Value))throw new IOException("模组选择包含无效的标识。");string key=Canonical(pair.Key),target=Canonical(pair.Value);if(result.ContainsKey(key))throw new IOException("同一模组有重复选择。");result.Add(key,target);}foreach(var pair in result)if(pair.Value.Length>0&&pair.Value!=pair.Key&&result.ContainsKey(pair.Value)&&result[pair.Value]!=pair.Value)throw new IOException("请直接选择最终使用的模组，不要设置连续替换。");return result;
 }
 internal static string[] Apply(IEnumerable<string> ids,IDictionary<string,string> choices){
  if(choices==null||choices.Count==0)return ids.ToArray();var result=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);foreach(string id in ids){string target;string selected=choices.TryGetValue(Canonical(id),out target)?target:id;if(selected.Length>0&&seen.Add(Canonical(selected)))result.Add(selected);}return result.ToArray();
 }
}}
