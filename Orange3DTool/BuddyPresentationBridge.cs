using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace PZ3D {
 // Compatibility entry points only. This retired launcher layer must never be deployed again.
 sealed class PresentationBridge {
  public const string Id="OrangeShootPresentation",Name="OrangeShootPresentation.jar",ConfigName="OrangeShootPresentation.properties";
  public string LastBackup;
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  static string Hash(byte[] bytes){return DlssInstaller.Hash(bytes);}
  string Journal(Engine e){return Path.Combine(e.Data,"shoot-presentation-"+Hash(Encoding.UTF8.GetBytes(e.Game.ToUpperInvariant()))+".json");}
  static string Folder(Engine e){return Path.Combine(Path.GetDirectoryName(e.Mods),Id);}
  public static string PropertiesPath(Engine e){return Path.Combine(Folder(e),"42","media","java","client",ConfigName);}
  static string Jar(Engine e){return Path.Combine(Path.GetDirectoryName(PropertiesPath(e)),Name);}
  static string Info(Engine e){return Path.Combine(Folder(e),"42","mod.info");}
  internal static byte[] RemoveRow(byte[] bytes){if(bytes==null)return null;string text=new UTF8Encoding(false,true).GetString(bytes);string clean=Regex.Replace(text,@"(?m)^[ \t]*mod[ \t]*=[ \t]*OrangeShootPresentation[ \t]*,[^\r\n]*(?:\r?\n|$)","");return clean==text?bytes:new UTF8Encoding(false).GetBytes(clean);}
  static bool Agent(Engine e,string arg){if(!arg.StartsWith("-javaagent:",StringComparison.OrdinalIgnoreCase))return false;string path=arg.Substring(11).Split(new[]{'='},2)[0].Trim('"').Replace('/','\\');try{return string.Equals(Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(e.Game,path)),Path.Combine(e.Game,Name),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
  static byte[] CleanLaunch(Engine e,byte[] bytes){if(bytes==null)return null;string text=Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');if(text.IndexOf(Name,StringComparison.OrdinalIgnoreCase)<0)return bytes;var cfg=Json.Deserialize<Dictionary<string,object>>(text);object raw;if(cfg==null||!cfg.TryGetValue("vmArgs",out raw)||!(raw is System.Collections.IEnumerable)||raw is string)return bytes;var args=((System.Collections.IEnumerable)raw).Cast<object>().Select(Convert.ToString).ToArray();if(!args.Any(a=>Agent(e,a)))return bytes;cfg["vmArgs"]=args.Where(a=>!Agent(e,a)).ToArray();return new UTF8Encoding(bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191).GetPreamble().Concat(Encoding.UTF8.GetBytes(Json.Serialize(cfg))).ToArray();}
  public bool Enabled(Engine e){try{return File.Exists(e.Mods)&&!File.ReadAllBytes(e.Mods).SequenceEqual(RemoveRow(File.ReadAllBytes(e.Mods)))&&File.Exists(Jar(e));}catch{return false;}}
  public bool RetirementNeeded(Engine e){try{foreach(string file in new[]{Jar(e),Info(e),PropertiesPath(e),Journal(e),Path.Combine(e.Data,"aim-fallback.json"),Path.Combine(e.Game,Name),Path.Combine(e.Game,ConfigName)}){LauncherMaintenance.NoReparse(file);if(File.Exists(file)||Directory.Exists(file))return true;}byte[] mods=PlayProfileTransaction.Read(e.Mods,1048576),launch=PlayProfileTransaction.Read(e.Config);return !PlayProfileTransaction.Same(mods,RemoveRow(mods))||!PlayProfileTransaction.Same(launch,CleanLaunch(e,launch));}catch{return true;}}
  public string Enable(Engine e,string source){return Retire(e);}
  public string Disable(Engine e){return Retire(e);}
  public string RepairStartup(Engine e){return Retire(e);}
  static string BackupLabel(int index,string file){string name=Path.GetFileName(file);if(name.Length>60)name=name.Substring(0,20)+"-"+Hash(Encoding.UTF8.GetBytes(file)).Substring(0,12)+Path.GetExtension(file);return index.ToString("D2")+"-"+name;}
  static void Atomic(string file,byte[] before,byte[] after){LauncherMaintenance.NoReparse(file);Directory.CreateDirectory(Path.GetDirectoryName(file));string temp=file+".orange-retire-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temp,after);if(!PlayProfileTransaction.Same(before,PlayProfileTransaction.Read(file)))throw new IOException("退役期间文件被其他程序修改，未覆盖："+file);if(before==null)File.Move(temp,file);else File.Replace(temp,file,null);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public string Retire(Engine e){using(PlayProfileTransaction.Lock(e)){return RetireUnlocked(e);}}
  string RetireUnlocked(Engine e){
   // No runtime/process/API/fingerprint gate: cleanup is an explicit edit for the next launch.
   foreach(string path in new[]{e.User,e.Data,e.Mods,e.Config,Folder(e),Journal(e)})LauncherMaintenance.NoReparse(path);
   byte[] mods=PlayProfileTransaction.Read(e.Mods,1048576),clean=RemoveRow(mods),launch=PlayProfileTransaction.Read(e.Config),cleanLaunch=CleanLaunch(e,launch);
   string session=LaunchProfiles.Journal(e);byte[] sessionBefore=PlayProfileTransaction.Read(session),sessionAfter=LaunchProfiles.RetirePresentationRecord(e,mods,clean);
   if(!RetirementNeeded(e)&&sessionAfter==null)return "本地瞄准桥接已退役；使用作者原生枪械与瞄准控制。";
   string backup=Path.Combine(e.Data,"shoot-backups","retired-"+Guid.NewGuid().ToString("N"));LauncherMaintenance.NoReparse(backup);Directory.CreateDirectory(backup);LastBackup=backup;
   var saved=new Dictionary<string,string>();var removed=new List<string>();var pending=new List<string>();var preserved=new List<string>();
   Action<string,byte[]> save=(file,bytes)=>{if(bytes==null||saved.ContainsKey(file))return;string label=BackupLabel(saved.Count+1,file);File.WriteAllBytes(Path.Combine(backup,label),bytes);saved[file]=label;};
   save(e.Mods,mods);if(sessionAfter!=null){save(session,sessionBefore);Atomic(session,sessionBefore,sessionAfter);}
   // Detach before reading/removing JARs: a loaded JAR may deny even read access.
   if(!PlayProfileTransaction.Same(mods,clean))Atomic(e.Mods,mods,clean);
   if(!PlayProfileTransaction.Same(launch,cleanLaunch)){save(e.Config,launch);Atomic(e.Config,launch,cleanLaunch);}
   bool legacy=false;string journal=Journal(e);try{byte[] bytes=PlayProfileTransaction.Read(journal,65536);if(bytes!=null){var record=Json.Deserialize<Dictionary<string,string>>(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'));legacy=record!=null&&!record.ContainsKey("addon")&&record.ContainsKey("jarHash")&&record.ContainsKey("propertiesHash");}}catch{}
   var files=new List<string>{Jar(e),PropertiesPath(e),Info(e),Path.Combine(e.Data,"aim-fallback.json")};
   foreach(string file in new[]{Path.Combine(e.Game,Name),Path.Combine(e.Game,ConfigName)})if(legacy)files.Add(file);else if(File.Exists(file)||Directory.Exists(file))preserved.Add(file);
   files.Add(journal);
   foreach(string file in files){try{LauncherMaintenance.NoReparse(file);if(Directory.Exists(file))throw new IOException("预期文件位置是目录");if(!File.Exists(file))continue;if(file==journal&&legacy&&pending.Count>0){pending.Add(file+" · 保留旧文件所有权记录，待占用解除后重试清理");continue;}byte[] bytes=PlayProfileTransaction.Read(file,16777216);save(file,bytes);File.Delete(file);removed.Add(file);}catch(Exception ex){if(ex is OutOfMemoryException||ex is StackOverflowException)throw;pending.Add(file+" · "+ex.Message);}}
   // Only remove now-empty owned directories; never traverse or delete unknown content.
   foreach(string directory in new[]{Path.GetDirectoryName(Jar(e)),Path.Combine(Folder(e),"42/media/java"),Path.Combine(Folder(e),"42/media"),Path.Combine(Folder(e),"42"),Folder(e)})try{LauncherMaintenance.NoReparse(directory);if(Directory.Exists(directory)&&!Directory.EnumerateFileSystemEntries(directory).Any())Directory.Delete(directory);}catch(Exception ex){if(ex is OutOfMemoryException||ex is StackOverflowException)throw;pending.Add(directory+" · "+ex.Message);}
   string receipt=Path.Combine(backup,"retirement-receipt.json");File.WriteAllText(receipt,Json.Serialize(new{retiredUtc=DateTime.UtcNow.ToString("o"),defaultMods=e.Mods,backups=saved,removed=removed,pending=pending,preserved=preserved}),new UTF8Encoding(false));
   File.WriteAllText(Path.Combine(backup,"restore.txt"),"已从默认清单退役启动器本地瞄准桥接。保留作者枪械、Viewpoint、汉化、校准及未知文件。备份映射见 retirement-receipt.json；整份恢复配置会覆盖后续编辑，请先核对。已加载的旧 Java 代码在玩家下次自行重启游戏后退出。",new UTF8Encoding(true));
   return "本地瞄准桥接已从默认清单退役；保留作者原生控制与个人校准。"+(pending.Count>0?"\r\n[待清理] 文件仍被占用或无法移除，请稍后明确重试：\r\n"+string.Join("\r\n",pending):"\r\n已移除记录中的桥接文件和专用设置。")+(preserved.Count>0?"\r\n保留未记录的同名旧文件，需人工核对：\r\n"+string.Join("\r\n",preserved):"")+"\r\n已加载代码在下次自行重启游戏后退出。\r\n退役收据与恢复备份："+backup;
  }
 }
}
