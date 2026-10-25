using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace PZ3D {
 // A read-only view. The host owns observation; this form never owns a game process.
 sealed class GameStartupWindow:Form {
  readonly string sessionId;
  readonly int uiThread;
  readonly LauncherTheme theme;
  readonly object pendingGate=new object();
  GameStartupSnapshot pending;
  readonly Label stage,message,count,memory,heap,elapsed,status;
  readonly StartupProgress progress;
  readonly StartupProgress memoryBar;
  readonly Label log;
  readonly Panel issuesPanel;readonly Label issueHeader,issueTitle,issueMeaning,issueFrame,issueEvidence;readonly Button issueDetails;
  GameLogIssue[] observedIssues=new GameLogIssue[0];
  readonly OrangeMark brand;
  readonly Button minimize,close;
  readonly Font contentFont,headingFont,smallFont,logFont;
  readonly Icon appIcon;
  readonly ToolTip messageTip=new ToolTip{AutoPopDelay=15000,InitialDelay=400,ReshowDelay=150,ShowAlways=true};

  public GameStartupWindow(GameStartupLaunch launch,LauncherTheme theme){
   if(launch==null)throw new ArgumentNullException("launch");
   this.theme=theme??LauncherTheme.Create(false);sessionId=launch.SessionId;uiThread=Thread.CurrentThread.ManagedThreadId;
   Text="OZL · 游戏启动";Name="game-startup-window";contentFont=new Font("Segoe UI",10f);Font=contentFont;BackColor=this.theme.Background;ForeColor=this.theme.Text;
   headingFont=new Font(Font.FontFamily,10f,FontStyle.Bold);smallFont=new Font(Font.FontFamily,9f);logFont=new Font("Consolas",12f,FontStyle.Regular,GraphicsUnit.Pixel);
   try{appIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=appIcon;}catch{}
   AutoScaleDimensions=new SizeF(96f,96f);AutoScaleMode=AutoScaleMode.Dpi;
   StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;Bounds=Screen.FromPoint(Cursor.Position).Bounds;
   TopMost=false;DoubleBuffered=true;KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}};
   stage=Label("startup-stage","准备启动",this.theme.Text);stage.Font=headingFont;
   count=Label("startup-count","等待真实加载进度",this.theme.Muted);count.TextAlign=ContentAlignment.MiddleRight;count.Font=smallFont;
   progress=new StartupProgress(this.theme.Light){Name="startup-progress",BackColor=BackColor,ForeColor=this.theme.Accent,Indeterminate=true};Controls.Add(progress);
   memory=Label("startup-memory","进程物理内存 · 等待进程采样",this.theme.Muted);memory.TextAlign=ContentAlignment.MiddleCenter;
   heap=Label("startup-heap","Java 堆上限（配置） · 未知",this.theme.Muted);heap.Font=smallFont;heap.TextAlign=ContentAlignment.MiddleCenter;
   memoryBar=new StartupProgress(this.theme.Light){Name="startup-memory-bar",Maximum=1000,BackColor=BackColor,ForeColor=this.theme.Accent,AccessibleName="游戏进程物理内存占系统内存的比例"};Controls.Add(memoryBar);
   brand=new OrangeMark(this.theme,appIcon){Name="startup-brand"};Controls.Add(brand);
   message=Label("startup-message",launch.Message??"正在准备游戏启动",this.theme.Muted);message.TextAlign=ContentAlignment.MiddleCenter;message.Font=logFont;
   status=Label("startup-status","正在等待游戏进程与新日志",this.theme.Muted);status.Font=smallFont;status.TextAlign=ContentAlignment.MiddleCenter;
   log=Label("startup-log","等待本次启动的游戏日志…",this.theme.Muted);log.Font=logFont;log.TextAlign=ContentAlignment.BottomLeft;
   issuesPanel=new Panel{Name="startup-issues",BackColor=BackColor,BorderStyle=BorderStyle.None,Padding=Padding.Empty,AccessibleName="本次启动发现的问题"};Controls.Add(issuesPanel);
   issueHeader=IssueLabel("startup-issue-heading","日志分析 · 正在观察",this.theme.Muted);issueHeader.Font=logFont;
   issueTitle=IssueLabel("startup-issue-title","暂未发现需关注的日志",this.theme.Text);issueTitle.Font=logFont;
   issueMeaning=IssueLabel("startup-issue-meaning","持续观察本次新日志；错误记录不会中断启动。",this.theme.Muted);issueMeaning.Font=logFont;
   issueFrame=IssueLabel("startup-issue-frame","脚本位置 · 等待日志",this.theme.Muted);issueFrame.Font=logFont;
   issueEvidence=IssueLabel("startup-issue-evidence","原始依据将在这里显示",this.theme.Muted);issueEvidence.Font=logFont;
   issueDetails=Button("startup-issue-details","查看全部问题与原始日志");issuesPanel.Controls.Add(issueDetails);issueDetails.BackColor=BackColor;issueDetails.Font=logFont;issueDetails.Padding=Padding.Empty;issueDetails.Margin=Padding.Empty;issueDetails.FlatAppearance.MouseOverBackColor=BackColor;issueDetails.FlatAppearance.MouseDownBackColor=BackColor;issueDetails.ForeColor=this.theme.Accent;issueDetails.TextAlign=ContentAlignment.MiddleLeft;issueDetails.Click+=(sender,args)=>{using(var detail=CreateIssueDetails())detail.ShowDialog(this);};issueDetails.Enabled=false;
   elapsed=Label("startup-elapsed","已用时  0:00",this.theme.Muted);elapsed.Font=smallFont;elapsed.TextAlign=ContentAlignment.MiddleRight;
   minimize=Button("startup-minimize","隐藏画面");minimize.Click+=(s,e)=>Close();
   close=Button("startup-close","Esc 关闭观察");close.Click+=(s,e)=>Close();
   UpdateSnapshot(new GameStartupSnapshot{SessionId=sessionId,Stage=launch.Stage,Message=launch.Message,Failed=launch.Stage=="failed"});
   issuesPanel.BringToFront();PerformLayout();
  }
  Label Label(string name,string text,Color color){Label label=name=="startup-log"?(Label)new SingleLineLogLabel():new Label();label.Name=name;label.Text=text;label.ForeColor=color;label.BackColor=BackColor;label.TextAlign=ContentAlignment.MiddleLeft;label.AutoEllipsis=true;label.UseMnemonic=false;label.Margin=Padding.Empty;Controls.Add(label);return label;}

  Label IssueLabel(string name,string text,Color color){Label label=(name=="startup-issue-evidence"||name=="startup-issue-title")?(Label)new SingleLineLogLabel():new Label();label.Name=name;label.Text=text;label.ForeColor=color;label.BackColor=BackColor;label.TextAlign=ContentAlignment.TopRight;label.AutoEllipsis=true;label.UseMnemonic=false;label.Margin=Padding.Empty;issuesPanel.Controls.Add(label);return label;}
  Button Button(string name,string text){var button=new Button{Name=name,Text=text,FlatStyle=FlatStyle.Flat,BackColor=BackColor,ForeColor=theme.Muted,Font=smallFont,Cursor=Cursors.Hand};button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=theme.Surface;Controls.Add(button);return button;}
  protected override void OnLayout(LayoutEventArgs e){
   base.OnLayout(e);if(close==null)return;
   float scale=Math.Max(1f,Font.Size/10f);if(IsHandleCreated)using(var graphics=CreateGraphics())scale=Math.Max(scale,graphics.DpiX/96f);Func<int,int> s=value=>(int)(value*scale);
   int w=ClientSize.Width,h=ClientSize.Height,pad=Math.Max(s(28),(int)(w*.035));int top=s(18);
   stage.SetBounds(pad,top,w/2,s(26));count.SetBounds(w/2,top,w/2-pad,s(26));progress.SetBounds(pad,top+s(30),w-pad*2,14);
   memory.SetBounds(pad,top+s(52),w-pad*2,s(26));heap.SetBounds(pad,top+s(78),w-pad*2,s(25));memoryBar.SetBounds(w/2-s(130),top+s(108),s(260),8);
   // Corner logs retain a fixed readable pixel size even when the main form
   // scales. Long content is available through tooltips and the detail view.
   foreach(Control text in new Control[]{message,log,status,issueHeader,issueTitle,issueMeaning,issueFrame,issueEvidence,issueDetails})if(!ReferenceEquals(text.Font,logFont))text.Font=logFont;
   foreach(Control text in new Control[]{elapsed,minimize,close})if(!ReferenceEquals(text.Font,smallFont))text.Font=smallFont;
   int columnWidth=(int)(w*.32),lineHeight=(int)Math.Ceiling(logFont.GetHeight())+2,bodyBottom=h-pad-16;
   issuesPanel.SetBounds(w-pad-columnWidth,bodyBottom-lineHeight*6,columnWidth,lineHeight*6);
   issueHeader.SetBounds(0,0,columnWidth,lineHeight);issueTitle.SetBounds(0,lineHeight,columnWidth,lineHeight);
   issueMeaning.SetBounds(0,lineHeight*2,columnWidth,lineHeight);issueFrame.SetBounds(0,lineHeight*3,columnWidth,lineHeight);issueEvidence.SetBounds(0,lineHeight*4,columnWidth,lineHeight);
   issueDetails.SetBounds(0,lineHeight*5,columnWidth,lineHeight);issueDetails.TextAlign=ContentAlignment.MiddleRight;
   foreach(Label text in new[]{issueHeader,issueTitle,issueMeaning,issueFrame,issueEvidence})text.TextAlign=ContentAlignment.TopRight;
   log.SetBounds(pad,bodyBottom-lineHeight*4,columnWidth,lineHeight*4);log.TextAlign=ContentAlignment.BottomLeft;
   message.SetBounds(pad,log.Top-lineHeight,columnWidth,lineHeight);message.TextAlign=ContentAlignment.TopLeft;
   status.SetBounds(pad,message.Top-lineHeight*2-4,columnWidth,lineHeight*2);status.TextAlign=ContentAlignment.BottomLeft;
   int brandTop=Math.Max((int)(h*.20),top+s(126)),brandBottom=Math.Min((int)(h*.64),issuesPanel.Top-24);
   brand.SetBounds((int)(w*.20),brandTop,(int)(w*.60),Math.Max(80,brandBottom-brandTop));
   elapsed.SetBounds(pad,h-pad+4,200,24);elapsed.TextAlign=ContentAlignment.MiddleLeft;
   minimize.SetBounds(w-pad-224,h-pad+4,102,24);close.SetBounds(w-pad-122,h-pad+4,122,24);

  }
  static string TargetLabel(string target){return target=="continue"||target=="save"?"继续存档":target=="server"||target=="connect"?"连接服务器":"进入主菜单";}

  public void UpdateSnapshot(GameStartupSnapshot snapshot){
   if(snapshot==null||IsDisposed||Disposing||!String.Equals(snapshot.SessionId,sessionId,StringComparison.Ordinal))return;
   if(Thread.CurrentThread.ManagedThreadId==uiThread){lock(pendingGate)pending=null;Apply(snapshot);return;}
   lock(pendingGate)pending=snapshot;
   if(!IsHandleCreated)return;
   try{BeginInvoke((MethodInvoker)DrainPending);}catch(InvalidOperationException){} // Closing the observer is allowed at any time.
  }
  protected override bool ShowWithoutActivation{get{return true;}}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);DrainPending();}
  void DrainPending(){GameStartupSnapshot value;lock(pendingGate){value=pending;pending=null;}if(value!=null&&!IsDisposed&&!Disposing)Apply(value);}
  void Apply(GameStartupSnapshot snapshot){
   if(IsDisposed||Disposing)return;
   bool terminal=snapshot.Failed||snapshot.Complete;
   stage.Text="当前阶段 · "+(snapshot.Failed?"启动失败":snapshot.Complete?"游戏已就绪":snapshot.WaitingForInput?"等待游戏授权":StageLabel(snapshot.Stage));
   stage.ForeColor=snapshot.Failed?(theme.Light?Color.FromArgb(170,57,39):Color.FromArgb(245,153,119)):theme.Text;
   message.Text=Bound(snapshot.Message,1024);if(String.IsNullOrWhiteSpace(message.Text))message.Text=snapshot.Failed?"启动未能完成，最近日志已保留":"等待本次启动的游戏输出";
   messageTip.SetToolTip(message,message.Text);
   bool measured=snapshot.CompletedItems.HasValue&&snapshot.TotalItems.HasValue&&snapshot.TotalItems.Value>0&&snapshot.CompletedItems.Value>=0&&snapshot.CompletedItems.Value<=snapshot.TotalItems.Value;
   progress.VisualState=snapshot.Failed?"failed":snapshot.Complete?"complete":snapshot.WaitingForInput?"waiting":"loading";
   progress.Indeterminate=!measured&&!terminal&&!snapshot.WaitingForInput;
   if(measured){progress.Value=0;progress.Maximum=snapshot.TotalItems.Value;progress.Value=snapshot.CompletedItems.Value;count.Text=snapshot.CompletedItems.Value+" / "+snapshot.TotalItems.Value+" 项";}
   else {progress.Value=0;count.Text=snapshot.Failed?"最近输出已保留":snapshot.Complete?"主菜单已就绪":snapshot.WaitingForInput?"请在游戏中完成确认":"正在处理 · 请稍候";}
   progress.AccessibleDescription=stage.Text+" · "+(measured?count.Text:"总量未知；状态由当前日志证据确认")+" · "+progress.VisualState;
   double seconds=Double.IsNaN(snapshot.ElapsedSeconds)||Double.IsInfinity(snapshot.ElapsedSeconds)?0:Math.Max(0,Math.Min(snapshot.ElapsedSeconds,315360000));var duration=TimeSpan.FromSeconds(seconds);
   elapsed.Text="已用时  "+(duration.TotalHours>=1?((int)duration.TotalHours).ToString(CultureInfo.InvariantCulture)+":"+duration.Minutes.ToString("00")+":"+duration.Seconds.ToString("00"):((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture)+":"+duration.Seconds.ToString("00"));
   bool validMemory=snapshot.WorkingSetBytes.HasValue&&snapshot.WorkingSetBytes.Value>=0;
   bool validTotal=snapshot.TotalPhysicalBytes.HasValue&&snapshot.TotalPhysicalBytes.Value>0;
   memory.Text="进程物理内存 · "+(validMemory?Bytes(snapshot.WorkingSetBytes.Value,2)+(validTotal?" / 系统 "+Bytes(snapshot.TotalPhysicalBytes.Value,1):" / 系统总量未知"):"等待进程采样");
   memoryBar.Value=validMemory&&validTotal?(int)Math.Min(1000d,Math.Max(0d,(double)snapshot.WorkingSetBytes.Value/snapshot.TotalPhysicalBytes.Value*1000d)):0;
   memoryBar.AccessibleDescription=validMemory&&validTotal?"进程物理内存占系统总内存的实际比例；与 Java 堆上限分别展示":"物理内存数据尚未采样，比例未知";
   heap.Text="Java 堆上限（配置） · "+(snapshot.HeapLimitBytes.HasValue&&snapshot.HeapLimitBytes.Value>0?Bytes(snapshot.HeapLimitBytes.Value,2):"未知");
   status.Text=snapshot.Failed?"最近日志已保留，可关闭此窗口查看或重试":snapshot.WaitingForInput?"请切换到游戏窗口完成授权或确认，此窗口不会抢占前台":snapshot.Complete?"游戏已进入主菜单":snapshot.ProcessId.HasValue?"正在观察游戏进程 PID "+snapshot.ProcessId.Value+" · 物理内存不代表 Java 堆使用量":"正在等待游戏进程与新日志";
   status.Visible=snapshot.Failed||snapshot.WaitingForInput;
   if(snapshot.Lines!=null&&snapshot.Lines.Length>0){int first=Math.Max(0,snapshot.Lines.Length-4);var lines=new string[snapshot.Lines.Length-first];for(int i=0;i<lines.Length;i++)lines[i]=Bound(snapshot.Lines[first+i],512).Replace("\r"," ").Replace("\n"," ");log.Text=String.Join(Environment.NewLine,lines);}
   messageTip.SetToolTip(log,log.Text);
   ApplyIssues(snapshot.Issues);
   PerformLayout();
  }
  void ApplyIssues(GameLogIssue[] values){
   // Keep the current session evidence until close, including a terminal snapshot with no new issues.
   if(values!=null&&values.Length>0)observedIssues=values.Where(x=>x!=null).Take(24).Select(GameStartupReducer.DisplayIssue).ToArray();
   issueDetails.Enabled=observedIssues.Length>0;if(observedIssues.Length==0)return;
   var first=observedIssues[0];issueHeader.Text="日志分析 · "+observedIssues.Length+" 类问题（仅观察）";
   issueTitle.Text="相关模组："+(first.mods.Length==0?"未定位到模组":String.Join("、",first.mods.Take(2).ToArray())+(first.mods.Length>2?" 等 "+first.mods.Length+" 个":""));
   issueMeaning.Text=(!String.IsNullOrWhiteSpace(first.title)?first.title:"未分类日志问题")+" · "+(String.IsNullOrWhiteSpace(first.meaning)?"暂无法确定原因或是否影响启动":first.meaning);
   issueFrame.Text=first.frames.Length>0?String.Join(" · ",first.frames.Take(2).Select(x=>x.file+(x.line>0?":"+x.line:"")).ToArray()):"脚本位置：尚未定位";
   issueEvidence.Text=first.evidence.Length>0?first.evidence[0].text:"原始依据：等待完整日志";
   messageTip.SetToolTip(issueTitle,"相关模组："+(first.mods.Length==0?"未定位到模组":String.Join("、",first.mods))+"（相关调用不代表已确认责任）");messageTip.SetToolTip(issueFrame,issueFrame.Text);messageTip.SetToolTip(issueMeaning,issueMeaning.Text);messageTip.SetToolTip(issueEvidence,issueEvidence.Text);
  }
  Form CreateIssueDetails(){
   var detail=new Form{Name="startup-issue-dialog",Text="本次启动 · 问题与原始日志",BackColor=theme.Background,ForeColor=theme.Text,Font=Font,StartPosition=FormStartPosition.CenterParent,ShowInTaskbar=false,MinimizeBox=false,MaximizeBox=false,MinimumSize=new Size(640,420),Size=new Size(Math.Min(920,Screen.FromControl(this).WorkingArea.Width-60),Math.Min(680,Screen.FromControl(this).WorkingArea.Height-60))};
   var content=new StringBuilder("仅分析本次启动新增的日志。错误级别不等于启动失败；相关模组不等于已确认责任。\r\n原始依据已隐藏凭据、个人路径与网络标识，可选择文字并按 Ctrl+C 复制。\r\n\r\n");
   for(int i=0;i<observedIssues.Length;i++){var issue=observedIssues[i];content.AppendLine((i+1)+". "+issue.title+" · "+(issue.severity=="warning"?"提醒":"错误")+" · 片段内记录 "+issue.count+" 次");content.AppendLine("中文含义："+issue.meaning);content.AppendLine("相关模组："+(issue.mods.Length>0?String.Join("、",issue.mods):"未定位到模组"));if(!String.IsNullOrWhiteSpace(issue.advice))content.AppendLine("排查建议："+issue.advice);content.AppendLine("原始日志依据：");foreach(var evidence in issue.evidence){content.AppendLine("["+evidence.source+" · 片段第 "+evidence.line+" 行] "+evidence.text);}content.AppendLine();if(content.Length>60000)break;}
   var text=new TextBox{Name="startup-issue-detail-text",ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,BorderStyle=BorderStyle.None,BackColor=theme.Surface,ForeColor=theme.Text,Font=log.Font,Text=Bound(content.ToString(),65536),Dock=DockStyle.Fill,AccessibleName="问题中文含义、相关模组和原始日志依据"};
   var done=new Button{Name="startup-issue-detail-close",Text="关闭",Dock=DockStyle.Bottom,Height=42,FlatStyle=FlatStyle.Flat,BackColor=theme.Background,ForeColor=theme.Text};done.Click+=(sender,args)=>detail.Close();detail.Padding=new Padding(18);detail.Controls.Add(text);detail.Controls.Add(done);detail.CancelButton=done;detail.Shown+=(sender,args)=>text.Select(0,0);return detail;
  }
  static string Bound(string value,int limit){if(value==null)return "";return (value.Length>limit?value.Substring(0,limit):value).Replace("\0","");}
  static string Bytes(long value,int decimals){return (value/1073741824d).ToString("F"+decimals,CultureInfo.InvariantCulture)+" GB";}
  static string StageLabel(string value){switch(value){case "preparing":return "准备启动";case "submitted":return "等待游戏启动";case "loading":return "正在加载游戏";case "mods":return "正在加载模组";case "scripts":return "正在加载游戏脚本";case "lua":return "正在加载 Lua 与界面";case "clothing":return "正在加载服装资源";case "models":return "正在加载模型与动画";case "textures":return "正在加载贴图资源";case "booting":case "initializing":return "正在完成初始化";case "connecting":return "正在连接服务器";case "waiting":return "等待游戏输出";default:return "正在启动游戏";}}
  protected override void Dispose(bool disposing){if(disposing){lock(pendingGate)pending=null;messageTip.Dispose();headingFont.Dispose();smallFont.Dispose();logFont.Dispose();contentFont.Dispose();if(appIcon!=null)appIcon.Dispose();}base.Dispose(disposing);}

  internal sealed class StartupProgress:Control {
   [DllImport("user32.dll")]static extern bool SystemParametersInfo(uint action,uint param,[MarshalAs(UnmanagedType.Bool)]out bool value,uint flags);
   readonly bool light,motion;readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer{Interval=40};readonly Stopwatch clock=Stopwatch.StartNew();
   bool indeterminate;string visualState="loading";int value,maximum=100;
   public StartupProgress(bool light){this.light=light;bool enabled;motion=!SystemParametersInfo(0x1042,0,out enabled,0)||enabled;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleRole=AccessibleRole.ProgressBar;AccessibleName="游戏启动加载状态";animation.Tick+=(sender,args)=>Invalidate();}
   public bool MotionEnabled{get{return motion;}}
   public bool Indeterminate{get{return indeterminate;}set{indeterminate=value;RefreshAnimation();Invalidate();}}
   public string VisualState{get{return visualState;}set{visualState=value??"loading";RefreshAnimation();Invalidate();}}
   public int Maximum{get{return maximum;}set{maximum=Math.Max(1,value);this.value=Math.Min(this.value,maximum);Invalidate();}}
   public int Value{get{return value;}set{this.value=Math.Max(0,Math.Min(maximum,value));Invalidate();}}
   void RefreshAnimation(){if(motion&&Visible&&indeterminate&&visualState=="loading")animation.Start();else animation.Stop();}
   protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);RefreshAnimation();}
   static GraphicsPath Capsule(RectangleF bounds){var path=new GraphicsPath();float diameter=Math.Min(bounds.Height,bounds.Width);if(diameter<=0)return path;path.AddArc(bounds.X,bounds.Y,diameter,diameter,90,180);path.AddArc(bounds.Right-diameter,bounds.Y,diameter,diameter,270,180);path.CloseFigure();return path;}
   protected override void OnPaint(PaintEventArgs e){
    var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;if(Width<2||Height<2)return;
    float height=Math.Min(10,Height-2);var track=new RectangleF(0,(Height-height)/2f,Width-1,height);
    using(var path=Capsule(track))using(var brush=new SolidBrush(light?Color.FromArgb(218,226,236):Color.FromArgb(47,52,60)))g.FillPath(brush,path);
    float fillWidth=visualState=="complete"?track.Width:visualState=="failed"?height:visualState=="waiting"?Math.Min(140,track.Width*.18f):indeterminate?Math.Max(72,track.Width*.20f):track.Width*value/maximum;
    if(fillWidth<=0)return;float x=track.X;
    if(visualState=="waiting")x=(track.Width-fillWidth)/2;
    else if(indeterminate){double flow=motion?(Math.Sin(clock.Elapsed.TotalSeconds*1.15)+1)/2:.5;x=(float)((track.Width-fillWidth)*flow);}
    var fill=new RectangleF(x,track.Y,Math.Min(track.Width,fillWidth),height);
    Color start=visualState=="failed"?Color.FromArgb(207,77,59):Color.FromArgb(237,133,35),end=visualState=="failed"?Color.FromArgb(244,135,104):Color.FromArgb(255,205,102);
    using(var path=Capsule(fill))using(var brush=new LinearGradientBrush(fill,start,end,LinearGradientMode.Horizontal)){brush.GammaCorrection=true;g.FillPath(brush,path);}
    if(visualState=="waiting"){float center=fill.X+fill.Width/2;using(var pen=new Pen(Color.FromArgb(235,255,247,228),1.4f)){g.DrawLine(pen,center-2,track.Y+3,center-2,track.Bottom-3);g.DrawLine(pen,center+2,track.Y+3,center+2,track.Bottom-3);}}
    else if(visualState=="complete"){float right=track.Right-10;using(var pen=new Pen(Color.White,1.5f)){g.DrawLines(pen,new[]{new PointF(right-4,track.Y+5),new PointF(right-1,track.Y+7),new PointF(right+4,track.Y+2)});}}
   }
   protected override AccessibleObject CreateAccessibilityInstance(){return new StartupProgressAccessibility(this);}
   sealed class StartupProgressAccessibility:ControlAccessibleObject {readonly StartupProgress owner;public StartupProgressAccessibility(StartupProgress owner):base(owner){this.owner=owner;}public override string Value{get{return owner.visualState=="complete"?"主菜单已就绪":owner.visualState=="failed"?"启动失败":owner.visualState=="waiting"?"等待用户操作":owner.indeterminate?"加载中，总量未知":owner.value+" / "+owner.maximum;}set{}}}
   protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
  }

  sealed class SingleLineLogLabel:Label {
   protected override void OnPaint(PaintEventArgs e){string[] rows=Text.Split(new[]{Environment.NewLine},StringSplitOptions.None);int height=Math.Max(1,Height/Math.Max(1,rows.Length));for(int i=0;i<rows.Length;i++)TextRenderer.DrawText(e.Graphics,rows[i],Font,new Rectangle(0,i*height,Width,height),ForeColor,TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter|((TextAlign==ContentAlignment.TopRight||TextAlign==ContentAlignment.MiddleRight||TextAlign==ContentAlignment.BottomRight)?TextFormatFlags.Right:TextFormatFlags.Left));}
  }

  sealed class OrangeMark:Control {
   [DllImport("user32.dll")]static extern bool SystemParametersInfo(uint action,uint param,[MarshalAs(UnmanagedType.Bool)]out bool value,uint flags);
   readonly LauncherTheme theme;readonly Bitmap logo;
   readonly System.Windows.Forms.Timer animation=new System.Windows.Forms.Timer{Interval=33};
   readonly Stopwatch elapsed=Stopwatch.StartNew();readonly bool animated;
   public OrangeMark(LauncherTheme theme,Icon icon){
    this.theme=theme;
    try{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("OrangeAppLogo.png"))if(stream!=null)using(var source=Image.FromStream(stream))logo=new Bitmap(source);}catch{}
    if(logo==null&&icon!=null)try{logo=icon.ToBitmap();}catch{}
    bool motion;animated=!SystemParametersInfo(0x1042,0,out motion,0)||motion;
    SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleName="OZL 橙子标识";
    animation.Tick+=(s,e)=>Invalidate();
   }
   protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible&&animated)animation.Start();else animation.Stop();}
   protected override void OnPaint(PaintEventArgs e){
    base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    float size=Math.Min(230f,Height*.57f),cx=Width/2f,cy=Height*.40f;
    double seconds=animated?elapsed.Elapsed.TotalSeconds:0;float angle=(float)(seconds*18)%360;
    var circle=new RectangleF(cx-size/2-24,cy-size/2-24,size+48,size+48);
    using(var track=new Pen(Color.FromArgb(24,theme.Accent),1f))g.DrawEllipse(track,circle);
    int opacity=animated?(int)(108+Math.Sin(seconds*1.4)*35):110;
    using(var arc=new Pen(Color.FromArgb(opacity,theme.Accent),2f)){arc.StartCap=LineCap.Round;arc.EndCap=LineCap.Round;for(int i=0;i<4;i++)g.DrawArc(arc,circle,angle+i*90,24);}
    if(logo!=null)g.DrawImage(logo,new RectangleF(cx-size/2,cy-size/2,size,size));
    else{
     var state=g.Save();g.TranslateTransform(cx-size/2,cy-size/2);g.ScaleTransform(size/100,size/100);
     using(var ring=new GraphicsPath(FillMode.Alternate))using(var orange=new SolidBrush(Color.FromArgb(246,156,35))){ring.AddPolygon(new[]{new PointF(50,10),new PointF(91,33),new PointF(91,79),new PointF(50,100),new PointF(9,79),new PointF(9,33)});ring.AddPolygon(new[]{new PointF(50,39),new PointF(67,48),new PointF(67,66),new PointF(50,76),new PointF(33,66),new PointF(33,48)});g.FillPath(orange,ring);}
     using(var leaf=new SolidBrush(Color.FromArgb(127,172,74)))g.FillPolygon(leaf,new[]{new PointF(77,17),new PointF(80,4),new PointF(95,0),new PointF(91,12)});g.Restore(state);
    }
    float baseline=cy+size/2+Height*.07f,wordSize=Math.Min(31,Height*.09f);
    using(var word=new Font("Segoe UI",wordSize,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(theme.Text)){var measured=g.MeasureString("OZL",word);g.DrawString("OZL",word,brush,cx-measured.Width/2,baseline);}
    using(var caption=new Font("Segoe UI",9,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(theme.Muted)){const string text="ORANGE ZOMBOID LAUNCHER";var measured=g.MeasureString(text,caption);g.DrawString(text,caption,brush,cx-measured.Width/2,baseline+wordSize*1.4f);}
   }
   protected override void Dispose(bool disposing){if(disposing){animation.Dispose();if(logo!=null)logo.Dispose();}base.Dispose(disposing);}
  }
 }
}
