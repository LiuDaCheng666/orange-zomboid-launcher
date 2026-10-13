using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace PZ3D {
 sealed partial class MainForm {
  Label liveStatus,liveDetail,taskStatus;Button cancelTask,retryTask;
  DateTime taskStarted,lastTaskUpdate;string taskMessage="就绪 · 只读取本地状态，安装 / 更新需主动点击";bool taskActive;string cachedRuntime="";DateTime cachedLogStamp=DateTime.MinValue;
  const string ApprovalGuide="首次启动：ZombieBuddy Java 模组授权\r\n\r\n这不是卡死：游戏正在等待你决定是否允许 Java 模组运行。\r\n\r\n1. 核对 Mod 名称和来源，确认是你下载的 Viewpoint。Java 模组可以执行代码并访问本机文件，只允许可信来源。\r\n2. 在对应行勾选 Allow（允许）。不必勾选 Trust author（信任作者），它会扩大信任范围。\r\n3. 勾选 Save decisions to disk（保存决定），然后点 OK。相同版本通常无需重复确认，更新后可能需要重新授权。\r\n4. Mod 名称空白、未知或来源无法确认时，不要直接同意。先取消并检查游戏日志、模组列表和下载来源。\r\n\r\n工具不会自动允许所有 Java 模组，也不会伪造你的授权。授权完成不等于服务器内 3D 已生效，仍须检查加载状态并按 O 测试。";
  Control CreateDashboard(){
   var panel=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=card,Padding=new Padding(10,5,10,5),ColumnCount=2,RowCount=3};panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,29));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,23));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,29));
   liveStatus=new Label{Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,11,FontStyle.Bold),Text="3D 状态：正在读取…",AutoEllipsis=true};liveDetail=new Label{Dock=DockStyle.Fill,Text="配置状态和游戏实际加载分开判断",AutoEllipsis=true};taskStatus=new Label{Dock=DockStyle.Fill,Text=taskMessage,AutoEllipsis=true};panel.Controls.Add(liveStatus,0,0);panel.Controls.Add(liveDetail,0,1);panel.Controls.Add(taskStatus,0,2);
   var help=Button("首次启动授权说明",()=>MessageBox.Show(this,ApprovalGuide,"首次 Java 模组授权",MessageBoxButtons.OK,MessageBoxIcon.Information));help.Dock=DockStyle.Fill;help.MinimumSize=Size.Empty;help.Padding=Padding.Empty;panel.Controls.Add(help,1,0);panel.SetRowSpan(help,2);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};cancelTask=Button("取消下载",()=>{});cancelTask.Click+=(s,e)=>{if(taskActive){downloadCancel.Cancel();taskMessage="已请求取消下载 · 文件操作会安全完成";UpdateTaskDisplay();}};retryTask=Button("检查更新",FetchWorkshop);foreach(var b in new[]{cancelTask,retryTask}){b.MinimumSize=Size.Empty;b.Size=new Size(83,27);b.AutoSize=false;b.Padding=Padding.Empty;b.Margin=new Padding(1,0,1,0);actions.Controls.Add(b);}panel.Controls.Add(actions,1,2);return panel;
  }
  void StartVisibleTask(string text){taskActive=true;taskStarted=lastTaskUpdate=DateTime.Now;taskMessage=text;progress.Indeterminate=true;stage.Text=text;UpdateTaskDisplay();}
  void UpdateTask(string text,bool measured){taskMessage=text;lastTaskUpdate=DateTime.Now;progress.Indeterminate=!measured;UpdateTaskDisplay();}
  void FinishVisibleTask(bool success,string reason){taskActive=false;taskMessage=success?"任务完成 · "+reason:"任务未完成 · "+reason;progress.Indeterminate=false;if(success)progress.Value=100;UpdateTaskDisplay();}
  void UpdateTaskDisplay(){if(taskStatus==null)return;string elapsed=taskActive?" · 已用时 "+(int)(DateTime.Now-taskStarted).TotalSeconds+" 秒":"";string wait=taskActive && (DateTime.Now-lastTaskUpdate).TotalSeconds>=8?" · 等待 Steam / 网络响应，尚未完成":"";taskStatus.Text=taskMessage+elapsed+wait;cancelTask.Enabled=taskActive;retryTask.Enabled=!busy;}
  void UpdateDashboard(){if(liveStatus==null)return;UpdateTaskDisplay();try{
   var engine=GetEngine();bool configured=engine.Enabled;bool files=File.Exists(Path.Combine(engine.Game,"ZombieBuddy.jar"))&&File.Exists(Path.Combine(engine.Game,"zbNative.dll"));DateTime? started=null;
   foreach(var process in Process.GetProcessesByName("ProjectZomboid64")){using(process){try{if(string.Equals(Path.GetDirectoryName(process.MainModule.FileName),engine.Game,StringComparison.OrdinalIgnoreCase)){started=process.StartTime;break;}}catch{}}}
   string logfile=Path.Combine(engine.User,"console.txt");bool current=false;
   if(started.HasValue&&File.Exists(logfile)){DateTime stamp=File.GetLastWriteTime(logfile);if(stamp!=cachedLogStamp){using(var stream=new FileStream(logfile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream)){cachedRuntime=reader.ReadToEnd();}cachedLogStamp=stamp;}var header=Regex.Match(cachedRuntime,@"(?m)^LOG\s*: General.*?>\s*(\d{2}-\d{2}-\d{4} \d{2}:\d{2}:\d{2})");DateTime logged;if(header.Success&&DateTime.TryParseExact(header.Groups[1].Value,"dd-MM-yyyy HH:mm:ss",CultureInfo.InvariantCulture,DateTimeStyles.None,out logged))current=logged>=started.Value.AddSeconds(-5)&&logged<=DateTime.Now.AddSeconds(5);}
   string result=RuntimeEvidence.Evaluate(configured,files,cachedRuntime,current);liveStatus.Text="3D 状态："+result;bool light=launcherTheme!=null&&launcherTheme.Light;liveStatus.ForeColor=result.Contains("失败")||result.Contains("不完整")?(light?Color.FromArgb(157,48,35):Color.FromArgb(255,133,117)):result.Contains("已加载")?(light?Color.FromArgb(29,110,69):Color.FromArgb(126,225,168)):(light?Color.FromArgb(125,81,9):Color.FromArgb(255,196,83));liveDetail.Text="下次启动配置："+(configured?"开启":"关闭")+"   当前游戏："+(started.HasValue?"运行中":"未运行")+"   Buddy 文件："+(files?"齐全":"缺失")+"   联机兼容与画面效果需实测";
  }catch(Exception e){liveStatus.Text="3D 状态：暂时无法读取";liveDetail.Text=e.Message;}}
 }
}
