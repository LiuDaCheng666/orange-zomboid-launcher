using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PZ3D {
 static class GameStartupWindowTests {
  [DllImport("user32.dll")]static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  static int passed;
  static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
  static GameStartupLaunch Launch(){return new GameStartupLaunch{SessionId="native-fixture",GamePath=Path.GetTempPath(),UserPath=Path.GetTempPath(),DataPath=Path.GetTempPath(),StartedUtc=DateTime.UtcNow,TargetKind="continue",Stage="preparing",Message="正在准备游戏启动"};}
  static Form NewWindow(bool light){
   Type type=typeof(GameStartupWindowTests).Assembly.GetType("PZ3D.GameStartupWindow");
   Check(type!=null,"native independent startup window exists");
   return (Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Launch(),LauncherTheme.Create(light)},null);
  }
  static T Find<T>(Form form,string name) where T:Control{var found=form.Controls.Find(name,true);if(found.Length==0)throw new Exception("FAIL: required native control missing: "+name);return (T)found[0];}
  static void Update(Form form,GameStartupSnapshot snapshot){form.GetType().GetMethod("UpdateSnapshot").Invoke(form,new object[]{snapshot});}
  static GameStartupSnapshot Loading(){return new GameStartupSnapshot{SessionId="native-fixture",Stage="loading",Message="正在载入脚本与模组",Lines=new[]{"[12:41:05] Loading: media/lua/shared/", "[12:41:06] Loading: media/maps/Muldraugh, KY/", "[12:41:07] 正在读取本次启动的游戏输出"},ElapsedSeconds=37,ProcessId=3210,WorkingSetBytes=1610612736L,TotalPhysicalBytes=17179869184L,HeapLimitBytes=8589934592L,Complete=false};}
  static GameStartupSnapshot Problems(){var sample=Loading();sample.Lines=new[]{"LOG : General f:0> Loading Mods","LOG : General f:0> Loading Scripts","LOG : General f:0> Loading Lua","ERROR: Lua f:0> attempted index: health of non-table: null","Lua((MOD:Unseen Solar Observatory)).tick(SolarProbe.lua:73)","LOG : General f:0> Game booting"};sample.Issues=new[]{new GameLogIssue{id="fixture-1",title="Lua 脚本读取空值",meaning="脚本试图读取不存在对象的 health 字段。",advice="保留原始日志并核对兼容版本。",severity="error",count=3,mods=new[]{"Unseen Solar Observatory"},frames=new[]{new GameLogFrame{mod="Unseen Solar Observatory",file="SolarProbe.lua",line=73}},evidence=new[]{new GameLogEvidence{source="本次启动",line=42,text="ERROR: Lua f:0> attempted index: health of non-table: null"},new GameLogEvidence{source="本次启动",line=43,text="Lua((MOD:Unseen Solar Observatory)).tick(SolarProbe.lua:73)"}}}};return sample;}
  static void ProblemPanel(){
   foreach(bool light in new[]{false,true})foreach(bool scaled in new[]{false,true})foreach(Size size in new[]{new Size(1280,720),new Size(1920,1080)})using(Form f=NewWindow(light)){
    f.ClientSize=size;if(scaled)ScaleFonts(f,1.5f);Update(f,Problems());IntPtr nativeHandle=f.Handle;f.PerformLayout();
    var panel=Find<Panel>(f,"startup-issues");var title=Find<Label>(f,"startup-issue-title");var meaning=Find<Label>(f,"startup-issue-meaning");var evidence=Find<Label>(f,"startup-issue-evidence");var button=Find<Button>(f,"startup-issue-details");var logs=Find<Label>(f,"startup-log");
    var header=Find<Label>(f,"startup-issue-heading");var leftHeading=Find<Label>(f,"startup-message");
    Check(panel.BackColor==f.BackColor&&panel.BorderStyle==BorderStyle.None&&panel.Padding==Padding.Empty&&title.BackColor==f.BackColor&&meaning.BackColor==f.BackColor&&evidence.BackColor==f.BackColor&&button.BackColor==f.BackColor,"analysis text shares the fullscreen canvas without a visible panel surface or inset");
    Check(panel.Width<=f.ClientSize.Width*.35&&panel.Height<=110&&logs.Font.SizeInPoints<=9.1&&panel.Width==logs.Width&&panel.Bottom==logs.Bottom&&title.Font.Equals(logs.Font)&&meaning.Font.Equals(logs.Font)&&evidence.Font.Equals(logs.Font),"compact corner logs stay at most 35 percent wide and 110 pixels high with capped text size even at 150 percent");
    Check(header.TextAlign==ContentAlignment.TopRight&&title.TextAlign==ContentAlignment.TopRight&&meaning.TextAlign==ContentAlignment.TopRight&&evidence.TextAlign==ContentAlignment.TopRight&&button.TextAlign==ContentAlignment.MiddleRight&&logs.TextAlign==ContentAlignment.BottomLeft&&leftHeading.TextAlign==ContentAlignment.TopLeft,"left logs align left and every analysis text line and detail action aligns right");
    Check(title.Text.Contains("相关模组：Unseen Solar Observatory"),"related mod name is visible explicitly before the Chinese explanation");
    Check(Find<Label>(f,"startup-issue-frame").Text.Contains("SolarProbe.lua:73"),"script filename and line are visible independently from raw evidence");
    Check(!panel.Bounds.IntersectsWith(Find<Control>(f,"startup-brand").Bounds),"bottom text columns do not overlap the central animated brand");
    Check(panel.Right<=f.ClientSize.Width&&panel.Bottom<=f.ClientSize.Height&&panel.Left>=f.ClientSize.Width/2&&panel.Top>=f.ClientSize.Height/2&&!panel.Bounds.IntersectsWith(logs.Bounds),"problem area occupies the bottom right without overlapping four-line log at "+(scaled?"150%":"100%"));
    Check(f.Controls.GetChildIndex(panel)<f.Controls.GetChildIndex(Find<Control>(f,"startup-brand")),"problem heading remains above the transparent central brand control");
    Check(title.Text.Contains("Unseen Solar Observatory")&&meaning.Text.Contains("health")&&evidence.Text.Contains("attempted index")&&button.Enabled,"problem area shows Chinese meaning with literal raw evidence and enabled details");
    var detail=(Form)f.GetType().GetMethod("CreateIssueDetails",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null);using(detail){var text=Find<TextBox>(detail,"startup-issue-detail-text");Check(text.ReadOnly&&text.Multiline&&text.ScrollBars==ScrollBars.Vertical&&text.Text.Contains("Unseen Solar Observatory")&&text.Text.Contains("SolarProbe.lua:73")&&text.Text.Contains("health"),"native problem details preserve mod context and original selectable scrolling evidence");}
    var unknown=Problems();unknown.Issues[0].mods=new string[0];Update(f,unknown);Check(title.Text.Contains("未定位到模组"),"unlocated mod context is stated explicitly instead of attributing an unknown error");
    var multiple=Problems();string longMod="Arbitrary Observatory "+new string('m',100);multiple.Issues[0].mods=new[]{longMod,"Another Lunar Module","Third Unseen Module"};Update(f,multiple);var tips=(ToolTip)f.GetType().GetField("messageTip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);Check(!title.Text.Contains("Third Unseen Module")&&tips.GetToolTip(title).Contains(longMod)&&tips.GetToolTip(title).Contains("Third Unseen Module"),"preview limits related mods to two while tooltip retains complete long names and additional context");using(var details=(Form)f.GetType().GetMethod("CreateIssueDetails",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null)){Check(Find<TextBox>(details,"startup-issue-detail-text").Text.Contains("Third Unseen Module"),"all related mods remain available in selectable issue details");}
    var hostile=Problems();hostile.Issues[0].meaning="password=fixture-secret";hostile.Issues[0].evidence[0].text=@"ERROR: C:\PrivateFixture\secret.log";Update(f,hostile);Check(!meaning.Text.Contains("fixture-secret")&&!evidence.Text.Contains("PrivateFixture"),"problem UI sanitizes arbitrary snapshot strings at its display boundary");
    var complete=Loading();complete.Complete=true;Update(f,complete);Check(title.Text.Contains("Unseen Solar Observatory"),"problem observations remain visible until the observer closes");
   }
  }
  static bool Different(Bitmap first,Bitmap next){for(int x=0;x<first.Width;x++)for(int y=0;y<first.Height;y++)if(first.GetPixel(x,y)!=next.GetPixel(x,y))return true;return false;}
  static void ProgressDesign(){using(Form f=NewWindow(false)){
   f.ClientSize=new Size(1280,720);Update(f,Loading());IntPtr native=f.Handle;f.PerformLayout();var bar=Find<GameStartupWindow.StartupProgress>(f,"startup-progress");
   Check(bar.Height>=12&&bar.Height<=20,"startup loading rail has a substantial rounded track instead of a thin outlined line");
   using(var bitmap=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(bitmap,bar.ClientRectangle);Check(bitmap.GetPixel(0,0).ToArgb()==f.BackColor.ToArgb()&&bitmap.GetPixel(bar.Width/2,bar.Height/2).ToArgb()!=f.BackColor.ToArgb(),"rounded rail leaves clear outer corners and paints a visible interior track");}
   Check(bar.AccessibleDescription.Contains("未知")&&bar.AccessibilityObject.Value.Contains("未知")&&!Find<Label>(f,"startup-count").Text.Contains("%"),"unknown total has an explicit accessible description without an invented percentage");
   using(var first=new Bitmap(bar.Width,bar.Height))using(var next=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(first,bar.ClientRectangle);Thread.Sleep(240);bar.DrawToBitmap(next,bar.ClientRectangle);Check(bar.MotionEnabled?Different(first,next):!Different(first,next),"indeterminate rail animates only when Windows permits motion");}
   var ram=Find<Control>(f,"startup-memory-bar");Check(ram.Height>=8&&ram.Height<=12,"physical memory rail has room for a visible six-pixel track without an outline");using(var bitmap=new Bitmap(ram.Width,ram.Height)){ram.DrawToBitmap(bitmap,ram.ClientRectangle);Color used=bitmap.GetPixel(ram.Width/20,ram.Height/2),unused=bitmap.GetPixel(ram.Width*3/4,ram.Height/2);Check(used.R>unused.R+60&&used.G>unused.G,"physical memory rail visibly separates the measured working-set fraction from unused system RAM");}
   var measured=Loading();measured.CompletedItems=3;measured.TotalItems=8;Update(f,measured);using(var bitmap=new Bitmap(bar.Width,bar.Height)){bar.DrawToBitmap(bitmap,bar.ClientRectangle);Color fill=bitmap.GetPixel(bar.Width/5,bar.Height/2),track=bitmap.GetPixel(bar.Width*3/4,bar.Height/2);Check(fill.R>track.R+60&&!bar.Indeterminate&&bar.Value==3&&bar.Maximum==8,"known item count fills the actual fraction and leaves the rest of the track unfilled");}
   var waiting=Loading();waiting.WaitingForInput=true;Update(f,waiting);Check(bar.VisualState=="waiting"&&!bar.Indeterminate&&bar.AccessibilityObject.Value.Contains("操作"),"authorization pauses flow and announces a clear waiting state");
   var failed=Loading();failed.Failed=true;Update(f,failed);Check(bar.VisualState=="failed"&&!bar.Indeterminate&&bar.AccessibilityObject.Value.Contains("失败"),"failed startup shows a stopped error state rather than continuing loading flow");
   var ready=Loading();ready.Complete=true;Update(f,ready);Check(bar.VisualState=="complete"&&!bar.Indeterminate&&bar.AccessibilityObject.Value.Contains("就绪"),"verified completion gives the rail a completed state and accessible ready text");
  }}

  static void AttentionPriority(){
   using(Form form=NewWindow(false)){
    var sample=Problems();var core=sample.Issues[0];core.attention="primary";
    var background=new GameLogIssue{attention="background",type="asset-bone",severity="error",title="模型骨骼信息提示",meaning="background-only-bone-marker",evidence=new[]{new GameLogEvidence{text="Could not find bone index for node name: Body"}}};
    sample.Issues=new[]{background};Update(form,sample);
    Check(Find<Label>(form,"startup-issue-heading").Text.Contains("辅助")&&!Find<Label>(form,"startup-issue-meaning").Text.Contains("background-only-bone-marker"),"startup canvas folds background diagnostics instead of showing them as core faults");
    var observed=(GameLogIssue[])form.GetType().GetField("observedIssues",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
    Check(observed.Length==1&&observed[0].attention=="background"&&Find<Button>(form,"startup-issue-details").Enabled,"startup details retain auxiliary evidence and copied attention metadata");
    sample.Issues=new[]{background,core};Update(form,sample);
    Check(Find<Label>(form,"startup-issue-title").Text.Contains("Unseen Solar Observatory")&&Find<Label>(form,"startup-issue-meaning").Text.Contains("health"),"actionable MOD fault wins the startup preview regardless of background array order");
    using(var detail=(Form)form.GetType().GetMethod("CreateIssueDetails",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null))Check(!Find<TextBox>(detail,"startup-issue-detail-text").Text.Contains("不等于")&&!Find<Label>(form,"startup-issue-heading").Text.Contains("仅观察"),"startup analysis displays result and evidence without disclaimer boilerplate");
    sample.Issues=new[]{background};Update(form,sample);var tip=(ToolTip)form.GetType().GetField("messageTip",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
    Check(!tip.GetToolTip(Find<Label>(form,"startup-issue-title")).Contains("Unseen Solar Observatory"),"auxiliary-only snapshot clears previous core fault tooltip");
   }
  }
  [STAThread] public static int Main(string[] args){if(args.Length==1&&args[0]=="--sentinel"){Thread.Sleep(30000);return 0;}try{Application.EnableVisualStyles();ProgressDesign();ProblemPanel();AttentionPriority();Run();if(args.Length==2&&args[0]=="--capture")Capture(args[1]);Console.WriteLine("GAME STARTUP WINDOW: "+passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   using(Form f=NewWindow(false)){
    Check(!f.TopMost&&f.FormBorderStyle==FormBorderStyle.None&&f.Bounds==Screen.FromPoint(Cursor.Position).Bounds,"observer fills its monitor without borders or topmost focus");
    Update(f,Loading());
    Check(Find<GameStartupWindow.StartupProgress>(f,"startup-progress").Indeterminate,"unknown loading total stays indeterminate");
    Check(Find<Label>(f,"startup-count").Text.IndexOf('%')<0,"unknown total never fabricates percentage");
    var sample=Loading();sample.CompletedItems=3;sample.TotalItems=8;Update(f,sample);
    Check(!Find<GameStartupWindow.StartupProgress>(f,"startup-progress").Indeterminate&&Find<GameStartupWindow.StartupProgress>(f,"startup-progress").Value==3&&Find<GameStartupWindow.StartupProgress>(f,"startup-progress").Maximum==8,"only measured item counts determine loading progress");
    sample.TotalItems=0;Update(f,sample);Check(Find<GameStartupWindow.StartupProgress>(f,"startup-progress").Indeterminate,"invalid item count returns to indeterminate");
    sample.Stage="scripts";Update(f,sample);Check(Find<Label>(f,"startup-stage").Text.Contains("脚本"),"verified loading stages appear in the phase heading");
    Check(Find<Label>(f,"startup-memory").Text.Contains("1.50 GB")&&Find<Label>(f,"startup-memory").Text.Contains("16.0 GB")&&Find<Label>(f,"startup-memory").Text.Contains("物理"),"memory label identifies measured process physical memory and system total");
    Check(Find<GameStartupWindow.StartupProgress>(f,"startup-memory-bar").Value==93,"memory bar uses process working set over system RAM, not Java heap");
    Check(Find<Label>(f,"startup-heap").Text.Contains("8.00 GB")&&Find<Label>(f,"startup-heap").Text.Contains("配置"),"configured Java heap limit is labelled separately");
    sample.WorkingSetBytes=null;sample.TotalPhysicalBytes=null;sample.HeapLimitBytes=null;Update(f,sample);Check(Find<Label>(f,"startup-memory").Text.Contains("等待")&&Find<GameStartupWindow.StartupProgress>(f,"startup-memory-bar").Value==0,"missing measured memory is explicitly waiting");
    sample=Loading();sample.Message="<b>权限 & 授权</b>";sample.Lines=new[]{@"{\rtf1 not formatted}","<script>plain & literal</script>"};sample.WaitingForInput=true;Update(f,sample);
    Check(Find<Label>(f,"startup-message").Text==sample.Message&&!Find<Label>(f,"startup-message").UseMnemonic&&Find<Label>(f,"startup-log").Text.Contains("<script>plain & literal</script>"),"snapshot messages and log render literal untrusted text");
    Check(Find<Label>(f,"startup-stage").Text.Contains("授权")&&Find<Label>(f,"startup-status").Text.Contains("游戏窗口"),"input wait clearly asks the user to handle the game window");
    sample.Failed=true;sample.WaitingForInput=false;sample.Message="游戏提前退出，最近日志已保留";Update(f,sample);Check(Find<Label>(f,"startup-stage").Text.Contains("失败")&&Find<Label>(f,"startup-message").Text==sample.Message&&Find<Label>(f,"startup-log").Text.Contains("plain & literal"),"failure remains visible with the recent log");
    var stale=Loading();stale.SessionId="other-session";stale.Message="STALE";Update(f,stale);Check(!Find<Label>(f,"startup-message").Text.Contains("STALE"),"other launch sessions cannot overwrite this observer");
    sample=Loading();sample.ElapsedSeconds=3661;Update(f,sample);Check(Find<Label>(f,"startup-elapsed").Text.Contains("1:01:01"),"elapsed time retains hours without wrapping");
    Exception workerFailure=null;sample.Message="后台快照";var worker=new Thread(()=>{try{Update(f,sample);}catch(Exception ex){workerFailure=ex;}});worker.Start();worker.Join();
    IntPtr handle=f.Handle;Application.DoEvents();Check(workerFailure==null&&Find<Label>(f,"startup-message").Text=="后台快照","background snapshots before native handle creation are safely marshalled");
    sample=Loading();sample.Message="native handle 已创建后的后台快照";worker=new Thread(()=>Update(f,sample));worker.Start();worker.Join();Application.DoEvents();Check(Find<Label>(f,"startup-message").Text==sample.Message,"background snapshots after handle creation marshal to the UI thread");
    var earlier=Loading();earlier.Message="旧后台快照";worker=new Thread(()=>Update(f,earlier));worker.Start();worker.Join();sample.Message="最新 UI 快照";Update(f,sample);Application.DoEvents();Check(Find<Label>(f,"startup-message").Text=="最新 UI 快照","queued earlier background snapshots cannot replace a newer direct UI update");
    sample.Lines=new string[90];for(int i=0;i<sample.Lines.Length;i++)sample.Lines[i]=i+new string('x',5000);Update(f,sample);string[] tail=Find<Label>(f,"startup-log").Text.Split(new[]{Environment.NewLine},StringSplitOptions.None);Check(tail.Length==4&&tail[0].StartsWith("86x")&&tail[0].Length==512,"fullscreen view keeps only four short recent log lines");
    Update(f,null);f.Close();Update(f,sample);Check(f.IsDisposed,"closing observer disposes its window and late snapshots are harmless");
   }
   using(Form f=NewWindow(true)){Update(f,Loading());f.ClientSize=new Size(1280,720);f.CreateControl();f.PerformLayout();var lines=Find<Label>(f,"startup-log");var message=Find<Label>(f,"startup-message");Check(lines.Top>f.Height/2&&Find<Label>(f,"startup-memory").Bottom<f.Height/4&&lines.BackColor==f.BackColor&&message.Left==lines.Left&&message.Bottom<=lines.Top&&message.Top>f.Height*.7,"fullscreen layout keeps top memory and all status text in the bottom-left log area");}
   using(Form f=NewWindow(false)){IntPtr handle=f.Handle;typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,new object[]{new KeyEventArgs(Keys.Escape)});Check(f.IsDisposed,"Escape closes only the observation screen");}
   using(var child=Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--sentinel"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden}))try{
    using(Form f=NewWindow(false)){var sample=Loading();sample.ProcessId=child.Id;Update(f,sample);f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();Application.DoEvents();Find<Button>(f,"startup-close").PerformClick();Check(f.IsDisposed&&!child.HasExited,"observer close button leaves its observed test process running");}
   }finally{if(!child.HasExited)child.Kill();child.WaitForExit();}
  }
  static void Capture(string directory){
   Directory.CreateDirectory(directory);
   foreach(bool light in new[]{false,true})foreach(int layout in new[]{0,1,2,3})using(Form f=NewWindow(light)){
    Update(f,Problems());f.ClientSize=layout==0||layout==3?new Size(1920,1080):new Size(1280,720);
    if(layout>=2)ScaleFonts(f,1.5f);
    f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;f.Show();f.PerformLayout();var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<240){Application.DoEvents();Thread.Sleep(10);}
    Check(GetForegroundWindow()!=f.Handle,"offscreen native show and periodic updates never activate the observer");
    using(var bitmap=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{Check(PrintWindow(f.Handle,dc,0),"native window capture renders successfully");}finally{g.ReleaseHdc(dc);}}bitmap.Save(Path.Combine(directory,(light?"light":"dark")+(layout==0?"-default":layout==1?"-minimum":layout==2?"-minimum-scaled150":"-default-scaled150")+".png"));}
    if(!light&&layout==1){foreach(string state in new[]{"measured","waiting","failed","complete"}){var sample=Problems();sample.Stage="scripts";if(state=="measured"){sample.CompletedItems=3;sample.TotalItems=8;}if(state=="waiting")sample.WaitingForInput=true;if(state=="failed"){sample.Failed=true;sample.Message="游戏进程在就绪前退出";}if(state=="complete")sample.Complete=true;Update(f,sample);Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{Check(PrintWindow(f.Handle,dc,0),"native loading rail "+state+" state capture renders successfully");}finally{g.ReleaseHdc(dc);}}bitmap.Save(Path.Combine(directory,"dark-progress-"+state+".png"));}}}
    if(layout==2){var many=Problems();many.Issues=new GameLogIssue[12];for(int i=0;i<many.Issues.Length;i++)many.Issues[i]=Problems().Issues[0];Update(f,many);using(var detail=(Form)f.GetType().GetMethod("CreateIssueDetails",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,null)){
     detail.StartPosition=FormStartPosition.Manual;detail.Location=new Point(-20000,-20000);detail.ShowInTaskbar=false;detail.Show();Application.DoEvents();using(var bitmap=new Bitmap(detail.Width,detail.Height)){using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{Check(PrintWindow(detail.Handle,dc,0),"native scrolling issue detail capture renders successfully");}finally{g.ReleaseHdc(dc);}}bitmap.Save(Path.Combine(directory,(light?"light":"dark")+"-issue-details-scaled150.png"));}
    }}
    if(!light&&layout==0){Control brand=Find<Control>(f,"startup-brand");using(var first=new Bitmap(brand.Width,brand.Height))using(var next=new Bitmap(brand.Width,brand.Height)){
     brand.DrawToBitmap(first,brand.ClientRectangle);timer.Restart();while(timer.ElapsedMilliseconds<350){Application.DoEvents();Thread.Sleep(10);}brand.DrawToBitmap(next,brand.ClientRectangle);bool changed=false;for(int x=0;x<first.Width&&!changed;x++)for(int y=0;y<first.Height;y++)if(first.GetPixel(x,y)!=next.GetPixel(x,y)){changed=true;break;}
     bool animated=(bool)brand.GetType().GetField("animated",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brand);Check(animated?changed:!changed,"brand animation advances real frames while respecting Windows reduced motion");first.Save(Path.Combine(directory,"animation-frame-1.png"));next.Save(Path.Combine(directory,"animation-frame-2.png"));
    }}
   }
  }
  static void ScaleFonts(Control control,float scale){var font=control.Font;foreach(Control child in control.Controls)ScaleFonts(child,scale);control.Font=new Font(font.FontFamily,font.Size*scale,font.Style);}
 }
}
