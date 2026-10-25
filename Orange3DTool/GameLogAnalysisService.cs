using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PZ3D {
 // Native-only filesystem boundary. No source is chosen by a WebView payload.
 sealed class GameLogAnalysisService:IDisposable {
  internal const long FileLimit=64L*1024*1024,BatchLimit=256L*1024*1024;
  const int FileCountLimit=128,ZipEntryLimit=512;
  readonly object gate=new object();
  readonly string user,game;
  CancellationTokenSource cancellation;
  bool disposed;
  GameLogAnalysisStatus snapshot=new GameLogAnalysisStatus{jobId="",state="idle",message="选择本机日志，或导入玩家发来的日志。",progress=0};
  internal GameLogAnalysisService(string user,string game){this.user=user;this.game=game;}
  internal GameLogAnalysisStatus Status(){lock(gate)return Copy(snapshot);}
  static GameLogAnalysisStatus Copy(GameLogAnalysisStatus value){return new GameLogAnalysisStatus{jobId=value.jobId,state=value.state,message=value.message,progress=value.progress,report=value.report};}
  internal GameLogAnalysisStatus StartLocal(){return Start(null,"local");}
  internal GameLogAnalysisStatus StartImport(string[] paths){if(paths==null||paths.Length==0)throw new InvalidOperationException("请先选择日志。");return Start((string[])paths.Clone(),"imported");}
  GameLogAnalysisStatus Start(string[] paths,string origin){
   lock(gate){
    if(disposed)throw new ObjectDisposedException("GameLogAnalysisService");
    if(snapshot.state=="running")throw new InvalidOperationException("日志正在分析，请等待完成或先取消。");
    if(cancellation!=null)cancellation.Dispose();cancellation=new CancellationTokenSource();var token=cancellation.Token;string id=Guid.NewGuid().ToString("N");
    snapshot=new GameLogAnalysisStatus{jobId=id,state="running",message="正在发现日志并核对来源…",progress=0};
    Task.Run(()=>{
     try{
      token.ThrowIfCancellationRequested();string[] selected=paths??DiscoverLocal(user,game,token);
      var report=AnalyzePaths(selected,origin,token,(value,text)=>{lock(gate){if(disposed||snapshot.jobId!=id)return;snapshot=new GameLogAnalysisStatus{jobId=id,state="running",message=text,progress=Math.Max(0,Math.Min(99,value))};}});
      token.ThrowIfCancellationRequested();lock(gate){if(!disposed&&snapshot.jobId==id)snapshot=new GameLogAnalysisStatus{jobId=id,state="completed",message="分析完成。",progress=100,report=report};}
     }catch(OperationCanceledException){lock(gate){if(!disposed&&snapshot.jobId==id)snapshot=new GameLogAnalysisStatus{jobId=id,state="cancelled",message="已取消分析。",progress=0};}}
     catch{lock(gate){if(!disposed&&snapshot.jobId==id)snapshot=new GameLogAnalysisStatus{jobId=id,state="failed",message="日志分析未完成。请确认文件仍可读取，或尝试单独导入日志。",progress=0};}}
    });return Copy(snapshot);
   }
  }
  internal GameLogAnalysisStatus Cancel(){lock(gate){if(cancellation!=null&&snapshot.state=="running"){cancellation.Cancel();snapshot=new GameLogAnalysisStatus{jobId=snapshot.jobId,state="running",message="正在取消分析…",progress=snapshot.progress};}return Copy(snapshot);}}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;if(cancellation!=null){cancellation.Cancel();cancellation.Dispose();cancellation=null;}}}

  static bool KnownName(string name){return Regex.IsMatch(name,@"\A(?:console(?:[ ()0-9]+)?|coop-console(?:[ ()0-9]+)?|server-console(?:[ ()0-9]+)?|hs_err_pid[0-9]+|.*_DebugLog|.*_connections|log_[0-9]+)\.(?:txt|log)\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));}
  static string Session(string name){var m=Regex.Match(name,@"\d{4}-\d\d-\d\d_\d\d-\d\d",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));return m.Success?m.Value:"";}
  static string Kind(string name){return name.IndexOf("connections",StringComparison.OrdinalIgnoreCase)>=0?"connections":name.StartsWith("hs_err_pid",StringComparison.OrdinalIgnoreCase)?"jvm-crash":"game-log";}
  static void Safe(string path){LauncherMaintenance.NoReparse(Path.GetFullPath(path));}
  internal static string[] DiscoverLocal(string user,string game,CancellationToken token){
   var found=new List<string>();string console=Path.Combine(user,"console.txt");Safe(console);if(File.Exists(console))found.Add(console);
   var logs=new List<string>();string folder=Path.Combine(user,"Logs");Safe(folder);
   if(Directory.Exists(folder)){
    var roots=new List<string>{folder};int seen=0;
    foreach(string entry in Directory.EnumerateDirectories(folder).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(16)){token.ThrowIfCancellationRequested();try{Safe(entry);if(Path.GetFileName(entry).StartsWith("logs_",StringComparison.OrdinalIgnoreCase))roots.Add(entry);}catch{}}
    foreach(string root in roots)foreach(string path in Directory.EnumerateFiles(root,"*_DebugLog.txt")){token.ThrowIfCancellationRequested();if(++seen>256)break;try{Safe(path);logs.Add(path);}catch{}}
    string companion=null;foreach(string candidate in logs.OrderByDescending(File.GetLastWriteTimeUtc).ThenBy(p=>p,StringComparer.Ordinal)){token.ThrowIfCancellationRequested();if(found.Count==0||Batch.MatchingTail(console,candidate)){companion=candidate;break;}}
    // DebugLog often mirrors console with timestamp prefixes. Read console once; use the
    // positively matched DebugLog only to locate the same run's independent network events.
    if(companion!=null){if(found.Count==0)found.Add(companion);string paired=companion.Substring(0,companion.Length-"_DebugLog.txt".Length)+"_connections.txt";Safe(paired);if(File.Exists(paired))found.Add(paired);}
   }
   return found.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  }

  internal static GameLogReport AnalyzePaths(string[] paths,string origin,CancellationToken token,Action<int,string> progress){
   var batch=new Batch(token,progress,origin=="local");var report=GameLogAnalyzer.Analyze(batch.Inputs(paths??new string[0]),token,progress);
   report.origin=origin;report.bytesRead=batch.Bytes;report.warnings=(report.warnings??new string[0]).Concat(batch.Warnings).Distinct().Take(64).ToArray();report.incomplete|=batch.Warnings.Count>0;
   GameLogAnalyzer.RebuildExport(report);
   return report;
  }
  sealed class Batch {
   readonly CancellationToken token;readonly Action<int,string> progress;
   readonly HashSet<string> digests=new HashSet<string>(StringComparer.Ordinal);
   readonly HashSet<string> visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   readonly bool local;
   string consoleSession="";
   internal readonly List<string> Warnings=new List<string>();
   internal long Bytes;int count,entries;
   internal Batch(CancellationToken token,Action<int,string> progress,bool local){this.token=token;this.progress=progress;this.local=local;}
   void Warn(string value){if(Warnings.Count<64&&!Warnings.Contains(value))Warnings.Add(value);}
   static string Label(string path){return GameDiagnostics.Sanitize(Path.GetFileName(path));}
   internal IEnumerable<GameLogInput> Inputs(string[] paths){
    if(paths.Length==0)Warn("未找到本次游戏日志。请先运行游戏，或手动导入日志。");
    if(local){string console=paths.FirstOrDefault(p=>Path.GetFileName(p).Equals("console.txt",StringComparison.OrdinalIgnoreCase)),debug=paths.FirstOrDefault(p=>p.EndsWith("_DebugLog.txt",StringComparison.OrdinalIgnoreCase)),connection=paths.FirstOrDefault(p=>p.EndsWith("_connections.txt",StringComparison.OrdinalIgnoreCase));if(debug==null&&connection!=null)debug=connection.Substring(0,connection.Length-"_connections.txt".Length)+"_DebugLog.txt";if(console!=null&&debug!=null&&MatchingTail(console,debug))consoleSession=Session(Path.GetFileName(debug));}
    foreach(string selected in paths){token.ThrowIfCancellationRequested();string full=null;try{full=Path.GetFullPath(selected);Safe(full);}catch{Warn("已跳过不安全、不可访问或含联接的来源。");}
     if(full==null)continue;
     if(Directory.Exists(full)){foreach(string file in Find(full,0))foreach(var input in FileInputs(file))yield return input;}
     else foreach(var input in FileInputs(full))yield return input;
    }
   }
   IEnumerable<string> Find(string root,int depth){
    if(depth>4){Warn("日志目录超过 4 层，较深来源未分析。");yield break;}
    string[] children=null;try{Safe(root);children=Directory.EnumerateFileSystemEntries(root).Take(1025).ToArray();}catch{Warn("部分日志目录无法安全读取。");}
    if(children==null)yield break;
    if(children.Length>1024){Warn("一个目录超过发现预算，部分条目未扫描。");children=children.Take(1024).ToArray();}
    foreach(string path in children){token.ThrowIfCancellationRequested();if(++entries>4096){Warn("目录发现达到条目预算，未继续扫描。");yield break;}
     bool safe=true;try{Safe(path);}catch{safe=false;Warn("已跳过联接、重解析或不可访问的来源。");}if(!safe)continue;
     if(Directory.Exists(path)){string name=Path.GetFileName(path);if(name.Equals("Saves",StringComparison.OrdinalIgnoreCase)||name.Equals("mods",StringComparison.OrdinalIgnoreCase)||name.Equals("Workshop",StringComparison.OrdinalIgnoreCase))continue;foreach(string child in Find(path,depth+1))yield return child;}
     else if(KnownName(Path.GetFileName(path))||Path.GetFileName(path).Equals("logs.zip",StringComparison.OrdinalIgnoreCase))yield return path;
    }
   }
   IEnumerable<GameLogInput> FileInputs(string path){
    token.ThrowIfCancellationRequested();if(!visited.Add(path))yield break;
    string ext=Path.GetExtension(path).ToLowerInvariant(),name=Label(path);
    if(!new[]{".txt",".log",".zip"}.Contains(ext)){Warn("已跳过不支持的文件类型；只支持 TXT、LOG、ZIP。");yield break;}
    if(count>=FileCountLimit||Bytes>=BatchLimit){Warn("分析达到 128 个文件或 256 MiB 批次预算，剩余来源未分析。");yield break;}
    if(ext==".zip"){
     List<GameLogInput> prepared=null;try{prepared=ReadZip(path);}catch(OperationCanceledException){throw;}catch{Warn("日志 ZIP 无法读取、格式无效或超出安全预算。");}
     if(prepared!=null)foreach(var item in prepared)yield return item;yield break;
    }
    GameLogInput value=null;try{
     Safe(path);using(var file=OpenFile(path)){long length=file.Length;if(length>FileLimit||length>BatchLimit-Bytes){Warn("一个文本日志超过 64 MiB 或剩余批次预算，未读取。");}else{byte[] bytes=Read(file,length);Bytes+=bytes.Length;string session=Session(Path.GetFileName(path));if(local&&Path.GetFileName(path).Equals("console.txt",StringComparison.OrdinalIgnoreCase)&&consoleSession.Length>0)session=consoleSession;value=Make(bytes,name,Kind(Path.GetFileName(path)),session,File.GetLastWriteTimeUtc(path));if(file.Length!=length)Warn("读取期间日志发生变化，末尾可能不完整；可以稍后重新分析。");}}
    }catch(OperationCanceledException){throw;}catch{Warn("一个文本日志无法读取、编码无效或不是文本；未用于正常结论。");}
    if(value!=null)yield return value;
   }
   static FileStream OpenFile(string path){Safe(path);var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);try{GameDiagnostics.VerifyHandle(file,path);return file;}catch{file.Dispose();throw;}}
   internal static bool MatchingTail(string console,string debug){try{var a=Tail(console);var b=Tail(debug);int count=Math.Min(16,Math.Min(a.Length,b.Length));return count>=3&&a.Skip(a.Length-count).SequenceEqual(b.Skip(b.Length-count),StringComparer.Ordinal);}catch{return false;}}
   static string[] Tail(string path){using(var file=OpenFile(path)){int size=(int)Math.Min(65536,file.Length);file.Position=file.Length-size;byte[] bytes=new byte[size];int offset=0,got;while(offset<size&&(got=file.Read(bytes,offset,size-offset))>0)offset+=got;string text=Encoding.UTF8.GetString(bytes,0,offset);var lines=text.Split('\n');return lines.Skip(size<file.Length?1:0).Select(l=>Regex.Replace(l.Trim(),@"^\[[^\]\r\n]{1,64}\]\s*","",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)).TrimEnd('.')).Where(l=>l.Length>0).ToArray();}}
   byte[] Read(Stream source,long length){if(length<0||length>FileLimit)throw new InvalidDataException();byte[] result=new byte[(int)length];int offset=0;while(offset<result.Length){token.ThrowIfCancellationRequested();int got=source.Read(result,offset,Math.Min(65536,result.Length-offset));if(got<=0)throw new EndOfStreamException();offset+=got;}return result;}
   List<GameLogInput> ReadZip(string path){
    var result=new List<GameLogInput>();using(var file=OpenFile(path)){
     if(file.Length>FileLimit)throw new InvalidDataException();
     using(var zip=new ZipArchive(file,ZipArchiveMode.Read,false)){
      if(zip.Entries.Count>ZipEntryLimit)throw new InvalidDataException();long declared=0;
      foreach(var entry in zip.Entries){token.ThrowIfCancellationRequested();string name=entry.FullName.Replace('\\','/');if(name.StartsWith("/",StringComparison.Ordinal)||name.Contains(":")||name.Split('/').Any(p=>p==".."||p=="."))throw new InvalidDataException();
       if(entry.Length>FileLimit||entry.Length>BatchLimit-declared)throw new InvalidDataException();declared+=entry.Length;if(entry.Length>1048576&&(entry.CompressedLength<=0||entry.Length/(double)entry.CompressedLength>200))throw new InvalidDataException();
      }
      foreach(var entry in zip.Entries){token.ThrowIfCancellationRequested();string raw=Path.GetFileName(entry.FullName.Replace('\\','/'));if(!KnownName(raw))continue;
       if(count>=FileCountLimit||entry.Length>BatchLimit-Bytes){Warn("日志 ZIP 达到文件数或批次预算，部分成员未分析。");break;}
       try{using(var stream=entry.Open()){byte[] bytes=Read(stream,entry.Length);if(stream.ReadByte()!=-1)throw new InvalidDataException();Bytes+=bytes.Length;var input=Make(bytes,GameDiagnostics.Sanitize(Path.GetFileName(path))+" · "+GameDiagnostics.Sanitize(raw),Kind(raw),Session(raw),entry.LastWriteTime.UtcDateTime);if(input!=null)result.Add(input);}}
       catch(OperationCanceledException){throw;}catch{Warn("ZIP 内一个日志无法解码或读取，已跳过。");}
      }
     }
    }if(result.Count==0&&Warnings.Count==0)Warn("ZIP 中没有可识别的游戏日志成员，或内容均与已选日志重复。");return result;
   }
   GameLogInput Make(byte[] bytes,string name,string kind,string session,DateTime modified){
    string digest;using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes));if(!digests.Add(digest))return null;
    string encoding,text=Decode(bytes,out encoding);count++;if(progress!=null)progress(Math.Min(90,count*3),"正在分析第 "+count+" 份日志…");
    return new GameLogInput{Id="source-"+count,Name=name,Kind=kind,SessionKey=String.IsNullOrEmpty(session)?"file-"+digest.Substring(0,17).Replace("-",""):session,Length=bytes.Length,ModifiedUtc=modified.ToString("o",CultureInfo.InvariantCulture),Encoding=encoding,Open=()=>new StringReader(text)};
   }
   static string Decode(byte[] bytes,out string encoding){
    Encoding decoder=new UTF8Encoding(false,true);int offset=0;encoding="utf-8";
    if(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191)offset=3;
    else if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254){offset=2;decoder=new UnicodeEncoding(false,false,true);encoding="utf-16le";}
    else if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255){offset=2;decoder=new UnicodeEncoding(true,false,true);encoding="utf-16be";}
    string text;try{text=decoder.GetString(bytes,offset,bytes.Length-offset);}catch(DecoderFallbackException){if(offset>0)throw;decoder=Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback);text=decoder.GetString(bytes);encoding="gb18030";}
    if(text.Any(c=>c=='\0'||(Char.IsControl(c)&&c!='\r'&&c!='\n'&&c!='\t')))throw new DecoderFallbackException();return text;
   }
  }
 }
}
