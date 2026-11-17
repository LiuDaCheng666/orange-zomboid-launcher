using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace PZ3D {
 static class GameGraphicsPreference {
  const string KeyPath=@"Software\Microsoft\DirectX\UserGpuPreferences";

  static bool IsGpuEntry(string entry){
   int equals=entry.IndexOf('=');
   return equals>=0&&entry.Substring(0,equals).Trim().Equals("GpuPreference",StringComparison.OrdinalIgnoreCase);
  }

  static string HighPerformanceValue(string current){
   var entries=current.Split(';').Where(entry=>!string.IsNullOrWhiteSpace(entry)).ToArray();
   var gpu=entries.Where(IsGpuEntry).ToArray();
   if(gpu.Length==1&&gpu[0].Substring(gpu[0].IndexOf('=')+1).Trim()=="2")return current;
   return string.Join(";",entries.Where(entry=>!IsGpuEntry(entry)).Concat(new[]{"GpuPreference=2"}))+";";
  }

  public static bool PreferHighPerformance(string executable){
   try{
    using(var key=Registry.CurrentUser.CreateSubKey(KeyPath))return PreferHighPerformance(executable,key);
   }catch(Exception error){
    LauncherDiagnostics.Write("无法设置游戏的高性能显卡偏好，继续按 Windows 当前设置启动",error);
    return false;
   }
  }

  // Windows reads this preference for the process being started, not its parent
  // launcher. Keep other graphics options in the same REG_SZ value unchanged.
  public static bool PreferHighPerformance(string executable,RegistryKey key){
   try{
    string path=Path.GetFullPath(executable),current=key.GetValue(path,"") as string??"";
    string preferred=HighPerformanceValue(current);
    if(current!=preferred)key.SetValue(path,preferred,RegistryValueKind.String);
    return true;
   }catch(Exception error){
    LauncherDiagnostics.Write("无法设置游戏的高性能显卡偏好，继续按 Windows 当前设置启动",error);
    return false;
   }
  }

  public static void PrepareSteamGame(string game){
   foreach(string executable in new[]{Path.Combine(game,"ProjectZomboid64.exe"),Path.Combine(game,"jre64","bin","java.exe")})
    if(File.Exists(executable))PreferHighPerformance(executable);
  }
 }
}
