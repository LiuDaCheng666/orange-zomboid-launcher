using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// This probe deliberately has no reference to the launcher test assembly. Every
// resource, version, service and form below comes from the actual built setup.
static class LegacySetupBuildProbe {
 static int failed;
 const BindingFlags InternalInstance=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool condition,string message){Console.WriteLine((condition?"PASS: ":"FAIL: ")+message);if(!condition)failed++;}
 static void Attempt(string name,Action action){try{action();}catch(Exception error){failed++;Console.WriteLine("FAIL: "+name+"\n"+error);}}
 [STAThread] public static int Main(string[] args){
  string setup=Path.GetFullPath(args[0]),version=args[1],destination=Path.GetFullPath(args[2]);
  var assembly=Assembly.LoadFile(setup);
  Attempt("canonical embedded VERSION",()=>{
   using(var stream=assembly.GetManifestResourceStream("Launcher.Version.txt")){
    Check(stream!=null,"actual legacy setup embeds required Launcher.Version.txt");
    if(stream!=null)using(var reader=new StreamReader(stream))Check(reader.ReadToEnd().Trim()==version,"actual legacy setup resource version="+version);
   }
  });
  var info=FileVersionInfo.GetVersionInfo(setup);
  Check(assembly.GetName().Version.ToString()==version+".0","actual legacy setup assembly version="+version+".0 (actual="+assembly.GetName().Version+")");
  Check(info.FileVersion==version+".0","actual legacy setup file version="+version+".0 (actual="+info.FileVersion+")");
  Check(info.ProductVersion==version,"actual legacy setup product version="+version+" (actual="+info.ProductVersion+")");
  Check(assembly.GetManifestResourceInfo("Setup.Core.zip")==null,"actual no-argument build retains legacy resource route");
  foreach(string name in new[]{"Setup.Launcher.exe","Setup.Uninstall.exe","Setup.Guide.md"})Check(assembly.GetManifestResourceInfo(name)!=null,"actual legacy setup payload="+name);
  Attempt("AppVersion.Current from actual legacy setup",()=>Check((string)assembly.GetType("PZ3D.AppVersion",true).GetProperty("Current").GetValue(null,null)==version,"actual legacy AppVersion.Current="+version));
  object service=null;
  Attempt("InstallerProgram.Service from actual legacy setup",()=>{
   service=assembly.GetType("PZ3D.InstallerProgram",true).GetMethod("Service",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
   Check(service!=null,"actual legacy InstallerProgram.Service initializes");
  });
  Attempt("InstallerForm from actual legacy setup",()=>{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   var recommendationType=assembly.GetType("PZ3D.InstallerDestination",true);
   object recommendation=Activator.CreateInstance(recommendationType,true);
   recommendationType.GetField("Root",InternalInstance).SetValue(recommendation,destination);
   var constructor=assembly.GetType("PZ3D.InstallerForm",true).GetConstructors(InternalInstance).Single(c=>c.GetParameters().Length==3);
   Delegate backend=service==null?null:Delegate.CreateDelegate(constructor.GetParameters()[1].ParameterType,service,service.GetType().GetMethod("Install",InternalInstance));
   using(var form=(Form)constructor.Invoke(new object[]{recommendation,backend,null})){
    Check(form.Text.Contains(version),"actual legacy installer form shows canonical version="+version);
    var path=form.Controls.Find("installer-path",true).Single();
    Check(path.Text==destination,"actual legacy installer form retains exact fixture destination");
    Check(form.Controls.Find("installer-install",true).Single().Enabled,"actual legacy installer form starts ready");
    Check(!Directory.Exists(destination),"actual legacy service/form initialization creates no destination");
   }
  });
  Console.WriteLine("LEGACY SETUP BUILD: "+(failed==0?"PASS":"FAIL ("+failed+")"));return failed==0?0:1;
 }
}
