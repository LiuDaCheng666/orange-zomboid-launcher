using System;
using System.Reflection;
namespace PZ3D {
 static class StatusTests {
  static void Check(bool value,string text){if(!value)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);}
  public static void Run(){
   var type=typeof(Engine).Assembly.GetType("PZ3D.RuntimeEvidence");
   Check(type!=null,"runtime evidence evaluator exists");
   Func<bool,bool,string,bool,string> evaluate=(config,loader,log,current)=>(string)type.GetMethod("Evaluate").Invoke(null,new object[]{config,loader,log,current});
   Check(evaluate(true,true,"[Viewpoint] loaded",false).Contains("下次启动启用")&&!evaluate(true,true,"[Viewpoint] loaded",false).Contains("已加载"),"old logs leave the configured next-launch status, not loaded");
   Check(evaluate(true,true,"[Viewpoint] loaded",true).Contains("已加载"),"current startup reports Viewpoint loaded");
   Check(evaluate(true,true,"[Viewpoint] loaded\nrequired mod \"Viewpoint\" not found",true).Contains("失败"),"later multiplayer missing-mod error overrides startup success");
   Check(evaluate(true,true,"Found patch class: viewpoint.Main",true).Contains("下次启动启用"),"discovery alone remains a next-launch configuration");
   Check(evaluate(true,true,"[Viewpoint] loaded\nZomboidFileSystem.loadMods( NeatUI )",true).Contains("清单已切换"),"server mod reset has its own neutral transition status");
   Check(evaluate(true,false,"",false).Contains("不完整"),"missing native loader is not enabled");
   Check(evaluate(false,true,"",false).Contains("未启用"),"disabled configuration is reported separately");
   Check(evaluate(false,false,"[Viewpoint] loaded",true).Contains("已加载"),"running game state remains loaded even if next-launch configuration is off");
   var format=typeof(SteamWorkshop).GetMethod("FormatDownloadProgress");Check(format!=null,"download presentation handles known and unknown byte totals");
   string unknown=(string)format.Invoke(null,new object[]{3809306528UL,0UL,0UL});Check(unknown.Contains("Viewpoint")&&unknown.Contains("等待")&&!unknown.Contains("%"),"unknown Steam total is waiting, not fake percentage");
   string known=(string)format.Invoke(null,new object[]{3810302175UL,1048576UL,4194304UL});Check(known.Contains("物件")&&known.Contains("1.0 / 4.0 MB"),"real downloaded bytes are displayed with player-readable component name");
   Program.PreviewMode=true;
   using(var form=new MainForm(System.IO.Path.Combine(LauncherTestPaths.Root,"orange-status-test-"+Guid.NewGuid().ToString("N")))){
    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
    typeof(MainForm).GetField("busy",flags).SetValue(form,true);
    typeof(MainForm).GetMethod("StartVisibleTask",flags).Invoke(form,new object[]{"正在连接 Steam"});
    var task=(System.Windows.Forms.Label)typeof(MainForm).GetField("taskStatus",flags).GetValue(form);
    var bar=(OrangeProgress)typeof(MainForm).GetField("progress",flags).GetValue(form);
    Check(task.Text.Contains("Steam")&&bar.Indeterminate,"task is visible immediately with animated unknown progress");
    var retry=(System.Windows.Forms.Button)typeof(MainForm).GetField("retryTask",flags).GetValue(form);Check(!retry.Enabled,"retry is disabled during active work");
    var cancel=(System.Windows.Forms.Button)typeof(MainForm).GetField("cancelTask",flags).GetValue(form);
    typeof(System.Windows.Forms.Button).GetMethod("OnClick",flags).Invoke(cancel,new object[]{EventArgs.Empty});
    var token=(System.Threading.CancellationTokenSource)typeof(MainForm).GetField("downloadCancel",flags).GetValue(form);Check(token.IsCancellationRequested,"cancel remains usable while ordinary action buttons are busy");
    typeof(MainForm).GetMethod("FinishVisibleTask",flags).Invoke(form,new object[]{false,"测试网络失败"});Check(!bar.Indeterminate&&task.Text.Contains("失败"),"failed task stops animation and keeps visible failure reason");
   }
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e.Message);return 1;}}
 }
}
