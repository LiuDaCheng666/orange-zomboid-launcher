using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PZ3D {
 // Paint the native control ourselves: common-controls WM_PRINT otherwise loses
 // its themed fill in real-control captures, and default white clashes with the UI.
 internal sealed class InstallerProgressBar:ProgressBar {
  readonly Timer animation;
  internal InstallerProgressBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);animation=new Timer{Interval=40};animation.Tick+=(s,e)=>{if(Visible&&Style==ProgressBarStyle.Marquee)Invalidate();};animation.Start();}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Color.FromArgb(54,59,67));int inset=1;var track=new Rectangle(inset,inset,Math.Max(0,Width-2*inset),Math.Max(0,Height-2*inset));using(var fill=new SolidBrush(Color.FromArgb(223,179,87))){if(Style==ProgressBarStyle.Marquee){int segment=Math.Max(24,track.Width/4),x=(Environment.TickCount&int.MaxValue)/12%Math.Max(1,track.Width+segment)-segment;var moving=Rectangle.Intersect(track,new Rectangle(x,track.Top,segment,track.Height));if(moving.Width>0)e.Graphics.FillRectangle(fill,moving);}else{track.Width=(int)(track.Width*(double)(Value-Minimum)/Math.Max(1,Maximum-Minimum));if(track.Width>0)e.Graphics.FillRectangle(fill,track);}}}
  protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
 }
 internal sealed class InstallerButton:Button {
  bool hovered;
  internal InstallerButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Size=new Size(156,44);Margin=new Padding(6);Cursor=Cursors.Hand;}
  protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
  protected override void OnPaint(PaintEventArgs e){
   e.Graphics.Clear(Parent==null?Color.FromArgb(26,29,34):Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var rect=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));int radius=Math.Min(16,Math.Min(rect.Width,rect.Height));
   using(var path=new GraphicsPath()){path.AddArc(rect.X,rect.Y,radius,radius,180,90);path.AddArc(rect.Right-radius,rect.Y,radius,radius,270,90);path.AddArc(rect.Right-radius,rect.Bottom-radius,radius,radius,0,90);path.AddArc(rect.X,rect.Bottom-radius,radius,radius,90,90);path.CloseFigure();
    Color fill=Enabled?(hovered?ControlPaint.Light(BackColor,.12f):BackColor):Color.FromArgb(52,56,63);
    using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);using(var pen=new Pen(Enabled?Color.FromArgb(126,108,69):Color.FromArgb(74,80,89)))e.Graphics.DrawPath(pen,path);
   }
   TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,Enabled?ForeColor:Color.FromArgb(168,174,183),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
   if(Focused&&Enabled)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(7,7,Width-14,Height-14),ForeColor,BackColor);
  }
 }
 // Keep CheckBox's native input, state and accessibility. Only repaint the disabled
 // label: the system disabled text color is unreadable on the installer's dark panel.
 internal sealed class InstallerCheckBox:CheckBox {
  internal Color DisabledForeColor;
  protected override void OnPaint(PaintEventArgs e){
   base.OnPaint(e);if(Enabled)return;
   var state=Checked?System.Windows.Forms.VisualStyles.CheckBoxState.CheckedDisabled:System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled;
   int textLeft=CheckBoxRenderer.GetGlyphSize(e.Graphics,state).Width+3;
   var label=new Rectangle(textLeft,0,Math.Max(0,Width-textLeft),Height);
   using(var brush=new SolidBrush(BackColor))e.Graphics.FillRectangle(brush,label);
   TextRenderer.DrawText(e.Graphics,Text,Font,label,DisabledForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
  }
 }
 internal sealed class InstallerForm:Form {
  readonly Func<string,Action<InstallerProgress>,InstallerInstallResult> install;
  readonly Action<InstallerInstallResult> openLauncher;
  readonly Func<InstallerInstallResult,InstallerShortcutResult> createShortcut;
  readonly CheckBox desktopShortcut;readonly Label shortcutOutcome;
  readonly TextBox destination,details;readonly Label reason,status,counts,installedPaths;
  readonly ProgressBar progress;readonly Button browse,installButton,openButton,finish,detailsToggle,copy;
  readonly TableLayoutPanel body;readonly Panel scroll;readonly LauncherTheme theme=LauncherTheme.Create(false);
  bool busy,terminal;InstallerInstallResult completed;internal int ExitCode;
  internal InstallerForm(InstallerDestination recommendation,Func<string,Action<InstallerProgress>,InstallerInstallResult> backend,Action<InstallerInstallResult> open):this(recommendation,backend,open,null){}
  internal InstallerForm(InstallerDestination recommendation,Func<string,Action<InstallerProgress>,InstallerInstallResult> backend,Action<InstallerInstallResult> open,Func<InstallerInstallResult,InstallerShortcutResult> shortcut) {
   install=backend;openLauncher=open;createShortcut=shortcut;Text="安装 OZL · "+AppVersion.Current;BackColor=theme.Background;ForeColor=theme.Text;Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Font;StartPosition=FormStartPosition.CenterScreen;
   var area=Screen.PrimaryScreen.WorkingArea;Size=new Size(Math.Min(800,Math.Max(400,area.Width-40)),Math.Min(610,Math.Max(340,area.Height-60)));
   try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,AutoSize=true,WrapContents=true,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(16,8,16,12),BackColor=theme.Background};
   finish=MakeButton("installer-finish","完成",false);finish.Visible=false;finish.Click+=(s,e)=>Close();
   installButton=MakeButton("installer-install","安装启动器",true);installButton.Click+=(s,e)=>StartInstall();
   openButton=MakeButton("installer-open","打开启动器",true);openButton.Visible=false;openButton.Click+=(s,e)=>OpenInstalled();
   footer.Controls.Add(finish);footer.Controls.Add(openButton);footer.Controls.Add(installButton);
   scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(24,18,24,4),BackColor=theme.Background};
   body=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=1,Padding=new Padding(0,0,0,12),BackColor=theme.Background};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   var title=Label("OZL",22);title.Font=new Font(Font.FontFamily,22,FontStyle.Bold);Add(title);
   Add(Label("原生安装向导  /  版本 "+AppVersion.Current,10,theme.Accent));
   Add(Label("安装独立的启动器工具。首次打开启动器需要系统 WebView2 Runtime。",10));
   Add(Label("安装位置",11));
   var pathRow=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=2,Margin=new Padding(0,2,0,2)};pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   destination=new TextBox{Name="installer-path",Text=recommendation.Root??"",Dock=DockStyle.Fill,BackColor=theme.Surface,ForeColor=theme.Text,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,9,6,0)};
   browse=MakeButton("installer-browse","更改位置…",false);browse.Width=132;browse.Click+=(s,e)=>Browse();pathRow.Controls.Add(destination,0,0);pathRow.Controls.Add(browse,1,0);Add(pathRow);
   reason=Label(recommendation.Reason??"可直接编辑完整安装路径。",9,theme.Muted);Add(reason);
   Add(Label("更改位置会选择存放父目录，并建议独立的 OrangeLauncher 子目录；也可在上方直接输入完整路径。",9,theme.Muted));
   desktopShortcut=new InstallerCheckBox{Name="installer-desktop-shortcut",Text="创建桌面快捷方式（当前用户）",Checked=true,AutoSize=true,Dock=DockStyle.Top,Margin=new Padding(0,6,0,6),ForeColor=theme.Text,DisabledForeColor=theme.Muted};Add(desktopShortcut);
   status=Label("准备就绪 · 点击安装后才创建目录",11);status.Name="installer-status";Add(status);
   progress=new InstallerProgressBar{Name="installer-progress",Dock=DockStyle.Top,Height=12,Style=ProgressBarStyle.Continuous,Maximum=1000,Margin=new Padding(0,8,0,6)};Add(progress);
   counts=Label("",9,theme.Muted);Add(counts);
   installedPaths=Label("",9);installedPaths.Visible=false;Add(installedPaths);
   shortcutOutcome=Label("",9);shortcutOutcome.Name="installer-shortcut-outcome";shortcutOutcome.Visible=false;Add(shortcutOutcome);
   var detailActions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,WrapContents=true,Margin=new Padding(0,2,0,2)};
   detailsToggle=MakeButton("installer-details-toggle","查看详情",false);detailsToggle.Width=120;detailsToggle.Click+=(s,e)=>{details.Visible=!details.Visible;copy.Visible=details.Visible;detailsToggle.Text=details.Visible?"收起详情":"查看详情";};
   copy=MakeButton("installer-copy","复制详情",false);copy.Width=120;copy.Visible=false;copy.Click+=(s,e)=>{try{Clipboard.SetText(details.Text);}catch(Exception error){status.Text="无法复制详情："+error.Message;}};detailActions.Controls.Add(detailsToggle);detailActions.Controls.Add(copy);Add(detailActions);
   details=new TextBox{Name="installer-details",Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Height=124,Dock=DockStyle.Top,Visible=false,BackColor=theme.Surface,ForeColor=theme.Text,Text="安装位置："+destination.Text+"\r\n安装器版本："+AppVersion.Current};Add(details);
   Add(Label("已有安装请选择启动器内更新，或选择新的空目录。安装失败会保留恢复资料。",9,theme.Muted));
   scroll.Controls.Add(body);Controls.Add(scroll);Controls.Add(footer);scroll.Resize+=(s,e)=>WrapLabels();Shown+=(s,e)=>WrapLabels();
   destination.TextChanged+=(s,e)=>{if(!busy&&!terminal){status.Text="准备安装到所示完整路径 · 点击安装后校验并创建";details.Text="安装位置："+destination.Text+"\r\n安装器版本："+AppVersion.Current;installButton.Enabled=!string.IsNullOrWhiteSpace(destination.Text);}};
   FormClosing+=(s,e)=>{if(busy){e.Cancel=true;status.Text="正在安全安装，请等待当前部署结束后再关闭。";}};
   if(!string.IsNullOrEmpty(recommendation.Error)){status.Text=recommendation.Error;details.Text+="\r\n"+recommendation.Error;}
   installButton.Enabled=!string.IsNullOrWhiteSpace(destination.Text);
  }
  Label Label(string text,float size,Color? color=null){return new Label{Text=text,AutoSize=true,Dock=DockStyle.Top,Font=new Font(Font.FontFamily,size),ForeColor=color??theme.Text,Margin=new Padding(0,5,0,5),UseMnemonic=false};}
  void Add(Control control){int row=body.RowCount++;body.RowStyles.Add(new RowStyle(SizeType.AutoSize));body.Controls.Add(control,0,row);}
  Button MakeButton(string name,string text,bool primary){return new InstallerButton{Name=name,Text=text,BackColor=primary?theme.Accent:theme.Surface,ForeColor=primary?Color.FromArgb(29,31,35):theme.Text};}
  void WrapLabels(){int width=Math.Max(200,scroll.ClientSize.Width-scroll.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-4);foreach(var label in body.Controls.OfType<Label>())label.MaximumSize=new Size(width,0);}
  void Browse(){using(var picker=new FolderBrowserDialog{Description="选择存放父目录；点击安装后才创建 OrangeLauncher 子目录",ShowNewFolderButton=false}){if(picker.ShowDialog(this)!=DialogResult.OK)return;try{var selected=InstallerDestinationPolicy.FromParent(picker.SelectedPath,InstallerPlatform.ProbeRecommendation);destination.Text=selected.Root;reason.Text="在所选父目录中创建独立工具子目录。";if(!string.IsNullOrEmpty(selected.Error))status.Text=selected.Error;}catch(Exception error){status.Text="无法使用所选位置："+error.Message;}}}
  void Post(Action action){if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke(action);}catch(InvalidOperationException){if(!IsDisposed&&!Disposing)throw;}}
  void StartInstall(){
   if(busy||terminal)return;string root;
   try{root=InstallerDestinationPolicy.ValidateInstallRoot(destination.Text);}catch(Exception error){ExitCode=1;status.Text="请更改安装位置："+error.Message;details.Text="安装位置校验失败（尚未创建目录）：\r\n"+error;details.Visible=true;copy.Visible=true;detailsToggle.Text="收起详情";WrapLabels();return;}
   bool wantsShortcut=desktopShortcut.Checked;busy=true;ExitCode=0;destination.Enabled=false;browse.Enabled=false;desktopShortcut.Enabled=false;installButton.Enabled=false;installButton.Text="正在安装…";status.Text="正在校验安装包";progress.Style=ProgressBarStyle.Marquee;progress.MarqueeAnimationSpeed=25;
   Task.Run(()=>{try{var result=install(root,p=>Post(()=>{if(busy&&!terminal)ShowProgress(p);}));if(result==null||!CoreDeployment.SamePath(root,result.Root))throw new InvalidOperationException("安装结果与所选目录不一致。");InstallerShortcutResult shortcutResult=null;if(wantsShortcut&&createShortcut!=null){try{shortcutResult=createShortcut(result);if(shortcutResult==null)throw new InvalidOperationException("未返回快捷方式结果。");}catch(Exception error){shortcutResult=new InstallerShortcutResult(false,"","桌面快捷方式未创建："+error.Message);}}Post(()=>CompleteInstall(root,result,shortcutResult,wantsShortcut));}catch(Exception error){Post(()=>{busy=false;ShowFailure(error);});}});
  }
  void CompleteInstall(string root,InstallerInstallResult result,InstallerShortcutResult shortcutResult,bool wantsShortcut){
   try{
    if(result==null||!CoreDeployment.SamePath(root,result.Root))throw new InvalidOperationException("安装结果与所选目录不一致。");
    completed=result;terminal=true;busy=false;ExitCode=0;progress.Style=ProgressBarStyle.Continuous;progress.Value=progress.Maximum;status.Text="安装完成 · 已核验记录与自有文件";counts.Text="100%  /  仅在所有核验成功后完成";
    installedPaths.Text="安装位置："+result.Root+"\r\n卸载器："+result.UninstallerPath;installedPaths.Visible=true;details.Text+="\r\n安装完成："+result.Root+"\r\n诊断资料："+result.EvidenceDirectory;installButton.Visible=false;openButton.Visible=true;finish.Visible=true;WrapLabels();
    shortcutOutcome.Text=shortcutResult==null?(wantsShortcut?"桌面快捷方式：本安装会话未启用创建服务。":"桌面快捷方式：已按选择跳过创建。"):shortcutResult.Message+(shortcutResult.Created&&!shortcutResult.Message.Contains(shortcutResult.Path)?"\r\n"+shortcutResult.Path:"");shortcutOutcome.Visible=true;details.AppendText("\r\n"+shortcutOutcome.Text);WrapLabels();
   }catch(Exception error){busy=false;ShowFailure(error);}
  }
  void ShowProgress(InstallerProgress value){
   if(value==null||value.Phase==InstallerPhase.Complete)return;status.Text=value.Message;
   if(value.Total<=0){progress.Style=ProgressBarStyle.Marquee;counts.Text="当前事务无细粒度计数，请等待安全完成。";}
   else{progress.Style=ProgressBarStyle.Continuous;progress.Value=(int)Math.Max(0,Math.Min(1000,(double)value.Current/value.Total*1000));counts.Text=value.Unit==InstallerProgressUnit.Bytes?"解压准备："+value.Current.ToString("N0")+" / "+value.Total.ToString("N0")+" 字节":"落盘核验："+value.Current+" / "+value.Total+" 个文件";}
   details.AppendText("\r\n"+value.Message+(value.Total>0?" ["+value.Current+"/"+value.Total+"]":""));
  }
  void ShowFailure(Exception error){terminal=true;ExitCode=1;progress.Style=ProgressBarStyle.Continuous;progress.Value=0;status.Text="安装未完成："+error.Message;counts.Text="保留原目录供排查；可选择新的空目录重新安装。";var failure=error as InstallerInstallException;details.Text="安装位置："+destination.Text+(failure==null?"":"\r\n失败阶段："+failure.Phase+"\r\n保留诊断位置："+failure.EvidenceDirectory)+"\r\n\r\n"+error;details.Visible=true;copy.Visible=true;detailsToggle.Text="收起详情";installButton.Visible=false;openButton.Visible=false;finish.Visible=true;finish.Text="关闭";WrapLabels();}
  void OpenInstalled(){if(completed==null||busy)return;try{openLauncher(completed);}catch(Exception error){status.Text="无法打开启动器："+error.Message;details.AppendText("\r\n打开失败："+error);details.Visible=true;copy.Visible=true;detailsToggle.Text="收起详情";WrapLabels();}}
 }
}
