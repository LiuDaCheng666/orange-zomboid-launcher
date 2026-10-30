using System;using System.IO;using System.Text;using System.Threading;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace PZ3D {static class NativeStateAtomicTests {
 static int assertions;static void Check(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 sealed class Record {public int revision{get;set;}public string padding{get;set;}public int footer{get;set;}}
 public static int Main(string[] args){string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);var evidence=new Dictionary<string,object>();try{
  string file=Path.Combine(root,"state.json");Check(LauncherNativeState.Read<Record>(file,()=>new Record{revision=-1}).revision==-1,"Missing state uses its default");
  LauncherNativeState.Write(file,new Record{revision=0,padding="original",footer=0});
  using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){bool failed=false;try{LauncherNativeState.Write(file,new Record{revision=1,padding="new",footer=1});}catch(IOException){failed=true;}Check(failed,"Control: a reader without Delete sharing obstructs replacement");}
  Check(LauncherNativeState.Read<Record>(file,()=>null).revision==0,"Failed replacement preserves the original record");Check(Directory.GetFiles(root,"*.new-*").Length==0,"Failed replacement removes its temporary file");
  using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read|FileShare.Delete)){
   LauncherNativeState.Write(file,new Record{revision=2,padding="replacement",footer=2});using(var reader=new StreamReader(held,Encoding.UTF8,true,1024,true)){Check(new JavaScriptSerializer().Deserialize<Record>(reader.ReadToEnd()).revision==0,"An open shared reader retains a complete original record");}
   Check(LauncherNativeState.Read<Record>(file,()=>null).revision==2,"A new reader observes the complete replacement");
  }
  File.WriteAllText(file,"{invalid",Encoding.UTF8);bool corrupt=false;try{LauncherNativeState.Read<Record>(file,()=>null);}catch(Exception ex){corrupt=ex.Message.Contains("损坏");}Check(corrupt,"Invalid JSON remains reported");Check(File.ReadAllText(file).Contains("invalid"),"Reading invalid JSON never changes it");
  File.WriteAllText(file,new string('x',2097153),Encoding.UTF8);bool oversized=false;try{LauncherNativeState.Read<Record>(file,()=>null);}catch(Exception ex){oversized=ex.Message.Contains("大小限制");}Check(oversized,"Oversized records retain the specific size error");
  string concurrent=Path.Combine(root,"concurrent.json"),padding=new string('x',1500000);LauncherNativeState.Write(concurrent,new Record{revision=0,padding=padding,footer=0});int reads=0,readErrors=0,writes=0;Exception writeFailure=null;string firstReadError=null;
  using(var start=new ManualResetEvent(false))using(var stop=new ManualResetEvent(false)){
   var workers=new List<Thread>();for(int n=0;n<2;n++){var worker=new Thread(()=>{start.WaitOne();while(!stop.WaitOne(0)){try{var value=LauncherNativeState.Read<Record>(concurrent,()=>null);if(value==null||value.revision!=value.footer||value.padding!=padding)throw new Exception("A reader observed a torn record: "+(value==null?"DEFAULT_NULL":"revision="+value.revision+", footer="+value.footer+", length="+(value.padding==null?-1:value.padding.Length)));Interlocked.Increment(ref reads);}catch(Exception ex){Interlocked.Increment(ref readErrors);Interlocked.CompareExchange(ref firstReadError,ex.ToString(),null);}}});worker.IsBackground=true;workers.Add(worker);worker.Start();}
   start.Set();try{for(int i=1;i<=80;i++){LauncherNativeState.Write(concurrent,new Record{revision=i,padding=padding,footer=i});writes++;}}catch(Exception ex){writeFailure=ex;}finally{stop.Set();foreach(var worker in workers)worker.Join();}
  }
  evidence["concurrency"]=new{writes=writes,reads=reads,readErrors=readErrors,writeError=writeFailure==null?null:writeFailure.ToString(),firstReadError=firstReadError};
  Check(writes==80,"All 80 atomic writes must succeed alongside actual production readers: "+(writeFailure==null?"":writeFailure.ToString()));Check(reads>0,"Concurrent readers must perform actual reads");Check(readErrors==0,"Every read must return a complete record: "+firstReadError);Check(LauncherNativeState.Read<Record>(concurrent,()=>null).revision==80,"Final record is the final committed revision");Check(Directory.GetFiles(root,"*.new-*").Length==0,"No replacement temporary files remain");
  evidence["passed"]=true;return 0;
 }catch(Exception ex){evidence["passed"]=false;evidence["error"]=ex.ToString();return 1;}finally{evidence["assertions"]=assertions;string json=new JavaScriptSerializer{MaxJsonLength=2097152}.Serialize(evidence);File.WriteAllText(Path.Combine(root,"results.json"),json,new UTF8Encoding(false));Console.WriteLine(json);}}
}}
