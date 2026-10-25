using System;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
namespace PZ3D {
 static class CoreComponents {
  public const ulong Buddy=3619862853,Viewpoint=3809306528;
  public static bool BuddyDownloaded(Engine e){return File.Exists(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"))&&File.Exists(Path.Combine(e.BuddyLib,"zbNative.dll"));}
  public static bool FrameworkReady(Engine e){try{return e.FrameworkConfigured&&File.Exists(Path.Combine(e.Game,"ZombieBuddy.jar"))&&File.Exists(Path.Combine(e.Game,"zbNative.dll"))&&Regex.IsMatch(File.ReadAllText(e.Mods),@"(?m)^\s*mod\s*=\s*\\?ZombieBuddy\s*,");}catch{return false;}}
  public static bool ViewpointConfigured(Engine e){return FrameworkReady(e)&&File.Exists(e.Mods)&&Regex.IsMatch(File.ReadAllText(e.Mods),@"(?m)^\s*mod\s*=\s*\\?Viewpoint\s*,");}
  public static string Version(Engine e){
   try{
    string jar=Path.Combine(e.Game,"ZombieBuddy.jar");if(!File.Exists(jar))jar=Path.Combine(e.BuddyLib,"ZombieBuddy.jar");
    using(var zip=ZipFile.OpenRead(jar)){
     var entry=zip.GetEntry("META-INF/MANIFEST.MF");if(entry==null)return "未读取到版本";
     using(var reader=new StreamReader(entry.Open())){
      var line=new System.Text.StringBuilder();
      // Signed entry sections can exceed 1 MB. Only the bounded main header matters.
      for(int read=0;read<65536;read++){
       int c=reader.Read();if(c<0)break;
       if(c=='\n'){
        string value=line.ToString().TrimEnd('\r');if(value.Length==0)break;
        if(value.StartsWith("Implementation-Version: ",StringComparison.OrdinalIgnoreCase))return value.Substring(24).Trim();
        line.Clear();
       }else line.Append((char)c);
      }
     }
    }
   }catch{}
   return "未读取到版本";
  }
 }
}
