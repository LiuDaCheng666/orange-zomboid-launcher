using System;
using System.IO;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
namespace PZ3D {
 sealed class GameVersionInfo {
  public string value="",state="unknown",source="version.txt",warning="";
 }
 static class LauncherStatusMetadata {
  // Display metadata comes only from the selected installation's bounded root file.
  // The JAR-based compatibility probe has a separate purpose and is never a fallback here.
  public static GameVersionInfo GameVersion(string gameRoot){
   try{
    string file=Path.Combine(Path.GetFullPath(gameRoot),"version.txt");
    for(string current=file;current!=null;current=Path.GetDirectoryName(current)){
     try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return Unknown("游戏版本路径包含重解析点，未读取版本。");}
     catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
    }
    using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
     long length=input.Length;if(length==0||length>4096)return Unknown("游戏根目录 version.txt 为空或超过 4096 字节，版本未知。");
     var bytes=new byte[(int)length];int count=0;while(count<bytes.Length){int read=input.Read(bytes,count,bytes.Length-count);if(read==0)break;count+=read;}
     if(count!=bytes.Length||input.Length!=length)return Unknown("读取期间游戏版本文件发生变化，请刷新后重试。");
     string text=new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF');int lineEnd=text.IndexOfAny(new[]{'\r','\n'});string line=lineEnd<0?text:text.Substring(0,lineEnd);
     var match=Regex.Match(line,@"\A(?<version>[0-9]{1,5}(?:\.[0-9]{1,5}){1,3})(?:[ \t]+[0-9a-fA-F]+)?[ \t]*\z");
     if(!match.Success)return Unknown("游戏根目录 version.txt 首行版本格式无法识别。");
     return new GameVersionInfo{value=match.Groups["version"].Value,state="known"};
    }
   }catch(FileNotFoundException){return Unknown("尚未找到游戏根目录 version.txt，版本未知。");}
   catch(DirectoryNotFoundException){return Unknown("尚未找到游戏根目录 version.txt，版本未知。");}
   catch(IOException){return Unknown("无法读取游戏根目录 version.txt，版本未知。");}
   catch(UnauthorizedAccessException){return Unknown("无权读取游戏根目录 version.txt，版本未知。");}
   catch(SecurityException){return Unknown("无权读取游戏根目录 version.txt，版本未知。");}
   catch(ArgumentException){return Unknown("游戏版本文件路径或文本格式无效，版本未知。");}
   catch(NotSupportedException){return Unknown("游戏版本文件路径不受支持，版本未知。");}
  }
  static GameVersionInfo Unknown(string warning){return new GameVersionInfo{warning=warning};}
 }
}
