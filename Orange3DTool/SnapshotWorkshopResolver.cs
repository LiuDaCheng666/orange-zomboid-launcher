using System;using System.Collections;using System.Collections.Generic;using System.IO;using System.Linq;using System.Net;using System.Text.RegularExpressions;using System.Threading;using System.Web.Script.Serialization;
namespace PZ3D {
 // Search is discovery, not an ID registry. Only the author's exact Mod ID
 // declaration can qualify an automatic download; downloaded files are re-read.
 static class SnapshotWorkshopResolver {
  internal sealed class Candidate {public string workshopId,name;}
  internal sealed class Result {public string workshopId="",name="";public Candidate[] candidates=new Candidate[0];}
  static bool Numeric(string value){ulong id;return ulong.TryParse(value,out id)&&id>0&&id.ToString()==value;}
  internal static Dictionary<string,string> Sources(string text){
   if(string.IsNullOrEmpty(text))return new Dictionary<string,string>(StringComparer.Ordinal);Dictionary<string,string> values;try{values=new JavaScriptSerializer().Deserialize<Dictionary<string,string>>(text);}catch(Exception error){throw new IOException("请填写有效的模组工坊来源。",error);}if(values==null||values.Any(pair=>string.IsNullOrWhiteSpace(pair.Key)||pair.Key.Any(char.IsControl)||!Numeric(pair.Value)))throw new IOException("工坊来源必须是有效的数字项目 ID。");return values;
  }
  internal static bool Declares(string description,string modId){
   string text=WebUtility.HtmlDecode(description??"");text=Regex.Replace(text,@"\[/?(?:b|i|u)\]","",RegexOptions.IgnoreCase);text=Regex.Replace(text,@"\[(?:br|hr)\]|</?(?:br|p|div)[^>]*>","\n",RegexOptions.IgnoreCase);
   return Regex.Matches(text,@"(?:^|\n)\s*Mod\s*ID\s*:\s*([^\r\n]+)",RegexOptions.IgnoreCase).Cast<Match>().Any(match=>match.Groups[1].Value.Trim()==modId);
  }
  internal static Result Select(IEnumerable<Candidate> candidates,HashSet<string> previous){
   var values=candidates.GroupBy(row=>row.workshopId,StringComparer.Ordinal).Select(group=>group.First()).ToArray();var recorded=values.Where(row=>previous.Contains(row.workshopId)).ToArray();var chosen=values.Length==1?values[0]:recorded.Length==1?recorded[0]:null;return new Result{workshopId=chosen==null?"":chosen.workshopId,name=chosen==null?"":chosen.name,candidates=values};
  }
  internal static Result Resolve(string modId,HashSet<string> previous,Func<string,object> search,CancellationToken token,Action<int,string> progress){
   token.ThrowIfCancellationRequested();if(progress!=null)progress(-1,"正在搜索缺失模组 · "+modId);object response=search("\""+modId.Replace("\"","")+"\"");token.ThrowIfCancellationRequested();var json=new JavaScriptSerializer{MaxJsonLength=16777216};var root=json.Deserialize<Dictionary<string,object>>(json.Serialize(response));object rows;var matches=new List<Candidate>();
   if(root!=null&&root.TryGetValue("items",out rows)&&rows is IEnumerable&&!(rows is string))foreach(object value in (IEnumerable)rows){token.ThrowIfCancellationRequested();var row=value as Dictionary<string,object>;object id,title,description;if(row==null||!row.TryGetValue("id",out id)||!(id is string)||!Numeric((string)id)||!row.TryGetValue("description",out description)||!(description is string)||!Declares((string)description,modId))continue;matches.Add(new Candidate{workshopId=(string)id,name=row.TryGetValue("title",out title)?Convert.ToString(title):modId});}
   return Select(matches,previous);
  }
  internal static HashSet<string> HistoricalItems(string user,CancellationToken token,Action<int,string> progress){
   var ids=new HashSet<string>(StringComparer.Ordinal);string logs=Path.Combine(user,"Logs");if(!Directory.Exists(logs))return ids;var pending=new Stack<string>();pending.Push(logs);int files=0;var clock=System.Diagnostics.Stopwatch.StartNew();var pattern=new Regex(@"[\\/]workshop[\\/]content[\\/]108600[\\/]([1-9][0-9]*)[\\/]",RegexOptions.IgnoreCase);
   while(pending.Count>0){token.ThrowIfCancellationRequested();string dir=pending.Pop();if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;foreach(string child in Directory.EnumerateDirectories(dir))pending.Push(child);foreach(string file in Directory.EnumerateFiles(dir)){token.ThrowIfCancellationRequested();if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0||!(file.EndsWith(".txt",StringComparison.OrdinalIgnoreCase)||file.EndsWith(".log",StringComparison.OrdinalIgnoreCase)))continue;try{using(var input=new StreamReader(new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))){string line;while((line=input.ReadLine())!=null){token.ThrowIfCancellationRequested();foreach(Match match in pattern.Matches(line))if(Numeric(match.Groups[1].Value))ids.Add(match.Groups[1].Value);if(clock.ElapsedMilliseconds>=250){if(progress!=null)progress(-1,"正在查找曾用的工坊来源 · 已读取 "+files+" 个日志文件");clock.Restart();}}}}catch(IOException){}files++;}}
   return ids;
  }
 }
}
