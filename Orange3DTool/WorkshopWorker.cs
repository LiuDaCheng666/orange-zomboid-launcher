using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class WorkshopWorker {
  public static bool AllowItem(ulong id){return id==3810637496UL||id==ShootClient.Item||InstallWorkflow.Requested(true).Contains(id)||ViewpointAddons.All.Any(a=>a.Item==id);}
  public static int Run(string game,string itemText,bool force){var json=new JavaScriptSerializer();try{var ids=itemText.Split(',').Select(ulong.Parse).Distinct().ToArray();if(ids.Length==0||ids.Any(id=>!AllowItem(id)))throw new Exception("下载请求含未知条目。");var paths=SteamWorkshop.Fetch(game,ids,(v,t)=>{Console.WriteLine(json.Serialize(new{kind="progress",value=v,text=t}));Console.Out.Flush();},CancellationToken.None,420,false,force);Console.WriteLine(json.Serialize(new{kind="paths",paths=paths.ToDictionary(p=>p.Key.ToString(),p=>p.Value)}));Console.Out.Flush();return 0;}catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
  public static Dictionary<ulong,string> Fetch(string game,ulong[] ids,Action<int,string> progress,CancellationToken cancel,bool force){var json=new JavaScriptSerializer();var info=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--workshop-worker \""+game.TrimEnd('\\')+"\" "+string.Join(",",ids)+ (force?" --force":"")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   using(var process=new Process{StartInfo=info}){Dictionary<ulong,string> paths=null;var errors=new StringBuilder();string protocolError=null;var sync=new object();process.OutputDataReceived+=(s,e)=>{if(e.Data==null||!e.Data.StartsWith("{"))return;try{var item=json.Deserialize<Dictionary<string,object>>(e.Data);if(Convert.ToString(item["kind"])=="progress"){if(progress!=null)progress(Convert.ToInt32(item["value"]),Convert.ToString(item["text"]));}else if(Convert.ToString(item["kind"])=="paths"){var raw=(Dictionary<string,object>)item["paths"];lock(sync)paths=raw.ToDictionary(p=>ulong.Parse(p.Key),p=>Convert.ToString(p.Value));}}catch(Exception ex){lock(sync)protocolError=ex.Message;}};process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(sync){if(errors.Length<65536)errors.AppendLine(e.Data);}};
    process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();DateTime deadline=DateTime.UtcNow.AddSeconds(ids.Length*480+30);try{while(!process.WaitForExit(100)){cancel.ThrowIfCancellationRequested();if(DateTime.UtcNow>deadline)throw new Exception("工坊下载子进程超时。");}process.WaitForExit();}catch{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}throw;}
    if(process.ExitCode!=0||protocolError!=null||paths==null||ids.Any(id=>!paths.ContainsKey(id)))throw new Exception("下载子进程已退出，释放 Steam 游戏身份。\r\n"+errors+protocolError);return paths;
   }
  }
 }
}
