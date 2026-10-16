using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace PZ3D {
 sealed class ShootClient {
  public const ulong Item=3813032735;
  public const string Id="ShootToThrillMultiplayer";
  public string LastBackup {get;private set;}
  public static bool BaseReady(Engine engine){return engine.Enabled&&File.Exists(engine.Mods)&&Has(File.ReadAllText(engine.Mods),"ZombieBuddy")&&File.Exists(Path.Combine(engine.Game,"ZombieBuddy.jar"))&&File.Exists(Path.Combine(engine.Game,"zbNative.dll"));}
  public static bool Enabled(Engine engine){return File.Exists(engine.Mods)&&Has(File.ReadAllText(engine.Mods),Id);}
  static bool Has(string text,string id){return Regex.IsMatch(text,@"(?m)^\s*mod\s*=\s*"+Regex.Escape(id)+@"\s*,");}
  public static void Validate(string source){string folder=Path.Combine(source,"mods",Id),info=Path.Combine(folder,"common","mod.info"),jar=Path.Combine(folder,"42","media","java","client","ISeeThem.jar");if(!File.Exists(info)||!File.Exists(jar)||new FileInfo(jar).Length==0||!Regex.IsMatch(File.ReadAllText(info),@"(?m)^id\s*=\s*ShootToThrillMultiplayer\s*$"))throw new Exception("射击多人版文件不完整或 Mod ID 已变化，未启用。请更新后重试。");}
  public string Enable(Engine engine,string source){using(PlayProfileTransaction.Lock(engine)){return EnableUnlocked(engine,source);}}
  string EnableUnlocked(Engine engine,string source){engine.Guard();if(!BaseReady(engine))throw new Exception("请先在首页安装 / 启用 3D 与 Buddy，再安装射击客户端测试。");Validate(source);string text=File.ReadAllText(engine.Mods);foreach(string id in new[]{"ShootToThrill","ShootToThrillMultiplayerTest","ISeeThem","ISeeThemMultiplayer","ProjectViewpointADS","ViewpointTrueBallistics"})if(Has(text,id))throw new Exception("检测到冲突或尚未验证的射击模组："+id+"。请先在游戏模组列表关闭它，本工具不会擅自删除。");string retirement=new PresentationBridge().Retire(engine);return Change(engine,true)+"\r\n"+retirement;}
  public string Disable(Engine engine){using(PlayProfileTransaction.Lock(engine)){return DisableUnlocked(engine);}}
  string DisableUnlocked(Engine engine){engine.Guard();string bridge=new PresentationBridge().Disable(engine);return Change(engine,false)+"\r\n"+bridge;}
  string Change(Engine engine,bool enable){if(!File.Exists(engine.Mods))throw new Exception("客户端默认模组列表不存在，未作修改。");byte[] original=File.ReadAllBytes(engine.Mods);string text=Encoding.UTF8.GetString(original);if(Has(text,Id)==enable)return enable?"射击多人版已配置；进服加载与实际效果仍待测试。":"射击客户端测试已关闭。";var block=Regex.Match(text,@"\bmods\s*\{(?<body>[^}]*)\}");if(!block.Success)throw new Exception("模组列表格式无法识别，未作修改。");string body=Regex.Replace(block.Groups["body"].Value,@"(?m)^[ \t]*mod\s*=\s*ShootToThrillMultiplayer\s*,[^\r\n]*(?:\r?\n|$)","");if(enable)body=body.TrimEnd()+"\r\n    mod = ShootToThrillMultiplayer,\r\n";string updated=text.Substring(0,block.Groups["body"].Index)+body+text.Substring(block.Groups["body"].Index+block.Groups["body"].Length);
   string backup=Path.Combine(engine.Data,"shoot-backups",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(backup);LastBackup=backup;File.WriteAllBytes(Path.Combine(backup,"default.txt"),original);File.WriteAllText(Path.Combine(backup,"人工恢复射击配置.txt"),"退出游戏后，将备份 default.txt 复制回：\r\n"+engine.Mods+"\r\n整份恢复会覆盖后续模组调整，请先备份当前文件。\r\n也可只移除 mod = ShootToThrillMultiplayer, 一行；不要删除整个 mods 文件夹。",new UTF8Encoding(true));string temp=engine.Mods+".orange-shoot-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,updated,new UTF8Encoding(false));if(!File.ReadAllBytes(engine.Mods).SequenceEqual(original))throw new Exception("模组列表被其他程序修改，拒绝覆盖。");engine.Guard();File.Replace(temp,engine.Mods,null);}finally{if(File.Exists(temp))File.Delete(temp);}return (enable?"已写入射击客户端测试配置，不代表服务器内已加载或命中正确。":"已关闭射击客户端测试；保留 3D、Buddy、工坊缓存及个人校准。")+"\r\n修改："+engine.Mods+"\r\n备份："+backup;
  }
 }
}
