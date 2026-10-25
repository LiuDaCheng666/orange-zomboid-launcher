using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace PZ3D {
 static class GameDiscovery {
  internal sealed class WorkshopSourceSet {internal readonly List<string> Roots=new List<string>();internal readonly List<string> Inputs=new List<string>();}
  internal static WorkshopSourceSet WorkshopSources(Engine e,List<string> warnings){
   var result=new WorkshopSourceSet();result.Roots.Add(e.Workshop);
   var steamRoots=new List<string>();if(!e.TestMode)steamRoots.AddRange(SteamRoots());
   string common=Path.GetDirectoryName(e.Game),apps=common==null?null:Path.GetDirectoryName(common);
   if(common!=null&&apps!=null&&Path.GetFileName(common).Equals("common",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(apps).Equals("steamapps",StringComparison.OrdinalIgnoreCase))steamRoots.Add(Path.GetDirectoryName(apps));
   foreach(string steam in steamRoots.Distinct(StringComparer.OrdinalIgnoreCase)){
    result.Roots.Add(Path.Combine(steam,"steamapps/workshop/content/108600"));
    string libraries=Path.Combine(steam,"steamapps/libraryfolders.vdf");result.Inputs.Add(libraries);
    try{LauncherMaintenance.NoReparse(libraries);if(File.Exists(libraries))foreach(Match m in Regex.Matches(File.ReadAllText(libraries),"\"path\"\\s*\"([^\"]+)\""))result.Roots.Add(Path.Combine(m.Groups[1].Value.Replace("\\\\","\\"),"steamapps/workshop/content/108600"));}catch(Exception ex){warnings.Add(ex.Message);}
   }
   return result;
  }
  public static bool IsGameFolder(string path){try{return !string.IsNullOrWhiteSpace(path)&&File.Exists(Path.Combine(path,"ProjectZomboid64.json"))&&File.Exists(Path.Combine(path,"ProjectZomboid64.exe"));}catch{return false;}}
  public static string[] SteamRoots(){var roots=new List<string>();foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string key in new[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"})try{using(var k=hive.OpenSubKey(key)){if(k!=null)foreach(string name in new[]{"SteamPath","InstallPath"}){string value=k.GetValue(name) as string;if(!string.IsNullOrWhiteSpace(value))roots.Add(value);}}}catch{}return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  // Discovery is deliberately finite: no descendant enumeration, process probing or SDK calls.
  // The elapsed budget is checked between local operations; it cannot interrupt an OS file read.
  sealed class Resolver {
   const int MaxRoots=128;
   readonly System.Diagnostics.Stopwatch elapsed=System.Diagnostics.Stopwatch.StartNew();
   readonly List<string> roots=new List<string>();
   readonly HashSet<string> seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   bool Available{get{return elapsed.ElapsedMilliseconds<2000;}}
   static string Canonical(string path){
    if(string.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith(@"\\",StringComparison.Ordinal)||path.Length<3||path[1]!=':'||(path[2]!='\\'&&path[2]!='/'))return "";
    string full=Path.GetFullPath(path);return full.Length>3?full.TrimEnd('\\','/'):full;
   }
   string SafePath(string path){if(!Available)return "";try{string full=Canonical(path);if(full.Length==0)return "";LauncherMaintenance.NoReparse(full);return Available?full:"";}catch{return "";}}
   internal string Game(string path){string full=SafePath(path);if(full.Length==0)return "";foreach(string marker in new[]{"ProjectZomboid64.exe","ProjectZomboid64.json"}){if(!Available)return "";try{string file=Path.Combine(full,marker);LauncherMaintenance.NoReparse(file);if(!File.Exists(file))return "";}catch{return "";}}return Available?full:"";}
   internal string Read(string path,int maximum){if(!Available)return "";try{path=SafePath(path);if(path.Length==0)return "";using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(stream.Length>maximum)return "";var bytes=new byte[maximum+1];int length=0,read;while(Available&&(read=stream.Read(bytes,length,bytes.Length-length))>0){length+=read;if(length>maximum)return "";}if(!Available)return "";return new System.Text.UTF8Encoding(false,true).GetString(bytes,0,length).TrimStart('\uFEFF');}}catch{return "";}}
   void Add(string path){if(!Available||roots.Count>=MaxRoots)return;string full=SafePath(path);if(full.Length!=0&&seen.Add(full))roots.Add(full);}
   static MatchCollection Fields(string text,string names){return Regex.Matches(text,"\"(?:"+names+")\"\\s*\"([^\"]+)\"",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));}
   string RootGame(string root){
    if(!Available)return "";string apps=Path.Combine(root,"steamapps"),common=Path.Combine(apps,"common");
    try{string text=Read(Path.Combine(apps,"appmanifest_108600.acf"),262144);var fields=Fields(text,"installdir");if(fields.Count==1){string folder=fields[0].Groups[1].Value;
      if(folder!="."&&folder!=".."&&!folder.EndsWith(".",StringComparison.Ordinal)&&!folder.EndsWith(" ",StringComparison.Ordinal)&&folder.IndexOfAny(Path.GetInvalidFileNameChars())<0&&folder.IndexOfAny(new[]{'\\','/'})<0&&!Path.IsPathRooted(folder)){
       string candidate=Path.GetFullPath(Path.Combine(common,folder));if(string.Equals(Path.GetDirectoryName(candidate),Path.GetFullPath(common),StringComparison.OrdinalIgnoreCase)){string game=Game(candidate);if(game.Length!=0)return game;}
      }
     }}catch{}
    return Game(Path.Combine(common,"ProjectZomboid"));
   }
   void AddRoots(IEnumerable<string> supplied){if(supplied!=null)foreach(string root in supplied){if(!Available||roots.Count>=MaxRoots)break;Add(root);}}
   void Expand(string[] supplied){foreach(string root in supplied){if(!Available)break;try{foreach(Match match in Fields(Read(Path.Combine(root,"steamapps","libraryfolders.vdf"),1048576),"path|[0-9]+")){if(!Available||roots.Count>=MaxRoots)break;Add(match.Groups[1].Value.Replace("\\\\","\\"));}}catch{}}}
   internal string Find(IEnumerable<string> supplied,IEnumerable<string> fallback=null){
    AddRoots(supplied);Expand(roots.ToArray());
    // Registered libraries, including their manifest installs, precede conventional fallbacks.
    int firstFallback=roots.Count;AddRoots(fallback);Expand(roots.Skip(firstFallback).ToArray());
    foreach(string root in roots){if(!Available)break;string game=RootGame(root);if(game.Length!=0)return game;}return "";
   }
   internal string Selection(string selected){
    string full=SafePath(selected);if(full.Length==0)return "";
    if(Path.GetFileName(full).Equals("ProjectZomboid64.exe",StringComparison.OrdinalIgnoreCase))return Game(Path.GetDirectoryName(full));
    string direct=Game(full);if(direct.Length!=0)return direct;
    if(Path.GetFileName(full).Equals("common",StringComparison.OrdinalIgnoreCase)){string apps=Path.GetDirectoryName(full);if(apps!=null&&Path.GetFileName(apps).Equals("steamapps",StringComparison.OrdinalIgnoreCase))full=Path.GetDirectoryName(apps);}
    else if(Path.GetFileName(full).Equals("steamapps",StringComparison.OrdinalIgnoreCase))full=Path.GetDirectoryName(full);
    return Find(new[]{full});
   }
   internal string Initial(string preference,IEnumerable<string> registered,IEnumerable<string> conventional){string saved=Game(Read(preference,4096).Trim());return saved.Length!=0?saved:Find(registered,conventional);}
  }
  static IEnumerable<string> ConventionalRoots(){
   foreach(var folder in new[]{Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.ProgramFiles}){string root=Environment.GetFolderPath(folder);if(!string.IsNullOrWhiteSpace(root))yield return Path.Combine(root,"Steam");}
   DriveInfo[] drives;try{drives=DriveInfo.GetDrives();}catch{yield break;}
   foreach(var drive in drives){bool local=false;try{local=drive.DriveType==DriveType.Fixed&&drive.IsReady;}catch{}if(local){yield return Path.Combine(drive.RootDirectory.FullName,"Steam");yield return Path.Combine(drive.RootDirectory.FullName,"SteamLibrary");}}
  }
  public static string Find(string[] steamRoots){return new Resolver().Find(steamRoots);}
  static IEnumerable<string> RegisteredRoots(){foreach(string root in SteamRoots())yield return root;}
  public static string Detect(){return new Resolver().Find(RegisteredRoots(),ConventionalRoots());}
  public static string ResolveSelection(string selected){return new Resolver().Selection(selected);}
  public static string InitialGame(string preferencePath){return new Resolver().Initial(preferencePath,RegisteredRoots(),ConventionalRoots());}
  internal static string InitialGame(string preferencePath,string[] registeredRoots,string[] conventionalRoots){return new Resolver().Initial(preferencePath,registeredRoots,conventionalRoots);}
 }
}
