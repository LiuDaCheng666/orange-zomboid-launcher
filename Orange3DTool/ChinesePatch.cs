using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace PZ3D {
 sealed class ChinesePatch {
  public const ulong Item=3810637496;
  public const string Id="LaoYuTranslationJava";
  public string LastBackup{get;private set;}
  public static bool Enabled(Engine e){return File.Exists(e.Mods)&&Regex.IsMatch(File.ReadAllText(e.Mods),@"(?m)^\s*mod\s*=\s*LaoYuTranslationJava\s*,");}
  public static void Validate(string source){string folder=Path.Combine(source,"mods",Id),common=Path.Combine(folder,"common","mod.info"),version=Path.Combine(folder,"42","mod.info"),jar=Path.Combine(folder,"42","media","java","client","LaoYuTranslationJava.jar");if(!File.Exists(common)||!File.Exists(jar)||new FileInfo(jar).Length==0)throw new Exception("游戏内汉化文件不完整，请检查更新后重试。");string info=File.ReadAllText(common)+(File.Exists(version)?"\n"+File.ReadAllText(version):"");if(!Regex.IsMatch(info,@"(?m)^id\s*=\s*LaoYuTranslationJava\s*$")||!Regex.IsMatch(info,@"(?m)^javaPkgName\s*=\s*laoyu\.javacn\s*$"))throw new Exception("汉化补丁结构已变化，未启用，请更新工具适配。");}
  public string Enable(Engine e,string source){e.Guard();if(!ShootClient.BaseReady(e))throw new Exception("游戏内汉化需要基础 3D 和 ZombieBuddy；请一同勾选安装。");Validate(source);return Change(e,true);}
  public string Disable(Engine e){e.Guard();return Change(e,false);}
  string Change(Engine e,bool on){if(!File.Exists(e.Mods)){if(!on)return "游戏内汉化未启用。";throw new Exception("默认模组配置不存在。");}byte[] before=File.ReadAllBytes(e.Mods);string text=Encoding.UTF8.GetString(before).TrimStart('\uFEFF');var block=Regex.Match(text,@"\bmods\s*\{(?<body>[^}]*)\}");if(!block.Success)throw new Exception("模组列表格式无法识别，未修改汉化配置。");string body=Regex.Replace(block.Groups["body"].Value,@"(?m)^[ \t]*mod\s*=\s*LaoYuTranslationJava\s*,[^\r\n]*(?:\r?\n|$)","");if(on)body=body.TrimEnd()+"\r\n    mod = LaoYuTranslationJava,\r\n";string updated=text.Substring(0,block.Groups["body"].Index)+body+text.Substring(block.Groups["body"].Index+block.Groups["body"].Length);if(updated==text)return on?"游戏内汉化已配置；首次需游戏语言简体中文并批准 Java。":"游戏内汉化已卸载启用配置。";
   string backup=Path.Combine(e.Data,"chinese-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);LastBackup=backup;File.WriteAllBytes(Path.Combine(backup,"default.txt"),before);File.WriteAllText(Path.Combine(backup,"人工恢复汉化.txt"),"退出游戏后，仅移除 "+e.Mods+" 中的 mod = LaoYuTranslationJava, 可关闭汉化。\r\n本补丁没有覆盖 Viewpoint JAR、游戏语言或个人设置。工坊文件保留。\r\n完整 default.txt 备份恢复前，请先备份当前文件，避免覆盖之后的其他模组调整。",new UTF8Encoding(true));string temp=e.Mods+".orange-cn-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,updated,new UTF8Encoding(false));if(!File.ReadAllBytes(e.Mods).SequenceEqual(before))throw new Exception("配置被外部修改，拒绝覆盖。");e.Guard();File.Replace(temp,e.Mods,null);}finally{if(File.Exists(temp))File.Delete(temp);}return (on?"游戏内汉化已配置：老余汉化JAVA版。首次选择简体中文、批准 laoyu.javacn 并重启；进服效果待验证。":"游戏内汉化已卸载启用配置，保留工坊文件和个人设置。")+"\r\n修改："+e.Mods+"\r\n备份："+backup;
  }
 }
}
