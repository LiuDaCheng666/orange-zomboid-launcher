using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
namespace PZ3D {
 sealed class HardwareInfo {
  public string gpu="显卡信息未检测到",driver="",warning="无法可靠读取专用显存容量。";
  public double? vramGiB;
  public int? totalMemoryGiB;
 }
 static class HardwareProbe {
  static readonly Lazy<HardwareInfo> cache=new Lazy<HardwareInfo>(Read,LazyThreadSafetyMode.ExecutionAndPublication);
  public static HardwareInfo Cached(){return cache.Value;}
  public static HardwareInfo Unknown(){return new HardwareInfo();}
  static string[] Csv(string line){var result=new List<string>();var value=new StringBuilder();bool quoted=false;for(int i=0;i<line.Length;i++){char c=line[i];if(c=='"'){if(quoted&&i+1<line.Length&&line[i+1]=='"'){value.Append('"');i++;}else quoted=!quoted;}else if(c==','&&!quoted){result.Add(value.ToString().Trim());value.Clear();}else value.Append(c);}if(quoted)return new string[0];result.Add(value.ToString().Trim());return result.ToArray();}
  public static HardwareInfo ParseNvidia(string output){foreach(string line in (output??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var fields=Csv(line);double mib;if(fields.Length!=3||fields[0].Length==0||fields[2].Length==0||!double.TryParse(fields[1],NumberStyles.Float,CultureInfo.InvariantCulture,out mib)||double.IsNaN(mib)||double.IsInfinity(mib)||mib<=0)continue;return new HardwareInfo{gpu=fields[0],driver=fields[2],vramGiB=mib/1024d,warning=""};}return Unknown();}
  static string SmiPath(){foreach(string path in new[]{Path.Combine(Environment.SystemDirectory,"nvidia-smi.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"NVIDIA Corporation","NVSMI","nvidia-smi.exe")})if(File.Exists(path))return path;return null;}
  static string Nvidia(string path){
   const int timeout=2000;var text=new StringBuilder();var sync=new object();using(var outputDone=new ManualResetEvent(false))using(var errorDone=new ManualResetEvent(false))using(var process=new Process{StartInfo=new ProcessStartInfo(path,"--query-gpu=name,memory.total,driver_version --format=csv,noheader,nounits"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}}){
    process.OutputDataReceived+=(s,e)=>{if(e.Data==null){try{outputDone.Set();}catch(ObjectDisposedException){}return;}lock(sync){if(text.Length<16384)text.AppendLine(e.Data.Substring(0,Math.Min(e.Data.Length,16384-text.Length)));}};
    process.ErrorDataReceived+=(s,e)=>{if(e.Data==null)try{errorDone.Set();}catch(ObjectDisposedException){}};
    var watch=Stopwatch.StartNew();process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();int remaining=Math.Max(0,timeout-(int)watch.ElapsedMilliseconds);
    if(!process.WaitForExit(remaining)){// This process object owns only the fixed read-only helper we just started.
     try{if(!process.HasExited)process.Kill();}catch{}throw new Exception("NVIDIA 显卡查询超过 2 秒，保留显存未知状态。");}
    outputDone.WaitOne(Math.Max(0,timeout-(int)watch.ElapsedMilliseconds));errorDone.WaitOne(Math.Max(0,timeout-(int)watch.ElapsedMilliseconds));if(process.ExitCode!=0)throw new Exception("NVIDIA 显卡查询未返回有效结果。");lock(sync)return text.ToString();
   }
  }
  static HardwareInfo Read(){HardwareInfo result=Unknown();string failure="";try{string smi=SmiPath();if(smi!=null)result=ParseNvidia(Nvidia(smi));}catch(Exception ex){failure=ex.Message;}
   if(!result.vramGiB.HasValue){try{var options=new EnumerationOptions{ReturnImmediately=true,Timeout=TimeSpan.FromSeconds(2)};using(var query=new ManagementObjectSearcher(new ManagementScope(@"root\CIMV2"),new ObjectQuery("SELECT Name, DriverVersion FROM Win32_VideoController"),options))using(var devices=query.Get()){var names=new List<string>();string driver="";foreach(ManagementObject device in devices){using(device){string name=Convert.ToString(device["Name"]);if(name.Length==0)continue;names.Add(name);if(driver.Length==0)driver=Convert.ToString(device["DriverVersion"]);}}if(names.Count>0){result.gpu=string.Join(" / ",names);result.driver=driver;}}}catch{result.gpu="显卡信息暂不可用";}
    result.warning=(failure.Length>0?failure+" ":"")+"专用显存未知：WMI 的 32 位 AdapterRAM 无法可靠区分大容量显卡，不使用其截断值。";
   }
   try{result.totalMemoryGiB=LauncherMaintenance.MemoryInfo().TotalGiB;}catch{result.warning+=(result.warning.Length>0?" ":"")+"物理内存读取不可用。";}return result;
  }
 }
}
