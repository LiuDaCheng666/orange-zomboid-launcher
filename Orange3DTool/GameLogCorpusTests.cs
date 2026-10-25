using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Opt-in local research. Never exports log text, paths, MOD names or report payloads.
 static class GameLogCorpusTests {
  const long Limit=64L*1024*1024;
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=16777216};
  static int assertions;
  static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);assertions++;Console.WriteLine("PASS: "+name);}
  static string Digest(Stream input){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
  static string DigestFile(string path){using(var stream=File.OpenRead(path))return Digest(stream);}
  static bool Safe(string path){for(string p=Path.GetFullPath(path);!String.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)return false;return true;}
  static IEnumerable<string> Local(string root){
   foreach(string n in new[]{"console.txt","coop-console.txt","server-console.txt"}){string p=Path.Combine(root,n);if(File.Exists(p)&&Safe(p))yield return p;}
   foreach(string p in Directory.EnumerateFiles(root,"hs_err_pid*.log"))if(Safe(p))yield return p;
   string logs=Path.Combine(root,"Logs");if(!Directory.Exists(logs))yield break;
   var dirs=new Queue<string>();dirs.Enqueue(logs);while(dirs.Count>0){string dir=dirs.Dequeue();if(!Safe(dir))continue;
    foreach(string p in Directory.EnumerateFiles(dir))if(Regex.IsMatch(Path.GetFileName(p),@"_(?:DebugLog|connections)\.txt$",RegexOptions.IgnoreCase)&&Safe(p))yield return p;
    foreach(string child in Directory.EnumerateDirectories(dir))if(Path.GetFileName(child).StartsWith("logs_",StringComparison.OrdinalIgnoreCase))dirs.Enqueue(child);
   }
  }
  static IEnumerable<string> Downloads(string root){
   foreach(string p in Directory.EnumerateFiles(root))if(Regex.IsMatch(Path.GetFileName(p),@"^(?:console.*\.txt|hs_err_pid.*\.log|.*_DebugLog\.txt|.*_connections\.txt|(?:server|coop)-console.*\.txt)$",RegexOptions.IgnoreCase)&&Safe(p))yield return p;
   foreach(string category in new[]{"console","coop-console","debug-log"}){string dir=Path.Combine(root,"logs",category);if(Directory.Exists(dir)&&Safe(dir))foreach(string p in Directory.EnumerateFiles(dir,"log_*.txt"))if(Regex.IsMatch(Path.GetFileName(p),@"^log_\d+\.txt$")&&Safe(p))yield return p;}
  }
  sealed class Measure {internal string hash,encoding;internal long bytes,lines;}
  sealed class Work {internal string path,scope;internal Measure measure;}
  static Measure Inspect(string path){
   if(new FileInfo(path).Length>Limit)throw new Exception("FAIL: input exceeds 64 MiB");var result=new Measure{bytes=new FileInfo(path).Length,hash=DigestFile(path)};
   foreach(var pair in new[]{new KeyValuePair<string,Encoding>("utf-8",new UTF8Encoding(false,true)),new KeyValuePair<string,Encoding>("gb18030",Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback))}){
    try{long lines=0;bool any=false,lastNewline=false;using(var reader=new StreamReader(path,pair.Value,true,16384)){var buffer=new char[16384];int read;while((read=reader.Read(buffer,0,buffer.Length))>0)for(int i=0;i<read;i++){any=true;lastNewline=buffer[i]=='\n';if(lastNewline)lines++;}}result.lines=lines+(any&&!lastNewline?1:0);result.encoding=pair.Key;return result;}catch(DecoderFallbackException){}
   }throw new Exception("A selected source has no supported strict encoding");
  }
  static GameLogInput Input(string path,string id,string session,string encoding){return new GameLogInput{Id=id,Name=Path.GetFileName(path),Kind="corpus",SessionKey=session,Length=new FileInfo(path).Length,Encoding=encoding,Open=()=>new StreamReader(path,encoding=="gb18030"?Encoding.GetEncoding(54936,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback):new UTF8Encoding(false,true),true,16384)};}
  static GameLogReport Analyze(string path){var m=Inspect(path);return GameLogAnalyzer.Analyze(new[]{Input(path,"manual","manual",m.encoding)},CancellationToken.None,null);}
  static bool Evidence(GameLogIssue issue,string token){return issue.evidence.Any(e=>e.text.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0)||issue.frames.Any(f=>f.function.IndexOf(token,StringComparison.OrdinalIgnoreCase)>=0);}
  static void Manual(string local,string downloads){
   var r=Analyze(Path.Combine(downloads,"console (35).txt"));var nil=r.issues.FirstOrDefault(i=>i.type=="lua-call-nil"&&i.mods.Length>0);Check(r.state=="entered-with-issues"&&nil!=null&&nil.impact=="runtime","real console35 generic MOD call-nil is runtime");
   r=Analyze(Path.Combine(downloads,"console (69).txt"));var a=r.issues.FirstOrDefault(i=>i.type=="lua-null-index"&&Evidence(i,"fireBlockReason"));var b=r.issues.FirstOrDefault(i=>i.type=="lua-null-index"&&Evidence(i,"aimLayer"));Check(r.state=="entered-with-issues"&&a!=null&&b!=null&&a.id!=b.id&&a.impact=="runtime"&&b.impact=="runtime","real console69 retains two distinct runtime fields");
   r=Analyze(Path.Combine(downloads,"console (30).txt"));Check(r.state=="failed"&&r.issues.Any(i=>i.type=="world-load"&&i.impact=="blocking")&&r.issues.Length<=80,"real console30 final world failure survives issue cap");
   r=Analyze(Path.Combine(downloads,"console (56).txt"));Check(r.state=="failed"&&r.issues.Any(i=>i.type=="missing-resource"&&i.impact=="blocking"&&Evidence(i,"bob_idle")),"real console56 required animation plus thread exit blocks");
   foreach(string minute in new[]{"00-39","02-37"}){
    string[] files=Local(local).Where(p=>Path.GetFileName(p).StartsWith("2026-10-22_"+minute,StringComparison.Ordinal)).OrderBy(p=>p).ToArray();Check(files.Length==2,"real paired local session available "+minute);
    r=GameLogAnalysisService.AnalyzePaths(files,"corpus",CancellationToken.None,null);
    if(minute=="00-39")Check((r.state=="entered"||r.state=="entered-with-issues")&&r.attempts.Any(t=>t.state=="failed"),"local retry enters after earlier failed attempt");else Check(r.state=="disconnected"&&r.attempts.All(t=>t.state!="entered"),"local connection loss never claims entered");
   }
   foreach(string name in new[]{"console (35).txt","console (69).txt"}){
    string path=Path.Combine(downloads,name);var m=Inspect(path);var original=GameLogAnalyzer.Analyze(new[]{Input(path,"original","one",m.encoding)},CancellationToken.None,null);
    var renamed=Input(path,"renamed","one",m.encoding);var open=renamed.Open;renamed.Open=()=>new RenameReader(open());var altered=GameLogAnalyzer.Analyze(new[]{renamed},CancellationToken.None,null);
    Check(original.state==altered.state&&original.issues.Count(i=>i.type.StartsWith("lua-"))==altered.issues.Count(i=>i.type.StartsWith("lua-"))&&altered.issues.Any(i=>i.mods.Contains("Corpus Fictional Unknown Mod")),"renaming actual MOD metadata preserves diagnosis "+name);
   }
   var unknown=GameLogAnalyzer.AnalyzeText("ERROR: Exception thrown\njava.lang.RuntimeException: Object tried to call nil\nLua((MOD:Corpus Never Published Fiction)).tick(fiction.lua:42)\nat zombie.gameStates.IngameState.update(IngameState.java:3)\n","fixture");Check(unknown.state=="entered-with-issues"&&unknown.issues.Any(i=>i.type=="lua-call-nil"&&i.mods.Contains("Corpus Never Published Fiction")),"fictional unseen MOD requires no production allowlist");
  }
  // Streaming line adapter is used only for small metamorphic fixtures, never the corpus pass.
  sealed class RenameReader:TextReader {
   readonly TextReader source;string pending="";int offset;
   internal RenameReader(TextReader source){this.source=source;}
   public override int Read(char[] buffer,int index,int count){int written=0;while(written<count){if(offset==pending.Length){string line=source.ReadLine();if(line==null)break;pending=Regex.Replace(line,@"(?<=MOD:).*?(?=\)\)|$)","Corpus Fictional Unknown Mod")+"\n";offset=0;}int n=Math.Min(count-written,pending.Length-offset);pending.CopyTo(offset,buffer,index+written,n);offset+=n;written+=n;}return written;}
   protected override void Dispose(bool disposing){if(disposing)source.Dispose();base.Dispose(disposing);}
  }
  sealed class CancellingReader:TextReader {
   readonly CancellationTokenSource cancellation;int reads;
   internal CancellingReader(CancellationTokenSource cancellation){this.cancellation=cancellation;}
   public override int Read(char[] buffer,int index,int count){for(int i=0;i<count;i++)buffer[index+i]='x';if(++reads==3)cancellation.Cancel();return count;}
  }
  static void Corpus(string local,string downloads,string output){
   Directory.CreateDirectory(output);var states=new Dictionary<string,int>();var issueTypes=new Dictionary<string,int>();var work=new List<Work>();var downloaded=new Dictionary<string,string>();int localCount=0,downloadCount=0,duplicates=0,excluded=0,incomplete=0;long bytes=0,lines=0;var total=Stopwatch.StartNew();
   string assembly=typeof(GameLogAnalyzer).Assembly.Location;string assemblyHash=DigestFile(assembly);
   using(var writer=new StreamWriter(Path.Combine(output,"corpus-files.jsonl"),false,new UTF8Encoding(false))){
    foreach(var scope in new[]{"local","downloads"})foreach(string path in (scope=="local"?Local(local):Downloads(downloads)).OrderBy(p=>p,StringComparer.Ordinal)){
     var measure=Inspect(path);string label=Path.GetFileName(path);string duplicateOf=null;
     if(scope=="downloads"&&label=="console (41).txt"){excluded++;writer.WriteLine(Json.Serialize(new{scope=scope,name=label,status="excluded-no-pz-fingerprint",sha256=measure.hash,bytes=measure.bytes,lines=measure.lines}));continue;}
     if(scope=="downloads"&&downloaded.TryGetValue(measure.hash,out duplicateOf)){duplicates++;writer.WriteLine(Json.Serialize(new{scope=scope,name=label,status="exact-duplicate",duplicateOf=duplicateOf,sha256=measure.hash,bytes=measure.bytes,lines=measure.lines}));continue;}
     if(scope=="local")localCount++;else{downloadCount++;downloaded.Add(measure.hash,label);}
     work.Add(new Work{path=path,scope=scope,measure=measure});
    }
    // Four independent <=64 MiB streams preserve the <=256 MiB concurrent input budget.
    Parallel.ForEach(work,new ParallelOptions{MaxDegreeOfParallelism=4},item=>{
     var watch=Stopwatch.StartNew();var report=GameLogAnalyzer.Analyze(new[]{Input(item.path,"source","one",item.measure.encoding)},CancellationToken.None,null);watch.Stop();
     lock(writer){bytes+=item.measure.bytes;lines+=item.measure.lines;if(report.incomplete)incomplete++;
     if(!states.ContainsKey(report.state))states[report.state]=0;states[report.state]++;foreach(var issue in report.issues){if(!issueTypes.ContainsKey(issue.type))issueTypes[issue.type]=0;issueTypes[issue.type]++;}
     writer.WriteLine(Json.Serialize(new{scope=item.scope,name=Path.GetFileName(item.path),status="analyzed",sha256=item.measure.hash,bytes=item.measure.bytes,lines=item.measure.lines,encoding=item.measure.encoding,state=report.state,stage=report.stage,issues=report.issues.Length,errors=report.errorCount,warnings=report.warningCount,incomplete=report.incomplete,milliseconds=watch.ElapsedMilliseconds,attempts=report.attempts.Length}));writer.Flush();Console.WriteLine(item.scope+" "+Path.GetFileName(item.path)+" "+report.state);}
    });
    int zipCount=0;string zipPath=Path.Combine(downloads,"logs.zip");using(var zip=ZipFile.OpenRead(zipPath))foreach(var entry in zip.Entries){if(!Regex.IsMatch(entry.FullName,@"^(?:console|coop-console|debug-log)/log_\d+\.txt$"))continue;Check(entry.Length<=Limit,"ZIP diagnostic member within limit");string hash;using(var stream=entry.Open())hash=Digest(stream);string duplicate;Check(downloaded.TryGetValue(hash,out duplicate),"ZIP member accounted by analyzed plain input");zipCount++;writer.WriteLine(Json.Serialize(new{scope="downloads-zip",name=entry.FullName,status="exact-duplicate-covered",duplicateOf=duplicate,sha256=hash,bytes=entry.Length}));}
    Check(zipCount==15,"all 15 ZIP diagnostic members covered without extraction");
   }
   Check(localCount==612&&downloadCount==93&&duplicates==1&&excluded==1,"complete final research scope 612 local and 93 unique downloaded inputs");
   var cancelWatch=Stopwatch.StartNew();bool cancelled=false;using(var cancellation=new CancellationTokenSource()){try{GameLogAnalyzer.Analyze(new[]{new GameLogInput{Id="cancel",Name="synthetic cancellation",Open=()=>new CancellingReader(cancellation)}},cancellation.Token,null);}catch(OperationCanceledException){cancelled=true;}}cancelWatch.Stop();Check(cancelled&&cancelWatch.ElapsedMilliseconds<1000,"mid-read cancellation bounded below one second");
   File.WriteAllText(Path.Combine(output,"corpus-summary.json"),Json.Serialize(new{assemblySha256=assemblyHash,checkedUtc=DateTime.UtcNow.ToString("o"),localInputs=localCount,uniqueDownloadedInputs=downloadCount,plainDuplicates=duplicates,excludedUnknown=excluded,zipMembersCoveredByExactHash=15,bytes=bytes,lines=lines,stateCounts=states,issueGroupTypeCounts=issueTypes,incompleteInputs=incomplete,milliseconds=total.ElapsedMilliseconds,cancelMilliseconds=cancelWatch.ElapsedMilliseconds,maxFileBytes=Limit,maxBatchBytes=4*Limit,parallelFileStreams=4,mode="streaming-file-by-file",accuracyBoundary="Scope, state observations and curated assertions only; not a root-cause accuracy score."}),new UTF8Encoding(false));
  }
  public static int Main(string[] args){try{
   bool corpus=args.Contains("--corpus"),real=corpus||args.Contains("--real");
   if(!corpus){if(GameLogAnalyzerTests.Main()!=0||GameLogAnalysisServiceTests.Main()!=0||GameDiagnosticsTests.Main()!=0)return 1;}
   string local=Environment.GetEnvironmentVariable("OZL_CORPUS_LOCAL")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Zomboid");string downloads=Environment.GetEnvironmentVariable("OZL_CORPUS_DOWNLOADS")??@"D:\User\MC233\Downloads";
   if(real)Manual(local,downloads);if(corpus){string output=Environment.GetEnvironmentVariable("OZL_CORPUS_OUTPUT");if(String.IsNullOrEmpty(output))throw new Exception("Set OZL_CORPUS_OUTPUT to an ignored local evidence directory");Corpus(local,downloads,output);}
   Console.WriteLine("Corpus assertions passed="+assertions);return 0;
  }catch(Exception e){Console.WriteLine("Corpus failure: "+e.GetType().Name+" "+(e.Message.StartsWith("FAIL:")?e.Message:"details withheld to preserve source privacy"));return 1;}}
 }
}
