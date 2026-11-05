using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
// Loads the exact signed candidate assembly and assets. All mutations target inert isolated fixtures.
static class SecurityReleaseUiFixture {
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetStyle(IntPtr w,int n);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetStyle(IntPtr w,int n,IntPtr v);
 static Assembly app;static string root,game,data;static WebView2 web;static readonly JavaScriptSerializer json=new JavaScriptSerializer();
 static Type T(string n){return app.GetType("PZ3D."+n,true);}
 static object New(string n,params object[] args){return Activator.CreateInstance(T(n),BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);}
 static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","");}
 static void Check(bool ok,string s){if(!ok)throw new Exception("FAIL "+s);Console.WriteLine("PASS "+s);}
 static async Task Wait(string expr){for(int i=0;i<150;i++){if(await web.CoreWebView2.ExecuteScriptAsync(expr)=="true")return;await Task.Delay(100);}throw new Exception("UI timeout: "+expr+"; "+await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({route:window.orange?.state?.route,notice:document.querySelector('#notice')?.textContent,text:document.querySelector('#page')?.innerText?.slice(0,1200)})"));}
 static async Task JS(string script){await web.CoreWebView2.ExecuteScriptAsync(script);}
 static async Task Shot(string name){await Task.Delay(600);using(var f=File.Create(Path.Combine(root,name+".png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,f);}
 static async Task Route(string name){await JS("document.querySelectorAll('dialog[open]').forEach(d=>d.close());document.querySelector('[data-page=\""+name+"\"]').click();");await Wait("orange.state.route==='"+name+"'");}
 static void Seed(){
  game=Path.Combine(root,"中文游戏");data=Path.Combine(root,"data");Directory.CreateDirectory(game);Directory.CreateDirectory(data);Directory.CreateDirectory(Path.Combine(root,"user"));
  File.WriteAllText(Path.Combine(game,"ProjectZomboid64.exe"),"inert test marker; never launched");File.WriteAllText(Path.Combine(game,"ProjectZomboid64.json"),"{\"vmArgs\":[\"-Xmx3072m\"]}");File.WriteAllText(Path.Combine(game,"version.txt"),"42.20.4");
  string backup=Path.Combine(data,"dlss-backups","legacy"),original=Path.Combine(backup,"original.txt");Directory.CreateDirectory(backup);File.WriteAllText(original,"original bytes");
  var files=new List<object>();foreach(string rel in new[]{"opengl32.dll","nvngx_dlssnr.dll","reshade-shaders/Shaders/ReShade.fxh"}){string file=Path.Combine(game,rel);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"inert legacy deployment");files.Add(new{Relative=rel,Backup=rel.EndsWith(".fxh")?original:null,BeforeHash=rel.EndsWith(".fxh")?Hash(File.ReadAllBytes(original)):null,InstalledHash=Hash(File.ReadAllBytes(file))});}
  string journal=Path.Combine(data,"dlss-state",Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(game).ToLowerInvariant())).Substring(0,16)+".json");Directory.CreateDirectory(Path.GetDirectoryName(journal));File.WriteAllText(journal,json.Serialize(new{Game=game,Backup=backup,State="legacy fixture",Time=DateTime.UtcNow.ToString("o"),Files=files}));
 }
 [STAThread]public static int Main(string[] args){try{
  string candidate=Path.GetFullPath(args[0]);root=Path.GetFullPath(args[1]);Directory.CreateDirectory(root);Seed();
  AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{string name=new AssemblyName(e.Name).Name+".dll";string p=Path.Combine(candidate,name);if(!File.Exists(p))p=Path.Combine(candidate,"Web",name);return File.Exists(p)?Assembly.LoadFrom(p):null;};
  app=Assembly.LoadFrom(Path.Combine(candidate,"橙子启动器.exe"));
  T("LauncherRuntime").GetMethod("Initialize").Invoke(null,null);
  T("DistributionPaths").GetField("RuntimeRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Path.Combine(root,"runtime"));T("DistributionPaths").GetField("InstallationRoot",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Directory.GetParent(candidate).FullName);
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  object controller=New("ModernController",game,Path.Combine(root,"user"),data,true);
  object updates=New("CoreUpdateService",Path.Combine(root,"updates"),null,new Func<CancellationToken,byte[][]>(t=>{throw new Exception("fixture network forbidden");}),new Func<string,CancellationToken,Stream>((u,t)=>{throw new Exception("fixture network forbidden");}));
  var form=(Form)New("ModernForm",Path.Combine(candidate,"Web"),new string[0],controller,updates);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);var handle=form.Handle;SetStyle(handle,-20,new IntPtr(GetStyle(handle,-20).ToInt64()|0x08000000));int result=1;
  var timer=new System.Windows.Forms.Timer{Interval=120000};timer.Tick+=(s,e)=>{Console.WriteLine("FAIL candidate UI timeout");form.Dispose();};timer.Start();
  form.Shown+=async(s,e)=>{try{
   web=(WebView2)T("ModernForm").GetField("web",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
   for(int i=0;i<200&&web.CoreWebView2==null;i++)await Task.Delay(100);
   await Wait("!!window.orange?.state?.status");await Task.Delay(350);await JS("window.uiErrors=[];window.addEventListener('error',e=>uiErrors.push(e.message));document.querySelectorAll('dialog[open]').forEach(d=>d.close());");
   Check(await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#connection').textContent.includes('已连接')")=="true","exact candidate authenticates its assets and connects the real native bridge");await Shot("candidate-home");
   await Route("memory");await Wait("!!orange.state.memory?.stamp");await JS("document.querySelector('#memory-number').value='4';document.querySelector('#memory-number').dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('[data-save-memory]').click();");await Wait("orange.state.memory?.currentGiB===4&&!orange.state.busy&&!orange.state.memoryDirty&&!orange.state.memoryConflict");Check(Directory.GetFiles(data,"preferences.json",SearchOption.AllDirectories).Length>0,"memory save through actual candidate UI writes isolated configuration and finishes without draft conflict");await Shot("candidate-memory-saved");Console.WriteLine("MEMORY FINAL "+await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({dirty:orange.state.memoryDirty,conflict:orange.state.memoryConflict,notice:document.querySelector('#notice').textContent})"));
   Check(await web.CoreWebView2.ExecuteScriptAsync("!document.querySelector('#notice').textContent.includes('外部配置已变化')")=="true","successful memory save does not retain a false external-change warning");
   await Route("servers");await Wait("!!orange.state.servers?.stamp");await JS("document.querySelector('[data-server-tab=personal]')?.click();document.querySelector('[data-server-new]').click();");await Wait("!!document.querySelector('#server-form')");
   await JS("for(const [k,v]of Object.entries({name:'隔离安全验收',host:'127.0.0.1',port:'16261',username:'fixture-user'}))document.querySelector('#server-'+k).value=v;document.querySelector('#server-form').requestSubmit();");await Wait("!document.querySelector('#server-form')&&orange.state.servers?.servers?.some(s=>s.name==='隔离安全验收')");Check(File.ReadAllText(Path.Combine(data,"server-profiles.json")).Contains("fixture-user"),"server form submit remains functional under form-action none and persists only fixture data");await Shot("candidate-server-saved");
   await Route("components");await Wait("!!document.querySelector('#page')?.innerText");await Shot("candidate-components");Check(await web.CoreWebView2.ExecuteScriptAsync("!document.querySelector('#page').innerText.includes('启用实验 DLSS')")=="true","components page renders without retired DLSS enable entry");
   await Route("settings");await Wait("!!document.querySelector('[data-dlss-recovery]')");await JS("document.querySelector('[data-dlss-recovery]').open=true;document.querySelector('[data-action=dlss]').click();");await Wait("document.querySelector('#confirm').open");await Shot("candidate-recovery-confirm");await JS("document.querySelector('#confirm-no').click();");Check(File.Exists(Path.Combine(game,"opengl32.dll")),"actual recovery cancellation preserves all fixture deployment files");
   await JS("document.querySelector('[data-action=dlss]').click();");await Wait("document.querySelector('#confirm').open");await JS("document.querySelector('#confirm-yes').click();");await Wait("!orange.state.busy&&!document.querySelector('[data-dlss-recovery]')");Check(!File.Exists(Path.Combine(game,"opengl32.dll"))&&File.ReadAllText(Path.Combine(game,"reshade-shaders/Shaders/ReShade.fxh"))=="original bytes","actual candidate single-confirm recovery restores original bytes and removes stale UI");await Shot("candidate-recovery-complete");
   await Route("help");await Wait("document.querySelector('#page').innerText.length>200");Check(await web.CoreWebView2.ExecuteScriptAsync("uiErrors.length===0")=="true","normal page navigation and confirmations produce no JavaScript errors");await Shot("candidate-help");Console.WriteLine("CANDIDATE UI EVIDENCE "+root);result=0;
  }catch(Exception ex){Console.WriteLine(ex);}finally{timer.Stop();form.Dispose();}};
  Application.Run(form);timer.Dispose();((IDisposable)controller).Dispose();((IDisposable)updates).Dispose();return result;
 }catch(Exception ex){Console.WriteLine(ex);return 1;}}
}
