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
  public static string Find(string[] steamRoots){var roots=new List<string>(steamRoots??new string[0]);foreach(string steam in roots.ToArray())try{string file=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\"")){string value=m.Groups[1].Value.Replace("\\\\","\\");if(Path.IsPathRooted(value))roots.Add(value);}}catch{}
   foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))try{string apps=Path.Combine(root,"steamapps"),manifest=Path.Combine(apps,"appmanifest_108600.acf");if(File.Exists(manifest)){var match=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s*\"([^\"]+)\"");if(match.Success){string folder=match.Groups[1].Value.Replace("\\\\","\\");if(!Path.IsPathRooted(folder)&&folder.IndexOfAny(new[]{'\\','/'})<0){string game=Path.Combine(apps,"common",folder);if(IsGameFolder(game))return Path.GetFullPath(game);}}}string fallback=Path.Combine(apps,"common","ProjectZomboid");if(IsGameFolder(fallback))return Path.GetFullPath(fallback);}catch{}return "";
  }
 }
}
