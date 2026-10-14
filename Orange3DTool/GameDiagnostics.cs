using System;using System.Collections.Generic;using System.Globalization;using System.IO;using System.Linq;using System.Runtime.InteropServices;using System.Text;using System.Text.RegularExpressions;using Microsoft.Win32.SafeHandles;
namespace PZ3D {
 sealed class GameDiagnosticFinding {public string id,severity,confidence,title,message,source,nextStep;public string[] evidence;public bool historical;}
 sealed class GameDiagnosticWarning {public string id,source,message;}
 sealed class GameDiagnosticSource {public string id,kind,label,modifiedUtc="",newestEvidenceUtc="",freshness="unknown",status="missing",encoding="unknown";public long bytesRead;public bool historical=true,truncated;}
 sealed class GameDiagnosticsReport {public string checkedUtc,reportText;public GameDiagnosticFinding[] findings;public GameDiagnosticWarning[] warnings;public GameDiagnosticSource[] sources;}
 // Allowlisted disk evidence only: no live session, process, Steam, registry or mutable journal probes.
 static class GameDiagnostics {
  const int FileBytes=262144,TotalBytes=2097152,EntryLimit=128,LineLimit=4096;const long FileCeiling=134217728;
  static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
  static Match Match(string s,string p){return Regex.Match(s,p,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);}
  static bool Has(string s,string p){return Match(s,p).Success;}
  static string Replace(string s,string p,string r){return Regex.Replace(s,p,r,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);}
  static string Limit(string s,int n){return s.Length<=n?s:s.Substring(0,n);}
  internal static string Sanitize(string text){try{
   text=LauncherJournal.Sanitize(text??"");
   text=Replace(text,@"\bBearer\s+[^\r\n""']+","Bearer [已隐藏]");
   text=Replace(text,@"\b(?:access[_-]?token|refresh[_-]?token|api[_-]?(?:token|key)|session[_-]?token)(?:\\*[""'])?\s*[=:][^\r\n]*","credential=[已隐藏]");
   text=Replace(text,@"\bSTEAM_[0-5]:[01]:\d+\b|\[U:[0-5]:\d+\]|\b\d{17}\b","[Steam ID 已隐藏]");
   text=Replace(text,@"\b(?:\d{1,3}\.){3}\d{1,3}\b","[IP 已隐藏]");
   text=Replace(text,@"(?<![\w])(?:[0-9a-f]{0,4}:){2,}[0-9a-f:.%]*","[IP 已隐藏]");
   text=Replace(text,@"(?:[A-Z]:[\\/]|\\\\)[^\r\n""']*","[本地路径已隐藏]");
   return Replace(text,@"(?<![\w:])/(?:[^\s/""']+/)*[^\s""']*","[本地路径已隐藏]");
  }catch{return "[内容无法安全显示]";}}
  sealed class State {
   internal readonly DateTime Now;internal readonly List<GameDiagnosticFinding> Findings=new List<GameDiagnosticFinding>();internal readonly List<GameDiagnosticWarning> Warnings=new List<GameDiagnosticWarning>();internal readonly List<GameDiagnosticSource> Sources=new List<GameDiagnosticSource>();
   internal int RemainingBytes=TotalBytes,RemainingEntries=EntryLimit;
   internal State(DateTime now){Now=now.ToUniversalTime();}
   internal void Warn(string id,string source,string message){if(Warnings.Count<16&&!Warnings.Any(w=>w.id==id&&w.source==source))Warnings.Add(new GameDiagnosticWarning{id=id,source=source,message=Limit(Sanitize(message),512)});}
   internal GameDiagnosticsReport Finish(){
    var ordered=Findings.OrderBy(f=>f.severity=="error"?0:f.severity=="warning"?1:2).ThenBy(f=>Sources.FindIndex(s=>s.id==f.source)).ThenBy(f=>f.id,StringComparer.Ordinal).Take(24).ToArray();
    var b=new StringBuilder("本报告仅分析有限的本地磁盘证据；日志新旧不证明当前游戏会话。不能据此诊断网络延迟、GPU 瓶颈或完整根因。没有发现仅表示未识别到已收集证据中的已知错误。\n检查时间："+Now.ToString("o")+"\n");
    foreach(var s in Sources)b.AppendLine("来源："+s.label+" · "+s.status+" · "+s.freshness+" · "+s.encoding+" · "+s.bytesRead+" bytes"+(s.truncated?" · 仅保留有界片段":""));
    foreach(var f in ordered){b.AppendLine("["+f.severity+" / "+f.confidence+(f.historical?" / 历史或年代未核实":" / 近期磁盘证据")+"] "+f.title+" · "+f.source);b.AppendLine(f.message);foreach(string line in f.evidence)b.AppendLine("  "+line);b.AppendLine("建议："+f.nextStep);}
    foreach(var w in Warnings)b.AppendLine("提醒："+w.source+" · "+w.message);
    return new GameDiagnosticsReport{checkedUtc=Now.ToString("o"),findings=ordered,warnings=Warnings.ToArray(),sources=Sources.ToArray(),reportText=Limit(b.ToString(),48000)};
   }
  }
  sealed class Candidate {internal string Path;internal GameDiagnosticSource Source;internal DateTime Modified;internal bool Previous;}
  static Candidate At(string path,string id,string kind,string label,bool previous=false){return new Candidate{Path=path,Previous=previous,Source=new GameDiagnosticSource{id=id,kind=kind,label=label}};}
  internal static void ValidatePayload(Dictionary<string,object> payload){if(payload!=null&&payload.Count!=0)throw new Exception("日志诊断只接受空对象；界面不能提供路径、采集参数、修复或上传指令。");}
  public static GameDiagnosticsReport Read(Engine e){var state=new State(DateTime.UtcNow);try{
   if(e==null)throw new ArgumentException();
   Collect(state,At(Path.Combine(e.User,"console.txt"),"console","game-log","游戏控制台"));Collect(state,At(Path.Combine(e.Data,"diagnostics","launcher.log"),"launcher","launcher-log","启动器任务日志"));
   var logs=Discover(state,Path.Combine(e.User,"logs"),"user-logs",false,"game-log","游戏日志");int index=0;
   foreach(var c in logs.OrderByDescending(c=>c.Modified).ThenBy(c=>c.Path,StringComparer.Ordinal).Take(4)){c.Source.id="user-log-"+(++index);Collect(state,c);}if(logs.Count>4)state.Warn("collection-limit","collection","游戏日志超过文件数量预算，部分未读取。");
   var crash=Discover(state,e.User,"user-crashes",true,"jvm-crash","用户目录 JVM 崩溃日志").Concat(Discover(state,e.Game,"game-crashes",true,"jvm-crash","游戏目录 JVM 崩溃日志")).ToList();index=0;
   foreach(var c in crash.OrderByDescending(c=>c.Modified).ThenBy(c=>c.Path,StringComparer.Ordinal).Take(4)){c.Source.id="crash-"+(++index);Collect(state,c);}if(crash.Count>4)state.Warn("collection-limit","collection","崩溃日志超过文件数量预算，部分未读取。");
   Collect(state,At(Path.Combine(e.Data,"diagnostics","launcher.log.previous"),"launcher-previous","launcher-log","启动器上一份任务日志",true));
  }catch{state.Warn("collection-incomplete","collection","部分已知来源无法完成安全检查；本报告不代表来源完整或游戏正常。");}return state.Finish();}
  static List<Candidate> Discover(State state,string root,string id,bool crashes,string kind,string label){var result=new List<Candidate>();try{
   LauncherMaintenance.NoReparse(root);if(!Directory.Exists(root))return result;
   foreach(string entry in Directory.EnumerateFileSystemEntries(root)){
    if(state.RemainingEntries--<=0){state.RemainingEntries=0;state.Warn("collection-limit","collection","目录发现达到累计条目上限，来源清单可能不完整。");break;}
    string name=Path.GetFileName(entry),ext=Path.GetExtension(name);if(crashes?!Has(name,@"^hs_err_pid[0-9]+\.log$"):!(ext.Equals(".txt",StringComparison.OrdinalIgnoreCase)||ext.Equals(".log",StringComparison.OrdinalIgnoreCase)))continue;
    try{LauncherMaintenance.NoReparse(entry);var info=new FileInfo(entry);if((info.Attributes&FileAttributes.Directory)!=0)continue;var c=At(entry,id,kind,label);c.Modified=info.LastWriteTimeUtc;result.Add(c);}catch{state.Warn("source-reparse",id,"一个候选来源未通过路径检查；未跟随该来源。");}
   }
  }catch{state.Warn("source-reparse",id,"已知日志目录不可安全枚举，可能含重解析点或无法访问；未继续读取。");}return result;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint length,uint flags);
  static void VerifyHandle(FileStream file,string expected){var b=new StringBuilder(32768);uint count=GetFinalPathNameByHandle(file.SafeFileHandle,b,(uint)b.Capacity,0);if(count==0||count>=b.Capacity)throw new IOException();string actual=b.ToString();if(actual.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))actual=@"\\"+actual.Substring(8);else if(actual.StartsWith(@"\\?\",StringComparison.Ordinal))actual=actual.Substring(4);if(!string.Equals(Path.GetFullPath(expected),actual,StringComparison.OrdinalIgnoreCase))throw new IOException();}
  static byte[] ReadBytes(State state,GameDiagnosticSource source,FileStream file,long offset,int count){var bytes=new byte[count];file.Position=offset;int got=0,n;while(got<count&&(n=file.Read(bytes,got,count-got))>0){got+=n;source.bytesRead+=n;state.RemainingBytes-=n;}if(got!=count)throw new IOException();return bytes;}
  static void Collect(State state,Candidate c){var source=c.Source;if(state.Sources.Count>=12){state.Warn("collection-limit","collection","日志来源数量超过上限，未继续读取。");return;}state.Sources.Add(source);
   try{LauncherMaintenance.NoReparse(c.Path);}catch{source.status="rejected";state.Warn("source-reparse",source.id,"来源路径含重解析点或无法安全核对；未读取。");return;}
   try{
    if(!File.Exists(c.Path)){if(Directory.Exists(c.Path)){source.status="rejected";state.Warn("source-unreadable",source.id,"预期文本日志位置是目录，未读取。");}return;}
    using(var file=new FileStream(c.Path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
     VerifyHandle(file,c.Path);long length=file.Length;DateTime modified=File.GetLastWriteTimeUtc(c.Path);source.modifiedUtc=modified.ToString("o");
     if(length>FileCeiling){source.status="rejected";state.Warn("source-oversized",source.id,"文本来源超过安全大小上限，未读取。");return;}
     int allowance=Math.Min(FileBytes,state.RemainingBytes);if(allowance<4){source.status="skipped";state.Warn("collection-limit","collection","累计读取预算已用完，部分来源未核实。");return;}int retained=(int)Math.Min(length,allowance);source.truncated=length>retained;
     var head=ReadBytes(state,source,file,0,Math.Min(source.kind=="jvm-crash"?8192:3,retained));Encoding decoder;int bom=Bom(head,out decoder,out source.encoding);bool legacy=false;
     if(!source.truncated){byte[] rest=ReadBytes(state,source,file,head.Length,retained-head.Length);byte[] all=head.Concat(rest).ToArray();AnalyzeInto(state,source,Decode(all,bom,all.Length-bom,decoder,bom==0,ref legacy),modified,c.Previous);}
     else{
      int tailSize=retained-head.Length;long offset=length-tailSize;if(source.encoding.StartsWith("utf-16",StringComparison.Ordinal)&&((offset-bom)&1)!=0){offset++;tailSize--;}
      byte[] tail=ReadBytes(state,source,file,offset,tailSize);int start=TailLineStart(tail,source.encoding);if(start<0)start=tail.Length;int tailEnd=HeadLineEnd(tail,source.encoding);string tailText=Decode(tail,start,Math.Max(0,tailEnd-start),decoder,bom==0,ref legacy);
      if(source.kind=="jvm-crash"){int end=HeadLineEnd(head,source.encoding);bool headLegacy=false;string headText=Decode(head,bom,Math.Max(0,end-bom),decoder,bom==0,ref headLegacy);legacy|=headLegacy;var tailLines=tailText.Split('\n');int keep=Math.Max(0,LineLimit-headText.Split('\n').Length-1);if(tailLines.Length>keep){tailText=string.Join("\n",tailLines.Skip(tailLines.Length-keep));state.Warn("collection-limit",source.id,"尾部行数超过分析上限，已为崩溃头部保留分析预算。");}AnalyzeInto(state,source,headText+"\n"+tailText,modified,c.Previous);state.Warn("source-truncated",source.id,"崩溃日志仅保留有界头部及尾部完整行，中间部分未分析。");}
      else{AnalyzeInto(state,source,tailText,modified,c.Previous);state.Warn("source-truncated",source.id,"日志仅保留有界尾部完整行，较早内容未分析。");}
     }
     if(legacy){source.encoding="gb18030";state.Warn("legacy-encoding",source.id,"非 BOM 日志不是有效 UTF-8，已严格按 GB18030 读取；请核对原始日志编码。");}source.status="read";
    }
   }catch(DecoderFallbackException){source.status="rejected";state.Findings.RemoveAll(f=>f.source==source.id);state.Warn("source-encoding",source.id,"日志编码无效或为二进制，未用于结论。");}catch{source.status="unreadable";state.Findings.RemoveAll(f=>f.source==source.id);state.Warn("source-unreadable",source.id,"来源无法读取或读取期间已改变；未将不可读来源视为正常。");}
  }
  static int Bom(byte[] head,out Encoding encoding,out string name){encoding=new UTF8Encoding(false,true);name="utf-8";if(head.Length>=3&&head[0]==239&&head[1]==187&&head[2]==191)return 3;if(head.Length>=2&&head[0]==255&&head[1]==254){encoding=new UnicodeEncoding(false,false,true);name="utf-16le";return 2;}if(head.Length>=2&&head[0]==254&&head[1]==255){encoding=new UnicodeEncoding(true,false,true);name="utf-16be";return 2;}return 0;}
  static string Decode(byte[] data,int offset,int count,Encoding encoding,bool fallback,ref bool legacy){string text;try{text=encoding.GetString(data,offset,count);}catch(DecoderFallbackException){if(!fallback)throw;text=Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback).GetString(data,offset,count);legacy=true;}if(text.Any(c=>c=='\0'||(char.IsControl(c)&&c!='\r'&&c!='\n'&&c!='\t')))throw new DecoderFallbackException();return text;}
  static int TailLineStart(byte[] data,string name){if(name=="utf-16le"||name=="utf-16be"){for(int i=0;i+1<data.Length;i+=2)if(name=="utf-16le"?data[i]==10&&data[i+1]==0:data[i]==0&&data[i+1]==10)return i+2;}else for(int i=0;i<data.Length;i++)if(data[i]==10)return i+1;return -1;}
  static int HeadLineEnd(byte[] data,string name){if(name=="utf-16le"||name=="utf-16be"){for(int i=data.Length-2;i>=0;i-=2)if(name=="utf-16le"?data[i]==10&&data[i+1]==0:data[i]==0&&data[i+1]==10)return i+2;}else for(int i=data.Length-1;i>=0;i--)if(data[i]==10)return i+1;return 0;}
  internal static GameDiagnosticsReport Analyze(string text,DateTime now){var state=new State(now);var source=new GameDiagnosticSource{id="console",kind="game-log",label="游戏控制台",status="read",encoding="utf-8"};state.Sources.Add(source);AnalyzeInto(state,source,Limit(text??"",FileBytes),null,false);return state.Finish();}
  static DateTime? Timestamp(State state,GameDiagnosticSource source,string line,out bool unverified){unverified=false;var iso=Match(line,@"^\s*(\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?(?:Z|[+-]\d\d:\d\d))");var pz=Match(line,@"^(?:LOG|ERROR|WARN)\s*:.*?>\s*(\d\d-\d\d-\d{4} \d\d:\d\d:\d\d)");DateTime value;bool recognizable=iso.Success||pz.Success||Has(line,@"^(?:LOG|ERROR|WARN)\s*:.*?\bt:\s*\d+");
   if(iso.Success&&DateTime.TryParse(iso.Groups[1].Value,CultureInfo.InvariantCulture,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out value)){if(value<=state.Now.AddMinutes(5))return value;}
   else if(pz.Success&&DateTime.TryParseExact(pz.Groups[1].Value,"dd-MM-yyyy HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out value)&&!TimeZoneInfo.Local.IsInvalidTime(value)&&!TimeZoneInfo.Local.IsAmbiguousTime(value)){value=TimeZoneInfo.ConvertTimeToUtc(value);if(value<=state.Now.AddMinutes(5))return value;}
   if(recognizable){unverified=true;state.Warn("source-age-unknown",source.id,"证据时间格式无法核实、时间在未来或本地时间不明确；未沿用文件修改时间证明近期。");}return null;
  }
  static string Body(string line){line=Replace(line,@"^\s*\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?(?:Z|[+-]\d\d:\d\d)\s*","");line=Replace(line,@"^(?:LOG|ERROR|WARN)\s*:.*?>\s*","");line=Replace(line,@"^(?:OpenJDK|Java HotSpot\(TM\)) (?:64-Bit )?Server VM warning:\s*(?:INFO:\s*)?","");return Replace(line,@"^\d\d-\d\d-\d{4} \d\d:\d\d:\d\d\s*","").Trim();}
  static void AnalyzeInto(State state,GameDiagnosticSource source,string text,DateTime? modified,bool previous){
   var lines=text.Replace("\r\n","\n").Replace('\r','\n').Split('\n');if(lines.Length>LineLimit){state.Warn("collection-limit",source.id,"日志行数超过分析上限，仅分析最后的有界行。");lines=lines.Skip(lines.Length-LineLimit).ToArray();}DateTime? context=null,newest=null;bool contextUnknown=false,precedingHistorical=true;string preceding="",precedingRaw="";
   foreach(string raw in lines){if(raw.Length>4096){state.Warn("collection-limit",source.id,"一行日志超过安全长度上限，已略过该行。");preceding=precedingRaw="";continue;}
    bool unverified;DateTime? at=Timestamp(state,source,raw,out unverified);if(unverified){context=null;contextUnknown=true;}else if(at.HasValue){context=at;contextUnknown=false;if(!newest.HasValue||at.Value>newest.Value)newest=at;}
    DateTime? age=at??(contextUnknown?null:context??modified);bool historical=previous||!age.HasValue||age.Value<state.Now.AddHours(-24)||age.Value>state.Now.AddMinutes(5);string body=Body(raw);string rule=Rule(body,precedingHistorical==historical?preceding:"",source.kind);bool contextual=rule=="instrumentation-failure"&&Has(body,@"^at\s+");string[] support=contextual?new[]{precedingRaw,raw}:new[]{raw};preceding=body;precedingRaw=raw;precedingHistorical=historical;if(rule==null)continue;
    var existing=state.Findings.FirstOrDefault(f=>f.id==rule&&f.source==source.id);if(existing==null){if(state.Findings.Count>=24){state.Warn("collection-limit","collection","诊断发现达到数量上限，部分附加错误未展示。");continue;}existing=Finding(rule,source.id);state.Findings.Add(existing);}else if(existing.historical!=historical||contextual)existing.evidence=new string[0];existing.evidence=(existing.evidence??new string[0]).Concat(support.Select(line=>Limit(Sanitize(line),384))).Distinct().Reverse().Take(3).Reverse().ToArray();existing.historical=historical;
   }
   DateTime? sourceAge=newest??(contextUnknown?null:modified);source.newestEvidenceUtc=newest.HasValue?newest.Value.ToString("o"):"";source.historical=previous||!sourceAge.HasValue||sourceAge.Value<state.Now.AddHours(-24)||sourceAge.Value>state.Now.AddMinutes(5);source.freshness=!sourceAge.HasValue?"unknown":source.historical?"historical":"recent";if(!sourceAge.HasValue)state.Warn("source-age-unknown",source.id,"没有可核实的日志时间；磁盘证据年代未知，不代表本次游戏会话。");
  }
  static string Rule(string body,string previous,string kind){
   if(kind=="launcher-log"){if(Has(body,@"^(?:.*?·\s*)?Steam (?:订阅状态同步超时.*phase=subscription-sync|订阅被拒绝.*phase=subscription-result|订阅确认超时)"))return "subscription-not-confirmed";if(Has(body,@"^.+?·\s*(?:failed|partial)\s*·"))return "launcher-task-failure";return null;}
   if(Has(body,@"^(?:#\s*)?(?:agent library failed to init\s*:\s*instrument|Could not find agent library\s+|Error opening zip file or JAR manifest missing\s*:)"))return "agent-load-failure";
   if(Has(body,@"^\*+\s*java\.lang\.instrument\s+ASSERTION FAILED"))return "instrumentation-failure";
   if(Has(body,@"^at\s+(?:java\.instrument/)?sun\.instrument\.InstrumentationImpl\.loadAgent[0-9]*\(")&&Has(previous,@"^(?:Caused by:\s*)?java\.[\w.]+(?:Exception|Error)\b.*agent"))return "instrumentation-failure";
   if(Has(body,@"^(?:(?:ERROR|WARN)\s*:\s*|Caused by:\s*|Exception in thread\s+""[^""]*""\s*)?java\.lang\.OutOfMemoryError\s*:\s*(?:Java heap space|GC overhead limit exceeded)\b"))return "java-heap-exhausted";
   if(Has(body,@"^(?:#\s*)?(?:Native memory allocation\b.*\bfailed\b|os::commit_memory\b.*\bfailed\b|There is insufficient memory for the Java Runtime Environment\b|Could not reserve enough space for\b.*\bobject heap\b)"))return "native-memory-allocation";
   if(Has(body,@"^(?:(?:ERROR|WARN)\s*:\s*|Caused by:\s*|Exception in thread\s+""[^""]*""\s*)?java\.lang\.(?:ClassNotFoundException|NoClassDefFoundError)\s*:\s*\S+"))return "missing-java-class";
   if(Has(body,@"^(?:(?:ERROR|WARN)\s*:\s*)?(?:required mod\s+""[^""]+""\s+not found|missing required (?:mod |mod dependency|dependency)\b)"))return "missing-mod-dependency";
   if(Has(body,@"^(?:(?:ERROR|WARN)\s*:\s*)?duplicate mod(?:\s+ID)?\s*[:=]\s*\S+"))return "duplicate-mod";
   if(Has(body,@"^#\s*EXCEPTION_ACCESS_VIOLATION\s+\(0x[0-9a-f]+\)"))return "native-access-violation";return null;
  }
  static GameDiagnosticFinding Finding(string rule,string source){string title,message,next,severity="error";switch(rule){
   case "agent-load-failure":title="JVM 加载代理失败";message="日志明确记录代理初始化或载入失败；尚不能确认是哪个模组或配置导致。";next="人工检查启动代理参数及对应加载器文件是否匹配当前版本。";break;
   case "instrumentation-failure":title="Java instrumentation 错误";message="日志记录 instrumentation 断言或加载代理异常；旧代理、冲突或版本问题仍属可能原因。";next="核对最近变更和启动代理配置，保留该异常行供进一步排查。";break;
   case "java-heap-exhausted":title="Java 堆内存错误";message="日志明确记录 JVM 堆空间或 GC 开销错误；这不证明系统交换空间不足。";next="人工核对游戏负载、模组使用和配置的堆内存，结合系统余量判断。";break;
   case "native-memory-allocation":title="JVM 内存分配或提交失败";message="日志明确记录分配或预留失败；系统提交限制、可用内存或堆设置是待核实原因，未确认页面文件故障。";next="人工核对系统内存及提交用量、游戏堆设置；本工具不会修改页面文件。";break;
   case "missing-java-class":title="Java 类加载错误";message="日志明确记录类无法加载或初始化；依赖缺失或版本不匹配需进一步核对，命名空间不证明某个模组是元凶。";next="按异常中的类名核对依赖与兼容版本；初始化错误不等于类文件缺失。";break;
   case "missing-mod-dependency":title="游戏报告模组依赖未满足";message="游戏日志记录必需模组或依赖未找到；没有据此安装或更改任何模组。";next="人工核对所需模组 ID、已安装版本和加载列表。";break;
   case "duplicate-mod":severity="warning";title="游戏报告重复模组标识";message="日志记录重复模组 ID；这本身不证明是崩溃原因。";next="人工检查该标识的本地来源及加载清单，先核对再决定处理。";break;
   case "native-access-violation":title="JVM 原生访问冲突";message="崩溃日志记录原生访问冲突；驱动或原生库问题仅为可能原因，不能归因某模组或判断 GPU 性能。";next="保留错误签名，人工核对发生时间、版本与最近变更。";break;
   case "subscription-not-confirmed":severity="warning";title="启动器订阅确认记录未完成";message="记录中的启动器任务未能确认订阅标志或结果；这不证明当前账号仍未订阅，也不是游戏崩溃证据。";next="在 Steam 中人工刷新当前订阅及工坊状态，再核对启动器任务结果。";break;
   default:severity="warning";title="启动器任务记录失败或部分完成";message="日志记录启动器操作失败或部分完成；该记录不等于游戏加载失败或游戏崩溃。";next="人工查看对应任务及当前文件状态，再决定是否重试。";break;
  }return new GameDiagnosticFinding{id=rule,severity=severity,confidence="confirmed",title=Sanitize(title),message=Sanitize(message),source=source,nextStep=Sanitize(next)};}
 }
}
