using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
namespace PZ3D {
 // Syntax and state evidence only. MOD names are data, never a diagnosis rule.
 static class GameLogAnalyzer {
  const int MaxIssues=80,MaxFrames=16,MaxEvidence=12,MaxLine=8192,MaxEvents=8192;
  const long FileChars=67108864,TotalChars=268435456;
  static readonly TimeSpan Timeout=TimeSpan.FromMilliseconds(100);
  static readonly Dictionary<string,Regex> Patterns=new Dictionary<string,Regex>();
  static Regex Pattern(string p){lock(Patterns){Regex r;if(!Patterns.TryGetValue(p,out r)){r=new Regex(p,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,Timeout);Patterns[p]=r;}return r;}}
  static Match Match(string s,string p){return Pattern(p).Match(s??"");}
  static bool Has(string s,string p){return Match(s,p).Success;}
  static string Replace(string s,string p,string replacement){return Pattern(p).Replace(s,replacement);}
  static bool Contains(string s,string part){return s.IndexOf(part,StringComparison.OrdinalIgnoreCase)>=0;}
  static string Limit(string s,int n){s=s??"";return s.Length>n?s.Substring(0,n):s;}
  internal static string Sanitize(string text){try{
   string s=Limit(text,MaxLine);
   s=Replace(s,@"\b(?:password|passwd|userPassword|serverPassword|username|user[_-]?name|account|login|token|ticket|authorization|userCipher|serverCipher|access[_-]?token|refresh[_-]?token|api[_-]?(?:token|key)|session[_-]?token)(?:\\*[""'])?\s*[=:]\s*[^\r\n]*","credential=[已隐藏]");
   s=Replace(s,@"\bBearer\s+[^\r\n""']+","Bearer [已隐藏]");
   s=Replace(s,@"\bSTEAM_[0-5]:[01]:\d+\b|\[U:[0-5]:\d+\]|\b\d{17}\b","[Steam ID 已隐藏]");
   s=Replace(s,@"\b(?:\d{1,3}\.){3}\d{1,3}\b","[IP 已隐藏]");
   s=Replace(s,@"(?<![\w])(?:[0-9a-f]{0,4}:){2,}[0-9a-f:.%]*","[IP 已隐藏]");
   s=Replace(s,@"(?:[A-Z]:[\\/]|\\\\)[^\r\n""'<>]*","[本地路径已隐藏]");
   s=Replace(s,@"(?<![\w:])/(?:[^\s/""']+/)*[^\s""']*","[本地路径已隐藏]");
   return new string(s.Where(c=>!char.IsControl(c)||c=='\t').ToArray());
  }catch(RegexMatchTimeoutException){return "[内容无法安全显示]";}}
  sealed class Event {internal string Kind,Source,Text;internal int Line,Order;internal DateTime? Time;internal GameLogAttempt Attempt;}
  sealed class Session {internal string Key;internal readonly List<Event> Events=new List<Event>();internal readonly List<Block> Blocks=new List<Block>();internal readonly List<GameLogAttempt> Attempts=new List<GameLogAttempt>();}
  sealed class Block {internal string Message,Severity,Type,Source;internal int Line,Order;internal DateTime? Time;internal bool Runtime,Required;internal readonly List<GameLogFrame> Frames=new List<GameLogFrame>();internal readonly List<GameLogEvidence> Evidence=new List<GameLogEvidence>();}
  sealed class Analysis {
   internal readonly GameLogReport Report=new GameLogReport{checkedUtc=DateTime.UtcNow.ToString("o"),origin="imported",state="unknown",stage="unknown"};
   internal readonly List<GameLogSource> Sources=new List<GameLogSource>();internal readonly List<string> Warnings=new List<string>();internal readonly List<Session> Sessions=new List<Session>();internal readonly Dictionary<string,Session> SessionMap=new Dictionary<string,Session>();internal long Chars;internal int Order;internal CancellationToken Cancellation;
   internal void Warn(string text){Report.incomplete=true;if(Warnings.Count<24&&!Warnings.Contains(text))Warnings.Add(text);}
   internal Session GetSession(string key){key=key??"";Session session;if(!SessionMap.TryGetValue(key,out session)){session=new Session{Key=Sanitize(key)};SessionMap[key]=session;Sessions.Add(session);}return session;}
   internal void AddEvent(Session session,string kind,string source,int line,DateTime? time,string raw){if(session.Events.Count>=MaxEvents){Warn("状态事件达到上限，后续尝试结论可能不完整。");return;}session.Events.Add(new Event{Kind=kind,Source=source,Line=line,Time=time,Order=Order,Text=kind=="thread-exit"?Limit(Sanitize(raw),800):""});}
  }
  internal static GameLogReport AnalyzeText(string text,string origin){string value=text??"";var report=Analyze(new[]{new GameLogInput{Id="buffer",Name="日志片段",Kind=origin??"buffer",SessionKey="buffer",Length=value.Length,Encoding="text",Open=()=>new StringReader(value)}},CancellationToken.None,null);report.origin=Sanitize(origin??"buffer");report.reportText=Export(report);return report;}
  internal static GameLogReport Analyze(IEnumerable<GameLogInput> inputs,CancellationToken cancellation,Action<int,string> progress){
   cancellation.ThrowIfCancellationRequested();var a=new Analysis{Cancellation=cancellation};int index=0;
   foreach(var input in inputs??Enumerable.Empty<GameLogInput>()){
    cancellation.ThrowIfCancellationRequested();if(index>=128||a.Chars>=TotalChars){a.Warn("输入数量或读取总量达到上限，部分日志未分析。");break;}if(input==null)continue;index++;
    string sourceId=Sanitize(Limit(input.Id??("source-"+index),160));var source=new GameLogSource{id=sourceId,name=Sanitize(Limit(input.Name??"日志",300)),kind=Sanitize(Limit(input.Kind??"unknown",64)),encoding=Sanitize(Limit(input.Encoding??"unknown",64)),modifiedUtc=Sanitize(Limit(input.ModifiedUtc,64)),status="read",truncated=input.Truncated};a.Sources.Add(source);if(index==1)a.Report.origin=source.kind;
    if(input.Truncated)a.Warn("来源 "+source.name+" 已截断，结论仅覆盖提供的内容。");if(!string.IsNullOrWhiteSpace(input.Warning))a.Warn(Sanitize(input.Warning));
    var session=a.GetSession(input.SessionKey??input.Id??("source-"+index));
    try{using(var reader=input.Open==null?null:input.Open()){if(reader==null)throw new IOException("没有可读内容");Read(a,session,source,reader,cancellation);}}
    catch(OperationCanceledException){throw;}catch(Exception ex){source.status="unreadable";a.Warn("来源 "+source.name+" 未能完整读取："+Sanitize(ex.Message));}
    if(source.status=="read"&&!source.truncated&&input.Length>0){a.Report.bytesRead+=input.Length-source.bytesRead;source.bytesRead=input.Length;}
    if(progress!=null)progress(Math.Min(95,index),"已分析 "+index+" 个来源");
   }
   cancellation.ThrowIfCancellationRequested();return Finish(a);
  }
  // Read bounded characters rather than TextReader.ReadLine: malicious single lines cannot allocate arbitrarily.
  static void Read(Analysis a,Session session,GameLogSource source,TextReader reader,CancellationToken cancellation){
   var chars=new char[4096];var line=new StringBuilder();int lineNumber=1;long sourceChars=0;bool longLine=false;Block active=null;var preceding=new List<GameLogFrame>();DateTime? lastTime=null;
   Action consume=()=>{a.Order++;try{ParseLine(a,session,source.id,lineNumber,line.ToString(),ref active,preceding,ref lastTime);}catch(RegexMatchTimeoutException){a.Warn("部分复杂日志行超过解析时间限制。");}line.Length=0;longLine=false;lineNumber++;};
   int count;while((count=reader.Read(chars,0,chars.Length))>0){cancellation.ThrowIfCancellationRequested();for(int i=0;i<count;i++){if((i&1023)==0)cancellation.ThrowIfCancellationRequested();if(sourceChars>=FileChars||a.Chars>=TotalChars){source.truncated=true;a.Warn("读取容量达到上限，来源已截断。");goto done;}sourceChars++;a.Chars++;char c=chars[i];if(c=='\n'){consume();continue;}if(c=='\r')continue;if(line.Length<MaxLine)line.Append(c);else if(!longLine){longLine=true;source.truncated=true;a.Warn("过长日志行已截断，后续行仍会继续分析。");}}}
   done: if(line.Length>0)consume();Flush(a,session,ref active);source.bytesRead=sourceChars; // decoded character count is a lower bound; service replaces it with actual byte accounting.
   a.Report.bytesRead+=source.bytesRead;
  }
  static string Severity(string line){var m=Match(line,@"^\s*(ERROR|WARN(?:ING)?|FATAL)\s*[:\[]");return !m.Success?"":m.Groups[1].Value.StartsWith("WARN",StringComparison.OrdinalIgnoreCase)?"warning":"error";}
  static string Body(string line){var m=Match(line,@"^\s*(?:ERROR|WARN(?:ING)?|FATAL|LOG|DEBUG|INFO)\s*:\s*[^>]*>\s*(.*)$");if(m.Success)return m.Groups[1].Value.Trim();return Replace(line,@"^\s*(?:ERROR|WARN(?:ING)?|FATAL|LOG|DEBUG|INFO)\s*:\s*","").Trim();}
  static DateTime? Timestamp(string line){var m=Match(line,@"\b(\d{2}-\d{2}-(?:\d{4}|\d{2}))[ T](\d{2}:\d{2}:\d{2}(?:\.\d+)?)\b");DateTime value;if(m.Success&&DateTime.TryParseExact(m.Groups[1].Value+" "+m.Groups[2].Value,new[]{"dd-MM-yyyy HH:mm:ss","dd-MM-yyyy HH:mm:ss.fff","dd-MM-yy HH:mm:ss","dd-MM-yy HH:mm:ss.fff"},CultureInfo.InvariantCulture,DateTimeStyles.None,out value))return value;m=Match(line,@"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?");if(m.Success&&DateTime.TryParse(m.Value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out value))return value;return null;}
  static GameLogFrame Frame(string line){Match m=Match(line,@"Lua\(\(MOD:(.*?)\)\)\.(.*?)\((.*?):(\d+)\)");if(m.Success)return NewFrame(m.Groups[1].Value,m.Groups[3].Value,m.Groups[2].Value,m.Groups[4].Value);m=Match(line,@"function:\s*(.*?)\s*--\s*file:\s*(.*?)\s+line\s*#\s*(\d+)(?:\s*\|\s*MOD:\s*(.*))?");if(m.Success)return NewFrame(m.Groups[4].Value,m.Groups[2].Value,m.Groups[1].Value,m.Groups[3].Value);m=Match(line,@"(?:^|\s)(?:at\s+)?([\w.$/]+)\(([^():]+\.(?:java|lua)):(\d+)\)");if(m.Success)return NewFrame("",m.Groups[2].Value,m.Groups[1].Value,m.Groups[3].Value);return null;}
  static GameLogFrame NewFrame(string mod,string file,string function,string number){int line;int.TryParse(number,out line);return new GameLogFrame{mod=Sanitize(Limit(mod.Trim(),200)),file=Sanitize(Limit(file.Trim(),300)),function=Sanitize(Limit(function.Trim(),200)),line=line};}
  static void AddFrame(List<GameLogFrame> frames,GameLogFrame frame){if(frame==null||frames.Count>=MaxFrames||frames.Any(f=>f.mod==frame.mod&&f.file==frame.file&&f.function==frame.function&&f.line==frame.line))return;frames.Add(frame);}
  static void Evidence(Block block,string source,int line,string text){if(block.Evidence.Count<MaxEvidence)block.Evidence.Add(new GameLogEvidence{source=source,line=line,text=Limit(Sanitize(text),800)});}
  static void EssentialEvidence(Block block,string source,int line,string text){if(block.Evidence.Any(e=>e.source==source&&e.line==line))return;var evidence=new GameLogEvidence{source=source,line=line,text=Limit(Sanitize(text),800)};if(block.Evidence.Count<MaxEvidence)block.Evidence.Add(evidence);else block.Evidence[MaxEvidence-1]=evidence;}
  static bool Header(string body){return body.Length==0||Has(body,@"^(?:[-=]{3,}|STACK TRACE\s*:?|Stack trace\s*:?)$")||Contains(body,"dumping Lua stack trace")||Has(body,@"(?:^|>)\s*Exception thrown\b");}
  static string Classify(string message){
   if(Has(message,@"attempted index:.*(?:non-table:\s*(?:null|nil)|nil)|attempt to index.*(?:nil|null)"))return "lua-null-index";
   if(Contains(message,"Object tried to call nil")||Has(message,@"attempt to call.*nil"))return "lua-call-nil";
   if(Contains(message,"__len not defined")||Has(message,@"attempt to get length.*nil"))return "lua-length";
   if(Contains(message,"Cannot load world")||Contains(message,"WorldDictionaryException"))return "world-load";
   if(Contains(message,"OutOfMemoryError")&&(Contains(message,"heap")||Contains(message,"GC overhead")))return "java-heap";
   if(Has(message,@"Native memory allocation.*failed|os::commit_memory.*failed|Could not reserve enough space|insufficient memory for the Java Runtime"))return "native-memory";
   if(Has(message,@"EXCEPTION_ACCESS_VIOLATION|SIGSEGV|A fatal error has been detected by the Java Runtime"))return "native-crash";
   if(Has(message,@"Required Animation Asset not found|NoSuchFileException|FileNotFoundException|Missing dictionary script on client|(?:required|missing) (?:animation |texture |sound )?(?:asset|resource).*(?:not found|missing)"))return "missing-resource";
   if(Has(message,@"NoClassDefFoundError|ClassNotFoundException|UnsatisfiedLinkError"))return "java-dependency";
   if(Has(message,@"required mod\b.*(?:not found|missing)|mod\b.*requires\b.*(?:missing|not found)|missing (?:required )?(?:mod|dependency)"))return "missing-dependency";
   if(Has(message,@"duplicate mod (?:ID|identifier)"))return "duplicate-mod";
   if(Has(message,@"checksum.*(?:mismatch|does not match|failed)|(?:mismatch|failed).*checksum"))return "checksum";
   if(Has(message,@"Workshop.*(?:download|item).*(?:failed|failure|error)|Workshop.*(?:failed|failure).*download"))return "workshop";
   if(Has(message,@"Error occurred during initialization of VM|agent library failed to init|Error opening zip file or JAR manifest missing|Could not find agent library"))return "jvm-startup";
   if(Has(message,@"\b(?:[\w$]+\.)+[\w$]*(?:Exception|Error)\b"))return "java-exception";
   return "unclassified";
  }
  static bool ActualExceptionStart(string body){return Has(body,@"^(?:(?:Caused by|Suppressed):\s*|Exception in thread\s+[""'][^""']+[""']\s+|Unhandled\s+)?(?:[\w$]+\.)+[\w$]*(?:Exception|Error)(?::|\s|$)");}
  static bool ActualFatalStatement(string body){return Has(body,@"^(?:#\s*)?(?:Cannot load world\b|Native memory allocation\b|os::commit_memory\b|Could not reserve enough space\b|There is insufficient memory for the Java Runtime|Error occurred during initialization of VM\b|agent library failed to init\b|Error opening zip file or JAR manifest missing\b|Could not find agent library\b|EXCEPTION_ACCESS_VIOLATION\b|SIGSEGV\b|A fatal error has been detected by the Java Runtime\b)");}
  static string ObservedType(string body,string severity){string type=Classify(body);if(type=="unclassified")return type;
   bool actualSyntax=ActualExceptionStart(body)||ActualFatalStatement(body);
   if(!actualSyntax&&Has(body,@"^(?:(?:optional|test|error)\s+)*(?:example|documentation|docs|sample)\b|^(?:示例|例如)"))return "unclassified";
   bool critical=type=="world-load"||type=="java-heap"||type=="native-memory"||type=="native-crash"||type=="jvm-startup"||type=="java-dependency"||type=="java-exception";
   if(critical&&!actualSyntax)return "unclassified";
   if(severity=="warning"&&(type=="world-load"||type=="jvm-startup"||type=="native-crash"))return "unclassified";
   return type;
  }
  static bool NetworkRecord(string body,string name){return Has(body,@"^(?:RakNet\s*/\s*)?"+Regex.Escape(name)+@"\s*\.?$")||Has(body,@"\bmessage\s*=\s*[""']"+Regex.Escape(name)+@"[""']");}
  static void ParseLine(Analysis a,Session session,string source,int lineNumber,string raw,ref Block active,List<GameLogFrame> preceding,ref DateTime? lastTime){
   string normalized=raw;if(raw.Length>1&&raw[0]=='['){int end=raw.IndexOf(']');if(end>0&&end<80&&Has(raw.Substring(0,end),@"\d{2}-\d{2}-\d{2,4}"))normalized=raw.Substring(end+1).TrimStart();}
   string severity=Severity(normalized),body=Body(normalized);if(severity=="error")a.Report.errorCount++;if(severity=="warning")a.Report.warningCount++;
   var time=Timestamp(raw);if(time.HasValue)lastTime=time;
   string type=ObservedType(body,severity);bool stackLine=Has(body,@"^(?:Lua\(\(MOD:|function:|(?:at\s+)?[\w.$/]+\([^)]*\)\.?\s*$)");
   GameLogFrame frame=stackLine&&(Contains(raw,".lua")||Contains(raw,".java"))?Frame(raw):null;
   string eventKind=null;
   if(NetworkRecord(body,"connection-attempt-failed"))eventKind="connection-failed";
   else if(NetworkRecord(body,"connection-request-accepted"))eventKind="accepted";
   else if(Has(body,@"^STATE:\s*(?:enter\s+(?:zombie\.gameStates\.)?IngameState|exit\s+(?:zombie\.gameStates\.)?GameLoadingState)\b")||(frame!=null&&Has(frame.function,@"(?:^|\.)IngameState\.(?:update|render|onTick)")))eventKind="entered";
   else if(type=="world-load")eventKind="world-failed";
   else if(NetworkRecord(body,"connection-lost"))eventKind="disconnected";
   else if(Has(body,@"^GameThread exited\s*\.?$")||type=="jvm-startup"||type=="native-crash")eventKind="thread-exit";
   else if(Has(body,@"^(?:STATE:\s*enter\s+(?:zombie\.gameStates\.)?GameLoadingState\b|game loading took\b)"))eventKind="loading";
   else if(NetworkRecord(body,"connection-request")||NetworkRecord(body,"connection-attempt"))eventKind="connecting";
   if(eventKind!=null)a.AddEvent(session,eventKind,source,lineNumber,lastTime,raw);
   if(active!=null&&eventKind=="entered"){active.Runtime=true;EssentialEvidence(active,source,lineNumber,raw);}
   bool header=Header(body);
   if(header){if(active!=null&&active.Evidence.Count<3)Evidence(active,source,lineNumber,raw);return;}
   bool exception=type!="unclassified";
   // A Java frame's type name is context, not another exception occurrence.
   if(frame!=null&&!Contains(raw,"Exception:")&&!Contains(raw,"Error:")&&!Has(raw,@"(?:Exception|Error)\s*[:$]")){
    if(active!=null){AddFrame(active.Frames,frame);Evidence(active,source,lineNumber,raw);}else {if(preceding.Count>=MaxFrames)preceding.RemoveAt(0);AddFrame(preceding,frame);}return;
   }
   if(exception){
    bool cause=Contains(body,"Caused by:")||Contains(body,"Suppressed:");
    bool luaCopy=active!=null&&type==active.Type&&Contains(body,active.Message)&&!Has(active.Message,@"\b(?:[\w$]+\.)+[\w$]*(?:Exception|Error)\b")&&Has(body,@"\b(?:[\w$]+\.)+[\w$]*(?:Exception|Error)\b");
    if(active!=null&&(cause||luaCopy)){if(type!="java-exception")active.Type=type;Evidence(active,source,lineNumber,raw);return;}
    Flush(a,session,ref active);active=new Block{Message=body,Severity=severity=="warning"?"warning":"error",Type=type,Source=source,Line=lineNumber,Order=a.Order,Time=lastTime,Required=severity!="warning"&&Contains(body,"Required Animation Asset")};foreach(var prior in preceding)AddFrame(active.Frames,prior);preceding.Clear();Evidence(active,source,lineNumber,raw);return;
   }
   if(severity.Length>0){Flush(a,session,ref active);active=new Block{Message=body,Severity=severity,Type="unclassified",Source=source,Line=lineNumber,Order=a.Order,Time=lastTime};foreach(var prior in preceding)AddFrame(active.Frames,prior);preceding.Clear();Evidence(active,source,lineNumber,raw);return;}
   if(active!=null){if(Contains(body,"at ")||Contains(body,"... ")||eventKind!=null)Evidence(active,source,lineNumber,raw);else Flush(a,session,ref active);}if(eventKind==null)preceding.Clear();
  }
  static void Flush(Analysis a,Session session,ref Block active){if(active==null)return;if(session.Blocks.Count<20000)session.Blocks.Add(active);else a.Warn("异常记录达到处理上限，重复次数可能偏低。");active=null;}
  static string StateSummary(string state){switch(state){case "entered":return "有明确进入世界的证据";case "entered-with-issues":return "已进入世界，同时记录到问题";case "failed":return "最后一次尝试有明确失败或阻断证据";case "disconnected":return "记录到连接丢失，原因尚未确定";default:return "没有足够证据确认进入世界或最终结果";}}
  static void Resolve(Analysis a,Session session){
   // Companion sources can interleave. Only comparable explicit timestamps may reorder them.
   var events=session.Events.ToList();if(events.Count>0&&events.All(e=>e.Time.HasValue))events=events.OrderBy(e=>e.Time.Value).ThenBy(e=>e.Order).ToList();
   else if(events.Select(e=>e.Source).Distinct().Count()>1){var worldSources=new HashSet<string>(events.Where(e=>e.Kind=="entered"||e.Kind=="world-failed").Select(e=>e.Source));events=events.OrderBy(e=>worldSources.Contains(e.Source)?1:0).ThenBy(e=>e.Order).ToList();a.Warn("配对来源缺少可比较的完整时间；网络事件未用于推翻其他来源的明确世界进展，尝试顺序可能不完整。");}
   session.Events.Clear();session.Events.AddRange(events);GameLogAttempt current=null;int number=0;
   foreach(var e in events){a.Cancellation.ThrowIfCancellationRequested();bool terminal=current!=null&&(current.state=="failed"||current.state=="disconnected");if(current==null||(terminal&&(e.Kind=="accepted"||e.Kind=="connecting"||e.Kind=="entered"))||(e.Kind=="connecting"&&current.stage!="unknown")){
     current=new GameLogAttempt{id="attempt-"+(a.Sessions.IndexOf(session)+1)+"-"+(++number),sessionKey=session.Key,state="unknown",stage="unknown",firstLine=e.Line};session.Attempts.Add(current);
    }e.Attempt=current;current.lastLine=e.Line;
    switch(e.Kind){case "connecting":case "accepted":current.stage="connection";break;case "connection-failed":current.state="failed";current.stage="connection";break;case "entered":current.state="entered";current.stage="runtime";break;case "world-failed":current.state="failed";current.stage="world-loading";break;case "disconnected":current.state="disconnected";current.stage="disconnection";break;case "loading":if(current.state!="entered")current.stage="world-loading";break;case "thread-exit":var blocking=session.Blocks.LastOrDefault(b=>(b.Required||b.Type=="jvm-startup"||b.Type=="native-crash"||b.Type=="native-memory"||b.Type=="java-heap")&&b.Source==e.Source&&e.Line>=b.Line&&e.Line-b.Line<=80&&Before(b,e));if(blocking!=null){current.state="failed";current.stage=current.stage=="unknown"?"startup":current.stage;EssentialEvidence(blocking,e.Source,e.Line,e.Text);}break;}
   }
   if(current==null){current=new GameLogAttempt{id="attempt-"+(a.Sessions.IndexOf(session)+1)+"-1",sessionKey=session.Key,state="unknown",stage="unknown",firstLine=1,lastLine=1};session.Attempts.Add(current);}
   foreach(var attempt in session.Attempts)attempt.summary=StateSummary(attempt.state);
  }
  static bool Before(Block b,Event e){return b.Time.HasValue&&e.Time.HasValue?b.Time.Value<=e.Time.Value:b.Order<=e.Order;}
  static bool EventBefore(Event e,Block block){return block.Time.HasValue&&e.Time.HasValue?e.Time.Value<=block.Time.Value:e.Order<=block.Order;}
  static GameLogAttempt AttemptFor(Session session,Block block){Event latest=session.Events.LastOrDefault(e=>EventBefore(e,block));return latest==null?session.Attempts[0]:latest.Attempt??session.Attempts[0];}
  static GameLogIssue Explain(Block b,string phase,string attempt){string title="未分类的日志提示",meaning=b.Severity=="warning"?"日志记录了一条警告；尚不能据此判断功能失效或加载失败。":"日志记录了一条错误，但现有规则无法确定具体原因和影响。",advice="结合发生阶段与前后日志复核；保留原始证据及复现步骤，不要仅凭红字删除文件或存档。";
   switch(b.Type){
    case "lua-null-index":title="Lua 脚本读取空值";var field=Match(b.Message,@"attempted index:\s*([^\r\n]+?)\s+of non-table");meaning="脚本试图从空值或非表对象读取"+(field.Success?"字段“"+Sanitize(field.Groups[1].Value)+"”":"字段")+"。这说明该次调用的数据或对象不符合脚本预期。";advice="按脚本名、函数和行号核对涉及 MOD 的游戏版本、依赖与加载顺序；更新后在备份的测试存档中逐步隔离。堆栈涉及的 MOD 不等于已证明的根因。";break;
    case "lua-call-nil":title="Lua 调用了不存在的函数";meaning="脚本试图调用 nil（空值），预期函数当时不存在或尚未初始化。日志没有证明它为何缺失。";advice="核对对应脚本行、所需依赖和适用游戏版本；在备份的测试环境逐个调整涉及的 MOD，保留能够复现的最小组合。";break;
    case "lua-length":title="Lua 对不支持的对象取长度";meaning="脚本执行长度运算时，对象不支持该操作，可能是空值或类型不符。";advice="核对堆栈首个脚本行及传入数据，检查涉及 MOD 的版本和依赖；在测试存档中复现后向作者提供证据。";break;
    case "world-load":title="世界加载被阻断";meaning="日志明确表示世界无法加载或世界字典发生异常；此前进入游戏的记录不能代表这次加载成功。";advice="先备份存档，核对客户端、服务器与存档要求的内容及版本；检查相邻的缺失字典脚本提示，再验证游戏文件。不要直接删除世界字典或存档。";break;
    case "missing-resource":title="缺少或无法读取所需资源";meaning="日志指出某个资源文件、动画或字典脚本缺失或无法读取；现有证据不能确定是安装、兼容性还是文件访问问题。";advice="先验证游戏文件，核对所需资源对应的 MOD、游戏分支和服务器内容。保留具体资源名与相邻异常，避免把缺资源直接归因为下载失败。";break;
    case "java-heap":title="Java 堆内存耗尽";meaning="JVM 报告 Java 堆空间或垃圾回收压力超限；这不能直接证明系统虚拟内存不足。";advice="记录当时的内存使用与复现步骤，减少测试环境中的高占用内容，检查堆内存设置是否适合机器的可用内存。";break;
    case "native-memory":title="JVM 无法分配系统内存";meaning="JVM 明确报告保留、提交或分配本机内存失败，可能阻断进程继续运行。";advice="检查当时可用内存和系统提交上限，关闭高占用程序并核对虚拟内存设置；不要只提高 Java 堆上限。";break;
    case "native-crash":title="本机运行时崩溃";meaning="日志包含本机访问冲突或 JVM 致命崩溃标记，尚不能据此确定驱动、原生组件或游戏代码责任。";advice="保留 hs_err 崩溃报告，核对崩溃线程与问题帧，验证游戏文件，并在最小组件环境中复现。";break;
    case "java-dependency":title="Java 类或本机库无法加载";meaning="日志明确指出所需 Java 类或动态库不可用，具体缺失组件需结合异常名称核对。";advice="验证游戏文件，核对 Java 与组件架构、版本和依赖；避免从不明来源单独下载 DLL 或类文件。";break;
    case "missing-dependency":title="缺少 MOD 依赖";meaning="日志明确提示所需 MOD 或依赖未找到，但未证明它导致了这次连接或世界加载失败。";advice="核对提示中的依赖标识、订阅内容及启用顺序，并确认客户端与服务器要求一致。";break;
    case "duplicate-mod":title="MOD 标识重复";meaning="日志发现重复的 MOD 标识，可能存在多份内容或版本重叠。";advice="核对同标识内容的来源和版本，在备份配置后保留需要的一份，并重新验证启用列表。";break;
    case "checksum":title="文件校验不一致";meaning="日志明确记录校验不匹配；不一致的具体文件与责任来源仍需前后文确认。";advice="核对客户端与服务器的游戏/MOD版本，先验证游戏文件并按服务器要求同步内容；不要关闭校验来掩盖差异。";break;
    case "workshop":title="创意工坊下载出现错误";meaning="日志明确记录工坊下载或条目处理失败，不能仅凭这条记录确定网络、磁盘或条目状态根因。";advice="检查 Steam 下载状态、可用磁盘空间和具体条目错误，待下载完成后再核对本地内容与服务器版本。";break;
    case "jvm-startup":title="Java 启动或代理加载失败";meaning="JVM 初始化或代理文件加载出现明确错误，游戏可能尚未启动。";advice="核对日志中的代理/JAR文件和启动参数，验证游戏与组件完整性，在备份设置后逐项排查。";break;
    case "java-exception":title="Java 运行时异常";meaning="日志记录 Java 异常。异常类型、消息与调用栈已保留，但现有证据不足以唯一确定根因。";advice="从异常消息和最前面的业务/脚本帧定位，结合发生阶段与复现步骤检查版本、依赖及资源。";break;
   }
   string impact=b.Type=="world-load"||b.Type=="jvm-startup"||b.Type=="native-crash"?"blocking":phase=="runtime"?"runtime":"candidate";
   return new GameLogIssue{type=b.Type,severity=b.Severity,impact=impact,title=title,meaning=meaning,advice=advice,phase=phase,attemptId=attempt,count=1,mods=b.Frames.Select(f=>f.mod).Where(m=>!string.IsNullOrEmpty(m)).Distinct().ToArray(),frames=b.Frames.ToArray(),evidence=b.Evidence.ToArray()};
  }
  static GameLogReport Finish(Analysis a){var issues=new List<GameLogIssue>();var grouped=new Dictionary<string,GameLogIssue>();foreach(var session in a.Sessions){Resolve(a,session);foreach(var block in session.Blocks){a.Cancellation.ThrowIfCancellationRequested();var attempt=AttemptFor(session,block);string phase=block.Runtime?"runtime":"unknown";var previous=session.Events.LastOrDefault(e=>EventBefore(e,block)&&e.Attempt==attempt);if(phase!="runtime"&&previous!=null)phase=previous.Kind=="entered"?"runtime":attempt.stage=="runtime"?"unknown":attempt.stage;
     var issue=Explain(block,phase,attempt.id);if(block.Required&&attempt.state=="failed"){issue.impact="blocking";if(issue.phase=="unknown")issue.phase="startup";}
     string key=attempt.id+"|"+block.Type+"|"+Sanitize(block.Message)+"|"+string.Join("|",issue.frames.Where(f=>f.mod.Length>0||f.file.EndsWith(".lua",StringComparison.OrdinalIgnoreCase)).Take(4).Select(f=>f.mod+"/"+f.file+":"+f.line+":"+f.function));GameLogIssue existing;if(grouped.TryGetValue(key,out existing)){existing.count++;if(issue.phase=="runtime"){existing.phase="runtime";if(existing.impact!="blocking")existing.impact="runtime";}continue;}
     if(issues.Count>=MaxIssues){a.Warn("问题分组达到展示上限，优先保留明确阻断与具体异常；部分不同问题未展示。");var weakest=issues.OrderBy(Priority).First();if(Priority(issue)<=Priority(weakest))continue;issues.Remove(weakest);string removed=grouped.First(p=>object.ReferenceEquals(p.Value,weakest)).Key;grouped.Remove(removed);}issues.Add(issue);grouped[key]=issue;
    }}
   for(int i=0;i<issues.Count;i++)issues[i].id="issue-"+(i+1);a.Report.issues=issues.ToArray();a.Report.sources=a.Sources.ToArray();a.Report.fileCount=a.Sources.Count;a.Report.attempts=a.Sessions.SelectMany(s=>s.Attempts).Take(512).ToArray();if(a.Sessions.Count==1){var last=a.Sessions[0].Attempts.Last();a.Report.state=last.state;a.Report.stage=last.stage;if(a.Report.state=="entered"&&issues.Any(i=>i.attemptId==last.id&&i.severity=="error"))a.Report.state="entered-with-issues";}a.Report.summary=(a.Sessions.Count>1?"包含 "+a.Sessions.Count+" 个独立会话；请分别查看各次尝试，未将它们合并为一次最终结果":StateSummary(a.Report.state))+"。发现 "+issues.Count+" 组问题；次数是日志出现次数，可能包含重复记录。";a.Report.warnings=a.Warnings.ToArray();a.Report.reportText=Export(a.Report);return a.Report;
  }
  static int Priority(GameLogIssue issue){if(issue.impact=="blocking")return 10;if(issue.type.StartsWith("lua-",StringComparison.Ordinal))return 7;if(issue.type=="java-heap"||issue.type=="native-memory")return 8;if(issue.type!="unclassified"&&issue.type!="java-exception")return 5;if(issue.type=="java-exception")return 3;return issue.severity=="error"?2:1;}
  internal static void RebuildExport(GameLogReport report){if(report!=null){report.origin=Sanitize(report.origin);report.reportText=Export(report);}}
  static string Export(GameLogReport r){var text=new StringBuilder();text.AppendLine("检查时间："+r.checkedUtc).AppendLine("来源："+r.origin).AppendLine(r.summary);
   text.AppendLine().AppendLine("来源文件（证据中的来源 ID 对照）：");int tableStart=text.Length;var usedSources=new HashSet<string>(r.issues.SelectMany(i=>i.evidence).Select(e=>e.source));
   foreach(var source in r.sources.OrderByDescending(s=>usedSources.Contains(s.id))){string row="  "+Sanitize(source.id)+" → "+Limit(Sanitize(source.name),200)+" | 编码 "+Limit(Sanitize(source.encoding),40)+" | 状态 "+Limit(Sanitize(source.status),40)+(source.truncated?" | 已截断":"");if(text.Length+row.Length-tableStart>12000){text.AppendLine("  来源表已达到导出上限；其余来源请在分析页查看。");break;}text.AppendLine(row);}
   text.AppendLine().AppendLine("会话与尝试：");tableStart=text.Length;var usedAttempts=new HashSet<string>(r.issues.Select(i=>i.attemptId));
   foreach(var attempt in r.attempts.OrderByDescending(t=>usedAttempts.Contains(t.id))){string row="  "+Sanitize(attempt.id)+" | 会话 "+Limit(Sanitize(attempt.sessionKey),120)+" | 阶段 "+Limit(Sanitize(attempt.stage),32)+" | "+Limit(Sanitize(attempt.summary),160)+" ("+Limit(Sanitize(attempt.state),32)+")";if(text.Length+row.Length-tableStart>10000){text.AppendLine("  尝试表已达到导出上限；其余尝试请在分析页查看。");break;}text.AppendLine(row);}
   foreach(var issue in r.issues.OrderByDescending(Priority)){text.AppendLine().AppendLine("["+issue.severity+" / "+issue.phase+" / "+issue.impact+"] "+issue.title+" ×"+issue.count+" · 尝试 "+issue.attemptId).AppendLine(issue.meaning).AppendLine("建议："+issue.advice);if(issue.mods.Length>0)text.AppendLine("堆栈涉及 MOD："+string.Join("、",issue.mods));foreach(var e in issue.evidence)text.AppendLine("  "+e.source+":"+e.line+" "+e.text);}foreach(var warning in r.warnings)text.AppendLine("提示："+warning);return Limit(text.ToString(),120000);}
 }
}
