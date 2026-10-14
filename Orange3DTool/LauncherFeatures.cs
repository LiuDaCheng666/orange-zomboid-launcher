using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  internal static BuddyTakeoverResult ConfirmBuddyTakeover(Engine engine,bool recover,Func<string,string,bool> confirm,Action<int,string> progress,CancellationToken token,BuddyTakeoverEnvironment environment=null){
   string stamp,message;string[] locations;bool supported,exit;
   if(recover){var state=BuddyTakeover.Recovery(engine,environment);stamp=state.stamp;message=state.message;locations=state.locations;supported=state.pending&&state.supported;exit=state.requiresSteamExit;}
   else{var preview=BuddyTakeover.Preview(engine,environment);stamp=preview.stamp;message=preview.message;locations=preview.locations;supported=preview.supported;exit=preview.requiresSteamExit;}
   if(!supported)throw new Exception(message+"\n请核对正确 Steam 客户端及私有恢复资料，保留未知参数和外部编辑；不要整份覆盖 VDF。");
   bool yes=confirm(recover?"恢复本轮接管改动？":"接管旧 Buddy 启动参数？",message+"\n\n"+string.Join("\n",locations)+"\n\n保留其他参数；组件安装、Java 授权和游戏启动仍须分别决定。");
   bool steam=yes&&exit&&confirm("正常退出并重新打开 Steam？","一次性请求当前已核实的客户端正常退出，最多等待 30 秒。处理后请求重新打开同一客户端；登录与就绪须在 Steam 中确认。");
   if(exit&&!steam)yes=false;
   return recover?BuddyTakeover.Recover(engine,stamp,yes,steam,progress,token,environment):BuddyTakeover.Apply(engine,stamp,yes,steam,progress,token,environment);
  }
  internal static BuddyTakeoverPreview LegacyInstallConflict(Engine engine,string operation,BuddyTakeoverEnvironment environment=null){
   if(operation!="InstallComponents"&&operation!="InstallSelected3D"&&operation!="EnableShootClient")return null;
   var preview=BuddyTakeover.Preview(engine,environment);return preview.supported&&preview.hasConflicts?preview:null;
  }
  internal static string BuddyTakeoverSummary(BuddyTakeoverResult result){
   string message=result.cleanupCompleted&&!result.steamVerified?"已清理已识别的游戏文件参数；Steam 启动项未核实、未接管。":result.message;
   return (result.status=="partial"?"[失败] Steam 重新打开未完成；旧参数清理已校验。\n":"")+message+"\n组件安装仍须继续原安装流程；本次游戏加载待验证。";
  }
  void LegacyBuddyTakeover(bool recover,Func<string> continueInstall=null){
   if(Program.PreviewMode){Write("预览：旧参数接管只展示界面，未执行修改。");return;}
   BuddyTakeoverResult result=null;
   Run(()=>{result=ConfirmBuddyTakeover(GetEngine(),recover,(title,body)=>(bool)Invoke(new Func<bool>(()=>MessageBox.Show(this,body,title,MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)==DialogResult.OK)),ReportDownload,downloadCancel.Token);if(result.status=="cancelled")throw new OperationCanceledException(result.message);if(result.status=="failed"||result.status=="confirmationRequired"||result.status=="recoveryRequired")throw new Exception(result.message);return BuddyTakeoverSummary(result);},recover?"正在核对接管恢复记录":"正在扫描旧 Buddy 启动参数",()=>{if(continueInstall!=null&&result.cleanupCompleted&&!result.recoveryPending&&MessageBox.Show(this,BuddyTakeoverSummary(result)+"\nSteam 登录与就绪状态请在客户端确认。\n是否继续刚才的组件安装？安装仍会重新校验文件；Java 授权逐文件决定。","继续原组件安装",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)==DialogResult.OK)Run(continueInstall,"正在继续原组件安装");});
  }
  internal static void DisplayMemoryInfo(NumericUpDown input,Label label,LauncherMemoryInfo info,int heap){
   input.Minimum=1;input.Maximum=Math.Max(1,Math.Max(heap,info.MaximumHeapGiB));input.Value=heap;
   label.Text="当前最大堆："+heap+" GiB · 系统可见总内存："+info.TotalGiB+" GiB · 当前系统可用："+info.AvailableGiB+" GiB\n基础推荐："+info.RecommendedHeapGiB+" GiB · 设置上限（按总量）："+info.MaximumHeapGiB+" GiB。推荐是起始建议，下次启动生效。";
   if(heap>info.MaximumHeapGiB)label.Text+="\n现有配置超过设置上限，按实际值显示；原文件保持原值，请选择上限内的值后保存。";
   if(info.AvailableGiB<Math.Max(heap,info.RecommendedHeapGiB)+2L)label.Text+="\n当前其他程序占用较高；下次启动前可关闭不需要的程序。当前可用内存不会降低设置上限。";
  }
  void SetupLauncherFeatures(TabControl tabs){
   var memory=Page(tabs,"启动与内存");
   AddText(memory,"这里设置的是游戏 Java 最大堆内存（Xmx），不是显存，也不改变 Windows 分页文件。调高内存不一定提高帧率；应为系统、Steam 和其他程序留下余量。退出游戏后保存，下次启动生效。");
   var heapState=new Label{AutoSize=true,MaximumSize=new Size(950,0),Margin=new Padding(8),Text="点击读取当前配置和可用内存"};memory.Controls.Add(heapState);
   var input=new NumericUpDown{Name="launcher-memory-number",Minimum=1,Maximum=512,Value=8,Width=140,Margin=new Padding(8)};memory.Controls.Add(input);
   Action reload=()=>{var info=LauncherMaintenance.MemoryInfo();int heap=LauncherMaintenance.ReadHeapGiB(GetEngine());DisplayMemoryInfo(input,heapState,info,heap);};
   var heapActions=new FlowLayoutPanel{AutoSize=true,WrapContents=true};heapActions.Controls.Add(Button("读取当前配置",reload));heapActions.Controls.Add(Button("保存启动内存",()=>{int selected=(int)input.Value;Run(()=>LauncherMaintenance.SetHeapGiB(GetEngine(),selected),"正在保存游戏启动内存");}));memory.Controls.Add(heapActions);
   if(!Program.PreviewMode){try{reload();}catch(Exception ex){heapState.Text=ex.Message;}}
   var maintenance=Page(tabs,"维护与诊断");
   var buddyState=new Label{Name="launcher-buddy-state",AutoSize=true,MaximumSize=new Size(950,0),Margin=new Padding(8),Text="先扫描旧启动参数；不会在打开软件时接管。"};maintenance.Controls.Add(buddyState);
   AddButton(maintenance,"扫描旧启动参数",()=>{if(Program.PreviewMode){buddyState.Text="预览：只展示扫描入口。";return;}var preview=BuddyTakeover.Preview(GetEngine());var recovery=BuddyTakeover.Recovery(GetEngine());buddyState.Text=preview.message+"\n"+string.Join(" · ",preview.locations)+(recovery.pending?"\n有待恢复的接管记录："+recovery.message:"");});
   AddButton(maintenance,"接管旧启动参数",()=>LegacyBuddyTakeover(false));
   AddButton(maintenance,"恢复本轮接管改动",()=>LegacyBuddyTakeover(true));
   AddText(maintenance,"已识别 Buddy 参数先确认范围；需要改 Steam 启动项时再单独确认正常退出。未知目录、选项或复杂 BAT 保留并人工核对。恢复只处理本轮自有改动，不能整份覆盖更新后的 VDF；必要时先打开并登录正确客户端，再刷新恢复状态。接管后可继续原组件安装，逐文件决定 Java 授权。");
   if(!Program.PreviewMode){try{var recovery=BuddyTakeover.Recovery(GetEngine());if(recovery.pending)buddyState.Text="有待恢复的接管记录："+recovery.message;}catch(Exception ex){buddyState.Text=ex.Message;}}
   AddText(maintenance,"操作前必须退出游戏。清缓存不会备份缓存，不清除角色、车辆数据库、单人存档和个人模组；安装配置的恢复备份仍保留。删除后下次进服需要重新获取地图，第一次进入可能更慢。");
   AddButton(maintenance,"清除多人地图缓存",()=>{if(MessageBox.Show(this,"将清除本机全部已缓存服务器的多人地图区块缓存，不备份。\n保留角色和车辆数据库、未知文件及其他存档。下次进服重新下载地图。\n请先退出游戏，确定清除？","清除多人缓存",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)==DialogResult.OK)Run(()=>LauncherMaintenance.ClearMultiplayerCache(GetEngine()),"正在清除多人地图缓存");});
   AddText(maintenance,"工坊全量重下载仅针对 Project Zomboid（108600）的工坊本地文件。保留订阅、其他游戏、个人本地 mods 和存档。必须先核对 Steam 订阅清单，无法核对就不会开始删除。下载中不要退出 Steam；失败后必须完成重试才可启动。");
   AddButton(maintenance,"清空工坊文件并重新下载",()=>{if(MessageBox.Show(this,"将删除本游戏工坊缓存中的模组文件，不备份，然后重新下载当前账号全部订阅。\n不删除其他游戏、存档、个人 mods，不取消订阅。可能需要大量流量和较长时间。\n必须在线登录 Steam，确定继续？","工坊全量重下载",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)==DialogResult.OK)Run(()=>LauncherMaintenance.RebuildWorkshop(GetEngine(),ReportDownload,downloadCancel.Token),"正在核对 Steam 订阅清单，尚未清除文件");});
   AddButton(maintenance,"打开游戏日志目录",()=>Links.Open(UserDir()));
   var infoPage=Page(tabs,"扩展适配说明");
   AddText(infoPage,"只收录服务器无需操作的客户端扩展。当前两个候选仅声明客户端 Java，缺少 Buddy 所需的本地保留预加载声明，因此暂不提供安装。没有修改它们的 JAR、服务器清单或授权记录。");
   AddText(infoPage,"Smooth Frames：动画预加载、慢 Lua 和帧时间诊断，以及特定原版错误修复。可能增加载入时间；不是网络延迟修复，也不保证平均帧率提升。作者当前多人部署说明需要服务器列入模组清单，不符合你的纯客户端要求。");
   AddButton(infoPage,"Smooth Frames 作者页面",()=>Links.Open("https://steamcommunity.com/sharedfiles/filedetails/?id=3816784011"));
   AddText(infoPage,"KK’s TREE：Viewpoint 近处树木视觉增强。当前作者条件为游戏 42.21、Buddy 2.3.2 及 Viewpoint。树林场景可能增加 GPU 开销。未确认服务器未列出时的保留加载；暂不开放安装。");
   AddButton(infoPage,"KK’s TREE 作者页面",()=>Links.Open("https://steamcommunity.com/sharedfiles/filedetails/?id=3813172420"));
   var choices=buttons.Parent.Controls[0] as FlowLayoutPanel;
   if(choices!=null){var smooth=new GoldCheckBox{Text="Smooth Frames（暂不支持纯客户端）",AutoSize=true,Enabled=false,Checked=false};var trees=new GoldCheckBox{Text="KK’s TREE（暂不支持纯客户端）",AutoSize=true,Enabled=false,Checked=false};choices.Controls.Add(smooth);choices.Controls.Add(trees);}
  }
  void LaunchFromLauncher(){
   var engine=GetEngine();bool running=Process.GetProcessesByName("ProjectZomboid64").Length>0||Process.GetProcessesByName("ProjectZomboid32").Length>0;
   string reason=LauncherLaunch.Reason(engine,busy,running);if(reason!="")throw new Exception(reason);
   if(Program.PreviewMode){Write("预览模式：未启动 Steam 或游戏。");return;}
   engine.Guard();Process.Start(new ProcessStartInfo("steam://rungameid/108600"){UseShellExecute=true});Write("已向 Steam 发送启动请求，实际运行状态以游戏进程为准。");
  }
 }
}
