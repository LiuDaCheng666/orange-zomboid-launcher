using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class GameStartupComponentTests {
  const string Argument="-javaagent:OrangeLauncherRuntime/startup/OrangeStartup.jar";
  static readonly string Nonce=new string('a',32),Next=new string('b',32);
  static Type Component {get{return typeof(Engine).Assembly.GetType("PZ3D.GameStartupComponent");}}
  static object Call(string name,params object[] args){try{return Component.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,args);}catch(TargetInvocationException error){throw error.InnerException;}}
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
  static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
  sealed class Fixture:IDisposable {
   internal string Root=Path.Combine(Path.GetTempPath(),"ozl-owned-startup-"+Guid.NewGuid().ToString("N"));internal Engine E;
   internal Fixture(){string game=Path.Combine(Root,"game");Directory.CreateDirectory(game);E=new Engine(game,Path.Combine(Root,"user"),Path.Combine(Root,"data"),true);File.WriteAllText(E.Config,"{\"vmArgs\":[\"-Xmx12g\",\"-Dprivate=keep\"],\"windows\":{\"10\":{\"vmArgs\":[\"-XX:+UseZGC\"]}},\"custom\":42}");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"fixture");}
   internal string PathOf(string name){return Path.Combine(E.Game,"OrangeLauncherRuntime","startup",name);}
   public void Dispose(){Directory.Delete(Root,true);}
  }
  static void Deployment(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);byte[] jar=File.ReadAllBytes(f.PathOf("OrangeStartup.jar")),config=File.ReadAllBytes(f.E.Config);string session=File.ReadAllText(f.PathOf("session.properties"));
   Check(jar.SequenceEqual(BundledAssets.ReadOwnAsset("OrangeStartup.jar")),"deployed payload equals authenticated bundled asset");
   Check(session.Contains("version=OZL1\n")&&session.Contains("session="+Nonce+"\n")&&session.Contains("game="+Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFullPath(f.E.Game))))&&session.All(c=>c<128),"session uses exact ASCII protocol and canonical game binding");
   string json=File.ReadAllText(f.E.Config);Check(json.Contains("-Xmx12g")&&json.Contains("-Dprivate=keep")&&json.Contains("UseZGC")&&json.Contains("custom"),"deployment preserves heap other branches and custom settings");
   Check((bool)Call("IsOwnedArgument",f.E,Argument)&&LauncherLaunch.Reason(f.E,false,false)=="","launch accepts exactly the bundled verified owned agent");
   File.WriteAllText(f.PathOf("unknown.txt"),"keep");Call("Prepare",f.E,Nonce);
   Check(config.SequenceEqual(File.ReadAllBytes(f.E.Config))&&jar.SequenceEqual(File.ReadAllBytes(f.PathOf("OrangeStartup.jar")))&&File.ReadAllText(f.PathOf("unknown.txt"))=="keep","repeat prepare is idempotent and preserves unknown files");
   File.WriteAllText(f.E.Config,"{\"vmArgs\":[\"-Xmx12g\",\""+Argument+"\",\""+Argument+"\",\"-javaagent:foreign.jar\"],\"windows\":{\"10\":{\"vmArgs\":[\""+Argument+"\",\"-Dbranch=keep\"]}}}");Call("Prepare",f.E,Next);json=File.ReadAllText(f.E.Config);
   Check(json.Split(new[]{Argument},StringSplitOptions.None).Length==2&&json.Contains("foreign.jar")&&json.Contains("-Dbranch=keep"),"owned argument occurs once while foreign agent and branch content remain");
   Check(LauncherLaunch.Reason(f.E,false,false)!="","foreign javaagent still blocks launch");
   foreach(string foreign in new[]{"-javaagent:C:/elsewhere/OrangeStartup.jar",Argument+"=foreign","-javaagent:orangelaucherruntime/startup/OrangeStartup.jar"})Check(!(bool)Call("IsOwnedArgument",f.E,foreign),"same name and options never expand javaagent allowlist");
  }}
  static void Conflicts(){foreach(string reserved in new[]{"OrangeStartup.jar","session.properties","issues.txt","ownership.json"})using(var f=new Fixture()){
   Directory.CreateDirectory(Path.GetDirectoryName(f.PathOf(reserved)));File.WriteAllText(f.PathOf(reserved),"foreign");byte[] before=File.ReadAllBytes(f.E.Config);
   Reject(()=>Call("Prepare",f.E,Nonce),"foreign reserved file refused: "+reserved);Check(File.ReadAllText(f.PathOf(reserved))=="foreign"&&before.SequenceEqual(File.ReadAllBytes(f.E.Config)),"conflict leaves foreign file and config intact");
  }
  using(var f=new Fixture()){Call("Prepare",f.E,Nonce);File.AppendAllText(f.PathOf("OrangeStartup.jar"),"edited");Check(!(bool)Call("IsOwnedArgument",f.E,Argument),"edited owned jar never bypasses launch validation");byte[] config=File.ReadAllBytes(f.E.Config);Reject(()=>Call("Prepare",f.E,Next),"edited payload refused rather than overwritten");Check(config.SequenceEqual(File.ReadAllBytes(f.E.Config)),"payload refusal preserves JVM configuration");}
  using(var f=new Fixture()){Call("Prepare",f.E,Nonce);File.AppendAllText(f.PathOf("session.properties"),"foreign=true\n");Reject(()=>Call("Prepare",f.E,Next),"edited session refused");}
  using(var f=new Fixture()){Call("Prepare",f.E,Nonce);File.WriteAllText(f.PathOf("ownership.json"),File.ReadAllText(f.PathOf("ownership.json")).TrimEnd('}')+",\"foreign\":true}");Reject(()=>Call("Prepare",f.E,Next),"unknown ownership fields preserved rather than discarded");}
  using(var f=new Fixture()){Call("Prepare",f.E,Nonce);File.WriteAllText(f.E.Config,File.ReadAllText(f.E.Config).Replace(Argument,Argument+"\",\""+Argument));using(var held=new FileStream(f.E.Config,FileMode.Open,FileAccess.Read,FileShare.Read)){Reject(()=>Call("Prepare",f.E,Next),"failed config commit rolls back deployment");}Check(File.ReadAllText(f.PathOf("session.properties")).Contains("session="+Nonce+"\n"),"failed preparation restores exact previous session");}
  using(var f=new Fixture()){Reject(()=>Call("Prepare",f.E,"bad"),"invalid nonce rejected before deployment");Check(!Directory.Exists(f.PathOf("")),"invalid session creates no managed directory");}
  using(var f=new Fixture()){string journal=LaunchProfiles.Journal(f.E);Directory.CreateDirectory(Path.GetDirectoryName(journal));File.WriteAllText(journal,"foreign");Reject(()=>Call("Prepare",f.E,Nonce),"pending launch recovery blocks component mutation");Check(!Directory.Exists(f.PathOf("")),"pending recovery creates no managed files");}
  }
  static GameStartupLaunch Launch(Fixture f,string nonce){return new GameStartupLaunch{GamePath=f.E.Game,UserPath=f.E.User,DataPath=f.E.Data,SessionId=nonce,StartedUtc=DateTime.UtcNow,Stage="preparing",TargetKind="normal"};}
  static Dictionary<string,byte[]> RuntimeFiles(Fixture f){return new Dictionary<string,byte[]>{{"OrangeStartup.jar",File.Exists(f.PathOf("OrangeStartup.jar"))?File.ReadAllBytes(f.PathOf("OrangeStartup.jar")):null},{"session.properties",File.Exists(f.PathOf("session.properties"))?File.ReadAllBytes(f.PathOf("session.properties")):null},{"issues.txt",File.Exists(f.PathOf("issues.txt"))?File.ReadAllBytes(f.PathOf("issues.txt")):null},{"ownership.json",File.Exists(f.PathOf("ownership.json"))?File.ReadAllBytes(f.PathOf("ownership.json")):null},{"config",File.ReadAllBytes(f.E.Config)}};}
  static string RuntimePath(Fixture f,string name){return name=="config"?f.E.Config:f.PathOf(name);}
  static void RestoreFiles(Fixture f,Dictionary<string,byte[]> files){foreach(var file in files){string path=RuntimePath(f,file.Key);if(file.Value==null){if(File.Exists(path))File.Delete(path);}else File.WriteAllBytes(path,file.Value);}}
  static string OptionalHash(byte[] bytes){return bytes==null?null:DlssInstaller.Hash(bytes);}
  static void PendingFixture(Fixture f,Dictionary<string,byte[]> old,Dictionary<string,byte[]> next,string kind="prepare"){File.WriteAllText(f.PathOf("ownership.json"),new JavaScriptSerializer().Serialize(new{version="OZL1-PENDING",kind=kind,jar=OptionalHash(old["OrangeStartup.jar"]),session=OptionalHash(old["session.properties"]),issues=OptionalHash(old["issues.txt"]),nextJar=OptionalHash(next["OrangeStartup.jar"]),nextSession=OptionalHash(next["session.properties"]),nextIssues=OptionalHash(next["issues.txt"])}),new UTF8Encoding(false));}
  static void CrashRecovery(){
   foreach(bool upgrade in new[]{false,true})for(int cut=0;cut<16;cut++)using(var f=new Fixture()){
    var old=RuntimeFiles(f);if(upgrade){Call("Prepare",f.E,Nonce);byte[] prior=Encoding.UTF8.GetBytes("intact previous startup version");File.WriteAllBytes(f.PathOf("OrangeStartup.jar"),prior);var json=new JavaScriptSerializer();var owner=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f.PathOf("ownership.json")));owner["jar"]=DlssInstaller.Hash(prior);File.WriteAllText(f.PathOf("ownership.json"),json.Serialize(owner),new UTF8Encoding(false));File.WriteAllText(f.E.Config,"{\"vmArgs\":[\"-Xmx12g\",\""+Argument+"\",\""+Argument+"\"]}");old=RuntimeFiles(f);}
    Call("Prepare",f.E,Next);var next=RuntimeFiles(f);RestoreFiles(f,old);PendingFixture(f,old,next);string[] order={"OrangeStartup.jar","session.properties","issues.txt","config"};foreach(string file in order.Where((name,index)=>(cut&(1<<index))!=0))File.WriteAllBytes(RuntimePath(f,file),next[file]);File.WriteAllText(f.PathOf("foreign.txt"),"preserve");
    Check(LauncherLaunch.PreparationReason(f.E,false,false)=="","interrupted "+(upgrade?"upgrade":"install")+" boundary "+cut+" remains eligible for safe preparation");string retry=cut%2==0?Next:Nonce;Call("Prepare",f.E,retry);
    Check((bool)Call("IsOwnedArgument",f.E,Argument)&&File.ReadAllText(f.PathOf("session.properties")).Contains("session="+retry+"\n")&&File.ReadAllLines(f.PathOf("issues.txt"))[1]==retry&&!File.ReadAllText(f.PathOf("ownership.json")).Contains("PENDING"),"interrupted "+(upgrade?"upgrade":"install")+" boundary "+cut+" recovers automatically");Check(File.ReadAllText(f.PathOf("foreign.txt"))=="preserve"&&File.ReadAllText(f.E.Config).Contains("-Xmx12g"),"partial deployment preserves foreign files and memory");
   }
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var old=RuntimeFiles(f);Call("Prepare",f.E,Next);var next=RuntimeFiles(f);RestoreFiles(f,old);PendingFixture(f,old,next);byte[] pending=File.ReadAllBytes(f.PathOf("ownership.json"));Check(!(bool)Call("WriteIssues",Launch(f,Nonce),new GameStartupSnapshot{SessionId=Nonce})&&pending.SequenceEqual(File.ReadAllBytes(f.PathOf("ownership.json"))),"previous observer cannot consume a pending new deployment before its session file commits");Call("Prepare",f.E,Next);Check(File.ReadAllLines(f.PathOf("issues.txt"))[1]==Next,"pending deployment still recovers after stale observer refusal");
   }
   for(int cut=0;cut<=1;cut++)using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var old=RuntimeFiles(f);var snapshot=new GameStartupSnapshot{SessionId=Nonce,Issues=new[]{new GameLogIssue{attention="primary",title="真实问题",meaning="检查日志"}}};Check((bool)Call("WriteIssues",Launch(f,Nonce),snapshot),"IPC crash fixture captures an actual owned update");var next=RuntimeFiles(f);RestoreFiles(f,old);PendingFixture(f,old,next,"issues");if(cut==1)File.WriteAllBytes(f.PathOf("issues.txt"),next["issues.txt"]);
    byte[] pending=File.ReadAllBytes(f.PathOf("ownership.json"));Check(!(bool)Call("WriteIssues",Launch(f,Next),new GameStartupSnapshot{SessionId=Next})&&pending.SequenceEqual(File.ReadAllBytes(f.PathOf("ownership.json"))),"stale observer cannot consume another session's pending write");Check((bool)Call("WriteIssues",Launch(f,Nonce),snapshot),"interrupted IPC boundary "+cut+" recovers on next analysis write");Call("Prepare",f.E,Next);Check((bool)Call("IsOwnedArgument",f.E,Argument),"interrupted IPC never blocks the following launch");
   }
   foreach(string edited in new[]{"OrangeStartup.jar","session.properties","issues.txt","ownership.json"})using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var old=RuntimeFiles(f);Call("Prepare",f.E,Next);var next=RuntimeFiles(f);RestoreFiles(f,old);PendingFixture(f,old,next);File.WriteAllText(f.PathOf(edited),"foreign edited bytes");byte[] config=File.ReadAllBytes(f.E.Config);Reject(()=>Call("Prepare",f.E,Nonce),"pending recovery refuses foreign edits to "+edited);Check(File.ReadAllText(f.PathOf(edited))=="foreign edited bytes"&&config.SequenceEqual(File.ReadAllBytes(f.E.Config)),"pending recovery preserves foreign metadata and configuration");
   }
  }
  static void RewriteSession(Fixture f,string text){File.WriteAllText(f.PathOf("session.properties"),text,new UTF8Encoding(false));var json=new JavaScriptSerializer();var record=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f.PathOf("ownership.json")));record["session"]=DlssInstaller.Hash(File.ReadAllBytes(f.PathOf("session.properties")));File.WriteAllText(f.PathOf("ownership.json"),json.Serialize(record),new UTF8Encoding(false));}
  static void Upgrade(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);byte[] previous=Encoding.UTF8.GetBytes("intact prior owned version fixture");File.WriteAllBytes(f.PathOf("OrangeStartup.jar"),previous);var json=new JavaScriptSerializer();var record=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f.PathOf("ownership.json")));record["jar"]=DlssInstaller.Hash(previous);File.WriteAllText(f.PathOf("ownership.json"),json.Serialize(record),new UTF8Encoding(false));File.WriteAllText(f.PathOf("foreign.txt"),"keep");
   Check(!(bool)Call("IsOwnedArgument",f.E,Argument)&&LauncherLaunch.Reason(f.E,false,false)!="","ownership record alone cannot authorize old or arbitrary executable bytes");
   Check(LaunchPreflight.Check(f.E,false,false).items.First(i=>i.id=="launch-config").status=="ok"&&previous.SequenceEqual(File.ReadAllBytes(f.PathOf("OrangeStartup.jar"))),"read-only preflight permits preparation of intact prior owned version without authorizing execution");
   Call("Prepare",f.E,Next);Check(File.ReadAllBytes(f.PathOf("OrangeStartup.jar")).SequenceEqual(BundledAssets.ReadOwnAsset("OrangeStartup.jar"))&&(bool)Call("IsOwnedArgument",f.E,Argument)&&LauncherLaunch.Reason(f.E,false,false)=="","ordinary upgrade replaces intact owned prior version with current authenticated resource");Check(File.ReadAllText(f.PathOf("foreign.txt"))=="keep"&&File.ReadAllText(f.E.Config).Contains("-Xmx12g"),"upgrade preserves foreign files and player memory");
  }}
  static void Boundaries(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);var snapshot=new GameStartupSnapshot{SessionId=Nonce};string text=File.ReadAllText(f.PathOf("session.properties"));var values=text.Trim().Split('\n').Select(line=>line.Split(new[]{'='},2)).ToDictionary(v=>v[0],v=>v[1]);long past=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-601;
   RewriteSession(f,text.Replace("created="+values["created"],"created="+past).Replace("expires="+values["expires"],"expires="+(past+600)));Check(!(bool)Call("WriteIssues",Launch(f,Nonce),snapshot),"expired but structurally valid session cannot receive exchange");
   Call("Prepare",f.E,Next);Check(File.ReadAllText(f.PathOf("session.properties")).Contains("session="+Next+"\n"),"new launch renews an unchanged expired owned session");
   text=File.ReadAllText(f.PathOf("session.properties"));RewriteSession(f,text.Replace("game="+Convert.ToBase64String(Encoding.UTF8.GetBytes(f.E.Game)),"game="+Convert.ToBase64String(Encoding.UTF8.GetBytes(f.Root))));
   Check(!(bool)Call("WriteIssues",Launch(f,Next),new GameStartupSnapshot{SessionId=Next}),"wrong game binding rejects exchange even with matching ownership hash");Reject(()=>Call("Prepare",f.E,Nonce),"wrong-root existing session cannot be overwritten");
  }
  using(var f=new Fixture()){Call("Prepare",f.E,Nonce);File.Delete(f.PathOf("OrangeStartup.jar"));Check(!(bool)Call("IsOwnedArgument",f.E,Argument),"missing owned jar cannot bypass validation");Reject(()=>Call("Prepare",f.E,Next),"missing owned payload refuses ambiguous repair");}
  using(var f=new Fixture()){Directory.CreateDirectory(f.PathOf("OrangeStartup.jar"));Reject(()=>Call("Prepare",f.E,Nonce),"reserved file occupied by a directory is preserved");Check(Directory.Exists(f.PathOf("OrangeStartup.jar")),"directory conflict remains intact");}
  using(var f=new Fixture()){
   string target=Path.Combine(f.Root,"foreign-target"),link=Path.Combine(f.E.Game,"OrangeLauncherRuntime");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"keep.txt"),"foreign");
   using(var process=Process.Start(new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+link+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true})){process.StandardOutput.ReadToEnd();process.StandardError.ReadToEnd();process.WaitForExit();Check(process.ExitCode==0,"isolated directory junction fixture created");}
   try{Reject(()=>Call("Prepare",f.E,Nonce),"managed directory cannot traverse a reparse point");Check(File.ReadAllText(Path.Combine(target,"keep.txt"))=="foreign"&&!Directory.Exists(Path.Combine(target,"startup")),"reparse refusal preserves target without deploying outside game");}finally{Directory.Delete(link,false);}
  }
  }
  static void Exchange(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);var primary=new GameLogIssue{attention="primary",title="模组异常",meaning="脚本无法完成",mods=new[]{"ExampleMod"},evidence=new[]{new GameLogEvidence{text="error token=secret-value at C:\\Users\\Private\\game"}},advice="检查模组"};
   var snapshot=new GameStartupSnapshot{SessionId=Nonce,Issues=new[]{new GameLogIssue{attention="background",title="noise"},primary}};
   Check((bool)Call("WriteIssues",Launch(f,Nonce),snapshot),"current-session primary exchange written");string[] lines=File.ReadAllLines(f.PathOf("issues.txt"));Check(lines.Length==8&&lines[0]=="OZL1"&&lines[1]==Nonce&&Encoding.UTF8.GetString(Convert.FromBase64String(lines[3]))==primary.title,"issue file has eight-line versioned session protocol");
   Check(!File.ReadAllText(f.PathOf("issues.txt")).Contains("secret-value")&&!Encoding.UTF8.GetString(Convert.FromBase64String(lines[6])).Contains("secret-value"),"evidence sanitized before exchange");
   byte[] before=File.ReadAllBytes(f.PathOf("issues.txt"));Check(!(bool)Call("WriteIssues",Launch(f,Next),snapshot)&&before.SequenceEqual(File.ReadAllBytes(f.PathOf("issues.txt"))),"stale observer cannot write a different session");
   snapshot.Issues=new[]{new GameLogIssue{attention="review",title="unfamiliar"}};Check((bool)Call("WriteIssues",Launch(f,Nonce),snapshot),"quiet result clears exchange");lines=File.ReadAllLines(f.PathOf("issues.txt"));Check(lines.Length==8&&lines[3]==""&&lines[4]=="","review and background findings never fill right-side area");
   string large=new string('模',4000);snapshot.Issues=new[]{new GameLogIssue{attention="primary",title=large,meaning=large,advice=large,mods=Enumerable.Repeat(large,20).ToArray(),evidence=Enumerable.Range(0,6).Select(i=>new GameLogEvidence{text=large}).ToArray()}};Check((bool)Call("WriteIssues",Launch(f,Nonce),snapshot)&&new FileInfo(f.PathOf("issues.txt")).Length<=16384,"large actionable Chinese findings fit bounded exchange without disappearing");
   Call("Prepare",f.E,Next);Check(!(bool)Call("WriteIssues",Launch(f,Nonce),new GameStartupSnapshot{SessionId=Nonce,Issues=new[]{primary}}),"previous observer cannot overwrite new current launch");
   File.WriteAllText(f.PathOf("issues.txt"),"foreign");Check(!(bool)Call("WriteIssues",Launch(f,Next),new GameStartupSnapshot{SessionId=Next,Issues=new[]{primary}})&&File.ReadAllText(f.PathOf("issues.txt"))=="foreign","foreign exchange edits preserved");
  }}
  sealed class FakeMonitor:IGameStartupMonitor {internal int Started,SubmittedCount,FailedCount,Disposed;public void Start(){Started++;}public void Submitted(){SubmittedCount++;}public void Fail(string message){FailedCount++;}public void Dispose(){Disposed++;}}
  static void Wait(Func<bool> condition,string name){var clock=Stopwatch.StartNew();while(clock.ElapsedMilliseconds<6000&&!condition())System.Threading.Thread.Sleep(50);Check(condition(),name);}
  static void ContinuedObservation(){using(var f=new Fixture()){
   Call("Prepare",f.E,Nonce);string image=Path.Combine(f.E.Game,"ProjectZomboid64.exe"),console=Path.Combine(f.E.User,"console.txt");Directory.CreateDirectory(f.E.User);File.WriteAllText(console,"");
   using(var compiler=System.CodeDom.Compiler.CodeDomProvider.CreateProvider("CSharp")){var options=new System.CodeDom.Compiler.CompilerParameters(new[]{"System.dll"},image){GenerateExecutable=true,TempFiles=new System.CodeDom.Compiler.TempFileCollection(f.Root,false)};var result=compiler.CompileAssemblyFromSource(options,"using System; class Owned {static void Main(){Console.WriteLine(\"owned fixture\");Console.In.ReadLine();}}");Check(!result.Errors.HasErrors,"continued-observation native fixture compiles");}
   var launch=Launch(f,Nonce);object sync=new object();GameStartupSnapshot latest=null;Process client=null;using(var monitor=new GameStartupMonitor(launch,value=>{lock(sync){latest=value;}},false)){
    try{monitor.Start();client=Process.Start(new ProcessStartInfo(image){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true});Check(client.StandardOutput.ReadLine()=="owned fixture","continued-observation fixture starts without launching a game");Wait(()=>{lock(sync){return latest!=null&&latest.ProcessId==client.Id;}},"headless observer binds exact fresh fixture process");
     long past=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-700;string text=File.ReadAllText(f.PathOf("session.properties"));var values=text.Trim().Split('\n').Select(v=>v.Split(new[]{'='},2)).ToDictionary(v=>v[0],v=>v[1]);RewriteSession(f,text.Replace("created="+values["created"],"created="+past).Replace("expires="+values["expires"],"expires="+(past+600)));
     var snapshot=new GameStartupSnapshot{SessionId=Nonce,ProcessId=client.Id,ProcessBirth=client.StartTime.ToUniversalTime().Ticks,Issues=new[]{new GameLogIssue{attention="primary",title="后续世界异常",meaning="检查新世界日志"}}};Check((bool)Call("WriteIssues",launch,snapshot),"bound live client continues analysis after initial activation TTL");snapshot.ProcessBirth++;Check(!(bool)Call("WriteIssues",launch,snapshot),"expired exchange rejects a reused or mismatched process birth");
     File.AppendAllText(console,"LOG : Lua f:0> [OZL-STARTUP] menu-ready "+Nonce+"\n");Wait(()=>{lock(sync){return latest!=null&&latest.Complete;}},"headless observation sees actual menu handoff");File.AppendAllText(console,"java.lang.OutOfMemoryError: Java heap space\n");Wait(()=>{lock(sync){return latest!=null&&latest.Issues.Any(i=>i.type=="java-heap");}},"headless monitor continues actionable analysis after main menu");
    }finally{monitor.Dispose();if(client!=null){client.StandardInput.WriteLine("finish");client.WaitForExit(5000);client.Dispose();}}
   }
  }}
  static string Exported(Fixture f,int field){return Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllLines(f.PathOf("issues.txt"))[field]));}
  static void IssueSelection(){
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var launch=Launch(f,Nonce);var reducer=new GameStartupReducer(launch,false);var fake=new FakeMonitor();Action<GameStartupSnapshot> publish=null;using(var observer=new GameStartupObserver((request,changed)=>{publish=changed;return fake;})){
     observer.Handle(launch);reducer.Line("java.lang.IllegalStateException: older failure");var old=reducer.Snapshot(launch.StartedUtc);publish(old);string oldIdentity=(string)Call("IssueIdentity",old);
     reducer.Line("java.lang.OutOfMemoryError: Java heap space");var heap=reducer.Snapshot(launch.StartedUtc.AddSeconds(1));Check(heap.Issues.First(i=>i.attention=="primary").type=="java-heap"&&(string)Call("IssueIdentity",heap)!=oldIdentity,"new core heap failure outranks an older lower-priority exception");publish(heap);Check(Exported(f,3).Contains("堆内存"),"observer exports the newly selected core failure");
     string heapIdentity=(string)Call("IssueIdentity",heap);DateTime marker=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(f.PathOf("issues.txt"),marker);reducer.Line("java.lang.IllegalArgumentException: newer lower-priority failure");var newer=reducer.Snapshot(launch.StartedUtc.AddSeconds(2));publish(newer);Check(newer.Issues.First(i=>i.attention=="primary").type=="java-heap"&&(string)Call("IssueIdentity",newer)==heapIdentity&&File.GetLastWriteTimeUtc(f.PathOf("issues.txt"))==marker,"newer lower-priority noise cannot displace or refresh the core failure");
    }
   }
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var launch=Launch(f,Nonce);var reducer=new GameStartupReducer(launch,false);reducer.Line("java.lang.IllegalStateException: first equal-priority occurrence");var first=reducer.Snapshot(launch.StartedUtc);reducer.Line("java.lang.IllegalArgumentException: second equal-priority occurrence");var second=reducer.Snapshot(launch.StartedUtc.AddSeconds(1));Check(second.Issues.First(i=>i.attention=="primary").meaning.Contains("second equal-priority"),"equal-priority selection uses the actual newer occurrence");
    reducer.Line("java.lang.IllegalStateException: first equal-priority occurrence");var repeated=reducer.Snapshot(launch.StartedUtc.AddSeconds(2));Check(repeated.Issues.First(i=>i.attention=="primary").meaning.Contains("first equal-priority")&&(string)Call("IssueIdentity",repeated)!=(string)Call("IssueIdentity",first),"merged repeated blocks retain their true latest occurrence order");Check(!new JavaScriptSerializer().Serialize(repeated).Contains("Observation"),"internal observation metadata is excluded from serialized snapshots");
   }
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var launch=Launch(f,Nonce);var reducer=new GameStartupReducer(launch,false);var fake=new FakeMonitor();Action<GameStartupSnapshot> publish=null;using(var observer=new GameStartupObserver((request,changed)=>{publish=changed;return fake;})){
     observer.Handle(launch);for(int i=0;i<150;i++)reducer.Line("LOG : General f:0> older ordinary row "+i);reducer.Line("java.lang.OutOfMemoryError: Java heap space");var first=reducer.Snapshot(launch.StartedUtc);publish(first);string identity=(string)Call("IssueIdentity",first);DateTime marker=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(f.PathOf("issues.txt"),marker);
     for(int i=0;i<20;i++)reducer.Line("LOG : General f:0> overlapping ordinary row "+i);var shifted=reducer.Snapshot(launch.StartedUtc.AddSeconds(1));publish(shifted);Check((string)Call("IssueIdentity",shifted)==identity&&File.GetLastWriteTimeUtc(f.PathOf("issues.txt"))==marker,"relative buffer-window shifts never refresh an already observed issue");
     for(int i=0;i<400;i++)reducer.Line("LOG : General f:0> later ordinary row "+i);var evicted=reducer.Snapshot(launch.StartedUtc.AddSeconds(2));publish(evicted);Check((string)Call("IssueIdentity",evicted)==identity&&File.GetLastWriteTimeUtc(f.PathOf("issues.txt"))==marker,"retained history after raw-window eviction has no fabricated freshness");
     reducer.Line("java.lang.OutOfMemoryError: Java heap space");var recurrence=reducer.Snapshot(launch.StartedUtc.AddSeconds(3));publish(recurrence);Check((string)Call("IssueIdentity",recurrence)!=identity&&File.GetLastWriteTimeUtc(f.PathOf("issues.txt"))!=marker&&Exported(f,3).Contains("堆内存"),"genuine identical-error recurrence after eviction gets fresh IPC");
    }
   }
  }
  static void IssueEligibility(){
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var launch=Launch(f,Nonce);var reducer=new GameStartupReducer(launch,false);var fake=new FakeMonitor();Action<GameStartupSnapshot> publish=null;using(var observer=new GameStartupObserver((request,changed)=>{publish=changed;return fake;})){
     observer.Handle(launch);reducer.Line("java.lang.OutOfMemoryError: Java heap space");publish(reducer.Snapshot(launch.StartedUtc));publish(reducer.Snapshot(launch.StartedUtc.AddSeconds(31)));Check(Exported(f,3)=="","expired observations clear IPC while remaining in diagnostic history");
     DateTime marker=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(f.PathOf("issues.txt"),marker);var stale=reducer.Snapshot(launch.StartedUtc.AddSeconds(32));publish(stale);Check(stale.Issues.Any(i=>i.type=="java-heap")&&File.GetLastWriteTimeUtc(f.PathOf("issues.txt"))==marker,"expiry retains history without repeatedly refreshing the empty packet");
     reducer.ObservedLine("java.lang.IllegalStateException: fresh lower-priority world failure",launch.StartedUtc.AddSeconds(33));publish(reducer.Snapshot(launch.StartedUtc.AddSeconds(33)));Check(Exported(f,4).Contains("fresh lower-priority"),"fresh later-operation failure is selected ahead of expired high-priority history");
    }
   }
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var launch=Launch(f,Nonce);var reducer=new GameStartupReducer(launch,false);var fake=new FakeMonitor();Action<GameStartupSnapshot> publish=null;using(var observer=new GameStartupObserver((request,changed)=>{publish=changed;return fake;})){
     observer.Handle(launch);reducer.Line("java.lang.OutOfMemoryError: Java heap space");publish(reducer.Snapshot(launch.StartedUtc));reducer.Line("LOG : Lua f:0> [OZL-STARTUP] menu-ready "+Nonce);reducer.Line("java.lang.IllegalStateException: later world operation failure");var world=reducer.Snapshot(launch.StartedUtc.AddSeconds(1));publish(world);Check(world.Issues.Any(i=>i.type=="java-heap")&&Exported(f,4).Contains("later world operation"),"verified menu handoff excludes boot history from later-operation issue selection");
    }
   }
  }
  static void SaturatedEligibility(){
   for(int mode=0;mode<7;mode++)using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var launch=Launch(f,Nonce);var reducer=new GameStartupReducer(launch,false);var fake=new FakeMonitor();Action<GameStartupSnapshot> publish=null;using(var observer=new GameStartupObserver((request,changed)=>{publish=changed;return fake;})){
     observer.Handle(launch);for(int i=0;i<(mode<3?24:80);i++)reducer.ObservedLine("java.lang.OutOfMemoryError: Java heap space historical "+i,launch.StartedUtc);var original=reducer.Snapshot(launch.StartedUtc);Check(original.Issues.Length==24&&original.Issues.All(i=>i.type=="java-heap"),"saturation fixture fills bounded history from distinct original heap issues, mode "+mode);publish(original);
     DateTime next=launch.StartedUtc.AddSeconds(mode<6&&mode%3==0?31:1);if(mode%3==1)reducer.ObservedLine("LOG : Lua f:0> [OZL-STARTUP] menu-ready "+Nonce,next);reducer.ObservedLine(mode==6?"java.lang.OutOfMemoryError: Java heap space fresh saturated world failure":"java.lang.IllegalStateException: fresh saturated world failure",next);var current=reducer.Snapshot(next);publish(current);
     if(mode<6&&mode%3<2){Check(current.Issues.Length==24&&current.Issues.Any(i=>i.type=="java-exception"&&i.meaning.Contains("fresh saturated")),(mode%3==0?"expired":"menu-ineligible")+" saturated heap history cannot evict a fresh core exception");Check(Exported(f,4).Contains("fresh saturated"),(mode%3==0?"expired":"menu-ineligible")+" saturation exports the fresh core exception");}
     else if(mode==6)Check(current.Issues.Length==24&&Exported(f,6).Contains("fresh saturated"),"80-group native analysis retains genuinely newer equal-priority occurrence");
     else Check(current.Issues.Length==24&&current.Issues.All(i=>i.type=="java-heap")&&Exported(f,3).Contains("堆内存"),"24 active higher-priority issues retain precedence over a fresh lower-priority issue");
    }
   }
  }
  static void Headless(){
   var field=typeof(ModernForm).GetField("gameStartupObserver",BindingFlags.Instance|BindingFlags.NonPublic);Check(field!=null&&field.FieldType==typeof(GameStartupObserver)&&typeof(ModernForm).GetField("gameStartupPresenter",BindingFlags.Instance|BindingFlags.NonPublic)==null,"production host no longer owns external startup presenter");
   var launch=new GameStartupLaunch{SessionId=Nonce,StartedUtc=DateTime.UtcNow.AddMinutes(-3),TargetKind="normal"};
   var ctor=typeof(GameStartupReducer).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(GameStartupLaunch),typeof(bool)},null);Check(ctor!=null,"headless reducer has explicit truthful observation option");var state=(GameStartupReducer)ctor.Invoke(new object[]{launch,false});state.ProcessId=42;Check(state.Snapshot(DateTime.UtcNow).Stage!="manual","long loading is not pseudo-completed at 90 seconds");
   using(var f=new Fixture()){
    Call("Prepare",f.E,Nonce);var fake=new FakeMonitor();Action<GameStartupSnapshot> publish=null;using(var observer=new GameStartupObserver((request,changed)=>{publish=changed;return fake;})){
     var request=Launch(f,Nonce);request.Stage="preflight";observer.Handle(request);Check(fake.Started==0,"headless observer starts only at committed launch");request.Stage="preparing";observer.Handle(request);observer.Handle(request);Check(fake.Started==1,"committed observer starts once without a window");
     var issue=new GameLogIssue{attention="primary",title="真实异常",meaning="需要检查",evidence=new[]{new GameLogEvidence{text="same actionable evidence",line=1}}};publish(new GameStartupSnapshot{SessionId=Nonce,Issues=new[]{issue}});Check(Encoding.UTF8.GetString(Convert.FromBase64String(File.ReadAllLines(f.PathOf("issues.txt"))[3]))==issue.title,"headless observer forwards primary analysis to game exchange");byte[] first=File.ReadAllBytes(f.PathOf("issues.txt"));publish(new GameStartupSnapshot{SessionId=Nonce,Issues=new[]{issue}});Check(first.SequenceEqual(File.ReadAllBytes(f.PathOf("issues.txt"))),"unchanged analysis does not rewrite exchange every monitor tick");
     DateTime marker=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc);File.SetLastWriteTimeUtc(f.PathOf("issues.txt"),marker);issue.evidence[0].line=8;publish(new GameStartupSnapshot{SessionId=Nonce,Issues=new[]{issue}});Check(File.GetLastWriteTimeUtc(f.PathOf("issues.txt"))==marker,"rolling relative line numbers cannot refresh old primary issue timestamps");
     request.Stage="submitted";observer.Handle(request);Check(fake.SubmittedCount==1,"headless observer preserves submitted lifecycle");request.Stage="failed";observer.Handle(request);Check(fake.FailedCount==1&&fake.Disposed==1,"launch failure disposes observation");publish(new GameStartupSnapshot{SessionId=Nonce});Check(first.SequenceEqual(File.ReadAllBytes(f.PathOf("issues.txt"))),"disposed observer ignores late callbacks");
    }Check(fake.Disposed==1,"observer disposal is idempotent");
   }
  }
  public static void Run(){Check(Component!=null,"owned startup component exists");IssueSelection();IssueEligibility();SaturatedEligibility();Deployment();CrashRecovery();Conflicts();Upgrade();Boundaries();Exchange();Headless();ContinuedObservation();}
  [STAThread] public static int Main(){try{Run();Console.WriteLine("ALL OWNED STARTUP COMPONENT TESTS PASSED");return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
