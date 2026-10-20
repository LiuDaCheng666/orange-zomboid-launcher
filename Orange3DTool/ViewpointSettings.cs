using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
namespace PZ3D {
 sealed class SettingInfo {
  public string Name,Group,Description,Kind;public string[] Choices;public double? Minimum,Maximum;
  public SettingInfo(string name,string group,string description,string kind,string choices=null){Name=name;Group=group;Description=description;Kind=kind;Choices=choices==null?null:choices.Split('|');}
  public void Validate(string value){
   if(Kind=="bool" && value!="true"&&value!="false")throw new Exception(Name+" 必须是 true 或 false。");
   if(Kind=="number"||Kind=="integer"){double n;int integer;if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsNaN(n)||double.IsInfinity(n)||n<0||(Kind=="integer"&&!int.TryParse(value,NumberStyles.Integer,CultureInfo.InvariantCulture,out integer)))throw new Exception(Name+" 必须是非负的有效"+(Kind=="integer"?"整数":"数值")+"。");if(Minimum.HasValue&&n<Minimum.Value||Maximum.HasValue&&n>Maximum.Value)throw new Exception(Name+" 超出作者当前范围："+Minimum+"—"+Maximum);} 
   if(Choices!=null&&!Choices.Contains(value))throw new Exception(Name+" 必须选择列出的有效值。");
   if(Kind=="key"){if(!Regex.IsMatch(value,@"^[A-Z0-9_+, ]*$"))throw new Exception("快捷键使用英文大写名称，例如 O、SHIFT+O；空值表示解绑。");foreach(string token in value.Split(new[]{'+',','},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()))if(!Regex.IsMatch(token,@"^(?:[A-Z]|[0-9]|F(?:[1-9]|1[0-9]|2[0-4])|SHIFT|CTRL|ALT|LSHIFT|RSHIFT|LCONTROL|RCONTROL|LMENU|RMENU|SPACE|TAB|RETURN|ESCAPE|INSERT|DELETE|HOME|END|PRIOR|NEXT|UP|DOWN|LEFT|RIGHT|BACK|CAPITAL|NUMPAD[0-9]|ADD|SUBTRACT|MULTIPLY|DIVIDE|DECIMAL)$"))throw new Exception("未识别的按键名称："+token+"。请核对游戏里的实际键名。");}
  }
 }
 static class SettingsCatalog {
  public static readonly Dictionary<string,SettingInfo> Items=Create();
  static Dictionary<string,SettingInfo> Create(){var d=new Dictionary<string,SettingInfo>();
   Action<string,string,string,string,string> add=(key,name,group,desc,kind)=>d[key]=new SettingInfo(name,group,desc,kind);
   add("view.verticalFov","第一人称垂直视野角","视角","值越大视野越宽，透视拉伸更明显；这里是垂直角度，不是水平角度。适合逐步调整。","number");
   add("view.thirdPersonVerticalFov","第三人称垂直视野角","视角","影响肩后视角的视野宽度；不是相机距离。","number");
   add("camera.headMovement","头部晃动强度","视角","控制头部移动的视觉幅度；减小通常更适合容易晕 3D 的玩家。","number");
   add("mouse.sensitivity","鼠标灵敏度","操作","调整转动视角的响应幅度；不是 Windows 鼠标速度。","number");
   add("mouse.invertY","反转鼠标纵向","操作","true 反转上下看，false 使用常规方向。","bool");
   add("mouse.readBeforeDrawing","绘制前读取鼠标","操作","调整输入读取时机，影响视角操作延迟。","bool");
   var keys=new[]{
    new[]{"firstPerson","开启／关闭第一人称","切换 Viewpoint 视角。默认 O。"},new[]{"thirdPerson","第一／第三人称切换","视角开启后，在眼睛视角和肩后视角间切换。默认 SHIFT+O。"},
    new[]{"settings","打开／关闭设置窗","打开 Viewpoint 自己的设置和光影界面。默认 DELETE。"},new[]{"screenshot","Viewpoint 截图","使用模组截图功能。默认 F9。"},
    new[]{"freeCamera","自由镜头","进入自由相机。默认 INSERT+O；联机请遵守服务器规则。"},new[]{"physicalCamera","物理自由镜头","模组的 Physical Free Cam 模式。默认 SHIFT+INSERT+O。"},
    new[]{"cameraForward","自由镜头前进","自由镜头飞行时向前。默认 W。"},new[]{"cameraBack","自由镜头后退","自由镜头飞行时向后。默认 S。"},new[]{"cameraLeft","自由镜头向左","默认 A。"},new[]{"cameraRight","自由镜头向右","默认 D。"},
    new[]{"cameraUp","自由镜头上升","默认 SPACE 或 E；逗号分隔备选按键。"},new[]{"cameraDown","自由镜头下降","默认 LSHIFT 或 Q。"},new[]{"cameraFast","自由镜头加速","按住此键加速飞行。LMENU 表示左 Alt。"},new[]{"cameraHome","自由镜头归位","NEXT 是 Page Down 的内部键名；使用前确认模组内绑定说明。"},
    new[]{"lootTake","拾取当前目标","模组准星拾取菜单的操作键，默认 F。"},new[]{"lootTakeAll","拾取全部","模组拾取菜单的全部拾取键，默认 R。"},new[]{"lootWindow","打开拾取窗口","模组拾取窗口操作，默认 TAB。"}};
   foreach(var k in keys)add("keys."+k[0],k[1],"快捷键",k[2]+" 空值解绑；+ 表示组合键。不要与其他操作设置相同组合。","key");
   foreach(var pair in new[]{new[]{"models","尸体立体模型"},new[]{"shadows","尸体阴影"},new[]{"inView","尸体视野筛选"},new[]{"tint","尸体色调处理"}})add("corpses."+pair[0],pair[1],"尸体显示","控制对应的尸体显示效果，影响尸体密集场景的视觉质量和渲染开销。","bool");
   add("corpses.limit","尸体数量限制","尸体显示","限制尸体显示相关预算；降低可能减少尸体密集场景的工作量，也可能减少显示内容。","integer");
   add("corpses.posesPerFrame","每帧尸体姿态预算","尸体显示","每帧更新的尸体姿态数量；越高单帧工作越多，越低更新可能更滞后。","integer");
   add("far.workers","远景工作线程","远景与性能","控制远景构建使用的后台线程数量。","integer");
   add("far.budgetMs","远景每帧时间预算","远景与性能","远景处理的毫秒预算；调高可能加快更新，但增加单帧时间。","number");
   add("floors.texelsPerTile","地面每格纹理精度","纹理","数值越大地面细节通常越丰富，同时增加纹理和显存开销。","integer");
   add("floors.detailChunks","高精度地面区块范围","纹理","更多高精度区块会增加内存与渲染开销。","integer");
   foreach(string prefix in new[]{"default","normal","vivid"})foreach(var p in new[]{new[]{"bloom","泛光","亮部向周边扩散，增强明亮物体的光晕。"},new[]{"motionBlur","运动模糊","运动时增加模糊；关闭可能更清晰、减轻眩晕，并减少相关后处理。"},new[]{"depthOfField","景深","焦点外画面变模糊；并非游戏分辨率降低。"},new[]{"sunShadows","太阳阴影","太阳照射产生阴影；增加阴影渲染工作。"},new[]{"lampShadows","灯光阴影","局部灯光产生阴影；多灯场景可能开销较高。"},new[]{"flashlightShadow","手电筒阴影","手电筒的动态阴影。"},new[]{"godrays","光束效果","遮挡与光照形成的光束后处理。"},new[]{"volumetrics","体积效果","体积光／雾相关效果；通常增加 GPU 工作量。"},new[]{"lensFlare","镜头光晕","模拟强光照射镜头的光斑。"},new[]{"clouds","云层","云层显示与处理。"},new[]{"puddles","水洼效果","水洼相关视觉效果。"},new[]{"farShadows","远景阴影","为远景提供阴影；可能增加远景工作量。"},new[]{"bounceLight","间接反弹光","模拟间接照明，改善环境亮度。"}})add("pack."+prefix+"."+p[0],p[1],"内置光影／"+prefix,p[2]+" 仅影响这个光影包，其他包有独立参数。","bool");
   foreach(string prefix in new[]{"default","normal","vivid"})foreach(var p in new[]{new[]{"exposure","曝光"},new[]{"contrast","对比度"},new[]{"saturation","饱和度"},new[]{"vibrance","鲜艳度"},new[]{"motionBlurStrength","运动模糊强度"},new[]{"dofStrength","景深强度"},new[]{"lensFlareStrength","镜头光晕强度"}})add("pack."+prefix+"."+p[0],p[1],"内置光影／"+prefix,"只调整当前命名光影包的效果强度；","number");
   add("packs.PZVoxelStudioViewpoint","启用额外物件模型包","物件模型","启用已安装的额外物件模型包。","bool");
   add("gameFps","游戏帧率上限","帧率","限制游戏帧率；开启无限帧率后此项不生效。","integer");
   add("gameUncapped","游戏不限制帧率","帧率","true 取消限制，false 使用 gameFps。无限制可能增加功耗和温度。","bool");
   add("menuFramerateIndex","菜单帧率选项编号","帧率","1 跟随游戏，2 无上限；3 起依次：500、430、400、360、330、300、244、240、165、144、120、95、90、75、60、55、45、30、24。17 对应 60 FPS。","integer");
   add("graphics.mode","渲染模式","画质与光影","模组当前渲染模式。Modern 为现代渲染模式；具体模式切换和兼容约束请在游戏设置内操作，外部暂只读。","readonly");
   add("graphics.pack","当前光影包","画质与光影","vivid 为鲜艳风格，default 为默认效果，normal 为基础风格；其他名称对应光影包。请在“光影”页切换光影包。","readonly");
   add("graphics.preset","当前画质预设","画质与光影","如 Ultra 表示极高画质预设。请在游戏内切换整套画质预设。","readonly");
   foreach(var p in new[]{new[]{"corpseMiB","尸体模型内存池"},new[]{"cpuCacheMiB","CPU 缓存池"},new[]{"farMiB","远景内存池"},new[]{"floorMiB","地面纹理池"},new[]{"lightAtlasMiB","灯光图集池"},new[]{"minecraftMiB","Minecraft 光影资源池"},new[]{"modelMeshMiB","模型网格池"},new[]{"nearMeshMiB","近景网格池"},new[]{"packMiB","模型包资源池"},new[]{"shellMiB","远景外壳池"}})add("memory."+p[0],p[1],"内存池（只读）","对应资源池的预算，单位 MiB。请在游戏内调整。","readonly");
   foreach(var p in new[]{new[]{"floors.filter","地面纹理过滤"},new[]{"models.filter","模型纹理过滤"},new[]{"sprites.filter","平面物件纹理过滤"}})add(p[0],p[1],"纹理","纹理采样过滤方式；Anisotropic 表示各向异性过滤，Lanczos 是重采样过滤方式。请在游戏内选择过滤方式。","readonly");
   SettingsChinese.Apply(d);return d;
  }
  public static SettingInfo Find(string key){SettingInfo info;if(Items.TryGetValue(key,out info))return info;return null;}
  public static string Group(string key){var info=Find(key);if(info!=null)return info.Group;if(key.StartsWith("minecraft.")){string[] parts=key.Split('.');return "Minecraft 光影／"+(parts.Length>1?parts[1]:"未识别");}if(key.StartsWith("memory."))return "内存池（只读）";if(key.StartsWith("keys."))return "调试快捷键（只读）";return "其他／"+key.Split('.')[0];}
  public static string Description(string key){var info=Find(key);if(info!=null)return info.Description;if(key.StartsWith("memory."))return "模组内存池参数，单位 MiB。请在游戏内调整。";if(key.StartsWith("minecraft."))return "光影包参数，请在游戏内的光影设置中调整。";return "高级参数，请在游戏内调整。";}
 }
 sealed class PropertyDocument {
  public string Text;public byte[] Original;public readonly Dictionary<string,string> Values=new Dictionary<string,string>();
  readonly Dictionary<string,Tuple<int,int,string>> spans=new Dictionary<string,Tuple<int,int,string>>();
  public PropertyDocument(byte[] bytes){Original=bytes;Text=new UTF8Encoding(false,true).GetString(bytes);if(Text.Length>0&&Text[0]=='\uFEFF')Text=Text.Substring(1);int offset=0;var lines=Regex.Matches(Text,@"[^\r\n]*(?:\r\n|\r|\n|$)");for(int i=0;i<lines.Count;i++){string raw=lines[i].Value;if(raw.Length==0)continue;int start=offset;offset+=raw.Length;string logical=raw.TrimEnd('\r','\n');string initial=logical.TrimStart(' ','\t','\f');if(initial.Length==0||initial[0]=='#'||initial[0]=='!')continue;while(Regex.Match(logical,@"\\+$").Length%2==1&&i+1<lines.Count){logical=logical.Substring(0,logical.Length-1);raw=lines[++i].Value;offset+=raw.Length;logical+=raw.TrimStart(' ','\t','\f').TrimEnd('\r','\n');}string trimmed=logical.TrimStart(' ','\t','\f');if(trimmed.Length==0||trimmed[0]=='#'||trimmed[0]=='!')continue;var split=Regex.Match(trimmed,@"^(?<key>(?:\\.|[^\\:=\s])*)(?<sep>\s*[=:]?\s*)(?<value>.*)$");if(!split.Success)continue;string key=Decode(split.Groups["key"].Value);Values[key]=Decode(split.Groups["value"].Value);string ending=raw.EndsWith("\r\n")?"\r\n":raw.EndsWith("\n")?"\n":raw.EndsWith("\r")?"\r":"";spans[key]=Tuple.Create(start,offset-start,ending);}}
  public static string Decode(string raw){return Regex.Replace(raw,@"\\(u[0-9a-fA-F]{4}|.)",m=>{string c=m.Groups[1].Value;if(c[0]=='u'&&c.Length==5)return ((char)int.Parse(c.Substring(1),NumberStyles.HexNumber)).ToString();return c=="n"?"\n":c=="r"?"\r":c=="t"?"\t":c=="f"?"\f":c;});}
  static string Escape(string s,bool key){var b=new StringBuilder();for(int i=0;i<s.Length;i++){char c=s[i];if(c=='\\')b.Append("\\\\");else if(c=='\n')b.Append("\\n");else if(c=='\r')b.Append("\\r");else if(c=='\t')b.Append("\\t");else if(c>126)b.Append("\\u"+((int)c).ToString("X4"));else if((key&&" :=#!".Contains(c))||(!key&&i==0&&c==' '))b.Append('\\').Append(c);else b.Append(c);}return b.ToString();}
  public byte[] Updated(Dictionary<string,string> changes){string updated=Text;foreach(var entry in changes.OrderByDescending(p=>spans[p.Key].Item1)){var span=spans[entry.Key];updated=updated.Remove(span.Item1,span.Item2).Insert(span.Item1,Escape(entry.Key,true)+"="+Escape(entry.Value,false)+span.Item3);}byte[] result=Encoding.UTF8.GetBytes(updated);if(Original.Length>=3&&Original[0]==239&&Original[1]==187&&Original[2]==191)result=new byte[]{239,187,191}.Concat(result).ToArray();return result;}
 }
 sealed class ViewpointSettings {
  public static readonly string[] Files={"viewpoint-live.properties","viewpoint-framecap.properties","viewpoint.properties"};
  readonly string root,backupRoot;readonly Dictionary<string,PropertyDocument> docs=new Dictionary<string,PropertyDocument>();public Dictionary<string,string> Values{get;private set;}
  public ViewpointSettings(string root,string backup){this.root=root;backupRoot=backup;Values=new Dictionary<string,string>();foreach(string name in Files){string path=Path.Combine(root,name);if(!File.Exists(path))continue;byte[] bytes;using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var memory=new MemoryStream()){stream.CopyTo(memory);bytes=memory.ToArray();}var doc=new PropertyDocument(bytes);docs[name]=doc;foreach(var p in doc.Values)Values[name+"|"+p.Key]=p.Value;}}
  static string Hash(byte[] bytes){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
  public bool Changed(){foreach(string name in Files){string file=Path.Combine(root,name);if(File.Exists(file)!=docs.ContainsKey(name))return true;if(docs.ContainsKey(name)&&Hash(File.ReadAllBytes(file))!=Hash(docs[name].Original))return true;}return false;}
  internal static void ValidateEditable(string key,string value){SettingInfo info=SettingsCatalog.Find(key);if(info==null||info.Kind=="readonly")throw new Exception("此参数请在游戏内修改。");info.Validate(value);if(key=="menuFramerateIndex"&&(int.Parse(value)<1||int.Parse(value)>21))throw new Exception("菜单帧率编号必须在 1 至 21 之间。");if((key=="mouse.sensitivity"||key.StartsWith("view."))&&double.Parse(value,CultureInfo.InvariantCulture)<=0)throw new Exception("灵敏度和视野角必须大于 0。");if(key.StartsWith("view.")&&double.Parse(value,CultureInfo.InvariantCulture)>=180)throw new Exception("垂直视野角必须小于 180 度。");}
  public string Save(Dictionary<string,string> changes,bool gameRunning){
   if(Changed())throw new Exception("设置已在游戏或其他程序中改变，请重新读取并核对，不会覆盖新设置。");
   var updates=new Dictionary<string,Dictionary<string,string>>();foreach(var p in changes){if(!Values.ContainsKey(p.Key))throw new Exception("参数不在当前文件中，拒绝新增未知参数。");string[] id=p.Key.Split(new[]{'|'},2);ValidateEditable(id[1],p.Value);if(p.Value==Values[p.Key])continue;if(!updates.ContainsKey(id[0]))updates[id[0]]=new Dictionary<string,string>();updates[id[0]][id[1]]=p.Value;}
   if(updates.Count==0)return "没有需要保存的修改。";
   var pending=updates.ToDictionary(p=>p.Key,p=>docs[p.Key].Updated(p.Value));string backup=Path.Combine(backupRoot,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-settings-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(backup);
   foreach(string name in pending.Keys)File.WriteAllBytes(Path.Combine(backup,name),docs[name].Original);
   File.WriteAllText(Path.Combine(backup,"人工恢复设置.txt"),"退出游戏后，把此目录内对应 properties 文件复制回：\r\n"+root+"\r\n只恢复需要撤销的文件；恢复整份备份会覆盖之后的游戏设置。\r\n修改：\r\n"+string.Join("\r\n",changes.Select(p=>p.Key+"="+p.Value)),new UTF8Encoding(true));
   var written=new List<string>();try{foreach(var p in pending){string file=Path.Combine(root,p.Key);if(Hash(File.ReadAllBytes(file))!=Hash(docs[p.Key].Original))throw new Exception("保存期间文件发生变化，停止写入。");string temp=file+".orange-"+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,p.Value);File.Replace(temp,file,null);written.Add(p.Key);}finally{if(File.Exists(temp))File.Delete(temp);}}}catch(Exception error){var failures=new List<string>();foreach(string name in written){try{if(Hash(File.ReadAllBytes(Path.Combine(root,name)))==Hash(pending[name]))File.WriteAllBytes(Path.Combine(root,name),docs[name].Original);else failures.Add(name+"（后续变化，未覆盖）");}catch{failures.Add(name);}}throw new Exception(error.Message+"\r\n备份："+backup+(failures.Count>0?"\r\n需人工恢复："+string.Join(", ",failures):"\r\n已撤销本次写入。"),error);}
   return "设置已保存，下次启动游戏读取。\r\n备份："+backup;
  }
 }
}
