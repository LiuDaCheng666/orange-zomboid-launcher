using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
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
  readonly OrangeProgress progress;
  readonly OrangeProgress memoryBar;
  readonly Label log;
  readonly OrangeMark brand;
  readonly Button minimize,close;
  readonly Font contentFont,headingFont,smallFont,logFont;
  readonly Icon appIcon;
  readonly ToolTip messageTip=new ToolTip{AutoPopDelay=15000,InitialDelay=400,ReshowDelay=150,ShowAlways=true};

  public GameStartupWindow(GameStartupLaunch launch,LauncherTheme theme){
   if(launch==null)throw new ArgumentNullException("launch");
   this.theme=theme??LauncherTheme.Create(false);sessionId=launch.SessionId;uiThread=Thread.CurrentThread.ManagedThreadId;
   Text="OZL · 游戏启动";Name="game-startup-window";contentFont=new Font("Segoe UI",10f);Font=contentFont;BackColor=this.theme.Background;ForeColor=this.theme.Text;
   headingFont=new Font(Font.FontFamily,10f,FontStyle.Bold);smallFont=new Font(Font.FontFamily,9f);logFont=new Font("Consolas",9.5f);
   try{appIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Icon=appIcon;}catch{}
   AutoScaleDimensions=new SizeF(96f,96f);AutoScaleMode=AutoScaleMode.Dpi;
   StartPosition=FormStartPosition.Manual;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;Bounds=Screen.FromPoint(Cursor.Position).Bounds;
   TopMost=false;DoubleBuffered=true;KeyPreview=true;KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}};
   stage=Label("startup-stage","准备启动",this.theme.Text);stage.Font=headingFont;
   count=Label("startup-count","等待真实加载进度",this.theme.Muted);count.TextAlign=ContentAlignment.MiddleRight;count.Font=smallFont;
   progress=new OrangeProgress{Name="startup-progress",BackColor=this.theme.Surface,ForeColor=this.theme.Accent,Indeterminate=true};Controls.Add(progress);
   memory=Label("startup-memory","进程物理内存 · 等待进程采样",this.theme.Muted);memory.TextAlign=ContentAlignment.MiddleCenter;
   heap=Label("startup-heap","Java 堆上限（配置） · 未知",this.theme.Muted);heap.Font=smallFont;heap.TextAlign=ContentAlignment.MiddleCenter;
   memoryBar=new OrangeProgress{Name="startup-memory-bar",Maximum=1000,BackColor=this.theme.Surface,ForeColor=this.theme.Accent};Controls.Add(memoryBar);
   brand=new OrangeMark(this.theme,appIcon){Name="startup-brand"};Controls.Add(brand);
   message=Label("startup-message",launch.Message??"正在准备游戏启动",this.theme.Muted);message.TextAlign=ContentAlignment.MiddleCenter;
   status=Label("startup-status","正在等待游戏进程与新日志",this.theme.Muted);status.Font=smallFont;status.TextAlign=ContentAlignment.MiddleCenter;
   log=Label("startup-log","等待本次启动的游戏日志…",this.theme.Muted);log.Font=logFont;log.TextAlign=ContentAlignment.BottomLeft;
   elapsed=Label("startup-elapsed","已用时  0:00",this.theme.Muted);elapsed.Font=smallFont;elapsed.TextAlign=ContentAlignment.MiddleRight;
   minimize=Button("startup-minimize","隐藏画面");minimize.Click+=(s,e)=>Close();
   close=Button("startup-close","Esc 关闭观察");close.Click+=(s,e)=>Close();
   UpdateSnapshot(new GameStartupSnapshot{SessionId=sessionId,Stage=launch.Stage,Message=launch.Message,Failed=launch.Stage=="failed"});
   PerformLayout();
  }
  Label Label(string name,string text,Color color){var label=new Label{Name=name,Text=text,ForeColor=color,BackColor=BackColor,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,UseMnemonic=false,Margin=Padding.Empty};Controls.Add(label);return label;}
  Button Button(string name,string text){var button=new Button{Name=name,Text=text,FlatStyle=FlatStyle.Flat,BackColor=BackColor,ForeColor=theme.Muted,Font=smallFont,Cursor=Cursors.Hand};button.FlatAppearance.BorderSize=0;button.FlatAppearance.MouseOverBackColor=theme.Surface;Controls.Add(button);return button;}
  protected override void OnLayout(LayoutEventArgs e){
   base.OnLayout(e);if(close==null)return;
   float scale=Math.Max(1f,Font.Size/10f);if(IsHandleCreated)using(var graphics=CreateGraphics())scale=Math.Max(scale,graphics.DpiX/96f);Func<int,int> s=value=>(int)(value*scale);
   int w=ClientSize.Width,h=ClientSize.Height,pad=Math.Max(s(28),(int)(w*.035));int top=s(18);
   stage.SetBounds(pad,top,w/2,s(26));count.SetBounds(w/2,top,w/2-pad,s(26));progress.SetBounds(pad,top+s(30),w-pad*2,s(3));
   memory.SetBounds(pad,top+s(44),w-pad*2,s(26));heap.SetBounds(pad,top+s(70),w-pad*2,s(25));memoryBar.SetBounds(w/2-s(130),top+s(100),s(260),s(2));
   int brandTop=Math.Max((int)(h*.20),top+s(126));brand.SetBounds((int)(w*.20),brandTop,(int)(w*.60),Math.Max(100,(int)(h*.64)-brandTop));
   status.SetBounds(w-pad-s(330),h-pad-s(132),s(330),s(46));status.TextAlign=ContentAlignment.BottomRight;
   int lineHeight=(int)Math.Ceiling(log.Font.GetHeight())+s(3),lineCount=Math.Min(6,Math.Max(1,log.Text.Split('\n').Length));log.SetBounds(pad,h-pad-lineHeight*lineCount,Math.Max(100,w-pad*2-s(280)),lineHeight*lineCount);
   message.SetBounds(log.Left,log.Top-s(36),log.Width,s(32));message.TextAlign=ContentAlignment.MiddleLeft;
   elapsed.SetBounds(w-pad-s(240),h-pad-s(82),s(240),s(26));minimize.SetBounds(w-pad-s(224),h-pad-s(48),s(102),s(32));close.SetBounds(w-pad-s(122),h-pad-s(48),s(122),s(32));
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
   stage.Text=snapshot.Failed?"启动失败":snapshot.Complete?"游戏已就绪":snapshot.WaitingForInput?"等待游戏授权":StageLabel(snapshot.Stage);
   stage.ForeColor=snapshot.Failed?(theme.Light?Color.FromArgb(170,57,39):Color.FromArgb(245,153,119)):theme.Text;
   message.Text=Bound(snapshot.Message,1024);if(String.IsNullOrWhiteSpace(message.Text))message.Text=snapshot.Failed?"启动未能完成，最近日志已保留":"等待本次启动的游戏输出";
   messageTip.SetToolTip(message,message.Text);
   bool measured=snapshot.CompletedItems.HasValue&&snapshot.TotalItems.HasValue&&snapshot.TotalItems.Value>0&&snapshot.CompletedItems.Value>=0&&snapshot.CompletedItems.Value<=snapshot.TotalItems.Value;
   progress.Indeterminate=!measured&&!terminal;
   if(measured){progress.Value=0;progress.Maximum=snapshot.TotalItems.Value;progress.Value=snapshot.CompletedItems.Value;count.Text=snapshot.CompletedItems.Value+" / "+snapshot.TotalItems.Value+" 项";}
   else {progress.Value=0;count.Text=snapshot.Failed?"最近输出已保留":snapshot.Complete?"已收到就绪信号":snapshot.WaitingForInput?"等待你的操作":"等待真实加载进度";}
   double seconds=Double.IsNaN(snapshot.ElapsedSeconds)||Double.IsInfinity(snapshot.ElapsedSeconds)?0:Math.Max(0,Math.Min(snapshot.ElapsedSeconds,315360000));var duration=TimeSpan.FromSeconds(seconds);
   elapsed.Text="已用时  "+(duration.TotalHours>=1?((int)duration.TotalHours).ToString(CultureInfo.InvariantCulture)+":"+duration.Minutes.ToString("00")+":"+duration.Seconds.ToString("00"):((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture)+":"+duration.Seconds.ToString("00"));
   bool validMemory=snapshot.WorkingSetBytes.HasValue&&snapshot.WorkingSetBytes.Value>=0;
   bool validTotal=snapshot.TotalPhysicalBytes.HasValue&&snapshot.TotalPhysicalBytes.Value>0;
   memory.Text="进程物理内存 · "+(validMemory?Bytes(snapshot.WorkingSetBytes.Value,2)+(validTotal?" / 系统 "+Bytes(snapshot.TotalPhysicalBytes.Value,1):" / 系统总量未知"):"等待进程采样");
   memoryBar.Value=validMemory&&validTotal?(int)Math.Min(1000d,Math.Max(0d,(double)snapshot.WorkingSetBytes.Value/snapshot.TotalPhysicalBytes.Value*1000d)):0;
   heap.Text="Java 堆上限（配置） · "+(snapshot.HeapLimitBytes.HasValue&&snapshot.HeapLimitBytes.Value>0?Bytes(snapshot.HeapLimitBytes.Value,2):"未知");
   status.Text=snapshot.Failed?"最近日志已保留，可关闭此窗口查看或重试":snapshot.WaitingForInput?"请切换到游戏窗口完成授权或确认，此窗口不会抢占前台":snapshot.Complete?"游戏已进入主菜单":snapshot.ProcessId.HasValue?"正在观察游戏进程 PID "+snapshot.ProcessId.Value+" · 物理内存不代表 Java 堆使用量":"正在等待游戏进程与新日志";
   status.Visible=snapshot.Failed||snapshot.WaitingForInput;
   if(snapshot.Lines!=null&&snapshot.Lines.Length>0){int first=Math.Max(0,snapshot.Lines.Length-6);var lines=new string[snapshot.Lines.Length-first];for(int i=0;i<lines.Length;i++)lines[i]=Bound(snapshot.Lines[first+i],512).Replace("\r"," ").Replace("\n"," ");log.Text=String.Join(Environment.NewLine,lines);}
   PerformLayout();
  }
  static string Bound(string value,int limit){if(value==null)return "";return (value.Length>limit?value.Substring(0,limit):value).Replace("\0","");}
  static string Bytes(long value,int decimals){return (value/1073741824d).ToString("F"+decimals,CultureInfo.InvariantCulture)+" GB";}
  static string StageLabel(string value){switch(value){case "preparing":return "准备启动";case "submitted":return "等待游戏启动";case "loading":return "正在加载游戏";case "mods":return "正在加载模组";case "scripts":return "正在加载游戏脚本";case "lua":return "正在加载 Lua 与界面";case "clothing":return "正在加载服装资源";case "models":return "正在加载模型与动画";case "textures":return "正在加载贴图资源";case "booting":case "initializing":return "正在完成初始化";case "connecting":return "正在连接服务器";case "waiting":return "等待游戏输出";default:return "正在启动游戏";}}
  protected override void Dispose(bool disposing){if(disposing){lock(pendingGate)pending=null;messageTip.Dispose();headingFont.Dispose();smallFont.Dispose();logFont.Dispose();contentFont.Dispose();if(appIcon!=null)appIcon.Dispose();}base.Dispose(disposing);}

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
