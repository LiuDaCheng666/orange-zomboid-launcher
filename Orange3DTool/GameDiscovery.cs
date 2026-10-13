using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace PZ3D {
 static class GameDiscovery {
  public static bool IsGameFolder(string path){try{return !string.IsNullOrWhiteSpace(path)&&File.Exists(Path.Combine(path,"ProjectZomboid64.json"))&&File.Exists(Path.Combine(path,"ProjectZomboid64.exe"));}catch{return false;}}
  public static string[] SteamRoots(){var roots=new List<string>();foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string key in new[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"})try{using(var k=hive.OpenSubKey(key)){if(k!=null)foreach(string name in new[]{"SteamPath","InstallPath"}){string value=k.GetValue(name) as string;if(!string.IsNullOrWhiteSpace(value))roots.Add(value);}}}catch{}return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  public static string Find(string[] steamRoots){var roots=new List<string>(steamRoots??new string[0]);foreach(string steam in roots.ToArray())try{string file=Path.Combine(steam,"steamapps","libraryfolders.vdf");if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"(?:path|[0-9]+)\"\\s*\"([^\"]+)\"")){string value=m.Groups[1].Value.Replace("\\\\","\\");if(Path.IsPathRooted(value))roots.Add(value);}}catch{}
   foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))try{string apps=Path.Combine(root,"steamapps"),manifest=Path.Combine(apps,"appmanifest_108600.acf");if(File.Exists(manifest)){var match=Regex.Match(File.ReadAllText(manifest),"\"installdir\"\\s*\"([^\"]+)\"");if(match.Success){string folder=match.Groups[1].Value.Replace("\\\\","\\");if(!Path.IsPathRooted(folder)&&folder.IndexOfAny(new[]{'\\','/'})<0){string game=Path.Combine(apps,"common",folder);if(IsGameFolder(game))return Path.GetFullPath(game);}}}string fallback=Path.Combine(apps,"common","ProjectZomboid");if(IsGameFolder(fallback))return Path.GetFullPath(fallback);}catch{}return "";
  }
 }
}
