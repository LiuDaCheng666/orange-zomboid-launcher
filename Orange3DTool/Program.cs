using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Management;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using ProgressBar = PZ3D.OrangeProgress;
using Button = PZ3D.GoldButton;
using CheckBox = PZ3D.GoldCheckBox;

namespace PZ3D {
 static class Program {
  public static bool PreviewMode;
  [STAThread] static int Main(string[] args) {
   LauncherRuntime.Initialize();
   if(args.Length==2&&(args[0]=="--buddy-crash"||args[0]=="--buddy-steam-crash"))return BuddyTakeoverTests.Main(args);
   ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
   if(args.Length==4&&args[0]=="--steam-service-worker")return SteamLauncherServices.Worker(args[1],args[2],args[3]);
   if(args.Length==3&&args[0]=="--playtime-watch")return PlaytimeTracker.Watch(args[1],args[2]);
   if(args.Length==3&&args[0]=="--session-watch")return LaunchProfiles.Watch(args[1],args[2]);
   if(args.Length==4&&args[0]=="--launcher-workshop-snapshot")return LauncherWorkshopRebuild.RunSnapshot(args[1],args[2],args[3]);
   if(args.Length==4&&args[0]=="--launcher-workshop-rebuild"){
    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true});
    Console.SetError(new StreamWriter(Console.OpenStandardError(),new UTF8Encoding(false)){AutoFlush=true});
    return LauncherWorkshopRebuild.Run(args[1],args[2],args[3]);
   }
   if(args.Length>=3&&args[0]=="--workshop-worker"){
    // WinForms workers have pipes, not a console. OutputEncoding calls the
    // Windows console API and can throw ERROR_INVALID_HANDLE before download.
    Console.SetOut(new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true});
    Console.SetError(new StreamWriter(Console.OpenStandardError(),new UTF8Encoding(false)){AutoFlush=true});
    return WorkshopWorker.Run(args[1],args[2],args.Contains("--force"));
   }
   if(args.Length>=2&&args[0]=="--check-workshop"){try{var engine=new Engine(args[1],Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Zomboid"),LauncherTestPaths.Root);var plan=WorkshopUpdate.Check(engine,InstallWorkflow.Requested(true),System.Threading.CancellationToken.None);Console.WriteLine("READ_ONLY_CHECK: installed="+plan.Paths.Count+" needed="+plan.Needed.Count+" nativeSteamLoaded="+System.Diagnostics.Process.GetCurrentProcess().Modules.Cast<System.Diagnostics.ProcessModule>().Any(m=>m.ModuleName.Equals("steam_api64.dll",StringComparison.OrdinalIgnoreCase)));return plan.Needed.Count==0?0:2;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
   if(args.Length>=2 && args[0]=="--subscribe-workshop"){try{var paths=SteamWorkshop.Fetch(args[1],InstallWorkflow.Requested(args.Contains("--models")),(v,t)=>Console.WriteLine(t),System.Threading.CancellationToken.None,420,true);foreach(var p in paths)Console.WriteLine("Subscribed: "+p.Key+"="+p.Value);return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
   if(args.Contains("--verify-bundle")){try{PortableTests.Bundle();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
   if(args.Length>=2 && args[0]=="--verify-cache"){try{FeatureTests.Real(args[1]);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
   if(args.Length>=2 && args[0]=="--prepare-dlss"){try{Console.WriteLine(ComponentCache.Prepare(args[1],(v,t)=>Console.WriteLine(v+"% "+t),System.Threading.CancellationToken.None));return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
   if(args.Length>=2 && args[0]=="--fetch-workshop"){try{var paths=SteamWorkshop.Fetch(args[1],InstallWorkflow.Requested(args.Contains("--models")),(v,t)=>Console.WriteLine(v+"% "+t),System.Threading.CancellationToken.None,420);foreach(var p in paths)Console.WriteLine(p.Key+"="+p.Value);return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
if(args.Contains("--self-test")) { try { BuddyTakeoverTests.Run(); SteamLaunchOptionsTests.Run(); BuddyLaunchArgumentsTests.Run(); LauncherNativeServicesTests.Run(); GameFavoritesTests.Run(); FullLauncherIntegrationTests.Run(); ModConfigurationsTests.Run(); SteamLauncherServicesTests.RunUnit(); ConcurrentSnapshotTests.Run(); JournalPrivacyTests.Run(); PhaseProgressTests.Run(); SteamUnavailableTests.Run(); PlaytimeDayTests.Run(); RoofRemovalTests.Run(); ServerRulesTests.Run(); AddonTests.Run(); AddonTests.Resolver(); ComponentEvidenceTests.Run(); SettingsChineseTests.Run(); GameVersionProbeTests.Run(); JavaApprovalsTests.Run(); SaveProfilesTests.Run(); if(ServerProfilesTests.Main()!=0)throw new Exception("服务器配置回归失败"); LaunchProfilesTests.Run(); PortableDeploymentTests.Run(); GameProcessGuardTests.Run(); HardwareProbeTests.Run(); ModernHostTests.Run(); ModernHostTests.QueuedCancel(); ModernControllerTests.Run(); LocalAddonTests.Run(); LauncherFeaturesTests.Run(); MemoryPolicyTests.Run(); LauncherMaintenanceTests.Run(); LauncherWorkshopRebuildTests.Run(); LauncherUiTests.Run(); ComponentBatchTests.Run(); ReticleSettingsTests.Run(); Test.Run(); Test.Extras(); Test.Audit(); FeatureTests.Run(); ChinesePatchTests.Run(); UiUpdateTests.Run(); PortableTests.Run(); WorkshopSubscriptionTests.Run(); SteamSubscriptionWaitTests.Run(); StatusTests.Run(); SettingsTests.Run(); ShaderTests.Run(); UpdateTests.Run(); DiscoveryTests.Run(); ShootClientTests.Run(); PresentationBridgeTests.Run(); return 0; } catch(Exception e) { Console.WriteLine(e); return 1; } }
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   if(args.Length>=2 && args[0]=="--preview"){PreviewMode=true;int index=0;if(args.Length>2)int.TryParse(args[2],out index);Func<string,string> option=key=>{int at=Array.IndexOf(args,key);return at>=0&&at+1<args.Length?args[at+1]:null;};using(var f=new MainForm()){f.PreviewLauncher(args[1],index,args.Contains("--busy"),option("--theme"),option("--size"),option("--page"));}return 0;}
   if(args.Length>=2&&args[0]=="--preview-help"){PreviewMode=true;using(var form=new MainForm())form.PreviewHelp(args[1]);return 0;}return ModernBootstrap.Run(args);
  }
 }
 static class Links {
  public const string Buddy="https://steamcommunity.com/sharedfiles/filedetails/?id=3619862853";
  public const string View="https://steamcommunity.com/sharedfiles/filedetails/?id=3809306528";
  public const string Models="https://steamcommunity.com/sharedfiles/filedetails/?id=3810302175";
  public const string Releases="https://github.com/zed-0xff/ZombieBuddy/releases/latest";
  public static void Open(string s) {if(string.Equals(s,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ThirdPartyNotices"),StringComparison.OrdinalIgnoreCase))s=BundledAssets.NoticePath;Process.Start(new ProcessStartInfo(s){UseShellExecute=true}); }
 }
 sealed class Engine {
  public string Game,User,Data;
  public string BuddySource,ViewSource,ModelsSource;
  public Action<int,string> Progress;
  public string LastBackup;
  public string PresetSource=BundledAssets.PresetPath;
  void Step(int value,string text){if(Progress!=null)Progress(value==100?100:-1,text);}
  public string ShaderDir {get{return Path.Combine(User,"viewpoint-shaderpacks");}}
  public static void ValidateShader(string zip){using(var archive=ZipFile.OpenRead(zip)){if(!archive.Entries.Any(e=>e.FullName.Replace('\\','/').StartsWith("shaders/")))throw new Exception("光影 ZIP 根目录必须包含 shaders 文件夹："+Path.GetFileName(zip));}}
  public bool TestMode;
  public static readonly string[] ModIds={"ZombieBuddy","Viewpoint","PZVoxelStudioViewpoint"};
  readonly Func<SteamLaunchOptionsSnapshot> steamOptions;
  public Engine(string game,string user,string data,bool test=false):this(game,user,data,test,null){}
  internal Engine(string game,string user,string data,bool test,Func<SteamLaunchOptionsSnapshot> options){Game=Path.GetFullPath(game);User=user;Data=data;TestMode=test;steamOptions=options??(test?(Func<SteamLaunchOptionsSnapshot>)SteamLaunchOptions.Unavailable:SteamLaunchOptions.Current);}
  internal SteamLaunchOptionsSnapshot ReadSteamOptions(){return steamOptions();}
  public string Config {get{return Path.Combine(Game,"ProjectZomboid64.json");}}
  public string Mods {get{return Path.Combine(User,"mods","default.txt");}}
  public string Workshop {get{return Path.GetFullPath(Path.Combine(Game,"..","..","workshop","content","108600"));}}
  public string BuddyLib {get{return Path.Combine(BuddySource??Path.Combine(Workshop,"3619862853"),"mods","ZombieBuddy","libs");}}
  public bool HasView {get{string source=ViewSource??Path.Combine(Workshop,"3809306528");return Directory.Exists(source) && Directory.EnumerateFiles(source,"mod.info",SearchOption.AllDirectories).Any(p=>Regex.IsMatch(File.ReadAllText(p),@"(?m)^id\s*=\s*Viewpoint\s*$"));}}
  public bool HasModels {get{return Directory.Exists(ModelsSource??Path.Combine(Workshop,"3810302175"));}}
  static JavaScriptSerializer Json=new JavaScriptSerializer();
  static bool Agent(string s){return BuddyLaunchArguments.Classify(s)==BuddyArgumentKind.Known;}
  Dictionary<string,object> ReadConfig(){return Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Config));}
  static string[] Args(Dictionary<string,object> c){return ((System.Collections.IEnumerable)c["vmArgs"]).Cast<object>().Select(o=>o.ToString()).ToArray();}
  public bool Enabled {get{try{return Args(ReadConfig()).Any(Agent) && File.Exists(Mods) && Regex.IsMatch(File.ReadAllText(Mods),@"(?m)^\s*mod\s*=\s*Viewpoint\s*,");}catch{return false;}}}
  public void Guard(){GuardCore(false);}
  internal void GuardPlayProfile(){GuardCore(true);}
  void GuardCore(bool ownProfileTransaction){
   if(!ownProfileTransaction&&PlayProfileTransaction.Pending(this))throw new Exception(PlayProfileTransaction.PendingMessage);
   BuddyTakeover.Guard(this);
   if(!File.Exists(Config))throw new Exception("所选文件夹不是游戏安装目录：找不到 ProjectZomboid64.json。");
   if(!TestMode && GameProcessGuard.IsRunning(Game))throw new Exception("请完全退出游戏后再切换（包括直接运行的游戏 Java 进程）。");
   foreach(string name in new[]{"ProjectZomboid64.bat","ProjectZomboid32.bat"}){string bat=Path.Combine(Game,name);LauncherMaintenance.NoReparse(bat);if(File.Exists(bat)){if(new FileInfo(bat).Length>2097152)throw new Exception("备用启动 BAT 过大，无法核实。");var cleanup=BuddyLaunchArguments.CleanBat(File.ReadAllText(bat));if(!cleanup.Safe)throw new Exception("备用启动 BAT 参数不明确，请人工检查 Buddy 启动语法。");if(cleanup.Changed)throw new Exception("备用启动 BAT 有旧 Buddy 参数，请先接管旧启动参数后继续。");}}
   var options=ReadSteamOptions();options.RequireReadable();if(options.HasField){var cleanup=BuddyLaunchArguments.CleanCommandLine(options.LaunchOptions);if(!cleanup.Safe)throw new Exception("当前游戏 Steam 启动选项不明确，请人工检查 Buddy 参数。");if(cleanup.Changed)throw new Exception("当前游戏 Steam 启动选项有旧 Buddy 参数，请先接管旧启动参数后继续。");}
  }
  public static string FindSteam(){using(var k=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){return k==null?null:(k.GetValue("SteamPath") as string);}}
  public static string DetectGame(){
   return GameDiscovery.Find(GameDiscovery.SteamRoots());
  }
  public string Switch(bool on,bool models){
   Step(0,"检查游戏进程与启动配置");
   Guard();if(!BuddyLaunchArguments.CleanJson(File.ReadAllText(Config)).Safe)throw new Exception("启动 JSON 含不明确的 Buddy 参数，未作修改，请人工检查。");if(!on)new PresentationBridge().Disable(this);if(!on)new DlssInstaller(Game,Data,TestMode).Disable();var cfg=ReadConfig();
   Step(10,"检查已下载模组与光影预设");
   if(on && (!HasView || !File.Exists(Path.Combine(BuddyLib,"ZombieBuddy.jar")) || !File.Exists(Path.Combine(BuddyLib,"zbNative.dll"))))throw new Exception("尚未下载完整依赖。请在“更新与下载”订阅 ZombieBuddy 和 Viewpoint，等待 Steam 完成下载后重试。");
   if(on && models && !HasModels)throw new Exception("可选 3D 物件包尚未下载，请先订阅或取消勾选。");
   string mod=File.Exists(Mods)?File.ReadAllText(Mods):"VERSION = 1,\r\n\r\nmods\r\n{\r\n}\r\n\r\nmaps\r\n{\r\n}\r\n";
   var block=Regex.Match(mod,@"\bmods\s*\{(?<body>[^}]*)\}");if(!block.Success)throw new Exception("客户端模组文件格式无法识别，未作修改。");
   string body=block.Groups["body"].Value;
   foreach(string id in ModIds.Concat(on?new string[0]:new[]{ShootClient.Id,ChinesePatch.Id}))body=Regex.Replace(body,@"(?m)^[ \t]*mod\s*=\s*"+Regex.Escape(id)+@"\s*,[^\r\n]*(?:\r?\n|$)","");
   if(on)body="\r\n    mod = ZombieBuddy,\r\n    mod = Viewpoint,\r\n"+(models?"    mod = PZVoxelStudioViewpoint,\r\n":"")+body.TrimStart('\r','\n');
   string updated=mod.Substring(0,block.Groups["body"].Index)+body+mod.Substring(block.Groups["body"].Index+block.Groups["body"].Length);
   cfg["vmArgs"]=Args(cfg).Where(a=>!Agent(a)).Concat(on?new[]{"-agentlib:zbNative"}:new string[0]).ToArray();
   // Remove loader arguments also from OS-specific launch branches.
   if(cfg.ContainsKey("windows")){var windows=cfg["windows"] as Dictionary<string,object>;if(windows!=null)foreach(var value in windows.Values){var branch=value as Dictionary<string,object>;if(branch!=null && branch.ContainsKey("vmArgs"))branch["vmArgs"]=Args(branch).Where(a=>!Agent(a)).ToArray();}}
   string preset=PresetSource;
   string[] packages=on&&Directory.Exists(preset)?Directory.GetFiles(preset,"*.zip"):new string[0];
   var shaderCopies=new List<string>();foreach(string zip in packages){ValidateShader(zip);string target=Path.Combine(ShaderDir,Path.GetFileName(zip));if(!File.Exists(target))shaderCopies.Add(zip);}
   string[] targets=new[]{Config,Mods,Path.Combine(Game,"ZombieBuddy.jar"),Path.Combine(Game,"zbNative.dll"),Path.Combine(Game,"ZombieBuddy.jar.new")}.Concat(shaderCopies.Select(p=>Path.Combine(ShaderDir,Path.GetFileName(p)))).ToArray();
   string backup=Path.Combine(Data,"backups",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(backup);
   LastBackup=backup;Step(25,"备份原始文件，生成人工恢复说明");
   var originals=new Dictionary<string,byte[]>();for(int i=0;i<targets.Length;i++){string p=targets[i];originals[p]=File.Exists(p)?File.ReadAllBytes(p):null;if(originals[p]!=null)File.WriteAllBytes(Path.Combine(backup,i+"-"+Path.GetFileName(p)),originals[p]);}
   File.WriteAllText(Path.Combine(backup,"manifest.json"),Json.Serialize(targets));
   var changes=targets.Select((p,i)=>new Dictionary<string,object>{{"path",p},{"backup",originals[p]==null?null:Path.Combine(backup,i+"-"+Path.GetFileName(p))},{"existedBefore",originals[p]!=null},{"operation",i<2?"修改配置":i==4?"清理待更新加载器":on?"安装/更新文件":"停用加载器"},{"beforeSHA256",originals[p]==null?null:Hash(originals[p])}}).ToArray();
   SaveReport(backup,changes,"准备操作");
   File.WriteAllText(Path.Combine(backup,"人工关闭3D.txt"),ManualGuide()+"\r\n\r\n此次文件与备份对应：\r\n"+string.Join("\r\n",changes.Select(c=>c["path"]+"\r\n  原文件备份："+(c["backup"]??"原先不存在；撤销安装时可移走此文件"))),new UTF8Encoding(true));
   try {
    Step(40,on?"复制 ZombieBuddy 加载器":"停用 ZombieBuddy 加载器");
    if(on){File.Copy(Path.Combine(BuddyLib,"ZombieBuddy.jar"),targets[2],true);File.Copy(Path.Combine(BuddyLib,"zbNative.dll"),targets[3],true);}
    else {foreach(string p in targets.Skip(2).Take(3))if(File.Exists(p))File.Delete(p);}
    if(on&&File.Exists(targets[4]))File.Delete(targets[4]);
    Step(60,"写入启动参数，保留内存和其他参数");Atomic(Config,Json.Serialize(cfg));
    Step(72,"更新客户端默认模组列表");Atomic(Mods,updated);
    Step(82,on?"创建 Viewpoint 光影目录并安装随附预设":"保留光影包，方便下次使用");
    if(on){Directory.CreateDirectory(ShaderDir);foreach(string zip in shaderCopies)File.Copy(zip,Path.Combine(ShaderDir,Path.GetFileName(zip)));}
    Step(94,"校验启用状态与修改记录");
    if(Enabled!=on)throw new Exception("切换后校验失败。");
    foreach(var c in changes){string p=(string)c["path"];c["afterSHA256"]=File.Exists(p)?Hash(File.ReadAllBytes(p)):null;}
    SaveReport(backup,changes,"已完成");Step(100,on?"3D 与 Buddy 已启用，下次启动生效":"3D 与 Buddy 已关闭");
   }catch(Exception failure){Step(0,"操作失败，正在恢复本阶段原文件");var errors=new List<string>();foreach(var entry in originals){try{if(entry.Value==null){if(File.Exists(entry.Key))File.Delete(entry.Key);}else{Directory.CreateDirectory(Path.GetDirectoryName(entry.Key));File.WriteAllBytes(entry.Key,entry.Value);}}catch(Exception err){errors.Add(entry.Key+": "+err.Message);}}try{SaveReport(backup,changes,errors.Count==0?"失败，已回滚":"失败，回滚不完整："+string.Join("; ",errors));}catch{}throw new Exception(failure.Message+"\r\n"+(errors.Count==0?"已恢复本阶段原文件。":"部分文件未恢复："+string.Join("; ",errors))+(!on?"\r\n此前已停用的开镜 / DLSS / 汉化组件不在本阶段回滚范围，需核对状态后重新安装。":"")+"\r\n人工恢复说明："+Path.Combine(backup,"人工关闭3D.txt"),failure);}
   return (on?"已写入 3D 和 ZombieBuddy 启动配置（不代表游戏已成功加载）。":"已关闭 3D 和 ZombieBuddy，加载器已移入备份。")+"\r\n备份："+backup+"\r\n下次正常启动游戏生效；进入服务器后仍需检查 Viewpoint 加载日志；已有存档的独立模组列表需在游戏内调整。";
  }
  static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create()){return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}}
  void SaveReport(string backup,Dictionary<string,object>[] changes,string state){File.WriteAllText(Path.Combine(backup,"changes.json"),Json.Serialize(new Dictionary<string,object>{{"state",state},{"game",Game},{"time",DateTime.Now.ToString("o")},{"files",changes}}));}
  public string ManualGuide(){return "人工关闭 3D / ZombieBuddy（先完全退出游戏）\r\n\r\n1. 打开 "+Config+"\r\n   在 vmArgs 及 windows 下各版本 vmArgs 中移除 -agentlib:zbNative 或 -javaagent:ZombieBuddy.jar 对应项，保持 JSON 逗号正确。不要改内存参数。\r\n2. 打开 "+Mods+"\r\n   仅移除 mod = ZombieBuddy, / mod = Viewpoint, / mod = PZVoxelStudioViewpoint, 三项，保留其他模组。\r\n3. 从 "+Game+" 移走 ZombieBuddy.jar、zbNative.dll、ZombieBuddy.jar.new（如有），建议移动到备份文件夹而非直接删除。\r\n4. 检查 Steam 游戏属性的启动选项，以及 ProjectZomboid64.bat 中的 _JAVA_OPTIONS，移除独立的 Buddy agent 参数。维护页“扫描旧启动参数”可只读检查，确认范围后接管已识别的旧 Buddy 参数；涉及当前账号 App108600 Steam 启动项时需另行确认一次性正常退出并重新打开 Steam。未知目录、未知选项和复杂 BAT 保留并人工处理；中断时用明确的恢复操作，仅恢复本轮自有改动。不要分享私有备份或整份覆盖更新后的 VDF。\r\n5. 已有存档若有独立模组列表，在游戏内存档模组设置中停用以上三项；服务器要求的模组请与管理员确认。\r\n6. 光影目录 "+ShaderDir+" 保留即可，关闭 3D 后不会生效。重启游戏。\r\n\r\n完整恢复某次备份会覆盖后来的设置，请优先按以上步骤只移除 3D 项。备份编号与原路径见 changes.json / manifest.json。";}
  static void Atomic(string p,string t){Directory.CreateDirectory(Path.GetDirectoryName(p));string temp=p+".pz3d-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,t,new UTF8Encoding(false));if(File.Exists(p))File.Replace(temp,p,null);else File.Move(temp,p);}finally{if(File.Exists(temp))File.Delete(temp);}}
  public string Import(string zip){
   string dir=ShaderDir;ValidateShader(zip);Directory.CreateDirectory(dir);
   string target=Path.Combine(dir,Path.GetFileName(zip));if(File.Exists(target))throw new Exception("已有同名光影包，请先重命名或自行移走旧包。");File.Copy(zip,target);return "已导入："+target+"\r\n进入游戏按 Delete 选择；Iris 兼容属于实验功能。";
  }
 }
 sealed partial class MainForm:Form {
  TextBox path,log,guide;Label status,gpu,advice,stage;CheckBox models;FlowLayoutPanel buttons;string data,lastBackup;bool busy;ProgressBar progress;DataGridView files;ListBox shaderList;
  Color bg=Color.FromArgb(18,23,31),card=Color.FromArgb(29,37,48),fg=Color.FromArgb(227,235,242);
  public MainForm(string preferenceDirectory=null){
   Text="PZ 3D Manager · Viewpoint";Size=new Size(1280,940);MinimumSize=new Size(1000,760);StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=fg;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.None;
   data=preferenceDirectory??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PZ3DManager");Directory.CreateDirectory(data);
   var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=7};Controls.Add(root);
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,68));root.RowStyles.Add(new RowStyle(SizeType.Absolute,56));root.RowStyles.Add(new RowStyle(SizeType.Absolute,90));root.RowStyles.Add(new RowStyle(SizeType.Absolute,94));root.RowStyles.Add(new RowStyle(SizeType.Absolute,110));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,95));
   var heading=new Label{Text="PZ 3D MANAGER\nViewpoint + ZombieBuddy · 随时切换",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,17,FontStyle.Bold)};root.Controls.Add(heading);
   var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130));
   path=new TextBox{Dock=DockStyle.Top,Text=Engine.DetectGame(),BackColor=card,ForeColor=fg,BorderStyle=BorderStyle.FixedSingle};string saved=Path.Combine(data,"game-path.txt");if(File.Exists(saved)&&GameDiscovery.IsGameFolder(File.ReadAllText(saved).Trim()))path.Text=File.ReadAllText(saved).Trim();row.Controls.Add(path,0,0);row.Controls.Add(Button("选择游戏目录",ChooseGameDirectory),1,0);root.Controls.Add(row);
   var hw=new Panel{Dock=DockStyle.Fill,BackColor=card,Padding=new Padding(12)};gpu=new Label{Dock=DockStyle.Top,Height=30,Text="正在检测显卡和显存…"};advice=new Label{Dock=DockStyle.Fill,Text="建议仅供参考，最终取决于场景、模组和光影兼容性。"};hw.Controls.Add(advice);hw.Controls.Add(gpu);root.Controls.Add(hw);
   root.Controls.Add(CreateDashboard());
   var operationPanel=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};operationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,48));operationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,28));operationPanel.RowStyles.Add(new RowStyle(SizeType.Absolute,24));
   var ops=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};buttons=ops;var enable=Button("安装 / 启用 3D",()=>Run(InstallSelected3D));enable.BackColor=Color.FromArgb(28,97,109);ops.Controls.Add(enable);ops.Controls.Add(Button("关闭 3D + Buddy",()=>Run(()=>GetEngine().Switch(false,false))));ops.Controls.Add(Button("刷新状态",RefreshState));models=new CheckBox{Text="额外 3D 物件包（默认安装）",Checked=true,AutoSize=true,Margin=new Padding(16,3,0,0)};ops.Controls.Add(models);operationPanel.Controls.Add(ops);stage=new Label{Text="准备就绪 · 切换前请退出游戏",Dock=DockStyle.Fill};operationPanel.Controls.Add(stage);progress=new ProgressBar{Dock=DockStyle.Fill,Maximum=100,Margin=new Padding(0,2,0,5)};operationPanel.Controls.Add(progress);root.Controls.Add(operationPanel);
   var tabs=new TabControl{Dock=DockStyle.Fill};root.Controls.Add(tabs);
   var changesTab=new TabPage("安装与文件改动"){BackColor=bg,ForeColor=fg};tabs.TabPages.Add(changesTab);var changeLayout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),RowCount=2,ColumnCount=1};changeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));changeLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));changesTab.Controls.Add(changeLayout);status=new Label{Dock=DockStyle.Fill,Text="正在读取安装状态…"};changeLayout.Controls.Add(status);
   files=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=card,BorderStyle=BorderStyle.None,EnableHeadersVisualStyles=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect};files.DefaultCellStyle.BackColor=card;files.DefaultCellStyle.ForeColor=fg;files.DefaultCellStyle.WrapMode=DataGridViewTriState.True;files.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(42,55,69);files.ColumnHeadersDefaultCellStyle.ForeColor=fg;files.ColumnHeadersHeight=38;files.Columns.Add("action","操作 / 内容");files.Columns.Add("file","目标文件（完整路径）");files.Columns.Add("backup","原文件备份 / 撤销方式");files.Columns[0].Width=180;files.Columns[1].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;files.Columns[1].FillWeight=110;files.Columns[2].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;changeLayout.Controls.Add(files);
   var updates=Page(tabs,"更新与下载");AddText(updates,"首次使用需在 Steam 订阅并等待下载。Steam 更新工坊内容，软件重新启用时部署本地最新加载器。GitHub 更新检查不会自动执行安装器。");AddButton(updates,"订阅 / 更新 ZombieBuddy",()=>Links.Open(Links.Buddy));AddButton(updates,"订阅 / 更新 Viewpoint",()=>Links.Open(Links.View));AddButton(updates,"可选：3D 物件模型包",()=>Links.Open(Links.Models));AddButton(updates,"联网检查 ZombieBuddy 最新发行版",CheckUpdates);AddButton(updates,"打开官方 GitHub Releases",()=>Links.Open(Links.Releases));AddButton(updates,"重新部署 Steam 已下载版本",()=>Run(()=>GetEngine().Switch(true,operationModels)));
   var shaders=Page(tabs,"光影包");AddText(shaders,"实际游戏读取目录（不是软件自建的光影库）：");shaders.Controls.Add(new TextBox{ReadOnly=true,Text=Path.Combine(UserDir(),"viewpoint-shaderpacks"),Width=900,BackColor=card,ForeColor=fg});AddText(shaders,"直接把光影 ZIP 放入上面的文件夹，或用“导入”按钮。核心包不附带第三方预设；导入的已有包不会被覆盖，关闭 3D 不删除光影包。");var shaderButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false};shaderButtons.Controls.Add(Button("导入光影 ZIP",()=>{using(var d=new OpenFileDialog{Filter="光影 ZIP|*.zip"}){if(d.ShowDialog()==DialogResult.OK){string selected=d.FileName;Run(()=>GetEngine().Import(selected));}}}));shaderButtons.Controls.Add(Button("打开实际光影目录",()=>{string d=Path.Combine(UserDir(),"viewpoint-shaderpacks");Directory.CreateDirectory(d);Links.Open(d);RefreshShaders();}));shaderButtons.Controls.Add(Button("刷新光影列表",RefreshShaders));shaders.Controls.Add(shaderButtons);shaderList=new ListBox{Width=900,Height=130,BackColor=card,ForeColor=fg,BorderStyle=BorderStyle.FixedSingle};shaders.Controls.Add(shaderList);AddText(shaders,"核心包不预装第三方光影。请从原作者渠道获取并自行导入，进入游戏按 Delete 选择。Minecraft Iris 光影兼容为实验功能，不保证每包能编译；保留玩家已有选择。");
   var info=Page(tabs,"人工关闭与备份");AddText(info,"即使软件无法启动，也可按下面说明人工关闭。每次切换都会保存独立的 changes.json 和 人工关闭3D.txt；表格里的备份是操作前原文件。");var recoveryButtons=new FlowLayoutPanel{AutoSize=true,WrapContents=false};recoveryButtons.Controls.Add(Button("打开本次备份",()=>{if(lastBackup==null)throw new Exception("还没有此游戏的操作备份。");Links.Open(lastBackup);}));recoveryButtons.Controls.Add(Button("打开全部备份",()=>{Directory.CreateDirectory(Path.Combine(data,"backups"));Links.Open(Path.Combine(data,"backups"));}));recoveryButtons.Controls.Add(Button("复制人工关闭说明",()=>Clipboard.SetText(guide.Text)));info.Controls.Add(recoveryButtons);guide=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Width=1080,Height=280,BackColor=card,ForeColor=fg};info.Controls.Add(guide);AddText(info,"关闭 3D 会同时关闭 Buddy；其他依赖 Buddy 的 Java 模组也受影响。本工具不修改存档、服务器设置和 Steam 启动选项。");
   var dlss=Page(tabs,"DLSS 5 实验方案");AddText(dlss,"状态：未部署 · 与 Viewpoint 联用尚未验证\nRTX 50 系列是硬件条件，不等于驱动可以给所有游戏打开 DLSS 5。第三方 DLSS5-Feeder 提供 OpenGL 实验路径，但还需要 ReShade、运动估计与神经渲染组件。此软件不自动运行它的安装脚本，也不修改驱动或安全软件设置。");AddText(dlss,"本包准备了关闭态配置模板 enabled=0，放在软件的 DLSS5-Experimental 文件夹，未写入游戏，也未安装任何注入 DLL。模板不是可运行插件，复制它不能启用 DLSS 5；完整适配与实际日志验证后才能增加启用开关。优先单机测试，不默认用于服务器。");AddButton(dlss,"打开实验模板目录（未部署）",()=>Links.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DLSS5-Experimental")));AddButton(dlss,"查看第三方方案原始文档",()=>Links.Open("https://github.com/jlrouzies-fr/DLSS5-Feeder"));AddButton(dlss,"查看 NVIDIA DLSS 5 官方说明",()=>Links.Open("https://research.nvidia.com/labs/adlr/DLSS5/"));
   log=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,BackColor=card,ForeColor=fg,BorderStyle=BorderStyle.None};root.Controls.Add(log);files.CellClick+=(s,e)=>{if(e.RowIndex>=0){var cells=files.Rows[e.RowIndex].Cells;Write("目标："+cells[1].Value+"\r\n备份 / 撤销："+cells[2].Value);}};files.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0)Clipboard.SetText(Convert.ToString(files.Rows[e.RowIndex].Cells[e.ColumnIndex].Value));};Shown+=async(s,e)=>{RefreshState();await DetectGpu();};
   SetupOrange(tabs,heading,enable);SetupLauncherFeatures(tabs);SetupLauncherShell(tabs);float scale;using(var graphics=CreateGraphics())scale=graphics.DpiX/96f;Scale(new SizeF(scale,scale));var area=Screen.PrimaryScreen.WorkingArea;MinimumSize=new Size(Math.Min((int)(1000*scale),area.Width-40),Math.Min((int)(720*scale),area.Height-60));Size=new Size(Math.Min((int)(1100*scale),area.Width-40),Math.Min((int)(760*scale),area.Height-60));
  }
  string UserDir(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Zomboid");}
  Engine operationEngine; bool operationModels;
  Engine GetEngine(){if(operationEngine!=null)return operationEngine;if(!GameDiscovery.IsGameFolder(path.Text.Trim()))throw new Exception("请选择有效游戏安装目录：需包含 ProjectZomboid64.exe 与 ProjectZomboid64.json。");var engine=new Engine(path.Text.Trim(),UserDir(),data);ApplyWorkshopPaths(engine);return engine;}
  Button Button(string text,Action click){var b=new Button{Text=text,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(140,40),Padding=new Padding(12,4,12,4),FlatStyle=FlatStyle.Flat,BackColor=card,ForeColor=fg,Margin=new Padding(3)};b.FlatAppearance.BorderColor=Color.FromArgb(64,85,102);b.Click+=(s,e)=>{try{if(!busy)click();}catch(Exception ex){ShowError(ex);}};return b;}
  FlowLayoutPanel Page(TabControl tabs,string name){var t=new TabPage(name){BackColor=bg,ForeColor=fg};tabs.TabPages.Add(t);var f=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(10)};t.Controls.Add(f);f.Resize+=(s,e)=>{int width=Math.Max(300,f.ClientSize.Width-60);foreach(Control c in f.Controls){if(c is Label)c.MaximumSize=new Size(width,0);if(c is TextBox||c is ListBox)c.Width=width;}};return f;}
  void AddText(FlowLayoutPanel p,string s){p.Controls.Add(new Label{Text=s,AutoSize=true,MaximumSize=new Size(1050,0),Margin=new Padding(8)});}
  void AddButton(FlowLayoutPanel p,string s,Action a){p.Controls.Add(Button(s,a));}
  void Write(string t){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+t.Replace("\r\n","\n").Replace("\n","\r\n")+"\r\n");}
  void ShowError(Exception e){Write(e.Message);MessageBox.Show(this,e.Message,"未完成操作",MessageBoxButtons.OK,MessageBoxIcon.Information);}
  async void Run(Func<string> action,string initial="正在准备操作 · 请等待",Action completed=null){if(busy)return;if(downloadCancel.IsCancellationRequested){downloadCancel.Dispose();downloadCancel=new System.Threading.CancellationTokenSource();}bool finished=false,takeover=false,selectedModels=models.Checked;busy=true;buttons.Enabled=false;path.ReadOnly=true;progress.Value=0;StartVisibleTask(initial);try{Write(initial);operationEngine=GetEngine();operationModels=models.Checked;operationEngine.Progress=(v,t)=>BeginInvoke(new Action(()=>{progress.Value=v;stage.Text=v+"% · "+t;UpdateTask(t,true);}));string result=await Task.Run(action);Write(result);bool partial=result.Contains("[失败]");stage.Text=partial?"部分完成 · 各组件结果见下方记录":"操作完成";FinishVisibleTask(!partial,partial?"失败组件不影响其他组件，详见记录":"详情见下方记录");RefreshState();finished=true;}catch(Exception e){stage.Text="操作未完成 · 请查看记录与人工恢复页面";FinishVisibleTask(false,e is OperationCanceledException?"已取消；请核对当前文件状态":e.Message.Split('\r','\n')[0]);BuddyTakeoverPreview conflict=null;if(!(e is OperationCanceledException)&&!Program.PreviewMode&&operationEngine!=null)try{conflict=LegacyInstallConflict(operationEngine,action.Method.Name);}catch{}if(conflict!=null){Write(e.Message);takeover=MessageBox.Show(this,e.Message+"\n\n已只读识别旧 Buddy 参数："+string.Join(" · ",conflict.locations)+"\n是否打开“接管旧启动参数”确认流程？尚未退出 Steam 或修改文件。","接管旧启动参数",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)==DialogResult.OK;}else ShowError(e);}finally{if(operationEngine!=null&&operationEngine.LastBackup!=null)lastBackup=operationEngine.LastBackup;operationEngine=null;LoadReport();buttons.Enabled=true;path.ReadOnly=false;busy=false;UpdateDashboard();}if(takeover)LegacyBuddyTakeover(false,()=>{operationModels=selectedModels;return action();});else if(finished&&completed!=null)completed();}
  public void Preview(string output,int tab,bool disabled=false){ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-2000,-2000);Show();var tabs=Controls[0].Controls.OfType<TabControl>().First();tabs.SelectedIndex=tab;if(disabled){buttons.Enabled=false;path.ReadOnly=true;StartVisibleTask("预览：正在连接 Steam · 等待额外 3D 物件包下载数据");}DateTime end=DateTime.Now.AddSeconds(6);while(DateTime.Now<end){Application.DoEvents();System.Threading.Thread.Sleep(20);}using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(output,System.Drawing.Imaging.ImageFormat.Png);}Close();}
  void RefreshState(){try{var e=GetEngine();status.Text="正常启动配置："+(e.Enabled?"3D 配置已写入（加载待验证）":"3D 未配置 / 配置不完整")+"    ZombieBuddy："+(File.Exists(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"))?"文件已下载":"待下载")+"    Viewpoint："+(e.HasView?"文件已下载":"待下载")+"\n文件存在不代表已订阅或游戏已加载；进入服务器后以游戏日志为准。下表显示最近一次操作。";guide.Text=e.ManualGuide();File.WriteAllText(Path.Combine(data,"game-path.txt"),e.Game);lastBackup=null;string backups=Path.Combine(data,"backups");if(Directory.Exists(backups))foreach(string d in Directory.GetDirectories(backups).OrderByDescending(p=>p)){try{var j=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(d,"changes.json")));if(string.Equals((string)j["game"],e.Game,StringComparison.OrdinalIgnoreCase)){lastBackup=d;break;}}catch{}}LoadReport();RefreshShaders();}catch(Exception ex){status.Text=ex.Message;}}
  void RefreshShaders(){RefreshShaderSelection();}
  void LoadReport(){files.Rows.Clear();if(lastBackup!=null){try{var j=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(lastBackup,"changes.json")));foreach(var item in (System.Collections.IEnumerable)j["files"]){var c=(Dictionary<string,object>)item;files.Rows.Add(c["operation"]+" · "+j["state"],c["path"],c["backup"]??"操作前不存在；人工撤销时移走新增文件");}string manual=Path.Combine(lastBackup,"人工关闭3D.txt");if(File.Exists(manual))guide.Text=File.ReadAllText(manual);return;}catch(Exception e){Write("读取修改清单失败："+e.Message);}}
   var engine=GetEngine();files.Rows.Add("修改启动参数（不改内存）",engine.Config,"操作前自动备份；移除 Buddy agent 参数即可人工关闭");files.Rows.Add("调整 3 项模组，保留其他项",engine.Mods,"操作前自动备份；移除 ZombieBuddy / Viewpoint / 物件包项");foreach(string name in new[]{"ZombieBuddy.jar","zbNative.dll","ZombieBuddy.jar.new"})files.Rows.Add("启用时部署 / 关闭时移走",Path.Combine(engine.Game,name),"操作前保存原文件；关闭失败时可手工移出游戏目录");files.Rows.Add("安装随附光影，关闭时保留",engine.ShaderDir,"只添加缺失的预设 ZIP，不覆盖已有包");}
  async Task DetectGpu(){var result=await Task.Run(()=>{
   try{var psi=new ProcessStartInfo("nvidia-smi","--query-gpu=name,memory.total,driver_version --format=csv,noheader,nounits"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};using(var p=Process.Start(psi)){if(!p.WaitForExit(5000)){p.Kill();throw new Exception();}string s=p.StandardOutput.ReadToEnd().Trim();if(p.ExitCode==0&&s.Length>0){string[] first=s.Split('\n')[0].Split(',');double mib;double.TryParse(first[1].Trim(),out mib);return new Tuple<string,double>(s,mib);}}}catch{}
   try{using(var q=new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController")){var names=q.Get().Cast<ManagementObject>().Select(o=>Convert.ToString(o["Name"]));return new Tuple<string,double>(string.Join(" / ",names)+" · 专用显存未可靠检测",0);}}catch{return new Tuple<string,double>("显卡检测不可用",0);}
  });string[] details=result.Item1.Split('\n')[0].Split(',');gpu.Text=result.Item2>0&&details.Length>=3?details[0].Trim()+"  ·  专用显存 "+(result.Item2/1024).ToString("F1")+" GB  ·  驱动 "+details[2].Trim():result.Item1.Replace("\n"," / ");double gb=result.Item2/1024;advice.Text=gb==0?"无法可靠读取显存；先用 Vanilla 预设，游戏内按 Delete 调整。":gb>=12?"可从 Default 开始，再测试 Vivid 或 Iris 光影；高显存不保证模组场景的帧率。":gb>=6?"先用 Default，体积光、阴影按实际帧率逐项调整。":"建议从 Vanilla 开始；较重光影可能超出显存预算。";}
  async void CheckUpdates(){if(busy)return;busy=true;try{Write("正在查询官方最新发行版…");string info=await Task.Run(()=>{using(var wc=new WebClient()){wc.Headers["User-Agent"]="PZ3DManager/1.0";wc.Encoding=Encoding.UTF8;var j=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(wc.DownloadString("https://api.github.com/repos/zed-0xff/ZombieBuddy/releases/latest"));return "ZombieBuddy 官方最新发行版："+j["tag_name"]+"\r\n"+j["html_url"]+"\r\nViewpoint 更新由 Steam 工坊负责，请使用上方官方订阅入口。";}});Write(info);}catch(Exception e){Write("更新查询失败（网络或 GitHub 限流），未改动安装。"+e.Message);MessageBox.Show(this,"查询失败。可使用官方 Releases 按钮查看最新版本；Steam 工坊仍负责实际模组更新。","更新检查");}finally{busy=false;}}
 }
 static class Test {
  public static void Audit(){
   string root=Path.Combine(LauncherTestPaths.Root,"pz3d-audit-"+Guid.NewGuid().ToString("N"));string game=Path.Combine(root,"steamapps","common","ProjectZomboid");Directory.CreateDirectory(game);var e=new Engine(game,Path.Combine(root,"User"),Path.Combine(root,"Data"),true);e.PresetSource=Path.Combine(root,"Presets");Directory.CreateDirectory(e.PresetSource);
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx28672m\"]}");Directory.CreateDirectory(e.BuddyLib);File.WriteAllText(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"),"fixture");File.WriteAllText(Path.Combine(e.BuddyLib,"zbNative.dll"),"fixture");string vp=Path.Combine(e.Workshop,"3809306528","mods","Viewpoint");Directory.CreateDirectory(vp);File.WriteAllText(Path.Combine(vp,"mod.info"),"id=Viewpoint\n");
   string preset=Path.Combine(e.PresetSource,"Preset.zip");using(var a=ZipFile.Open(preset,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("shaders/final.fsh").Open()))w.Write("fixture");}
   var steps=new List<int>();e.Progress=(v,t)=>steps.Add(v);e.Switch(true,false);Assert(steps.Count==9&&steps.Take(8).All(v=>v==-1)&&steps.Last()==100,"phase labels have unknown progress until actual installation completes");Assert(File.Exists(Path.Combine(e.ShaderDir,"Preset.zip")),"preset deployed into actual Viewpoint directory");
   var report=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(e.LastBackup,"changes.json")));var entries=((System.Collections.IEnumerable)report["files"]).Cast<Dictionary<string,object>>().ToArray();Assert(entries.Length==6&&(string)report["state"]=="已完成","audit includes shader and completed state");Assert(entries[0]["backup"]!=null&&File.Exists((string)entries[0]["backup"])&&((string)entries[0]["beforeSHA256"]).Length==64&&((string)entries[0]["afterSHA256"]).Length==64,"audit backup mapping and SHA256 evidence");Assert(File.ReadAllText(Path.Combine(e.LastBackup,"人工关闭3D.txt")).Contains(e.Config),"manual guide contains exact game paths");
   byte[] original=File.ReadAllBytes(Path.Combine(e.ShaderDir,"Preset.zip"));e.Switch(false,false);Assert(File.ReadAllBytes(Path.Combine(e.ShaderDir,"Preset.zip")).SequenceEqual(original),"disable retains shader packages");e.Switch(true,false);Assert(File.ReadAllBytes(Path.Combine(e.ShaderDir,"Preset.zip")).SequenceEqual(original),"reinstall preserves existing preset");
   string invalid=Path.Combine(root,"invalid.zip");using(var a=ZipFile.Open(invalid,ZipArchiveMode.Create))a.CreateEntry("nested/shaders/final.fsh");bool rejected=false;try{e.Import(invalid);}catch{rejected=true;}Assert(rejected&&!File.Exists(Path.Combine(e.ShaderDir,"invalid.zip")),"invalid shader layout rejected without copying");
   string failureGame=Path.Combine(root,"FailureGame");Directory.CreateDirectory(failureGame);var failureEngine=new Engine(failureGame,Path.Combine(root,"FailureUser"),Path.Combine(root,"FailureData"),true);string before="{\"vmArgs\":[\"-Xmx28672m\",\"-agentlib:zbNative\"]}";File.WriteAllText(failureEngine.Config,before);File.WriteAllText(Path.Combine(failureGame,"ZombieBuddy.jar"),"restore-me");Directory.CreateDirectory(failureEngine.Mods);bool failed=false;try{failureEngine.Switch(false,false);}catch{failed=true;}Assert(failed&&File.ReadAllText(failureEngine.Config)==before&&File.ReadAllText(Path.Combine(failureGame,"ZombieBuddy.jar"))=="restore-me","write failure rolls back config and loader");Assert(File.ReadAllText(Path.Combine(failureEngine.LastBackup,"changes.json")).Contains("失败，已回滚")&&File.Exists(Path.Combine(failureEngine.LastBackup,"人工关闭3D.txt")),"failed operation retains report and manual guide");Console.WriteLine("AUDIT TESTS PASSED; fixture retained at "+root);
  }
  public static void Extras(){
   string root=Path.Combine(LauncherTestPaths.Root,"pz3d-extra-"+Guid.NewGuid().ToString("N"));
   string game=Path.Combine(root,"steamapps","common","ProjectZomboid"),user=Path.Combine(root,"User");Directory.CreateDirectory(game);
   var e=new Engine(game,user,Path.Combine(root,"Data"),true);
   File.WriteAllText(e.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Xmx12g\"],\"windows\":{\"10\":{\"vmArgs\":[\"-javaagent:ZombieBuddy.jar\",\"-XX:+UseZGC\"]}}}");
   e.Switch(false,false);string disabled=File.ReadAllText(e.Config);Assert(!disabled.Contains("zbNative")&&!disabled.Contains("ZombieBuddy.jar")&&disabled.Contains("UseZGC"),"disable OS-specific agent too");
   e.Switch(false,false);Assert(File.ReadAllText(e.Config)==disabled,"idempotent disable");
   string bat=Path.Combine(game,"ProjectZomboid64.bat");File.WriteAllText(bat,"SET _JAVA_OPTIONS=-agentlib:zbNative");bool refused=false;try{e.Switch(false,false);}catch{refused=true;}Assert(refused,"detect independent BAT loader");
   string zip=Path.Combine(root,"example.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("shaders/final.fsh").Open())){w.Write("void main() {}");}}
   e.Import(zip);Assert(File.Exists(Path.Combine(user,"viewpoint-shaderpacks","example.zip")),"shader ZIP import");bool duplicate=false;try{e.Import(zip);}catch{duplicate=true;}Assert(duplicate,"shader import does not overwrite");
   Console.WriteLine("EXTRA TESTS PASSED");
  }
  static void Assert(bool value,string name){if(!value)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);}
  public static void Run(){string root=Path.Combine(LauncherTestPaths.Root,"pz3d-test-"+Guid.NewGuid().ToString("N"));string game=Path.Combine(root,"steamapps","common","ProjectZomboid"),user=Path.Combine(root,"User"),data=Path.Combine(root,"Data");Directory.CreateDirectory(game);Directory.CreateDirectory(Path.Combine(user,"mods"));var e=new Engine(game,user,data,true);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx28672m\",\"-Dexample=true\"],\"windows\":{\"10\":{\"vmArgs\":[\"-XX:+UseZGC\"]}},\"custom\":42}");File.WriteAllText(e.Mods,"VERSION = 1,\nmods\n{\n    mod = OtherMod,\n}\nmaps\n{\n}\n");string before=File.ReadAllText(e.Config);bool rejected=false;try{e.Switch(true,false);}catch{rejected=true;}Assert(rejected&&File.ReadAllText(e.Config)==before,"missing dependencies do not mutate");Directory.CreateDirectory(e.BuddyLib);File.WriteAllText(Path.Combine(e.BuddyLib,"ZombieBuddy.jar"),"fake-jar");File.WriteAllText(Path.Combine(e.BuddyLib,"zbNative.dll"),"fake-dll");string vp=Path.Combine(e.Workshop,"3809306528","mods","Viewpoint","42");Directory.CreateDirectory(vp);File.WriteAllText(Path.Combine(vp,"mod.info"),"id=Viewpoint\n");e.Switch(true,false);Assert(e.Enabled,"enable");e.Switch(true,false);Assert(Regex.Matches(File.ReadAllText(e.Mods),"mod = Viewpoint,").Count==1,"idempotent enable");e.Switch(false,false);Assert(!e.Enabled && !File.Exists(Path.Combine(game,"ZombieBuddy.jar"))&&!File.Exists(Path.Combine(game,"zbNative.dll")),"disable both and stash loader");Assert(File.ReadAllText(e.Mods).Contains("OtherMod")&&!File.ReadAllText(e.Mods).Contains("Viewpoint"),"preserve other mods");Assert(File.ReadAllText(e.Config).Contains("-Xmx28672m")&&File.ReadAllText(e.Config).Contains("-XX:+UseZGC")&&File.ReadAllText(e.Config).Contains("custom"),"preserve memory GC and extra settings");Assert(Directory.GetDirectories(Path.Combine(data,"backups")).Length==3,"backups created");Console.WriteLine("ALL PASSED; fixture retained at "+root);}
 }
}
