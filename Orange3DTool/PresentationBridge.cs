using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 sealed class LegacyPresentationBridge {
  public const string Pin="1E45A0343E557841D23024B57302EA9758400B7824CFA7F95FEE31F02D602132";
  public const string Name="OrangeShootPresentation.jar",ConfigName="OrangeShootPresentation.properties";
  public string LastBackup;
  static JavaScriptSerializer json=new JavaScriptSerializer();
  static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
  static byte[] Asset(){using(var input=typeof(Program).Assembly.GetManifestResourceStream("OrangeShootPresentation.jar")){if(input==null)throw new Exception("桥接资源缺失，请使用新单文件版。");using(var output=new MemoryStream()){input.CopyTo(output);return output.ToArray();}}}
  static bool Agent(string s){return s.StartsWith("-javaagent:",StringComparison.OrdinalIgnoreCase)&&s.IndexOf(Name,StringComparison.OrdinalIgnoreCase)>=0;}
  static string[] Args(Dictionary<string,object> cfg){return ((System.Collections.IEnumerable)cfg["vmArgs"]).Cast<object>().Select(o=>Convert.ToString(o)).ToArray();}
  string Journal(Engine e){return Path.Combine(e.Data,"shoot-presentation-"+Hash(Encoding.UTF8.GetBytes(e.Game.ToUpperInvariant()))+".json");}
  public bool Enabled(Engine e){try{return Args(json.Deserialize<Dictionary<string,object>>(File.ReadAllText(e.Config))).Any(Agent)&&File.Exists(Path.Combine(e.Game,Name))&&File.Exists(Path.Combine(e.Game,ConfigName))&&File.Exists(Journal(e));}catch{return false;}}
  public string Enable(Engine e,string source){e.Guard();if(!ShootClient.BaseReady(e)||!ShootClient.Enabled(e))throw new Exception("请先启用基础 3D 和射击客户端测试，再安装开镜恢复桥接。");ShootClient.Validate(source);string author=Path.Combine(source,"mods",ShootClient.Id,"42","media","java","client","ISeeThem.jar");if(Hash(File.ReadAllBytes(author))!=Pin)throw new Exception("射击 JAR 不在已核实版本范围，拒绝安装桥接。请先重新核对作者更新，不要强行使用旧桥接。");
   byte[] bridge=Asset();string text=ReticleSettings.Load(e.Data).Properties(author);string target=Path.Combine(e.Game,Name),properties=Path.Combine(e.Game,ConfigName),journal=Journal(e);if(Enabled(e)){if(Hash(File.ReadAllBytes(target))==Hash(bridge)&&File.ReadAllText(properties)==text)return "开镜补丁及原版瞄准回退已配置；游戏内效果待验证。";Disable(e);string previousBackup=LastBackup;try{return Enable(e,source);}catch(Exception ex){LastBackup=previousBackup;throw new Exception("旧开镜补丁已停用，但新版安装失败；未自动恢复旧版。旧版全部文件、启动配置和管理记录保存在："+previousBackup+"\r\n请按其中人工恢复开镜桥接.txt 核对，或重新点击安装。\r\n"+ex.Message,ex);}}if(File.Exists(target)||File.Exists(properties)||File.Exists(journal))throw new Exception("发现已有桥接文件或未清理记录，请先关闭桥接；不覆盖未知文件。");
   byte[] original=File.ReadAllBytes(e.Config);var cfg=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(original).TrimStart('\uFEFF'));if(Args(cfg).Any(Agent))throw new Exception("发现独立桥接启动参数，拒绝重复安装。");cfg["vmArgs"]=Args(cfg).Concat(new[]{"-javaagent:"+Name}).ToArray();string backup=Path.Combine(e.Data,"shoot-backups","presentation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);LastBackup=backup;File.WriteAllBytes(Path.Combine(backup,"ProjectZomboid64.json"),original);File.WriteAllText(Path.Combine(backup,"人工恢复开镜桥接.txt"),"退出游戏后，从 vmArgs 中仅移除 -javaagent:"+Name+"，再移走游戏目录中 "+Name+" 与 "+ConfigName+"。保留其他启动参数、Buddy 和个人校准。\r\n完整 JSON 备份："+Path.Combine(backup,"ProjectZomboid64.json")+"\r\n完整恢复会覆盖后续设置，恢复前先备份当前 JSON。",new UTF8Encoding(true));
   bool cfgWritten=false;try{File.WriteAllBytes(target,bridge);File.WriteAllText(properties,text,new UTF8Encoding(false));if(!File.ReadAllBytes(e.Config).SequenceEqual(original))throw new Exception("启动配置被外部修改，拒绝覆盖。");e.Guard();Atomic(e.Config,json.Serialize(cfg));cfgWritten=true;File.WriteAllText(journal,json.Serialize(new Dictionary<string,string>{{"jarHash",Hash(bridge)},{"propertiesHash",Hash(File.ReadAllBytes(properties))},{"backup",backup}}));}catch{if(cfgWritten&&!File.ReadAllBytes(e.Config).SequenceEqual(Encoding.UTF8.GetBytes(json.Serialize(cfg))))throw new Exception("启动配置被外部修改，保留桥接文件以免破坏启动。请按备份说明人工恢复："+backup);if(cfgWritten)File.WriteAllBytes(e.Config,original);if(File.Exists(target))File.Delete(target);if(File.Exists(properties))File.Delete(properties);if(File.Exists(journal))File.Delete(journal);throw;}
   return "已配置本地开镜恢复桥接；未修改服务器或作者 JAR。\r\n仅本地表现，实际渲染、开镜和命中对齐均待实测。\r\n新增："+target+"\r\n"+properties+"\r\n修改："+e.Config+"\r\n备份："+backup;
  }
  public string Disable(Engine e){
   e.Guard();string journal=Journal(e);byte[] launchSnapshot=File.ReadAllBytes(e.Config);var cfg=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(launchSnapshot).TrimStart('\uFEFF'));string exact="-javaagent:"+Name;
   if(Args(cfg).Any(a=>Agent(a)&&!string.Equals(a,exact,StringComparison.OrdinalIgnoreCase)))throw new Exception("桥接启动参数已被改为独立路径或额外参数，拒绝删文件，以免破坏游戏启动。请按备份说明核对后手动移除该参数。");
   if(!File.Exists(journal)){if(Args(cfg).Any(Agent))throw new Exception("有桥接参数但无管理记录，未擅自清理。请按备份说明人工恢复。");return "没有本工具管理的开镜恢复桥接。";}
   byte[] journalBytes=File.ReadAllBytes(journal);var record=json.Deserialize<Dictionary<string,string>>(Encoding.UTF8.GetString(journalBytes));
   string target=Path.Combine(e.Game,Name),properties=Path.Combine(e.Game,ConfigName),backup=Path.Combine(e.Data,"shoot-backups","presentation-disable-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);LastBackup=backup;
   var saved=new Dictionary<string,byte[]>();saved[e.Config]=launchSnapshot;saved[target]=File.Exists(target)?File.ReadAllBytes(target):null;saved[properties]=File.Exists(properties)?File.ReadAllBytes(properties):null;
   bool modified=(saved[target]!=null&&Hash(saved[target])!=record["jarHash"])||(saved[properties]!=null&&Hash(saved[properties])!=record["propertiesHash"]);
   foreach(var pair in saved)if(pair.Value!=null)File.WriteAllBytes(Path.Combine(backup,Path.GetFileName(pair.Key)),pair.Value);
   File.WriteAllBytes(Path.Combine(backup,"journal.json"),journalBytes);
   File.WriteAllText(Path.Combine(backup,"人工恢复开镜桥接.txt"),"本次已保存全部原文件和管理记录。退出游戏后，按原路径恢复文件；先恢复 JAR，再恢复启动参数。游戏目录："+e.Game+"\r\n管理记录："+journal,new UTF8Encoding(true));
   foreach(string file in new[]{target,properties,journal})if(File.Exists(file))using(var locked=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
   cfg["vmArgs"]=Args(cfg).Where(a=>!string.Equals(a,exact,StringComparison.OrdinalIgnoreCase)).ToArray();e.Guard();if(!File.ReadAllBytes(e.Config).SequenceEqual(saved[e.Config]))throw new Exception("启动配置已变化，拒绝覆盖。");
   bool changed=false;try{Atomic(e.Config,json.Serialize(cfg));changed=true;foreach(string file in new[]{target,properties})if(File.Exists(file))File.Delete(file);File.Delete(journal);}
   catch(Exception failure){var errors=new List<string>();foreach(string file in new[]{target,properties})try{if(saved[file]!=null)File.WriteAllBytes(file,saved[file]);}catch(Exception ex){errors.Add(file+": "+ex.Message);}try{File.WriteAllBytes(journal,journalBytes);}catch(Exception ex){errors.Add(journal+": "+ex.Message);}
    if(changed&&errors.Count==0)try{File.WriteAllBytes(e.Config,saved[e.Config]);}catch(Exception ex){errors.Add(e.Config+": "+ex.Message);}
    throw new Exception(failure.Message+"\r\n"+(errors.Count==0?"已回滚关闭操作。":"部分恢复失败，保留停用参数以免加载缺失 JAR："+string.Join("; ",errors))+"\r\n恢复备份："+backup,failure);}
   return "已关闭本地开镜恢复桥接，保留其他启动参数与射击配置。"+(modified?"修改过的桥接文件已保存在备份中。":"")+"\r\n备份："+backup;
  }
  static void Atomic(string path,string value){string temp=path+".orange-bridge-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,value,new UTF8Encoding(false));File.Replace(temp,path,null);}finally{if(File.Exists(temp))File.Delete(temp);}}
 }
}
