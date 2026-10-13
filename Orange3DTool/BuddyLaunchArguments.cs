using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 internal enum BuddyArgumentKind { None, Known, Unknown }
 internal sealed class BuddyCleanupResult {
  internal bool Safe {get;private set;}
  internal bool Changed {get;private set;}
  internal string Text {get;private set;}
  internal int RemovedCount {get;private set;}
  internal string Summary {get;private set;}
  internal BuddyCleanupResult(string original,string next,int count,bool safe){Safe=safe;Changed=safe&&original!=next;Text=safe?next:original;RemovedCount=safe?count:0;Summary=safe?(count==0?"未发现需清理的 Buddy 参数":"清理 "+count+" 项已识别的 Buddy 参数"):"Buddy 启动语法或自定义选项不明确，请人工检查。";}
 }
 internal static class BuddyLaunchArguments {
  const int Limit=2097152;
  internal static BuddyArgumentKind Classify(string argument){
   if(string.IsNullOrWhiteSpace(argument))return BuddyArgumentKind.None;
   string value=argument.Trim();bool quoted=value.IndexOf('"')>=0;
   if(quoted){try{var parsed=Tokens(value);if(parsed.Count!=1)return Candidate(value)?BuddyArgumentKind.Unknown:BuddyArgumentKind.None;value=parsed[0].Value;}catch{return Candidate(value)?BuddyArgumentKind.Unknown:BuddyArgumentKind.None;}}
   string tail,basename;bool library;
   if(value.StartsWith("-javaagent:",StringComparison.OrdinalIgnoreCase)){tail=value.Substring(11);basename="ZombieBuddy.jar";library=false;}
   else if(value.StartsWith("-agentlib:",StringComparison.OrdinalIgnoreCase)){tail=value.Substring(10);basename="zbNative";library=true;}
   else if(value.StartsWith("-agentpath:",StringComparison.OrdinalIgnoreCase)){tail=value.Substring(11);basename="zbNative.dll";library=false;}
   else return BuddyArgumentKind.None;
   int equals=tail.IndexOf('=');string file=equals<0?tail:tail.Substring(0,equals);string name=file.Replace('\\','/').Split('/').Last();
   if(!string.Equals(library?file:name,basename,StringComparison.OrdinalIgnoreCase))return BuddyArgumentKind.None;
   if(equals>=0&&equals<tail.Length-1)return BuddyArgumentKind.Unknown;
   if(file.IndexOfAny(new[]{'%','!','\r','\n','\0'})>=0)return BuddyArgumentKind.Unknown;
   if(!library&&!string.Equals(file,basename,StringComparison.OrdinalIgnoreCase)&&!Regex.IsMatch(file,@"^(?:[A-Za-z]:[\\/]|\\\\[^\\]+\\[^\\]+\\)"))return BuddyArgumentKind.Unknown;
   return BuddyArgumentKind.Known;
  }
  static bool Candidate(string value){return Regex.IsMatch(value??"",@"(?i)-(?:javaagent:""?(?:[^""\r\n]*[\\/])?ZombieBuddy\.jar|agentlib:zbNative|agentpath:""?(?:[^""\r\n]*[\\/])?zbNative\.dll)(?=[\s""=]|$)");}
  internal sealed class ArgumentToken {internal int Start,End;internal string Value;}
  internal static List<ArgumentToken> Tokens(string line){
   if(line==null||line.Length>Limit||line.IndexOf('\0')>=0)throw new FormatException();var result=new List<ArgumentToken>();int at=0;
   while(at<line.Length){while(at<line.Length&&char.IsWhiteSpace(line[at]))at++;if(at==line.Length)break;int start=at;bool quoted=false;var value=new StringBuilder();
    while(at<line.Length&&(quoted||!char.IsWhiteSpace(line[at]))){char c=line[at++];if(c=='"'){quoted=!quoted;continue;}if(c=='\\'){int begin=at-1;while(at<line.Length&&line[at]=='\\')at++;int count=at-begin;if(at<line.Length&&line[at]=='"'){value.Append('\\',count/2);if(count%2==0)quoted=!quoted;else value.Append('"');at++;}else value.Append('\\',count);continue;}value.Append(c);}
    if(quoted)throw new FormatException();result.Add(new ArgumentToken{Start=start,End=at,Value=value.ToString()});if(result.Count>65536)throw new FormatException();
   }return result;
  }
  internal static BuddyCleanupResult CleanCommandLine(string commandLine){
   try{var tokens=Tokens(commandLine);if(commandLine.IndexOfAny(new[]{'&','|','<','>','^','\r','\n'})>=0)throw new FormatException();return CleanTokens(commandLine,tokens);
   }catch{return new BuddyCleanupResult(commandLine,commandLine,0,false);}
  }
  static BuddyCleanupResult CleanTokens(string commandLine,IEnumerable<ArgumentToken> tokens){var remove=tokens.Where(t=>Classify(t.Value)!=BuddyArgumentKind.None).ToArray();if(remove.Any(t=>Classify(t.Value)==BuddyArgumentKind.Unknown))throw new FormatException();string next=commandLine;foreach(var token in remove.Reverse()){int start=token.Start,end=token.End;while(end<next.Length&&char.IsWhiteSpace(next[end]))end++;if(end==next.Length)while(start>0&&char.IsWhiteSpace(next[start-1]))start--;next=next.Remove(start,end-start);}return new BuddyCleanupResult(commandLine,next,remove.Length,true);}
  internal static bool HasAuthorizationOverride(string commandLine){
   try{return Tokens(commandLine??"").Any(t=>t.Value.StartsWith("-Duser.home",StringComparison.OrdinalIgnoreCase)||t.Value.IndexOf("config_dir",StringComparison.OrdinalIgnoreCase)>=0||t.Value.StartsWith("-D",StringComparison.OrdinalIgnoreCase)&&t.Value.IndexOf("prefix=",StringComparison.OrdinalIgnoreCase)>=0||t.Value.StartsWith("-javaagent:",StringComparison.OrdinalIgnoreCase)||t.Value.StartsWith("-agentlib:",StringComparison.OrdinalIgnoreCase)||t.Value.StartsWith("-agentpath:",StringComparison.OrdinalIgnoreCase));}catch{return true;}
  }
  internal static BuddyCleanupResult CleanJson(string json){
   try{if(json==null||json.Length>Limit)throw new FormatException();new JsonKeys(json).Validate();var serializer=new JavaScriptSerializer{MaxJsonLength=Limit,RecursionLimit=64};var root=serializer.Deserialize<Dictionary<string,object>>(json);if(root==null||!root.ContainsKey("vmArgs"))throw new FormatException();int count=0;bool normal=false;
    CleanArray(root,true,ref normal,ref count);object windows;if(root.TryGetValue("windows",out windows)){var branches=windows as Dictionary<string,object>;if(branches==null)throw new FormatException();foreach(var branch in branches.Values){var node=branch as Dictionary<string,object>;if(node==null)throw new FormatException();CleanBranches(node,ref normal,ref count);}}
    return new BuddyCleanupResult(json,count==0?json:serializer.Serialize(root),count,true);
   }catch{return new BuddyCleanupResult(json,json,0,false);}
  }
  static void CleanBranches(Dictionary<string,object> node,ref bool normal,ref int count){if(node.ContainsKey("vmArgs"))CleanArray(node,false,ref normal,ref count);foreach(var child in node.Values.OfType<Dictionary<string,object>>())CleanBranches(child,ref normal,ref count);}
  static void CleanArray(Dictionary<string,object> node,bool root,ref bool normal,ref int count){object raw=node["vmArgs"];if(!(raw is IEnumerable)||raw is string||raw is IDictionary)throw new FormatException();var list=new List<string>();foreach(object item in (IEnumerable)raw){string value=item as string;if(value==null)throw new FormatException();var kind=Classify(value);if(kind==BuddyArgumentKind.Unknown)throw new FormatException();if(kind==BuddyArgumentKind.None)list.Add(value);else if(root&&!normal&&string.Equals(value,"-agentlib:zbNative",StringComparison.OrdinalIgnoreCase)){normal=true;list.Add(value);}else count++;}node["vmArgs"]=list.ToArray();}
  static bool BatComment(string line){string trim=line.TrimStart();if(trim.StartsWith("@"))trim=trim.Substring(1);return Regex.IsMatch(trim,@"^(?i:rem)(?:\s|$)")||trim.StartsWith("::");}
  static BuddyCleanupResult CleanJavaInvocation(string text,List<ArgumentToken> tokens){
   int boundary=-1;var agents=new List<ArgumentToken>();
   for(int i=1;i<tokens.Count;i++){
    string arg=tokens[i].Value;
    if(arg=="-jar"){if(i+1>=tokens.Count||tokens[i+1].Value.Length==0)throw new FormatException();boundary=i;break;}
    if(arg=="-cp"||arg=="-classpath"||arg=="--class-path"){if(++i>=tokens.Count||tokens[i].Value.Length==0||tokens[i].Value.StartsWith("@"))throw new FormatException();continue;}
    if(arg.StartsWith("--class-path=",StringComparison.Ordinal)&&arg.Length>13)continue;
    if(arg.StartsWith("-D",StringComparison.Ordinal)&&arg.Length>2||arg.StartsWith("-XX:",StringComparison.Ordinal)||Regex.IsMatch(arg,@"^-Xm[sx][0-9]+[kKmMgG]?$"))continue;
    if(arg.StartsWith("-javaagent:",StringComparison.Ordinal)||arg.StartsWith("-agentlib:",StringComparison.Ordinal)||arg.StartsWith("-agentpath:",StringComparison.Ordinal)){agents.Add(tokens[i]);continue;}
    if(new[]{"-server","-client","-ea","-da","-esa","-dsa","-enableassertions","-disableassertions","-enablesystemassertions","-disablesystemassertions"}.Contains(arg))continue;
    if(!arg.EndsWith(".java",StringComparison.Ordinal)&&Regex.IsMatch(arg,@"^[\p{L}_$][\p{L}\p{N}_$]*(?:\.[\p{L}_$][\p{L}\p{N}_$]*)*$")){boundary=i;break;}
    throw new FormatException();
   }
   if(boundary<0)throw new FormatException();return CleanTokens(text,agents);
  }
  // Locate possible command boundaries only. Do not execute, expand or rewrite shell syntax.
  static List<string> BatCommands(string text){
   var parts=new List<string>();bool quoted=false;int start=0;
   for(int i=0;i<text.Length;i++){char c=text[i];if(c=='^'&&!quoted){if(i+1<text.Length)i++;continue;}if(c=='"'){quoted=!quoted;continue;}if(!quoted&&(c=='&'||c=='|')){parts.Add(text.Substring(start,i-start));start=i+1;}}
   if(quoted)throw new FormatException();parts.Add(text.Substring(start));return parts;
  }
  static BuddyCleanupResult CleanBatLine(string text){
   var commands=BatCommands(text);if(commands.Count==1)return CleanBatCommand(text);
   foreach(string command in commands)if(!BatComment(command)&&CleanBatCommand(command,true).RemovedCount>0)throw new FormatException();
   return new BuddyCleanupResult(text,text,0,true);
  }
  static BuddyCleanupResult CleanBatCommand(string text,bool requireKnown=false){
   var assignment=Regex.Match(text,@"^(?<prefix>\s*@?(?i:set)\s+)(?<quote>""?)(?<name>[A-Za-z_][A-Za-z0-9_]*)=(?<value>.*)$");
   if(assignment.Success){
    string value=assignment.Groups["value"].Value,quote=assignment.Groups["quote"].Value;if(quote.Length>0){if(!value.EndsWith("\""))throw new FormatException();value=value.Substring(0,value.Length-1);}
    var args=Tokens(value);if(!args.Any(t=>Classify(t.Value)!=BuddyArgumentKind.None))return new BuddyCleanupResult(text,text,0,true);
    if(!new[]{"_JAVA_OPTIONS","JAVA_TOOL_OPTIONS","JDK_JAVA_OPTIONS","JAVA_OPTS"}.Contains(assignment.Groups["name"].Value,StringComparer.OrdinalIgnoreCase))throw new FormatException();
    var clean=CleanTokens(value,args);return new BuddyCleanupResult(text,text.Substring(0,assignment.Groups["value"].Index)+clean.Text+quote,clean.RemovedCount,true);
   }
   var tokens=Tokens(text);if(tokens.Count>0){string executable=tokens[0].Value.TrimStart('@').Replace('\\','/').Split('/').Last();if(new[]{"java","java.exe","javaw","javaw.exe"}.Contains(executable,StringComparer.OrdinalIgnoreCase)){
    try{return CleanJavaInvocation(text,tokens);}catch(FormatException){if(requireKnown||tokens.Any(t=>Classify(t.Value)!=BuddyArgumentKind.None))throw;return new BuddyCleanupResult(text,text,0,true);}
   }if(requireKnown&&!string.Equals(executable,"echo",StringComparison.OrdinalIgnoreCase))throw new FormatException();}
   if(tokens.Any(t=>Classify(t.Value)!=BuddyArgumentKind.None))throw new FormatException();return new BuddyCleanupResult(text,text,0,true);
  }
  internal static BuddyCleanupResult CleanBat(string bat){
   if(bat==null||bat.Length>Limit)return new BuddyCleanupResult(bat,bat,0,false);int total=0;var output=new StringBuilder();
   try{var lines=Regex.Matches(bat,@"[^\r\n]*(?:\r\n|\r|\n|$)").Cast<Match>().Where(m=>m.Length>0).Select(m=>m.Value).ToArray();bool complex=lines.Where(line=>!BatComment(line)).Any(line=>line.IndexOfAny(new[]{'(',')','^'})>=0||Regex.IsMatch(line,@"^\s*@?(?i:if|for|else)(?:\s|$)"));
    foreach(string raw in lines){string text=raw.TrimEnd('\r','\n'),ending=raw.Substring(text.Length);if(BatComment(text)){output.Append(raw);continue;}
     var cleaned=CleanBatLine(text);if(cleaned.RemovedCount>0&&(complex||text.IndexOfAny(new[]{'%','!','^','&','|','<','>','(',')'})>=0))throw new FormatException();output.Append(cleaned.Text).Append(ending);total+=cleaned.RemovedCount;
    }return new BuddyCleanupResult(bat,output.ToString(),total,true);
   }catch{return new BuddyCleanupResult(bat,bat,0,false);}
  }
  // Serializer accepts duplicate properties; validate structure first to prevent silently choosing one.
  sealed class JsonKeys {
   readonly string text;int at;
   internal JsonKeys(string value){text=value;}
   internal void Validate(){Value(0);Space();if(at!=text.Length)throw new FormatException();}
   void Space(){while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;}
   void Need(char c){Space();if(at>=text.Length||text[at++]!=c)throw new FormatException();}
   string Str(){Space();int start=at;Need('"');bool end=false;while(at<text.Length){char c=text[at++];if(c=='\\'){if(at==text.Length)throw new FormatException();at++;}else if(c=='"'){end=true;break;}}if(!end)throw new FormatException();return new JavaScriptSerializer().Deserialize<string>(text.Substring(start,at-start));}
   void Value(int depth){if(depth>64)throw new FormatException();Space();if(at==text.Length)throw new FormatException();char c=text[at];if(c=='{'){at++;var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);Space();if(at<text.Length&&text[at]=='}'){at++;return;}while(true){if(!keys.Add(Str()))throw new FormatException();Need(':');Value(depth+1);Space();if(at<text.Length&&text[at]=='}'){at++;return;}Need(',');}}
    else if(c=='['){at++;Space();if(at<text.Length&&text[at]==']'){at++;return;}while(true){Value(depth+1);Space();if(at<text.Length&&text[at]==']'){at++;return;}Need(',');}}
    else if(c=='"')Str();else{int start=at;while(at<text.Length&&!char.IsWhiteSpace(text[at])&&",]}".IndexOf(text[at])<0)at++;if(at==start)throw new FormatException();}
   }
  }
 }
}
