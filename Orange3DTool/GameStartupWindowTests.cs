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
  static T Find<T>(Form form,string name) where T:Control{return (T)form.Controls.Find(name,true)[0];}
  static void Update(Form form,GameStartupSnapshot snapshot){form.GetType().GetMethod("UpdateSnapshot").Invoke(form,new object[]{snapshot});}
  static GameStartupSnapshot Loading(){return new GameStartupSnapshot{SessionId="native-fixture",Stage="loading",Message="正在载入脚本与模组",Lines=new[]{"[12:41:05] Loading: media/lua/shared/", "[12:41:06] Loading: media/maps/Muldraugh, KY/", "[12:41:07] 正在读取本次启动的游戏输出"},ElapsedSeconds=37,ProcessId=3210,WorkingSetBytes=1610612736L,TotalPhysicalBytes=17179869184L,HeapLimitBytes=8589934592L,Complete=false};}
  [STAThread] public static int Main(string[] args){if(args.Length==1&&args[0]=="--sentinel"){Thread.Sleep(30000);return 0;}try{Application.EnableVisualStyles();Run();if(args.Length==2&&args[0]=="--capture")Capture(args[1]);Console.WriteLine("GAME STARTUP WINDOW: "+passed+" PASS");return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   using(Form f=NewWindow(false)){
    Check(!f.TopMost&&f.FormBorderStyle==FormBorderStyle.None&&f.Bounds==Screen.FromPoint(Cursor.Position).Bounds,"observer fills its monitor without borders or topmost focus");
    Update(f,Loading());
    Check(Find<OrangeProgress>(f,"startup-progress").Indeterminate,"unknown loading total stays indeterminate");
    Check(Find<Label>(f,"startup-count").Text.IndexOf('%')<0,"unknown total never fabricates percentage");
    var sample=Loading();sample.CompletedItems=3;sample.TotalItems=8;Update(f,sample);
    Check(!Find<OrangeProgress>(f,"startup-progress").Indeterminate&&Find<OrangeProgress>(f,"startup-progress").Value==3&&Find<OrangeProgress>(f,"startup-progress").Maximum==8,"only measured item counts determine loading progress");
    sample.TotalItems=0;Update(f,sample);Check(Find<OrangeProgress>(f,"startup-progress").Indeterminate,"invalid item count returns to indeterminate");
    sample.Stage="scripts";Update(f,sample);Check(Find<Label>(f,"startup-stage").Text.Contains("脚本"),"verified loading stages appear in the phase heading");
    Check(Find<Label>(f,"startup-memory").Text.Contains("1.50 GB")&&Find<Label>(f,"startup-memory").Text.Contains("16.0 GB")&&Find<Label>(f,"startup-memory").Text.Contains("物理"),"memory label identifies measured process physical memory and system total");
    Check(Find<ProgressBar>(f,"startup-memory-bar").Value==93,"memory bar uses process working set over system RAM, not Java heap");
    Check(Find<Label>(f,"startup-heap").Text.Contains("8.00 GB")&&Find<Label>(f,"startup-heap").Text.Contains("配置"),"configured Java heap limit is labelled separately");
    sample.WorkingSetBytes=null;sample.TotalPhysicalBytes=null;sample.HeapLimitBytes=null;Update(f,sample);Check(Find<Label>(f,"startup-memory").Text.Contains("等待")&&Find<ProgressBar>(f,"startup-memory-bar").Value==0,"missing measured memory is explicitly waiting");
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
    sample.Lines=new string[90];for(int i=0;i<sample.Lines.Length;i++)sample.Lines[i]=i+new string('x',5000);Update(f,sample);string[] tail=Find<Label>(f,"startup-log").Text.Split(new[]{Environment.NewLine},StringSplitOptions.None);Check(tail.Length==6&&tail[0].StartsWith("84x")&&tail[0].Length==512,"fullscreen view keeps only six short recent log lines");
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
   foreach(bool light in new[]{false,true})foreach(int layout in new[]{0,1,2})using(Form f=NewWindow(light)){
    Update(f,Loading());f.ClientSize=layout==0?new Size(1920,1080):new Size(1280,720);
    if(layout==2)ScaleFonts(f,1.5f);
    f.StartPosition=FormStartPosition.Manual;f.Location=new Point(-20000,-20000);f.ShowInTaskbar=false;IntPtr foreground=GetForegroundWindow();f.Show();f.PerformLayout();var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<240){Application.DoEvents();Thread.Sleep(10);}
    Check(GetForegroundWindow()==foreground,"offscreen native show and periodic updates preserve the foreground window");
    using(var bitmap=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{Check(PrintWindow(f.Handle,dc,0),"native window capture renders successfully");}finally{g.ReleaseHdc(dc);}}bitmap.Save(Path.Combine(directory,(light?"light":"dark")+(layout==0?"-default":layout==1?"-minimum":"-minimum-scaled150")+".png"));}
    if(!light&&layout==0){Control brand=Find<Control>(f,"startup-brand");using(var first=new Bitmap(brand.Width,brand.Height))using(var next=new Bitmap(brand.Width,brand.Height)){
     brand.DrawToBitmap(first,brand.ClientRectangle);timer.Restart();while(timer.ElapsedMilliseconds<350){Application.DoEvents();Thread.Sleep(10);}brand.DrawToBitmap(next,brand.ClientRectangle);bool changed=false;for(int x=0;x<first.Width&&!changed;x++)for(int y=0;y<first.Height;y++)if(first.GetPixel(x,y)!=next.GetPixel(x,y)){changed=true;break;}
     bool animated=(bool)brand.GetType().GetField("animated",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(brand);Check(animated?changed:!changed,"brand animation advances real frames while respecting Windows reduced motion");first.Save(Path.Combine(directory,"animation-frame-1.png"));next.Save(Path.Combine(directory,"animation-frame-2.png"));
    }}
   }
  }
  static void ScaleFonts(Control control,float scale){var font=control.Font;foreach(Control child in control.Controls)ScaleFonts(child,scale);control.Font=new Font(font.FontFamily,font.Size*scale,font.Style);}
 }
}
