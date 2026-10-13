using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class WorkshopWorker {
  public static bool AllowItem(ulong id){return id==3810637496UL||id==ShootClient.Item||InstallWorkflow.Requested(true).Contains(id)||ViewpointAddons.All.Any(a=>a.Item==id);}
  internal static string SafeMessage(string message){string safe=LauncherJournal.Sanitize(Regex.Replace(message??"",@"\b\d{17}\b","[Steam ID 已隐藏]"));return safe.Substring(0,Math.Min(2000,safe.Length));}
  public static int Run(string game,string itemText,bool force){var json=new JavaScriptSerializer();try{var ids=itemText.Split(',').Select(ulong.Parse).Distinct().ToArray();if(ids.Length==0||ids.Any(id=>!AllowItem(id)))throw new Exception("下载请求含未知条目。");var paths=SteamWorkshop.Fetch(game,ids,(v,t)=>{Console.WriteLine(json.Serialize(new{kind="progress",value=v,text=SafeMessage(t)}));Console.Out.Flush();},CancellationToken.None,420,false,force);Console.WriteLine(json.Serialize(new{kind="paths",paths=paths.ToDictionary(p=>p.Key.ToString(),p=>p.Value)}));Console.Out.Flush();return 0;}catch(Exception e){Console.WriteLine(json.Serialize(new{kind="error",message=SafeMessage(e.Message)}));Console.Out.Flush();return 1;}}
  public static Dictionary<ulong,string> Fetch(string game,ulong[] ids,Action<int,string> progress,CancellationToken cancel,bool force){return Transport(game,ids,progress,cancel,force,Process.GetCurrentProcess().MainModule.FileName);}
  internal static Dictionary<ulong,string> Transport(string game,ulong[] ids,Action<int,string> progress,CancellationToken cancel,bool force,string executable){cancel.ThrowIfCancellationRequested();var json=new JavaScriptSerializer{MaxJsonLength=65536};var info=new ProcessStartInfo(executable,"--workshop-worker \""+game.TrimEnd('\\')+"\" "+string.Join(",",ids)+ (force?" --force":"")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   using(var process=new Process{StartInfo=info}){Dictionary<ulong,string> paths=null;string failure=null,protocolError=null;var sync=new object();process.OutputDataReceived+=(s,e)=>{if(e.Data==null||!e.Data.StartsWith("{")||cancel.IsCancellationRequested)return;try{if(e.Data.Length>65536)throw new Exception();var item=json.Deserialize<Dictionary<string,object>>(e.Data);string kind=Convert.ToString(item["kind"]);if(kind=="progress"){int value=Convert.ToInt32(item["value"]);if(value<-1||value>100)throw new Exception();if(progress!=null)progress(value,SafeMessage(Convert.ToString(item["text"])));}else if(kind=="paths"){var raw=(Dictionary<string,object>)item["paths"];var parsed=raw.ToDictionary(p=>ulong.Parse(p.Key),p=>Convert.ToString(p.Value));if(parsed.Count!=ids.Length||parsed.Any(p=>!ids.Contains(p.Key)||string.IsNullOrWhiteSpace(p.Value)))throw new Exception();lock(sync)paths=parsed;}else if(kind=="error"){lock(sync)if(failure==null)failure=SafeMessage(Convert.ToString(item["message"]));}else throw new Exception();}catch{lock(sync)protocolError="工坊下载子进程协议响应无效。";}};
    // Drain native output without retaining account identifiers or credentials.
    process.ErrorDataReceived+=(s,e)=>{};
    process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();DateTime deadline=DateTime.UtcNow.AddSeconds(ids.Length*480+30);try{while(!process.WaitForExit(100)){cancel.ThrowIfCancellationRequested();if(DateTime.UtcNow>deadline)throw new Exception("工坊下载子进程超时。");}process.WaitForExit();}catch{if(!process.HasExited){process.Kill();process.WaitForExit(5000);}throw;}
    cancel.ThrowIfCancellationRequested();if(process.ExitCode!=0||failure!=null||protocolError!=null||paths==null||ids.Any(id=>!paths.ContainsKey(id)))throw new Exception((!string.IsNullOrWhiteSpace(failure)?failure:protocolError??"原生工坊下载阶段失败：未收到完整下载结果。")+"\r\n下载子进程已退出，释放 Steam 游戏身份。");return paths;
   }
  }
 }
}
