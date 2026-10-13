using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
namespace PZ3D {
 // Capability check only. A client directory is not evidence of server-list-independent loading.
 static class LocalAddon {
  public static string ClientOnlyReason(string infoPath,string jarPath,string gameVersion,string buddyVersion){
   if(!File.Exists(infoPath)||!File.Exists(jarPath))return "文件未下载，无法确认纯客户端支持。";
   var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(string line in File.ReadAllLines(infoPath)){int split=line.IndexOf('=');if(split>0)values[line.Substring(0,split).Trim()]=line.Substring(split+1).Trim();}
   string path;if(!values.TryGetValue("javaJarFile",out path)||!path.Replace('\\','/').StartsWith("media/java/client/",StringComparison.Ordinal))return "不是声明的客户端 Java 扩展。";
   if(!Numeric(gameVersion)||!Numeric(buddyVersion))return "无法确认游戏或 Buddy 版本，未开放安装。";
   string min,max;if(values.TryGetValue("versionMin",out min)&&Compare(gameVersion,min,false)<0)return "游戏版本低于作者最低要求："+min;
   if(values.TryGetValue("versionMax",out max)&&Compare(gameVersion,max,true)>0)return "游戏版本超过作者支持范围："+max;
   if(values.TryGetValue("ZBVersionMin",out min)&&Compare(buddyVersion,min,false)<0)return "Buddy 版本低于作者最低要求："+min;
   if(values.TryGetValue("ZBVersionMax",out max)&&Compare(buddyVersion,max,false)>0)return "Buddy 版本超过作者支持范围："+max;
   try{using(var archive=ZipFile.OpenRead(jarPath)){var entry=archive.GetEntry("META-INF/MANIFEST.MF");if(entry!=null)using(var reader=new StreamReader(entry.Open())){string manifest=reader.ReadToEnd();if(Regex.IsMatch(manifest,@"(?im)^ZB-Preload:\s*true\s*$")){string declared;return values.TryGetValue("javaPreload",out declared)&&declared.Equals("true",StringComparison.OrdinalIgnoreCase)?"":"作者 mod.info 未声明 javaPreload，不能配置保留加载。";}}}}catch{return "JAR 文件损坏，无法确认本地预加载。";}
   return "作者当前 JAR 未声明 ZB-Preload；不能确认服务器不安装时保留加载。本工具不改写作者 JAR。";
  }
  static bool Numeric(string value){return value!=null&&Regex.IsMatch(value,@"^\d+(?:\.\d+)*$");}
  static int Compare(string actual,string bound,bool prefix){if(!Numeric(bound))throw new Exception("作者版本条件无法识别，未开放安装。");var a=actual.Split('.').Select(int.Parse).ToArray();var b=bound.Split('.').Select(int.Parse).ToArray();int length=prefix?b.Length:Math.Max(a.Length,b.Length);for(int i=0;i<length;i++){int x=i<a.Length?a[i]:0,y=i<b.Length?b[i]:0;if(x!=y)return x.CompareTo(y);}return 0;}
 }
}
