using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class DlssFile { public string Relative,Backup,BeforeHash,InstalledHash; }
 sealed class DlssRecord { public string Game,Backup,State,Time; public DlssFile[] Files; }
 sealed class DlssInstaller {
  readonly string game,data,active;readonly bool test;readonly JavaScriptSerializer json=new JavaScriptSerializer();
  public Action<int,string> Progress;
  public string LastBackup {get;private set;}
  public bool Enabled {get{return File.Exists(active);}}
  public DlssInstaller(string game,string data,bool test=false){this.game=Path.GetFullPath(game);this.data=data;this.test=test;active=Path.Combine(data,"dlss-state",Hash(Encoding.UTF8.GetBytes(this.game.ToLowerInvariant())).Substring(0,16)+".json");if(File.Exists(active)){var r=ReadRecord();LastBackup=r.Backup;}}
  void Step(int n,string s){if(Progress!=null)Progress(n,s);}
  void Guard(){if(!File.Exists(Path.Combine(game,"ProjectZomboid64.json")))throw new Exception("请先选择正确的游戏目录。");if(!test&&(Process.GetProcessesByName("ProjectZomboid64").Length>0||Process.GetProcessesByName("ProjectZomboid32").Length>0))throw new Exception("请完全退出游戏后切换 DLSS 5。");SafePath(game,"opengl32.dll");}
  public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
  public static string HashFile(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","");}
  public static string SafePath(string root,string relative){if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Split('/','\\').Any(p=>p==".."||p=="."||p.Length==0))throw new Exception("拒绝不安全文件路径："+relative);string full=Path.GetFullPath(Path.Combine(root,relative));string prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("文件超出目标目录。");for(string dir=Path.GetDirectoryName(full);dir!=null;dir=Path.GetDirectoryName(dir)){if(Directory.Exists(dir)&&(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝通过符号链接或目录联接写入："+dir);if(string.Equals(dir,Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))break;}if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝符号链接文件："+full);return full;}
  static bool Allowed(string rel){return RootNames.Contains(rel,StringComparer.OrdinalIgnoreCase)||(rel.StartsWith("reshade-shaders/",StringComparison.OrdinalIgnoreCase)&&new[]{".fx",".fxh",".png",".dds",".jpg",".jpeg",".txt",".md",".json"}.Contains(Path.GetExtension(rel).ToLowerInvariant()));}
  static readonly string[] RootNames={"opengl32.dll","dlss5-feed.addon64","renodx-dlss5.addon64","nvngx_dlss.dll","nvngx_dlssnr.dll"};
  public static bool IsX64(string p){using(var r=new BinaryReader(File.OpenRead(p))){if(r.BaseStream.Length<64||r.ReadUInt16()!=0x5a4d)return false;r.BaseStream.Position=60;uint off=r.ReadUInt32();if(off+24>r.BaseStream.Length)return false;r.BaseStream.Position=off;return r.ReadUInt32()==0x4550&&r.ReadUInt16()==0x8664;}}
  public static void ValidateCache(string cache){foreach(string rel in RootNames.Concat(new[]{"reshade-shaders/Shaders/DLSS5_Feed.fx","reshade-shaders/Shaders/ReShade.fxh","reshade-shaders/Shaders/ReShadeUI.fxh","reshade-shaders/Shaders/DrawText.fxh","reshade-shaders/Shaders/lumenite_Kernel.fx"})){string p=SafePath(cache,rel);if(!File.Exists(p)||new FileInfo(p).Length==0)throw new Exception("DLSS 组件缺失："+rel+"\r\n请先下载组件或选择完整的本地组件目录。");if(RootNames.Contains(rel)&&!IsX64(p))throw new Exception("组件不是有效的 x64 PE 文件："+rel);}}
  DlssRecord ReadRecord(){var r=json.Deserialize<DlssRecord>(File.ReadAllText(active));if(r==null||!string.Equals(r.Game,game,StringComparison.OrdinalIgnoreCase)||r.Files==null)throw new Exception("DLSS 状态记录无法验证，请用人工恢复说明处理。");foreach(var f in r.Files){if(!Allowed(f.Relative)&&!new[]{"ReShade.ini","ReShadePreset.ini","dlss5-feed.cfg"}.Contains(f.Relative))throw new Exception("状态记录含未知文件。");SafePath(game,f.Relative);}return r;}
  void Save(string path,DlssRecord r){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,json.Serialize(r),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public string Enable(string cache){
   cache=Path.GetFullPath(cache);
   Guard();if(Enabled){ReadRecord();return "DLSS 实验组件已有部署记录；不会重复覆盖。若组件损坏，先关闭再启用。部署不等于神经渲染已生效。";}
   Step(0,"检查完整 x64 OpenGL 实验组件");ValidateCache(cache);
   foreach(string rel in new[]{"opengl32.dll","ReShade.ini","ReShadePreset.ini","dlss5-feed.cfg","dlss5-feed-helper.addon64","nvngx_dlss.dll","nvngx_dlssnr.dll"})if(File.Exists(SafePath(game,rel)))throw new Exception("检测到非本工具管理的渲染文件，请先备份并移走，避免覆盖："+rel);
   if(Directory.EnumerateFiles(game,"*.addon64").Any())throw new Exception("游戏已有 ReShade 附加组件，请先处理既有安装，工具不擅自覆盖。");
   var sources=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(string p in Directory.EnumerateFiles(cache,"*",SearchOption.AllDirectories)){string rel=p.Substring(Path.GetFullPath(cache).TrimEnd('\\').Length+1).Replace('\\','/');if(Allowed(rel)){SafePath(cache,rel);sources.Add(rel,p);}}
   var generated=new Dictionary<string,byte[]>{{"ReShade.ini",Encoding.UTF8.GetBytes("[ADDON]\r\nAddonPath=.\\\r\nDisabledAddons=\r\n[GENERAL]\r\nEffectSearchPaths=.\\reshade-shaders\\Shaders\\**\r\nTextureSearchPaths=.\\reshade-shaders\\Textures\\**\r\nPresetPath=.\\ReShadePreset.ini\r\nPerformanceMode=0\r\n[DEPTH]\r\nDepthCopyBeforeClears=1\r\n")},{"ReShadePreset.ini",Encoding.UTF8.GetBytes("Techniques=Lumenite_Kernel@lumenite_Kernel.fx,DLSS5_Feed@DLSS5_Feed.fx\r\nTechniqueSorting=Lumenite_Kernel@lumenite_Kernel.fx,DLSS5_Feed@DLSS5_Feed.fx\r\n[DLSS5_Feed.fx]\r\nPreprocessorDefinitions=DLSS5_MV_PROVIDER=3\r\nDEBUG_VIEW=0\r\nMV_SCALE=1.000000\r\nMV_SIGN=1.000000,1.000000\r\n")},{"dlss5-feed.cfg",Encoding.UTF8.GetBytes("enabled=1\r\nmode=2\r\n")}};
   string backup=Path.Combine(data,"dlss-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(backup);LastBackup=backup;
   var files=new List<DlssFile>();var originals=new Dictionary<string,byte[]>();int i=0;
   foreach(string rel in sources.Keys.Concat(generated.Keys)){string p=SafePath(game,rel);byte[] old=File.Exists(p)?File.ReadAllBytes(p):null;originals.Add(rel,old);string b=old==null?null:Path.Combine(backup,"originals",(++i)+"-"+Path.GetFileName(rel));if(old!=null){Directory.CreateDirectory(Path.GetDirectoryName(b));File.WriteAllBytes(b,old);}files.Add(new DlssFile{Relative=rel,Backup=b,BeforeHash=old==null?null:Hash(old)});}
   var record=new DlssRecord{Game=game,Backup=backup,State="准备安装",Time=DateTime.UtcNow.ToString("o"),Files=files.ToArray()};Save(Path.Combine(backup,"changes.json"),record);WriteGuide(record);
   try{int done=0;foreach(var f in files){Step(15+70*(done++)/files.Count,"部署 "+f.Relative);string p=SafePath(game,f.Relative);Directory.CreateDirectory(Path.GetDirectoryName(p));if(sources.ContainsKey(f.Relative))File.Copy(sources[f.Relative],p,true);else File.WriteAllBytes(p,generated[f.Relative]);f.InstalledHash=HashFile(p);}record.State="组件已部署，等待游戏内验证";Save(Path.Combine(backup,"changes.json"),record);Save(active,record);Step(100,"部署完成；首次进入游戏需确认深度和神经渲染面板");}
   catch(Exception e){var errors=Restore(originals);record.State=errors.Count==0?"安装失败，已回滚":"安装失败，回滚不完整";Save(Path.Combine(backup,"changes.json"),record);throw new Exception(e.Message+"\r\n"+record.State+"\r\n"+string.Join("\r\n",errors)+"\r\n恢复说明："+backup,e);}
   return "已部署并启用 DLSS 实验组件，下次启动游戏加载。\r\n这不是游戏内成功证明；按内置教程确认深度、运动估计及神经渲染面板。\r\n备份与逐文件清单："+backup;
  }
  List<string> Restore(Dictionary<string,byte[]> originals){var errors=new List<string>();foreach(var entry in originals){try{string p=SafePath(game,entry.Key);if(entry.Value==null){if(File.Exists(p))File.Delete(p);}else{Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllBytes(p,entry.Value);}}catch(Exception e){errors.Add(entry.Key+": "+e.Message);}}return errors;}
  public string Disable(){Guard();if(!Enabled)return "没有本工具管理的 DLSS 部署记录，未删除任何其他文件。";var r=ReadRecord();LastBackup=r.Backup;var current=new Dictionary<string,byte[]>();var restore=new Dictionary<string,byte[]>();
   Step(0,"预检文件可恢复性与占用状态");foreach(var f in r.Files){string p=SafePath(game,f.Relative);byte[] now=null;if(File.Exists(p)){using(var locked=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.None)){using(var ms=new MemoryStream()){locked.CopyTo(ms);now=ms.ToArray();}}}current.Add(f.Relative,now);byte[] old=f.Backup==null?null:File.ReadAllBytes(f.Backup);if(old!=null&&Hash(old)!=f.BeforeHash)throw new Exception("原文件备份校验失败："+f.Relative);restore.Add(f.Relative,old);if(now!=null&&Hash(now)!=f.InstalledHash){string save=SafePath(Path.Combine(r.Backup,"user-edits"),f.Relative);Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllBytes(save,now);}}
   var errors=Restore(restore);if(errors.Count>0){var undo=Restore(current);r.State="关闭失败，"+(undo.Count==0?"已恢复操作前状态":"回滚不完整");Save(Path.Combine(r.Backup,"changes.json"),r);throw new Exception(r.State+"\r\n"+string.Join("\r\n",errors.Concat(undo))+"\r\n人工说明："+r.Backup);}
   r.State="已关闭，原文件已恢复";Save(Path.Combine(r.Backup,"changes.json"),r);File.Delete(active);Step(100,"DLSS 实验组件已关闭");return "已关闭工具管理的 DLSS 实验组件，原文件已恢复。游戏生成的日志与其他文件保留。\r\n用户调整过的配置保存在 user-edits；人工说明："+r.Backup;
  }
  void WriteGuide(DlssRecord r){string text="橙子3D一键启用工具 · 人工关闭 DLSS 5\r\n免费第三方辅助工具，相关组件版权归原作者。先完全退出游戏。\r\n\r\n游戏目录："+game+"\r\n移走工具部署的 opengl32.dll 即可阻止下一次 OpenGL 注入。若原先存在代理不要盲删；以下逐项恢复原文件，仅处理本清单里的文件，不删除整个 reshade-shaders 目录。\r\n\r\n";foreach(var f in r.Files)text+=SafePath(game,f.Relative)+"\r\n  "+(f.Backup==null?"原先不存在：移到备份文件夹即可。":"恢复原文件："+f.Backup)+"\r\n";text+="\r\n更改过的配置先另存。软件关闭成功后保留日志、原下载和其他用户文件。完成后重启游戏；不要手改显卡驱动、系统 DLL 或关闭安全软件。";File.WriteAllText(Path.Combine(r.Backup,"人工关闭DLSS5.txt"),text,new UTF8Encoding(true));}
 }
}
