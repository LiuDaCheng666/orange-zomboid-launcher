using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
namespace PZ3D {
 sealed class ShaderChoice {
  public string Id,Name,File,Description;public bool Valid=true;
 }
 sealed class ShaderSelection {
  public const string Eclipse="minecraft-Eclipse-Viewpoint-jianghuiban-v3";
  public const string EclipseFile="Eclipse-Viewpoint-jianghuiban-v3.zip";
  readonly string root,backup,file;readonly byte[] original;readonly bool existed;readonly PropertyDocument document;
  public readonly List<ShaderChoice> Packs=new List<ShaderChoice>();public string Selected{get;private set;}
  public ShaderSelection(string user,string backups){root=user;backup=backups;file=Path.Combine(root,"viewpoint-live.properties");existed=System.IO.File.Exists(file);original=existed?System.IO.File.ReadAllBytes(file):new byte[0];document=new PropertyDocument(original);string selected;Selected=document.Values.TryGetValue("graphics.pack",out selected)?selected:"";
   Packs.Add(new ShaderChoice{Id="normal",Name="Vanilla · 原版风格",Description="Viewpoint 内置基础效果，适合作为性能回退选项。"});Packs.Add(new ShaderChoice{Id="default",Name="Default · 默认效果",Description="Viewpoint 内置效果，和工具推荐的 Eclipse 默认包不是同一个。"});Packs.Add(new ShaderChoice{Id="vivid",Name="Vivid · 鲜艳效果",Description="Viewpoint 内置鲜艳风格。"});
   string dir=Path.Combine(root,"viewpoint-shaderpacks");if(Directory.Exists(dir))foreach(string zip in Directory.GetFiles(dir,"*.zip").OrderBy(p=>p)){string name=Path.GetFileNameWithoutExtension(zip);string id="minecraft-"+Regex.Replace(name,@"[^A-Za-z0-9_-]","_");var choice=new ShaderChoice{Id=id,Name=name,File=zip,Description=id==Eclipse?"专为 Viewpoint 3D 模组适配的 Eclipse 光影包（用户提供 v3）。仍需游戏内验证编译与画面效果。":"Minecraft / Iris 光影兼容为实验功能，不保证每个包都正常渲染。"};try{Engine.ValidateShader(zip);}catch(Exception e){choice.Valid=false;choice.Description="不可启用："+e.Message;}Packs.Add(choice);}
   foreach(var group in Packs.GroupBy(p=>p.Id).Where(g=>g.Count()>1))foreach(var p in group){p.Valid=false;p.Description="名称转换后 ID 重复，请先重命名文件再刷新。";}
  }
  public bool Changed(){return System.IO.File.Exists(file)!=existed||(existed&&!System.IO.File.ReadAllBytes(file).SequenceEqual(original));}
  public string Select(string id,bool gameRunning){if(gameRunning)throw new Exception("请完全退出游戏后切换光影；保存后下次启动读取。");if(Changed())throw new Exception("游戏或其他程序已改变光影设置，请刷新列表再选择，未覆盖新设置。");var pack=Packs.SingleOrDefault(p=>p.Id==id);if(pack==null||!pack.Valid)throw new Exception("光影未安装、文件无效或 ID 重复，不能启用。");if(pack.File!=null)Engine.ValidateShader(pack.File);
   string mode;if(document.Values.TryGetValue("graphics.mode",out mode)&&mode!="Modern")throw new Exception("未核实的渲染模式，暂不外部切包，请在游戏内调整。");
   var changes=new Dictionary<string,string>{{"graphics.pack",id},{"graphics.mode","Modern"}};if(changes.All(p=>document.Values.ContainsKey(p.Key)&&document.Values[p.Key]==p.Value))return "已经选择该光影；游戏渲染结果仍需验证。";
   var existing=changes.Where(p=>document.Values.ContainsKey(p.Key)).ToDictionary(p=>p.Key,p=>p.Value);byte[] bytes=document.Updated(existing);string extra="";foreach(var p in changes.Where(p=>!document.Values.ContainsKey(p.Key)))extra+=p.Key+"="+p.Value+"\r\n";if(extra.Length>0){string text=new UTF8Encoding(false,true).GetString(bytes);if(text.Length>0&&!text.EndsWith("\n")&&!text.EndsWith("\r"))extra="\r\n"+extra;bytes=bytes.Concat(Encoding.UTF8.GetBytes(extra)).ToArray();}
   string dir=Path.Combine(backup,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-shader-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(dir);if(existed)System.IO.File.WriteAllBytes(Path.Combine(dir,"viewpoint-live.properties"),original);System.IO.File.WriteAllText(Path.Combine(dir,"人工恢复光影.txt"),"退出游戏后恢复："+file+"\r\n"+(existed?"复制本目录 viewpoint-live.properties 到上面的路径；整份恢复会覆盖后续设置，请先另存当前文件。":"原配置文件不存在；撤销时先备份当前文件，移走本工具新建的 viewpoint-live.properties。")+"\r\n本次选择："+id+"；不代表已在游戏内成功渲染。",new UTF8Encoding(true));
   Directory.CreateDirectory(root);string temp=file+".orange-"+Guid.NewGuid().ToString("N")+".tmp";try{System.IO.File.WriteAllBytes(temp,bytes);if(Changed())throw new Exception("保存期间配置改变，停止写入。备份："+dir);if(existed)System.IO.File.Replace(temp,file,null);else System.IO.File.Move(temp,file);}finally{if(System.IO.File.Exists(temp))System.IO.File.Delete(temp);}return "已选择："+pack.Name+"。下次启动游戏读取；当前实际效果待游戏验证。\r\n备份："+dir;
  }
  public string DefaultOnce(string marker,bool running){if(System.IO.File.Exists(marker))return "保留玩家已有光影选择。";string result=Select(Eclipse,running);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(marker)));System.IO.File.WriteAllText(marker,Eclipse);return result;}
 }
 static class StartupHelp {
  public static bool ShouldShow(string preference){return !System.IO.File.Exists(preference)||System.IO.File.ReadAllText(preference)!="hidden";}
  public static void SetHidden(string preference,bool hidden){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(preference)));System.IO.File.WriteAllText(preference,hidden?"hidden":"show");}
  public static string Body(string root){string first="O（默认，尚未读取）",settings="DELETE（默认，尚未读取）",third="SHIFT+O（默认）";try{string path=Path.Combine(root,"viewpoint-live.properties");if(System.IO.File.Exists(path)){var doc=new PropertyDocument(System.IO.File.ReadAllBytes(path));string v;if(doc.Values.TryGetValue("keys.firstPerson",out v))first=v.Length==0?"未绑定":v;if(doc.Values.TryGetValue("keys.settings",out v))settings=v.Length==0?"未绑定":v;if(doc.Values.TryGetValue("keys.thirdPerson",out v))third=v.Length==0?"未绑定":v;}}catch{first+="，配置读取失败";}
   return "1. 完全退出游戏，在主页面勾选需要的组件，点“安装”，等待完成后再从 Steam 正常启动。枪械扩展及联机开镜补丁自动一起处理，不用分别修复或安装桥接。\r\n\r\n2. 进入游戏后，3D 开关当前绑定："+first+"\r\n第一／第三人称切换："+third+"\r\n设置窗口当前绑定："+settings+"\r\n枪械：右键瞄准；Ctrl+V 切换腰射/精瞄，以游戏实际绑定为准。\r\n\r\n3. 默认 O。如与其他操作冲突，点下方“编辑快捷键”；完全退出游戏后修改并保存，下次启动生效。工具无法检查其他模组所有快捷键，不会自动替你解绑。\r\n\r\n4. 游戏内中文：主页面勾选“游戏内中文（老余汉化）”并安装；游戏语言选择简体中文，首次核对批准 laoyu.javacn / LaoYuTranslationJava.jar，然后重启。汉化版权归老余，不覆盖 Viewpoint 原始 JAR。进服效果待验证。\r\n\r\n5. 卸载：只勾选不要的组件，点“卸载”。勾选基础 3D 时会连带关闭枪械和汉化；只卸载枪械请取消其余勾选。保留下载文件、光影与存档。\r\n\r\n6. Eclipse v3 是本工具初次安装的默认光影，专为 Viewpoint 3D 适配；之后保留玩家选择。“光影列表”可切换或回退。首次 Java 授权只允许来源确认的已安装组件，名称空白或未知不要直接允许。联机实际加载、开镜及画面效果仍需实测。";
  }
 }
}
