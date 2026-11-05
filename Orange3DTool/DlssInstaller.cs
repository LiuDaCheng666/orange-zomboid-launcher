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
  readonly string game,data,active;readonly JavaScriptSerializer json=new JavaScriptSerializer();
  public Action<int,string> Progress;
  public string LastBackup {get;private set;}
  public bool Enabled {get{return File.Exists(active);}}
  public DlssInstaller(string game,string data,bool test=false){this.game=Path.GetFullPath(game);this.data=data;active=StatePath(this.game,data);if(File.Exists(active)){var r=ReadRecord();LastBackup=r.Backup;}}
  static string StatePath(string game,string data){return Path.Combine(data,"dlss-state",Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToLowerInvariant())).Substring(0,16)+".json");}
  public static bool Installed(string game,string data){return File.Exists(StatePath(game,data));}
  void Step(int n,string s){if(Progress!=null)Progress(n,s);}
  void Guard(){if(!File.Exists(Path.Combine(game,"ProjectZomboid64.json")))throw new Exception("请先选择正确的游戏目录。");SafePath(game,"opengl32.dll");}
  public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
  public static string HashFile(string p){using(var h=SHA256.Create())using(var s=File.OpenRead(p))return BitConverter.ToString(h.ComputeHash(s)).Replace("-","");}
  public static string SafePath(string root,string relative){if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Split('/','\\').Any(p=>p==".."||p=="."||p.Length==0))throw new Exception("拒绝不安全文件路径："+relative);string full=Path.GetFullPath(Path.Combine(root,relative));string prefix=Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar;if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new Exception("文件超出目标目录。");for(string dir=Path.GetDirectoryName(full);dir!=null;dir=Path.GetDirectoryName(dir)){if(Directory.Exists(dir)&&(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝通过符号链接或目录联接写入："+dir);if(string.Equals(dir,Path.GetFullPath(root),StringComparison.OrdinalIgnoreCase))break;}if(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new Exception("拒绝符号链接文件："+full);return full;}
  static bool Allowed(string rel){return RootNames.Contains(rel,StringComparer.OrdinalIgnoreCase)||(rel.StartsWith("reshade-shaders/",StringComparison.OrdinalIgnoreCase)&&new[]{".fx",".fxh",".png",".dds",".jpg",".jpeg",".txt",".md",".json"}.Contains(Path.GetExtension(rel).ToLowerInvariant()));}
  static readonly string[] RootNames={"opengl32.dll","dlss5-feed.addon64","renodx-dlss5.addon64","nvngx_dlss.dll","nvngx_dlssnr.dll"};
  DlssRecord ReadRecord(){var r=json.Deserialize<DlssRecord>(File.ReadAllText(active));if(r==null||!string.Equals(r.Game,game,StringComparison.OrdinalIgnoreCase)||r.Files==null)throw new Exception("DLSS 状态记录无法验证，请用人工恢复说明处理。");foreach(var f in r.Files){if(!Allowed(f.Relative)&&!new[]{"ReShade.ini","ReShadePreset.ini","dlss5-feed.cfg"}.Contains(f.Relative))throw new Exception("状态记录含未知文件。");SafePath(game,f.Relative);}return r;}
  void Save(string path,DlssRecord r){Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,json.Serialize(r),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
  List<string> Restore(Dictionary<string,byte[]> originals){var errors=new List<string>();foreach(var entry in originals){try{string p=SafePath(game,entry.Key);if(entry.Value==null){if(File.Exists(p))File.Delete(p);}else{Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllBytes(p,entry.Value);}}catch(Exception e){errors.Add(entry.Key+": "+e.Message);}}return errors;}
  public string Disable(){Guard();if(!Enabled)return "没有本工具管理的 DLSS 部署记录，未删除任何其他文件。";var r=ReadRecord();LastBackup=r.Backup;var current=new Dictionary<string,byte[]>();var restore=new Dictionary<string,byte[]>();
   Step(0,"预检文件可恢复性与占用状态");foreach(var f in r.Files){string p=SafePath(game,f.Relative);byte[] now=null;if(File.Exists(p)){using(var locked=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.None)){using(var ms=new MemoryStream()){locked.CopyTo(ms);now=ms.ToArray();}}}current.Add(f.Relative,now);byte[] old=f.Backup==null?null:File.ReadAllBytes(f.Backup);if(old!=null&&Hash(old)!=f.BeforeHash)throw new Exception("原文件备份校验失败："+f.Relative);restore.Add(f.Relative,old);if(now!=null&&Hash(now)!=f.InstalledHash){string save=SafePath(Path.Combine(r.Backup,"user-edits"),f.Relative);Directory.CreateDirectory(Path.GetDirectoryName(save));File.WriteAllBytes(save,now);}}
   var errors=Restore(restore);if(errors.Count>0){var undo=Restore(current);r.State="关闭失败，"+(undo.Count==0?"已恢复操作前状态":"回滚不完整");Save(Path.Combine(r.Backup,"changes.json"),r);throw new Exception(r.State+"\r\n"+string.Join("\r\n",errors.Concat(undo))+"\r\n人工说明："+r.Backup);}
   r.State="已关闭，原文件已恢复";Save(Path.Combine(r.Backup,"changes.json"),r);File.Delete(active);Step(100,"DLSS 实验组件已关闭");return "已关闭工具管理的 DLSS 实验组件，原文件已恢复。游戏生成的日志与其他文件保留。\r\n用户调整过的配置保存在 user-edits；人工说明："+r.Backup;
  }
 }
}
