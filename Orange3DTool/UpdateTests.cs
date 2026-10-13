using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Text;
namespace PZ3D {
 static class UpdateTests {
  static void Check(bool v,string s){if(!v)throw new Exception("FAIL: "+s);Console.WriteLine("PASS: "+s);}
  public static void Run(){var type=typeof(Engine).Assembly.GetType("PZ3D.WorkshopUpdate");Check(type!=null,"non-Steam startup version planner exists");
   string vdf="\"AppWorkshop\" { \"WorkshopItemsInstalled\" { \"3809306528\" { \"manifest\" \"9102883971095548858\" } } \"WorkshopItemDetails\" { \"3809306528\" { \"subscribedby\" \"123\" } } }";
   var needs=type.GetMethod("NeedsDownload");Check(!(bool)needs.Invoke(null,new object[]{vdf,3809306528UL,"9102883971095548858","123",true}),"same installed/subscribed manifest skips downloading");Check((bool)needs.Invoke(null,new object[]{vdf,3809306528UL,"999","123",true}),"changed remote manifest requests update");Check((bool)needs.Invoke(null,new object[]{vdf,3809306528UL,"9102883971095548858","456",true}),"different Steam account cannot treat cached item as subscribed");Check((bool)needs.Invoke(null,new object[]{vdf,3809306528UL,"9102883971095548858","123",false}),"missing content never treated as reusable");
   var steam=typeof(SteamWorkshop).GetMethod("CanReuse");Check(steam!=null&&(bool)steam.Invoke(null,new object[]{5U})&&!(bool)steam.Invoke(null,new object[]{13U})&&!(bool)steam.Invoke(null,new object[]{21U}),"Steam download path skips current content but never skips pending updates");
   var worker=typeof(Engine).Assembly.GetType("PZ3D.WorkshopWorker");Check(worker!=null,"Steam API isolated from GUI in exiting worker");
   var info=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--workshop-worker \"C:\\orange-no-game-fixture\" 999"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
   using(var child=new Process{StartInfo=info}){child.Start();if(!child.WaitForExit(10000)){child.Kill();throw new Exception("FAIL: hidden worker must exit on invalid request before Steam initialization");}string error=child.StandardError.ReadToEnd();Check(child.ExitCode==1,"hidden redirected worker starts without a console and returns controlled validation failure");bool chinese=false;foreach(char c in error)if(c>='\u4e00'&&c<='\u9fff')chinese=true;Check(chinese&&!error.Contains("\uFFFD")&&!error.Contains("System.IO.IOException"),"worker failure text survives UTF-8 pipes without mojibake or encoding crash");}
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
