using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
namespace PZ3D {
 static class GameLogAnalysisServiceTests {
  static int passed;
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
  static GameLogReport Analyze(string[] paths){var type=typeof(Engine).Assembly.GetType("PZ3D.GameLogAnalysisService");Check(type!=null,"native local/import log service exists");try{return (GameLogReport)type.GetMethod("AnalyzePaths",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{paths,"imported",CancellationToken.None,null});}catch(TargetInvocationException e){throw e.InnerException;}}
  static string Root(){string p=Path.Combine(LauncherTestPaths.Root,"log-service-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
  static void Zip(string path,string name,string text){using(var file=File.Create(path))using(var zip=new ZipArchive(file,ZipArchiveMode.Create))using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(text);}
  const string Sample="ERROR: General f:15> Exception thrown\njava.lang.RuntimeException: attempted index: battery of non-table: null\nLua((MOD:Unseen Repair Kit)).tick(battery.lua:18)\nat zombie.gameStates.IngameState.update(IngameState.java:55)\n";
  public static int Main(){try{
   string root=Root(),log=Path.Combine(root,"console.txt");byte[] original=new UTF8Encoding(false).GetBytes(Sample);File.WriteAllBytes(log,original);DateTime before=File.GetLastWriteTimeUtc(log);
   var report=Analyze(new[]{log});Check(report.issues.Any(i=>i.mods.Contains("Unseen Repair Kit")),"real file reaches generic mod parser");Check(report.origin=="imported","import provenance retained");Check(File.ReadAllBytes(log).SequenceEqual(original)&&File.GetLastWriteTimeUtc(log)==before,"analysis preserves bytes and last-write time");
   string archive=Path.Combine(root,"logs.zip");Zip(archive,"logs/console.txt",Sample);report=Analyze(new[]{archive});Check(report.issues.Any(i=>i.mods.Contains("Unseen Repair Kit")),"ZIP member analyzed without extraction");Check(!Directory.Exists(Path.Combine(root,"logs")),"ZIP is never extracted");
   report=Analyze(new[]{log,archive});Check(report.fileCount==1,"byte-identical selected file and ZIP member analyzed once");
   string bad=Path.Combine(root,"bad.zip");Zip(bad,"../console.txt",Sample);report=Analyze(new[]{bad});Check(report.incomplete&&report.warnings.Length>0,"traversal ZIP rejected with explicit gap");
   string binary=Path.Combine(root,"binary.log");File.WriteAllBytes(binary,new byte[]{0,0,0,255,1,2,3});report=Analyze(new[]{binary});Check(report.incomplete&&report.issues.Length==0,"binary input never labeled healthy text");
   string old=Path.Combine(root,"coop-console.txt");File.WriteAllText(old,Sample+"旧版游戏日志\n",Encoding.GetEncoding(54936));report=Analyze(new[]{old});Check(report.sources.Any(s=>s.encoding=="gb18030"),"strict legacy Chinese encoding supported");
   string unsupported=Path.Combine(root,"readme.exe");File.WriteAllText(unsupported,Sample);report=Analyze(new[]{unsupported});Check(report.incomplete&&report.issues.Length==0,"executable inputs refused");
   string folder=Path.Combine(root,"batch");Directory.CreateDirectory(Path.Combine(folder,"Logs","session"));File.WriteAllText(Path.Combine(folder,"Logs","session","2026-10-22_01-00_DebugLog.txt"),Sample);File.WriteAllText(Path.Combine(folder,"passwords.txt"),"unrelated personal file");report=Analyze(new[]{folder});Check(report.fileCount==1,"folder only discovers allowlisted game logs");
   string local=Path.Combine(root,"local");Directory.CreateDirectory(Path.Combine(local,"Logs"));string body="ERROR: General f:5> unfamiliar callback failed\nLua((MOD:Unknown Battery)).tick(battery.lua:18)\nLOG : General f:5> STATE: exit zombie.gameStates.GameLoadingState\n";File.WriteAllText(Path.Combine(local,"console.txt"),body);File.WriteAllText(Path.Combine(local,"Logs","2026-10-22_01-00_DebugLog.txt"),String.Join("\n",body.TrimEnd('\n').Split('\n').Select(l=>"[22-10-26 01:00:00.000] "+l))+"\n");
   var serviceType=typeof(Engine).Assembly.GetType("PZ3D.GameLogAnalysisService");var localService=(IDisposable)Activator.CreateInstance(serviceType,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{local,Path.Combine(root,"game")},null);try{serviceType.GetMethod("StartLocal",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(localService,null);GameLogAnalysisStatus status=null;var until=DateTime.UtcNow.AddSeconds(8);do{Thread.Sleep(10);status=(GameLogAnalysisStatus)serviceType.GetMethod("Status",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(localService,null);}while(status.state=="running"&&DateTime.UtcNow<until);Check(status.state=="completed","background local analysis actually completes");Check(status.report.state=="entered-with-issues","matching console and dated DebugLog form one local session");Check(status.report.origin=="local"&&status.report.reportText.Contains("local"),"export retains local provenance");}finally{localService.Dispose();}
   string oversize=Path.Combine(root,"oversize.log");using(var file=File.Create(oversize))file.SetLength(64L*1024*1024+1);report=Analyze(new[]{oversize});Check(report.incomplete&&report.fileCount==0,"oversized files refused before reading");
   string bomb=Path.Combine(root,"bomb.zip");Zip(bomb,"console.txt",new string('x',2*1024*1024));report=Analyze(new[]{bomb});Check(report.incomplete&&report.fileCount==0,"high compression expansion is refused");
   foreach(string action in new[]{"logAnalysisStatus","logAnalysisLocal","logAnalysisImport","logAnalysisFolder","logAnalysisCancel","logAnalysisExport"}){
    string request="{\"id\":\"logs\",\"action\":\""+action+"\",\"payload\":{}}";
    Check(ModernHostPolicy.Validate("https://orange.local/index.html",request)!=null,"host accepts explicit log action "+action);
    bool rejected=false;try{ModernHostPolicy.Validate("https://orange.local/index.html",request.Replace("\"payload\":{}","\"payload\":{\"path\":\"C:/arbitrary\"}"));}catch{rejected=true;}Check(rejected,"host refuses page supplied paths "+action);
   }
   Console.WriteLine("ALL "+passed+" LOG SERVICE CHECKS PASSED");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
