using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;
namespace PZ3D {
 static class MemoryPolicyTests {
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  static object Invoke(MethodInfo method,params object[] args){try{return method.Invoke(null,args);}catch(TargetInvocationException e){throw e.InnerException;}}
  static LauncherMemoryInfo Policy(int total,int available){var method=typeof(LauncherMaintenance).GetMethod("CalculateMemoryInfo",BindingFlags.Static|BindingFlags.NonPublic);Check(method!=null,"total-only policy entry point exists (available RAM cannot limit next-launch budget)");return (LauncherMemoryInfo)Invoke(method,total,available);}
  static void Save(Engine e,int value,Func<LauncherMemoryInfo> provider){var method=typeof(LauncherMaintenance).GetMethod("SetHeapGiB",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(Engine),typeof(int),typeof(Func<LauncherMemoryInfo>)},null);Check(method!=null,"native save accepts an isolated memory provider");Invoke(method,e,value,provider);}
  static bool Denied(Action action){try{action();return false;}catch{return true;}}
  [STAThread] public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   foreach(var row in new[]{new[]{16,4,12},new[]{32,8,24},new[]{64,16,48},new[]{128,16,96},new[]{63,16,47},new[]{0,0,0},new[]{6,2,2},new[]{5,1,1}}){var info=Policy(row[0],0);Check(info.RecommendedHeapGiB==row[1]&&info.MaximumHeapGiB==row[2],"literal total RAM policy "+row[0]);}
   foreach(int available in new[]{1,12,50}){var info=Policy(64,available);Check(info.MaximumHeapGiB==48&&info.RecommendedHeapGiB==16&&info.AvailableGiB==available,"available "+available+" is display-only");}
   Check(Denied(()=>Policy(-1,0))&&Denied(()=>Policy(64,-1))&&Denied(()=>Policy(64,65)),"nonsensical physical memory refused");
   var largest=Policy(int.MaxValue,1);Check(largest.MaximumHeapGiB==1610612735&&largest.RecommendedHeapGiB==16,"large integer provider does not overflow");
   string root=Path.Combine(LauncherTestPaths.Root,"memory-policy-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);
   string original="{\"vmArgs\":[\"-Xmx2g\",\"-Xmx28g\",\"-Xms40g\",\"-javaagent:Other.jar\",\"-Dcustom=keep\"],\"windows\":{\"10\":{\"vmArgs\":[\"-Xmx3g\",\"-Xmx4g\",\"-Xms8g\",\"-XX:+UseZGC\"]},\"11\":{\"vmArgs\":[\"-Xms32g\",\"-Dbranch=keep\"]}},\"extra\":123}";
   File.WriteAllText(e.Config,original,new UTF8Encoding(true));byte[] before=File.ReadAllBytes(e.Config);Check(LauncherMaintenance.ReadHeapGiB(e)==28,"read preserves actual 28 GiB heap under low available RAM");
   int reads=0;Save(e,28,()=>Policy(64,++reads==1?12:1));var config=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(e.Config));var top=((IEnumerable)config["vmArgs"]).Cast<string>().ToArray();var windows=(Dictionary<string,object>)config["windows"];
   Check(top.Count(a=>a.StartsWith("-Xmx"))==1&&top.Contains("-Xmx28g")&&top.Contains("-Xms28g")&&top.Contains("-javaagent:Other.jar")&&top.Contains("-Dcustom=keep"),"low-available save28 normalizes duplicates and preserves third-party JVM arguments");
   foreach(var branch in windows.Values){var args=((IEnumerable)((Dictionary<string,object>)branch)["vmArgs"]).Cast<string>().ToArray();Check(args.Count(a=>a.StartsWith("-Xmx"))==1&&args.Contains("-Xmx28g"),"every Windows branch receives one Xmx28");}
   Check(File.ReadAllText(e.Config).Contains("-Xms8g")&&File.ReadAllText(e.Config).Contains("-Dbranch=keep")&&File.ReadAllText(e.Config).Contains("123"),"smaller Xms and unrelated branch and JSON data survive");
   Check(File.ReadAllBytes(Path.Combine(e.LastBackup,"ProjectZomboid64.json")).SequenceEqual(before),"atomic heap backup keeps exact old bytes including BOM");
   byte[] saved=File.ReadAllBytes(e.Config);Check(Denied(()=>Save(e,49,()=>Policy(64,50)))&&File.ReadAllBytes(e.Config).SequenceEqual(saved),"over-total-budget save refuses before changing bytes");
   Check(Denied(()=>Save(e,0,()=>Policy(64,1)))&&Denied(()=>Save(e,int.MaxValue,()=>Policy(64,1))),"invalid integer range rejected");
   Check(Denied(()=>Save(e,28,()=>new LauncherMemoryInfo{TotalGiB=-1,AvailableGiB=1,MaximumHeapGiB=int.MaxValue}))&&File.ReadAllBytes(e.Config).SequenceEqual(saved),"provider cannot forge a permissive budget");
   reads=0;Check(Denied(()=>Save(e,28,()=>Policy(++reads==1?64:16,1)))&&File.ReadAllBytes(e.Config).SequenceEqual(saved),"save rechecks total budget at atomic boundary");
   string external="{\"vmArgs\":[\"-Xmx6g\",\"-Dexternal=keep\"]}";e.Progress=(v,t)=>File.WriteAllText(e.Config,external);Check(Denied(()=>Save(e,28,()=>Policy(64,1)))&&File.ReadAllText(e.Config)==external,"external edits preserved at atomic boundary");e.Progress=null;
   string bat=Path.Combine(e.Game,"ProjectZomboid64.bat");File.WriteAllText(bat,"java -javaagent:ZombieBuddy.jar");Check(Denied(()=>Save(e,28,()=>Policy(64,1)))&&File.ReadAllText(e.Config)==external,"existing engine guard remains enforced");File.Delete(bat);
   var display=typeof(MainForm).GetMethod("DisplayMemoryInfo",BindingFlags.Static|BindingFlags.NonPublic);Check(display!=null,"legacy memory display uses real heap and policy");using(var input=new NumericUpDown())using(var label=new Label()){Invoke(display,input,label,Policy(64,1),60);Check(input.Value==60&&input.Maximum>=60&&label.Text.Contains("超过"),"legacy over-budget heap displays60 without clamping");Invoke(display,input,label,Policy(64,1),28);Check(input.Value==28&&input.Maximum==48&&label.Text.Contains("当前系统可用")&&label.Text.Contains("基础推荐"),"legacy low availability is advisory and budget stays48");Invoke(display,input,label,Policy(16,1),1);Check(input.Value==1,"legacy input allows actual1 GiB");}
   using(var input=new NumericUpDown())using(var label=new Label()){Invoke(display,input,label,Policy(64,12),6);Check(label.Text.Contains("占用较高")&&input.Value==6&&input.Maximum==48,"legacy low available12 advises without applying recommendation or capping budget");}
   Console.WriteLine("MEMORY POLICY TESTS PASSED; synthetic fixtures retained: "+root);
  }
 }
}
