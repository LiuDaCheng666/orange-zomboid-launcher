using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
namespace PZ3D {
 // Own offscreen test window only. Never attaches to or captures the user's desktop/game.
 static class BrowserContainmentNativeTests {
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr GetStyle(IntPtr window,int index);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr SetStyle(IntPtr window,int index,IntPtr value);
  static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Console.WriteLine("PASS "+name);}
  static string root;
  [STAThread]public static int Main(string[] args){
   try{
    if(args.Length!=2)throw new ArgumentException("source root and staged assets required");
    root=Path.Combine(LauncherTestPaths.Root,"browser-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(root);
    string assets=args[1],game=Path.Combine(root,"game"),user=Path.Combine(root,"user"),data=Path.Combine(root,"data");FeatureTests.Seed(game,data);Directory.CreateDirectory(user);
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    int result=1;
    using(var controller=new ModernController(game,user,data,true))
    using(var updates=new CoreUpdateService(Path.Combine(root,"updates"),null,token=>{throw new Exception("Test forbids update network");},(url,token)=>{throw new Exception("Test forbids update network");}))
    using(var form=new ModernForm(assets,new string[0],controller,updates)){
     form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-30000,-30000);
     // Prevent activation of this test-owned offscreen window.
     var handle=form.Handle;SetStyle(handle,-20,new IntPtr(GetStyle(handle,-20).ToInt64()|0x08000000));
     var timeout=new System.Windows.Forms.Timer{Interval=45000};timeout.Tick+=(s,e)=>{Console.WriteLine("FAIL native browser timeout");timeout.Stop();form.Dispose();};timeout.Start();
     form.Shown+=async(s,e)=>{try{
      var web=(WebView2)typeof(ModernForm).GetField("web",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
      for(int i=0;i<150&&(web.CoreWebView2==null||await web.CoreWebView2.ExecuteScriptAsync("typeof window.orange==='object'")!="true");i++)await Task.Delay(100);
      Check(web.CoreWebView2!=null&&await web.CoreWebView2.ExecuteScriptAsync("typeof window.orange==='object'")=="true","production WebView serves real local app under resource allowlist");
      await web.CoreWebView2.ExecuteScriptAsync("window.nativeRead=null;orange.request('coreUpdateStatus',{protocol:1}).then(value=>window.nativeRead={ok:typeof value.state==='string'},error=>window.nativeRead={ok:false,error:error.message});");
      for(int i=0;i<100&&await web.CoreWebView2.ExecuteScriptAsync("window.nativeRead!==null")!="true";i++)await Task.Delay(100);
      Check(await web.CoreWebView2.ExecuteScriptAsync("window.nativeRead?.ok===true")=="true","real page completes a read through the actual WebView native message bridge");
      var settings=web.CoreWebView2.Settings;
      Check(!settings.AreHostObjectsAllowed&&!settings.IsPasswordAutosaveEnabled&&!settings.IsGeneralAutofillEnabled&&!settings.AreDefaultScriptDialogsEnabled,"native browser disables host objects, password saving, autofill and script dialogs");
      Check(await web.CoreWebView2.ExecuteScriptAsync("(()=>{const d=document.createElement('dialog');document.body.append(d);d.showModal();const opened=d.open;d.close();const closed=!d.open;d.remove();document.querySelector('#ozl-update-announcement[open] [data-announcement-close]')?.click();return opened&&closed;})()") == "true","HTML modal dialogs still open and close with native script dialogs disabled");
      await web.CoreWebView2.ExecuteScriptAsync(@"window.compatImages=null;(async()=>{try{const local=new Image();local.src='/home.png';await local.decode();const values=Object.values(JSON.parse(document.querySelector('#home-scene-data')?.textContent||'{}')).filter(s=>typeof s==='string'&&s.startsWith('data:image/png;base64,'));if(!values.length)values.push('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6jX8AAAAASUVORK5CYII=');for(const src of values){const img=new Image();img.src=src;document.body.append(img);await img.decode();img.remove();}window.compatImages={ok:true,count:values.length};}catch(e){window.compatImages={ok:false,error:e.message};}})();");
      for(int i=0;i<100&&await web.CoreWebView2.ExecuteScriptAsync("window.compatImages!==null")!="true";i++)await Task.Delay(100);
      Check(await web.CoreWebView2.ExecuteScriptAsync("window.compatImages?.ok===true")=="true","local home image and data image previews decode under the real native resource policy");
      Console.WriteLine("COMPAT IMAGES "+await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.compatImages)"));
      int deniedExtra=0;
      web.CoreWebView2.WebResourceRequested+=(sender,request)=>{if(request.Request.Uri.Contains("/extra.js")&&request.Response!=null&&request.Response.StatusCode==403)deniedExtra++;};
      await web.CoreWebView2.ExecuteScriptAsync(@"window.policyViolations=[];document.addEventListener('securitypolicyviolation',e=>policyViolations.push(e.effectiveDirective));window.__badLocal=false;let x=document.createElement('script');x.src='/extra.js';document.body.append(x);let inline=document.createElement('script');inline.textContent='window.__badInline=true';document.body.append(inline);let f=document.createElement('iframe');f.src='data:text/html,<p>blocked</p>';document.body.append(f);let form=document.createElement('form');form.action='https://orange.local/index.html';form.method='POST';document.body.append(form);form.submit();try{new Worker('/app.js');}catch(e){};fetch('https://blocked.invalid/collect').catch(()=>{});");
      await Task.Delay(500);
      Console.WriteLine("PROBE extra denied="+deniedExtra+" state="+await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({badLocal:window.__badLocal,violations:window.policyViolations})"));
      Check(deniedExtra>0&&await web.CoreWebView2.ExecuteScriptAsync("window.__badLocal===false")=="true","real native request handler denies an added same-origin script with 403");
      Check(await web.CoreWebView2.ExecuteScriptAsync("window.__badInline!==true&&policyViolations.includes('script-src-elem')")=="true","inline payload rejected by actual browser CSP");
      Check(await web.CoreWebView2.ExecuteScriptAsync("policyViolations.includes('frame-src')&&policyViolations.includes('worker-src')&&policyViolations.includes('form-action')&&policyViolations.includes('connect-src')")=="true","actual browser rejects child frame, worker, form submission and script network");
      await web.CoreWebView2.ExecuteScriptAsync("document.querySelectorAll('dialog[open]').forEach(d=>d.close());");
      using(var stream=File.Create(Path.Combine(root,"native-browser.png")))await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png,stream);
      Console.WriteLine("NATIVE BROWSER EVIDENCE "+root);result=0;
     }catch(Exception ex){Console.WriteLine(ex);}finally{timeout.Stop();form.Dispose();}};
     Application.Run(form);timeout.Dispose();
    }
    return result;
   }catch(Exception e){Console.WriteLine(e);return 1;}
  }
 }
}
