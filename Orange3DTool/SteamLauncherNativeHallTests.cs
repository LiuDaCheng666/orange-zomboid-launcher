using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace PZ3D {
 static class SteamLauncherNativeHallTests {
  static void Check(bool pass,string text){if(!pass)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);}
  static Dictionary<string,object> Map(object value){var json=new JavaScriptSerializer();return json.Deserialize<Dictionary<string,object>>(json.Serialize(value));}
  static object Poll(string query,Action pump,Func<int> completion,Func<bool> refreshing,Func<int> count,Func<int,byte[]> details,Func<int[]> responded,Action<object> batch,CancellationToken cancel,int timeout=25000){try{return typeof(SteamLauncherNative).GetMethod("PollHall",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{query,pump,completion,refreshing,count,details,responded,null,cancel,batch,timeout});}catch(TargetInvocationException x){throw x.InnerException;}}
  static byte[] Row(int index,string name="First native",string description="Synthetic PZ"){
   var bytes=new byte[376];Action<int,byte[]> put=(offset,value)=>Buffer.BlockCopy(value,0,bytes,offset,value.Length);put(0,BitConverter.GetBytes((ushort)16261));put(2,BitConverter.GetBytes((ushort)27015));put(4,BitConverter.GetBytes(0xCB007109U+(uint)index));put(8,BitConverter.GetBytes(38));bytes[12]=1;put(78,Encoding.UTF8.GetBytes(description));put(144,BitConverter.GetBytes(108600U));put(148,BitConverter.GetBytes(7));put(152,BitConverter.GetBytes(32));put(172,Encoding.UTF8.GetBytes(name));return bytes;
  }
  static object[] Rows(object data){return ((System.Collections.IEnumerable)Map(data)["servers"]).Cast<object>().ToArray();}
  static bool Refuses(Action call,string fragment){try{call();return false;}catch(Exception x){return x.Message.Contains(fragment);}}
  public static void Run(){
   Check(typeof(SteamLauncherNative).GetMethod("PollHall",BindingFlags.Static|BindingFlags.NonPublic)!=null,"native Hall polling exposes responsive rows during the pump");
   int step=0;bool early=false;int firstSize=0;
   var result=Poll("",()=>step++,()=>step>=3?0:-1,()=>true,()=>1,i=>Row(i),()=>new[]{0},batch=>{early=step==1;firstSize=Rows(batch).Length;},CancellationToken.None);
   Check(early&&firstSize==1&&Rows(result).Length==1,"native first valid server is emitted before query completion");
   step=0;bool respondedBeforeFinal=false;var delayed=Row(0);delayed[12]=0;
   Poll("",()=>step++,()=>step>=3?0:-1,()=>true,()=>1,i=>step>=2?Row(0):delayed,()=>step==2?new[]{0}:new int[0],batch=>respondedBeforeFinal=step==2,CancellationToken.None);
   Check(respondedBeforeFinal,"responded callbacks make a formerly unresponsive server visible on the next pump");
   step=0;var batches=new List<int>();var ids=new List<string>();
   result=Poll("",()=>step++,()=>step>=2?0:-1,()=>true,()=>42,i=>{var bytes=Row(i==40?0:i);if(i==41)Buffer.BlockCopy(BitConverter.GetBytes(480U),0,bytes,144,4);return bytes;},()=>Enumerable.Range(0,42).ToArray(),batch=>{object[] rows=Rows(batch);batches.Add(rows.Length);ids.AddRange(rows.Select(row=>Convert.ToString(Map(row)["id"])));},CancellationToken.None);
   Check(batches[0]==1&&batches.All(size=>size>0&&size<=16)&&ids.Count==40&&ids.Distinct().Count()==40,"native batches stay bounded and deduplicate IDs across repeated pumps");
   Check(Rows(result).Length==41&&Convert.ToString(Map(Rows(result)[0])["id"])=="hall:203.0.113.9:16261","final native scan retains the original server order and result semantics");
   step=0;int beforeCompletion=0,totalEmitted=0;
   Poll("",()=>step++,()=>step>=8?0:-1,()=>true,()=>1000,i=>Row(i),()=>new int[0],batch=>{int size=Rows(batch).Length;totalEmitted+=size;if(step<8)beforeCompletion+=size;},CancellationToken.None);
   Check(totalEmitted==1000&&beforeCompletion==1000,"reaching the stream cap still flushes its last partial batch before completion");
   step=0;int matched=0;
   result=Poll("alpha",()=>step++,()=>0,()=>false,()=>3,i=>Row(i,i==0?"Alpha name":"Other",i==1?"ALPHA description":"Other"),()=>new int[0],batch=>matched+=Rows(batch).Length,CancellationToken.None);
   Check(matched==2&&Rows(result).Length==2,"incremental and final rows apply the same case-insensitive name and description query");
   using(var cancelled=new CancellationTokenSource()){int emitted=0;bool stopped=false;try{Poll("",()=>{},()=>-1,()=>true,()=>30,i=>Row(i),()=>new int[0],batch=>{emitted++;cancelled.Cancel();},cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped&&emitted==1,"native cancellation after the first batch prevents further batches");}
   using(var cancelled=new CancellationTokenSource()){int counts=0;bool stopped=false;try{Poll("",()=>{},()=>0,()=>false,()=>++counts==1?1:0,i=>Row(i),()=>new int[0],batch=>cancelled.Cancel(),cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"native cancellation is honored even when the final server snapshot becomes empty");}
   Check(Refuses(()=>Poll("",()=>{},()=>0,()=>false,()=>30001,i=>Row(i),()=>new int[0],batch=>{},CancellationToken.None),"数量"),"native polling refuses an out-of-range server count");
   result=Poll("",()=>{},()=>-1,()=>true,()=>1,i=>Row(i),()=>new int[0],batch=>{},CancellationToken.None,0);
   Check(Rows(result).Length==1&&Convert.ToString(Map(result)["message"]).Contains("部分有效服务器"),"bounded timeout retains the original partial-result warning");
   Check(Refuses(()=>Poll("",()=>{},()=>-1,()=>true,()=>1,i=>delayed,()=>new int[0],batch=>{},CancellationToken.None,0),"超时"),"timed-out unresponsive rows remain a failure instead of empty success");
  }
 }
}
