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
  sealed class Block {internal string Message,Severity,Type,Source,Family;internal int Line,Order;internal DateTime? Time;internal bool Runtime,Required,Fatal,EvidenceTruncated;internal readonly List<GameLogFrame> Frames=new List<GameLogFrame>();internal readonly List<GameLogEvidence> Evidence=new List<GameLogEvidence>();}
  sealed class Analysis {
   internal readonly GameLogReport Report=new GameLogReport{checkedUtc=DateTime.UtcNow.ToString("o"),origin="imported",state="unknown",stage="unknown"};
   internal readonly List<GameLogSource> Sources=new List<GameLogSource>();internal readonly List<string> Warnings=new List<string>();internal readonly List<Session> Sessions=new List<Session>();internal readonly Dictionary<string,Session> SessionMap=new Dictionary<string,Session>();internal long Chars;internal int Order;internal CancellationToken Cancellation;
   internal void Warn(string text){Report.incomplete=true;if(Warnings.Count<24&&!Warnings.Contains(text))Warnings.Add(text);}
   internal Session GetSession(string key){key=key??"";Session session;if(!SessionMap.TryGetValue(key,out session)){session=new Session{Key=Sanitize(key)};SessionMap[key]=session;Sessions.Add(session);}return session;}
   internal void AddEvent(Session session,string kind,string source,int line,DateTime? time,string raw){if(session.Events.Count>=MaxEvents){Warn("会话状态事件达到 8192 条上限。");return;}session.Events.Add(new Event{Kind=kind,Source=source,Line=line,Time=time,Order=Order,Text=kind=="thread-exit"?Limit(Sanitize(raw),800):""});}
  }
  internal static GameLogReport AnalyzeText(string text,string origin){string value=text??"";var report=Analyze(new[]{new GameLogInput{Id="buffer",Name="日志片段",Kind=origin??"buffer",SessionKey="buffer",Length=value.Length,Encoding="text",Open=()=>new StringReader(value)}},CancellationToken.None,null);report.origin=Sanitize(origin??"buffer");report.reportText=Export(report);return report;}
  internal static GameLogReport Analyze(IEnumerable<GameLogInput> inputs,CancellationToken cancellation,Action<int,string> progress){
   cancellation.ThrowIfCancellationRequested();var a=new Analysis{Cancellation=cancellation};int index=0;
   foreach(var input in inputs??Enumerable.Empty<GameLogInput>()){
    cancellation.ThrowIfCancellationRequested();if(index>=128||a.Chars>=TotalChars){a.Warn("输入数量或读取总量达到上限，部分日志未分析。");break;}if(input==null)continue;index++;
    string sourceId=Sanitize(Limit(input.Id??("source-"+index),160));var source=new GameLogSource{id=sourceId,name=Sanitize(Limit(input.Name??"日志",300)),kind=Sanitize(Limit(input.Kind??"unknown",64)),encoding=Sanitize(Limit(input.Encoding??"unknown",64)),modifiedUtc=Sanitize(Limit(input.ModifiedUtc,64)),status="read",truncated=input.Truncated};a.Sources.Add(source);if(index==1)a.Report.origin=source.kind;
   if(input.Truncated)a.Warn("来源 "+source.name+" 已截断。");if(!string.IsNullOrWhiteSpace(input.Warning))a.Warn(Sanitize(input.Warning));
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
   var chars=new char[4096];var line=new StringBuilder();int lineNumber=1;long sourceChars=0;bool longLine=false;Block active=null;GameLogEvidence pendingHeader=null;var preceding=new List<GameLogFrame>();
   Action consume=()=>{a.Order++;try{ParseLine(a,session,source.id,lineNumber,line.ToString(),ref active,preceding,ref pendingHeader);}catch(RegexMatchTimeoutException){a.Warn("部分复杂日志行超过解析时间限制。");}line.Length=0;longLine=false;lineNumber++;};
   int count;while((count=reader.Read(chars,0,chars.Length))>0){cancellation.ThrowIfCancellationRequested();for(int i=0;i<count;i++){if((i&1023)==0)cancellation.ThrowIfCancellationRequested();if(sourceChars>=FileChars||a.Chars>=TotalChars){source.truncated=true;a.Warn("读取容量达到上限，来源已截断。");goto done;}sourceChars++;a.Chars++;char c=chars[i];if(c=='\n'){consume();continue;}if(c=='\r')continue;if(line.Length<MaxLine)line.Append(c);else if(!longLine){longLine=true;source.truncated=true;a.Warn("超过 8192 字符的日志行已截断。");}}}
   done: if(line.Length>0)consume();Flush(a,session,ref active);source.bytesRead=sourceChars; // decoded character count is a lower bound; service replaces it with actual byte accounting.
   a.Report.bytesRead+=source.bytesRead;
  }
  static string Severity(string line){var m=Match(line,@"^\s*(ERROR|WARN(?:ING)?|FATAL)\s*[:\[]");return !m.Success?"":m.Groups[1].Value.StartsWith("WARN",StringComparison.OrdinalIgnoreCase)?"warning":"error";}
  static string Body(string line){var m=Match(line,@"^\s*(?:ERROR|WARN(?:ING)?|FATAL|LOG|DEBUG|INFO)\s*:\s*[^>]*>\s*(.*)$");if(m.Success)return m.Groups[1].Value.Trim();return Replace(line,@"^\s*(?:ERROR|WARN(?:ING)?|FATAL|LOG|DEBUG|INFO)\s*:\s*","").Trim();}
  static DateTime? Timestamp(string line){var m=Match(line,@"\b(\d{2}-\d{2}-(?:\d{4}|\d{2}))[ T](\d{2}:\d{2}:\d{2}(?:\.\d+)?)\b");DateTime value;if(m.Success&&DateTime.TryParseExact(m.Groups[1].Value+" "+m.Groups[2].Value,new[]{"dd-MM-yyyy HH:mm:ss","dd-MM-yyyy HH:mm:ss.fff","dd-MM-yy HH:mm:ss","dd-MM-yy HH:mm:ss.fff"},CultureInfo.InvariantCulture,DateTimeStyles.None,out value))return value;m=Match(line,@"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})?");if(m.Success&&DateTime.TryParse(m.Value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out value))return value;return null;}
  static GameLogFrame Frame(string line){Match m=Match(line,@"Lua\(\(MOD:(.*?)\)\)\.(.*?)\((.*?):(\d+)\)");if(m.Success)return NewFrame(m.Groups[1].Value,m.Groups[3].Value,m.Groups[2].Value,m.Groups[4].Value);m=Match(line,@"function:\s*(.*?)\s*--\s*file:\s*(.*?)\s+line\s*#\s*(\d+)(?:\s*\|\s*MOD:\s*(.*))?");if(m.Success)return NewFrame(m.Groups[4].Value,m.Groups[2].Value,m.Groups[1].Value,m.Groups[3].Value);m=Match(line,@"(?:^|\s)(?:at\s+)?([\w.$/]+)\(([^():]+\.(?:java|lua)):(\d+)\)");if(m.Success)return NewFrame("",m.Groups[2].Value,m.Groups[1].Value,m.Groups[3].Value);return null;}
  static GameLogFrame NewFrame(string mod,string file,string function,string number){int line;int.TryParse(number,out line);return new GameLogFrame{mod=Sanitize(Limit(mod.Trim(),200)),file=Sanitize(Limit(file.Trim(),300)),function=Sanitize(Limit(function.Trim(),200)),line=line};}
  static void AddFrame(List<GameLogFrame> frames,GameLogFrame frame){if(frame==null||frames.Count>=MaxFrames||frames.Any(f=>f.mod==frame.mod&&f.file==frame.file&&f.function==frame.function&&f.line==frame.line))return;frames.Add(frame);}
  static void Evidence(Block block,string source,int line,string text){if(block.Evidence.Any(e=>e.source==source&&e.line==line))return;string safe=Sanitize(text);if(safe.Length>800)block.EvidenceTruncated=true;if(block.Evidence.Count<MaxEvidence)block.Evidence.Add(new GameLogEvidence{source=source,line=line,text=Limit(safe,800)});else block.EvidenceTruncated=true;}
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
   if(!ActualExceptionStart(message)&&Has(message,@"Could not find bone index for node name\s*:"))return "asset-bone";
   if(!ActualExceptionStart(message)&&Has(message,@"^Could not find icon\s*:|^AngelCodeFont failed to load page\b.*\btexture\b"))return "asset-diagnostic";
   if(Has(message,@"^require\s*\([""'][^""']+[""']\)\s+failed\b"))return "lua-require";
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
  static void ParseLine(Analysis a,Session session,string source,int lineNumber,string raw,ref Block active,List<GameLogFrame> preceding,ref GameLogEvidence pendingHeader){
   string normalized=raw;if(raw.Length>1&&raw[0]=='['){int end=raw.IndexOf(']');if(end>0&&end<80&&Has(raw.Substring(0,end),@"\d{2}-\d{2}-\d{2,4}"))normalized=raw.Substring(end+1).TrimStart();}
   string severity=Severity(normalized),body=Body(normalized);if(severity=="error")a.Report.errorCount++;if(severity=="warning")a.Report.warningCount++;
   var time=Timestamp(raw);
   string type=ObservedType(body,severity);bool stackLine=Has(body,@"^(?:Lua\(\(MOD:|function:|(?:at\s+)?[\w.$/]+\([^)]*\)\.?\s*$)");
   GameLogFrame frame=stackLine&&(Contains(raw,".lua")||Contains(raw,".java"))?Frame(raw):null;
   string eventKind=null;
   if(NetworkRecord(body,"connection-attempt-failed")){eventKind="connection-failed";type="connection-failure";}
   else if(NetworkRecord(body,"connection-request-accepted"))eventKind="accepted";
   else if(Has(body,@"^STATE:\s*(?:enter\s+(?:zombie\.gameStates\.)?IngameState|exit\s+(?:zombie\.gameStates\.)?GameLoadingState)\b")||(frame!=null&&Has(frame.function,@"(?:^|\.)IngameState\.(?:update|render|onTick)")))eventKind="entered";
   else if(type=="world-load")eventKind="world-failed";
   else if(NetworkRecord(body,"connection-lost")){eventKind="disconnected";type="connection-lost";}
   else if(Has(body,@"^GameThread exited\s*\.?$")||type=="jvm-startup"||type=="native-crash")eventKind="thread-exit";
   else if(Has(body,@"^(?:STATE:\s*enter\s+(?:zombie\.gameStates\.)?GameLoadingState\b|game loading took\b)"))eventKind="loading";
   else if(NetworkRecord(body,"connection-request")||NetworkRecord(body,"connection-attempt"))eventKind="connecting";
   // A retry handshake must not let an earlier issue absorb its later entry/frame evidence.
   if(eventKind=="connecting"||eventKind=="accepted"||(eventKind=="entered"&&active!=null&&(active.Type=="connection-failure"||active.Type=="connection-lost"||active.Type=="world-load"))){Flush(a,session,ref active);preceding.Clear();pendingHeader=null;}
   if(eventKind!=null)a.AddEvent(session,eventKind,source,lineNumber,time,raw);
   if(active!=null&&eventKind=="entered"){active.Runtime=true;EssentialEvidence(active,source,lineNumber,raw);}
   bool header=Header(body);
   if(header){if(body.Length>0)pendingHeader=new GameLogEvidence{source=source,line=lineNumber,text=Limit(Sanitize(raw),800)};if(active!=null&&active.Evidence.Count<3)Evidence(active,source,lineNumber,raw);return;}
   bool exception=type!="unclassified";
   // A Java frame's type name is context, not another exception occurrence.
   if(frame!=null&&!Contains(raw,"Exception:")&&!Contains(raw,"Error:")&&!Has(raw,@"(?:Exception|Error)\s*[:$]")){
    if(active!=null){AddFrame(active.Frames,frame);Evidence(active,source,lineNumber,raw);}else {if(preceding.Count>=MaxFrames)preceding.RemoveAt(0);AddFrame(preceding,frame);}return;
   }
   if(exception){
    bool cause=Contains(body,"Caused by:")||Contains(body,"Suppressed:");
    bool luaCopy=active!=null&&type==active.Type&&Contains(body,active.Message)&&!Has(active.Message,@"\b(?:[\w$]+\.)+[\w$]*(?:Exception|Error)\b")&&Has(body,@"\b(?:[\w$]+\.)+[\w$]*(?:Exception|Error)\b");
    if(active!=null&&(cause||luaCopy)){if(type!="java-exception"||active.Type=="asset-bone"||active.Type=="asset-diagnostic"||active.Type=="lua-require"){if(type!=active.Type){active.Message=body;active.Family=null;}active.Type=type;}active.Required|=severity!="warning"&&Has(body,@"\brequired\s+(?:animation\s+)?(?:asset|resource)\b");active.Fatal|=Has(normalized,@"^\s*FATAL\s*[:\[]");Evidence(active,source,lineNumber,raw);return;}
    Flush(a,session,ref active);active=NewBlock(body,severity,type,source,lineNumber,a.Order,time,pendingHeader);active.Fatal=Has(normalized,@"^\s*FATAL\s*[:\[]")||(pendingHeader!=null&&Has(pendingHeader.text,@"^\s*FATAL\s*[:\[]"));pendingHeader=null;foreach(var prior in preceding)AddFrame(active.Frames,prior);preceding.Clear();Evidence(active,source,lineNumber,raw);return;
   }
   if(severity.Length>0){Flush(a,session,ref active);active=NewBlock(body,severity,"unclassified",source,lineNumber,a.Order,time,pendingHeader);active.Fatal=Has(normalized,@"^\s*FATAL\s*[:\[]")||(pendingHeader!=null&&Has(pendingHeader.text,@"^\s*FATAL\s*[:\[]"));pendingHeader=null;foreach(var prior in preceding)AddFrame(active.Frames,prior);preceding.Clear();Evidence(active,source,lineNumber,raw);return;}
   if(active!=null){if(Contains(body,"at ")||Contains(body,"... ")||eventKind!=null)Evidence(active,source,lineNumber,raw);else Flush(a,session,ref active);}if(eventKind==null){preceding.Clear();pendingHeader=null;}
  }
  static Block NewBlock(string body,string severity,string type,string source,int line,int order,DateTime? time,GameLogEvidence header){
   var block=new Block{Message=body,Severity=severity=="warning"?"warning":"error",Type=type,Source=source,Line=line,Order=order,Time=time,Required=severity!="warning"&&Has(body,@"\brequired\s+(?:animation\s+)?(?:asset|resource)\b")};
   // Only the specific asset-directory visitor establishes a non-required scan diagnostic.
   var directory=Match(body,@"[\\/]media[\\/](AnimSets|actiongroups)(?:[\\/\s]|$)");
   if(type=="missing-resource"&&!block.Required&&directory.Success&&header!=null&&Contains(header.text,"AdvancedAnimator")&&Contains(header.text,"visitFileFailed")&&Contains(body,"NoSuchFileException")){block.Type="asset-diagnostic";block.Family="animation-directory:"+directory.Groups[1].Value.ToLowerInvariant();}
   else if(type=="asset-bone")block.Family="skeleton-bone";
   else if(type=="asset-diagnostic"){var icon=Match(body,@"^Could not find icon\s*:\s*([^,\s]+)");block.Family=icon.Success?"icon:"+Sanitize(icon.Groups[1].Value):"font-texture:"+Sanitize(body);}
   if(header!=null)block.Evidence.Add(header);return block;
  }
  static void Flush(Analysis a,Session session,ref Block active){if(active==null)return;if(session.Blocks.Count<20000)session.Blocks.Add(active);else a.Warn("会话异常记录达到 20000 条上限。");active=null;}
  static string StateSummary(string state){switch(state){case "entered":return "已进入世界";case "entered-with-issues":return "已进入世界，同时记录到问题";case "failed":return "最后一次尝试失败或被阻断";case "disconnected":return "游戏连接已丢失";default:return "进入世界状态待确认";}}
  static void Resolve(Analysis a,Session session){
   // Companion sources can interleave. Only comparable explicit timestamps may reorder them.
   var events=session.Events.ToList();if(events.Count>0&&events.All(e=>e.Time.HasValue))events=events.OrderBy(e=>e.Time.Value).ThenBy(e=>e.Order).ToList();
   else if(events.Select(e=>e.Source).Distinct().Count()>1){var worldSources=new HashSet<string>(events.Where(e=>e.Kind=="entered"||e.Kind=="world-failed"||e.Kind=="loading").Select(e=>e.Source));events=events.OrderBy(e=>worldSources.Contains(e.Source)?1:0).ThenBy(e=>e.Order).ToList();a.Warn("配对来源缺少可比较的事件时间，尝试顺序待核对。");}
   session.Events.Clear();session.Events.AddRange(events);GameLogAttempt current=null;int number=0;
   foreach(var e in events){a.Cancellation.ThrowIfCancellationRequested();bool terminal=current!=null&&(current.state=="failed"||current.state=="disconnected");if(current==null||(terminal&&(e.Kind=="accepted"||e.Kind=="connecting"||e.Kind=="entered"))||(e.Kind=="connecting"&&current.stage!="unknown")){
     current=new GameLogAttempt{id="attempt-"+(a.Sessions.IndexOf(session)+1)+"-"+(++number),sessionKey=session.Key,state="unknown",stage="unknown",firstLine=e.Line};session.Attempts.Add(current);
    }e.Attempt=current;current.lastLine=e.Line;
    switch(e.Kind){case "connecting":case "accepted":current.stage="connection";break;case "connection-failed":current.state="failed";current.stage="connection";break;case "entered":current.state="entered";current.stage="runtime";break;case "world-failed":current.state="failed";current.stage="world-loading";break;case "disconnected":current.state="disconnected";current.stage="disconnection";break;case "loading":if(current.state!="entered")current.stage="world-loading";break;case "thread-exit":var blocking=session.Blocks.LastOrDefault(b=>(b.Fatal||b.Required||b.Type=="jvm-startup"||b.Type=="native-crash"||b.Type=="native-memory"||b.Type=="java-heap")&&b.Source==e.Source&&e.Line>=b.Line&&e.Line-b.Line<=80&&Before(b,e));if(blocking!=null){current.state="failed";current.stage=current.stage=="unknown"?"startup":current.stage;EssentialEvidence(blocking,e.Source,e.Line,e.Text);}break;}
   }
   if(current==null){current=new GameLogAttempt{id="attempt-"+(a.Sessions.IndexOf(session)+1)+"-1",sessionKey=session.Key,state="unknown",stage="unknown",firstLine=1,lastLine=1};session.Attempts.Add(current);}
   foreach(var attempt in session.Attempts)attempt.summary=StateSummary(attempt.state);
  }
  static bool Before(Block b,Event e){return b.Time.HasValue&&e.Time.HasValue?b.Time.Value<=e.Time.Value:b.Order<=e.Order;}
  static bool EventBefore(Event e,Block block){return block.Time.HasValue&&e.Time.HasValue?e.Time.Value<=block.Time.Value:e.Order<=block.Order;}
  static GameLogAttempt AttemptFor(Session session,Block block){Event exact=session.Events.LastOrDefault(e=>e.Source==block.Source&&e.Line==block.Line);if(exact!=null&&exact.Attempt!=null)return exact.Attempt;Event latest=session.Events.LastOrDefault(e=>EventBefore(e,block));return latest==null?session.Attempts[0]:latest.Attempt??session.Attempts[0];}
  static bool UsefulFailure(Block b){return b.Frames.Count>0&&Has(b.Message,@"\b(?:fail(?:ed|ure)?|error|exception|invalid|cannot|unable)\b|could not|attempt to|错误|失败|异常");}
  static string Attention(Block b){if(b.Fatal||b.Required)return "primary";if(b.Type=="asset-bone"||b.Type=="asset-diagnostic")return "background";if(b.Type=="lua-require"&&b.Severity=="warning")return "review";if(b.Type=="unclassified")return UsefulFailure(b)?"primary":"review";if(b.Type=="java-exception"&&Has(b.Message,@"\bKahluaException:\s*[A-Za-z_][A-Za-z0-9_]*\s*$"))return "review";return "primary";}
  static GameLogIssue Explain(Block b,string phase,string attempt){bool script=b.Frames.Any(f=>f.mod.Length>0||f.file.EndsWith(".lua",StringComparison.OrdinalIgnoreCase));string attention=Attention(b),title=(script?"Lua 脚本记录：":b.Frames.Count>0?"程序调用记录：":"日志记录：")+Limit(Sanitize(b.Message),110),meaning="具体原因待核对。",advice="核对相邻错误消息与调用位置。";
   switch(b.Type){
    case "lua-null-index":title="Lua 脚本读取空值";var field=Match(b.Message,@"attempted index:\s*([^\r\n]+?)\s+of non-table");meaning="脚本从空值或非表对象读取"+(field.Success?"字段“"+Sanitize(field.Groups[1].Value)+"”":"字段")+"。";advice="按脚本、函数和行号核对 MOD 版本、依赖及加载顺序。";break;
    case "lua-call-nil":title="Lua 调用了不存在的函数";meaning="脚本调用了 nil（空值），对应函数不存在或未初始化。";advice="核对对应脚本行、所需依赖和适用游戏版本。";break;
    case "lua-length":title="Lua 对不支持的对象取长度";meaning="脚本对空值或不支持长度运算的对象取长度。";advice="核对堆栈首个脚本行及传入数据，检查 MOD 版本和依赖。";break;
    case "lua-require":title="Lua 模块加载失败："+Limit(Sanitize(b.Message),100);meaning="Lua 的 require 调用加载模块失败。";advice="核对模块路径、调用脚本、游戏版本及依赖。";break;
    case "asset-bone":title="模型骨骼节点未匹配";var boneNode=Match(b.Message,@"node name:\s*[""']([^""']+)[""']");meaning=boneNode.Success?"未找到节点“"+Limit(Sanitize(boneNode.Groups[1].Value),180)+"”的骨骼索引。":"未找到模型节点的骨骼索引。";advice="核对该节点对应的模型骨架、动画资源和内容版本。";break;
    case "asset-diagnostic":title=b.Family!=null&&b.Family.StartsWith("animation-directory:",StringComparison.Ordinal)?"动画资源目录扫描提示":b.Family!=null&&b.Family.StartsWith("icon:",StringComparison.Ordinal)?"图标资源未找到":"字体纹理加载提示";meaning=title=="动画资源目录扫描提示"?"扫描动画资源时未找到目录。":"资源加载失败："+Limit(Sanitize(b.Message),180)+"。";advice="核对对应资源文件及其内容版本。";break;
    case "connection-failure":title="连接尝试失败";meaning="本次连接尝试失败。";advice="检查服务器地址、可达性和客户端版本。";break;
    case "connection-lost":title="游戏连接已丢失";meaning="游戏与服务器的连接已丢失。";advice="检查网络连接、服务器状态及相邻断线消息。";break;
    case "world-load":title="世界加载被阻断";meaning="世界无法加载或世界字典发生异常。";advice="核对客户端、服务器与存档要求的内容版本，检查缺失字典脚本并验证游戏文件。";break;
    case "missing-resource":title="缺少或无法读取所需资源";meaning="所需资源文件、动画或字典脚本缺失或无法读取。";advice="验证游戏文件，核对缺失资源对应的 MOD、游戏分支和服务器内容。";break;
    case "java-heap":title="Java 堆内存耗尽";meaning="Java 堆空间耗尽或垃圾回收开销超限。";advice="检查堆内存设置和可用内存，减少高占用内容。";break;
    case "native-memory":title="JVM 无法分配系统内存";meaning="JVM 保留、提交或分配本机内存失败。";advice="检查可用内存、系统提交上限和虚拟内存设置，关闭高占用程序。";break;
    case "native-crash":title="本机运行时崩溃";meaning="JVM 报告本机访问冲突或致命崩溃。";advice="检查 hs_err 报告中的崩溃线程与问题帧，验证游戏文件。";break;
    case "java-dependency":title="Java 类或本机库无法加载";meaning="所需 Java 类或本机库无法加载。";advice="验证游戏文件，核对 Java 与组件的架构、版本和依赖。";break;
    case "missing-dependency":title="缺少 MOD 依赖";meaning="所需 MOD 或依赖未找到。";advice="核对依赖标识、订阅内容、启用顺序及服务器要求。";break;
    case "duplicate-mod":title="MOD 标识重复";meaning="发现重复的 MOD 标识。";advice="核对重复内容的来源和版本，移除多余副本后检查启用列表。";break;
    case "checksum":title="文件校验不一致";meaning="文件校验值不一致。";advice="验证游戏文件，按服务器要求同步游戏与 MOD 版本。";break;
    case "workshop":title="创意工坊下载出现错误";meaning="创意工坊下载或条目处理失败。";advice="检查 Steam 下载状态、可用磁盘空间和具体条目错误。";break;
    case "jvm-startup":title="Java 启动或代理加载失败";meaning="JVM 初始化或代理加载失败。";advice="核对代理/JAR 文件、启动参数及组件完整性。";break;
    case "java-exception":title="Java 运行时异常";meaning="Java 抛出异常："+Limit(Sanitize(b.Message),180)+"。";advice="按异常消息和首个业务或脚本帧核对代码位置、版本、依赖及资源。";break;
   }
   if(b.Fatal)meaning+=" 日志级别为 FATAL。";
   string impact=b.Fatal||b.Type=="world-load"||b.Type=="jvm-startup"||b.Type=="native-crash"?"blocking":attention=="primary"&&phase=="runtime"?"runtime":"candidate";
   return new GameLogIssue{ObservationLine=b.Line,type=b.Type,severity=b.Severity,attention=attention,impact=impact,title=title,meaning=meaning,advice=advice,phase=phase,attemptId=attempt,count=1,mods=b.Frames.Select(f=>f.mod).Where(m=>!string.IsNullOrEmpty(m)).Distinct().ToArray(),frames=b.Frames.ToArray(),evidence=b.Evidence.ToArray()};
  }
  static string GroupKey(Block block,GameLogIssue issue){bool resource=!String.IsNullOrEmpty(block.Family);string context=string.Join("|",issue.frames.Where(f=>f.mod.Length>0||f.file.EndsWith(".lua",StringComparison.OrdinalIgnoreCase)).Take(4).Select(f=>f.mod+"/"+f.file+(resource?"":":"+f.line+":"+f.function)));return issue.attemptId+"|"+block.Type+"|"+issue.severity+"|"+issue.attention+"|"+(block.Fatal?"fatal":"normal")+"|"+(resource?block.Family:Sanitize(block.Message))+"|"+context;}
  static void Merge(Analysis a,GameLogIssue existing,GameLogIssue issue){existing.count++;existing.ObservationLine=Math.Max(existing.ObservationLine,issue.ObservationLine);if(issue.phase=="runtime"){existing.phase="runtime";if(existing.attention=="primary"&&existing.impact!="blocking")existing.impact="runtime";}var evidence=existing.evidence.ToList();foreach(var item in issue.evidence){if(evidence.Any(e=>e.text==item.text))continue;if(evidence.Count<MaxEvidence)evidence.Add(item);else a.Warn("部分分组证据超过 12 条，已采样。");}existing.evidence=evidence.ToArray();var frames=existing.frames.ToList();foreach(var frame in issue.frames)AddFrame(frames,frame);existing.frames=frames.ToArray();existing.mods=existing.mods.Concat(issue.mods).Distinct().Take(MaxFrames).ToArray();}
  static GameLogReport Finish(Analysis a){var issues=new List<GameLogIssue>();var grouped=new Dictionary<string,GameLogIssue>();int omitted=0;foreach(var session in a.Sessions){Resolve(a,session);foreach(var block in session.Blocks){a.Cancellation.ThrowIfCancellationRequested();var attempt=AttemptFor(session,block);string phase=block.Runtime?"runtime":"unknown";var previous=session.Events.LastOrDefault(e=>EventBefore(e,block)&&e.Attempt==attempt);if(phase!="runtime"&&previous!=null)phase=previous.Kind=="entered"?"runtime":attempt.stage=="runtime"?"unknown":attempt.stage;
     var issue=Explain(block,phase,attempt.id);if(block.Required&&attempt.state=="failed"){issue.impact="blocking";if(issue.phase=="unknown")issue.phase="startup";}if(block.EvidenceTruncated)a.Warn("部分证据超过每组 12 条或每条 800 字符上限，已截断。");
     string key=GroupKey(block,issue);issue.ObservationKey=key.Substring(issue.attemptId.Length+1);GameLogIssue existing;if(grouped.TryGetValue(key,out existing)){Merge(a,existing,issue);continue;}
     if(issues.Count>=MaxIssues){omitted++;var weakest=issues.OrderBy(Priority).First();if(Priority(issue)<=Priority(weakest))continue;issues.Remove(weakest);string removed=grouped.First(p=>object.ReferenceEquals(p.Value,weakest)).Key;grouped.Remove(removed);}issues.Add(issue);grouped[key]=issue;
    }}
   if(omitted>0)a.Warn("分组达到 80 组上限，省略或替换 "+omitted+" 次。");
   for(int i=0;i<issues.Count;i++)issues[i].id="issue-"+(i+1);a.Report.issues=issues.OrderByDescending(Priority).ToArray();a.Report.sources=a.Sources.ToArray();a.Report.fileCount=a.Sources.Count;a.Report.attempts=a.Sessions.SelectMany(s=>s.Attempts).Take(512).ToArray();if(a.Sessions.Count==1){var last=a.Sessions[0].Attempts.Last();a.Report.state=last.state;a.Report.stage=last.stage;if(a.Report.state=="entered"&&issues.Any(i=>i.attemptId==last.id&&i.attention=="primary"))a.Report.state="entered-with-issues";}a.Report.summary=(a.Sessions.Count>1?"包含 "+a.Sessions.Count+" 个独立会话":StateSummary(a.Report.state))+"。可定位故障 "+issues.Count(i=>i.attention=="primary")+" 组可定位故障，待复核 "+issues.Count(i=>i.attention=="review")+" 组，资源背景记录 "+issues.Count(i=>i.attention=="background")+" 组；原始 ERROR "+a.Report.errorCount+" 条、WARN "+a.Report.warningCount+" 条。";a.Report.warnings=a.Warnings.ToArray();a.Report.reportText=Export(a.Report);return a.Report;
  }
  internal static int Priority(GameLogIssue issue){if(issue.impact=="blocking")return 100;if(issue.attention=="background")return 0;if(issue.attention=="review")return 1;if(issue.type=="java-heap"||issue.type=="native-memory")return 90;if((issue.type??"").StartsWith("lua-",StringComparison.Ordinal))return 85;if(issue.type=="java-exception")return 80;return 70;}
  internal static void RebuildExport(GameLogReport report){if(report!=null){report.origin=Sanitize(report.origin);report.reportText=Export(report);}}
  static string Export(GameLogReport r){var text=new StringBuilder();text.AppendLine("检查时间："+r.checkedUtc).AppendLine("来源："+r.origin).AppendLine(r.summary);
   text.AppendLine().AppendLine("来源文件（证据中的来源 ID 对照）：");int tableStart=text.Length;var usedSources=new HashSet<string>(r.issues.SelectMany(i=>i.evidence).Select(e=>e.source));
   foreach(var source in r.sources.OrderByDescending(s=>usedSources.Contains(s.id))){string row="  "+Sanitize(source.id)+" → "+Limit(Sanitize(source.name),200)+" | 编码 "+Limit(Sanitize(source.encoding),40)+" | 状态 "+Limit(Sanitize(source.status),40)+(source.truncated?" | 已截断":"");if(text.Length+row.Length-tableStart>12000){text.AppendLine("  来源表达到 12000 字符上限。");break;}text.AppendLine(row);}
   text.AppendLine().AppendLine("会话与尝试：");tableStart=text.Length;var usedAttempts=new HashSet<string>(r.issues.Select(i=>i.attemptId));
   foreach(var attempt in r.attempts.OrderByDescending(t=>usedAttempts.Contains(t.id))){string row="  "+Sanitize(attempt.id)+" | 会话 "+Limit(Sanitize(attempt.sessionKey),120)+" | 阶段 "+Limit(Sanitize(attempt.stage),32)+" | "+Limit(Sanitize(attempt.summary),160)+" ("+Limit(Sanitize(attempt.state),32)+")";if(text.Length+row.Length-tableStart>10000){text.AppendLine("  尝试表达到 10000 字符上限。");break;}text.AppendLine(row);}
   foreach(var issue in r.issues.OrderByDescending(Priority)){text.AppendLine().AppendLine("["+issue.attention+" / "+issue.severity+" / "+issue.phase+" / "+issue.impact+"] "+issue.title+" ×"+issue.count+" · 尝试 "+issue.attemptId).AppendLine(issue.meaning).AppendLine("建议："+issue.advice);if(issue.mods.Length>0)text.AppendLine("堆栈涉及 MOD："+string.Join("、",issue.mods));foreach(var e in issue.evidence)text.AppendLine("  "+e.source+":"+e.line+" "+e.text);}foreach(var warning in r.warnings)text.AppendLine("提示："+warning);if(text.Length>120000){string warning="导出达到 120000 字符上限，后续证据未写入。";r.incomplete=true;r.warnings=r.warnings.Concat(new[]{warning}).Distinct().ToArray();return text.ToString(0,119800)+Environment.NewLine+"提示："+warning;}return text.ToString();}
 }
}
