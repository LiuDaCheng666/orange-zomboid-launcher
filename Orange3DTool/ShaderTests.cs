using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
namespace PZ3D {
 static class ShaderTests {
  static void Check(bool value,string text){if(!value)throw new Exception("FAIL: "+text);Console.WriteLine("PASS: "+text);}
  public static void Run(){
   PropertyBytes();
   var type=typeof(Engine).Assembly.GetType("PZ3D.ShaderSelection");Check(type!=null,"shader selection manager exists");
   string root=Path.Combine(LauncherTestPaths.Root,"orange-shaders-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"viewpoint-shaderpacks"));string zip=Path.Combine(root,"viewpoint-shaderpacks","Eclipse-Viewpoint-jianghuiban-v3.zip");using(var a=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var w=new StreamWriter(a.CreateEntry("shaders/test.fsh").Open()))w.Write("fixture");}
   string config=Path.Combine(root,"viewpoint-live.properties");File.WriteAllText(config,"# untouched\ngraphics.mode=Modern\ngraphics.pack=vivid\ngraphics.preset=Ultra\nunknown=keep\n");
   Func<object> load=()=>Activator.CreateInstance(type,new object[]{root,Path.Combine(root,"backups")});object store=load();var select=type.GetMethod("Select");select.Invoke(store,new object[]{"minecraft-Eclipse-Viewpoint-jianghuiban-v3",false});Check(File.ReadAllText(config).Contains("graphics.pack=minecraft-Eclipse-Viewpoint-jianghuiban-v3")&&File.ReadAllText(config).Contains("unknown=keep"),"shader choice writes actual pack ID and preserves unrelated values");
   Check(Directory.GetFiles(Path.Combine(root,"backups"),"viewpoint-live.properties",SearchOption.AllDirectories).Length==1,"shader switching backs up original");
   store=load();string before=File.ReadAllText(config);bool running=false;try{select.Invoke(store,new object[]{"default",true});}catch(TargetInvocationException){running=true;}Check(running&&File.ReadAllText(config)==before,"running game blocks shader switching");
   File.AppendAllText(config,"external=true\n");bool stale=false;try{select.Invoke(store,new object[]{"default",false});}catch(TargetInvocationException){stale=true;}Check(stale,"external shader edit blocks stale overwrite");
   store=load();bool absent=false;try{select.Invoke(store,new object[]{"minecraft-missing",false});}catch(TargetInvocationException){absent=true;}Check(absent,"missing shader pack cannot become selected");
   string marker=Path.Combine(root,"default.marker");store=load();type.GetMethod("DefaultOnce").Invoke(store,new object[]{marker,false});store=load();select.Invoke(store,new object[]{"default",false});store=load();type.GetMethod("DefaultOnce").Invoke(store,new object[]{marker,false});Check(File.ReadAllText(config).Contains("graphics.pack=default"),"default initialized once never resets player choice");
   File.Delete(config);store=load();select.Invoke(store,new object[]{"minecraft-Eclipse-Viewpoint-jianghuiban-v3",false});Check(File.ReadAllText(config).Contains("graphics.mode=Modern"),"first install can initialize only known shader keys");
   store=load();select.Invoke(store,new object[]{"normal",false});Check(File.ReadAllText(config).Contains("graphics.mode=Modern")&&File.ReadAllText(config).Contains("graphics.pack=normal"),"basic pack fallback retains verified Modern mode");
   var guide=typeof(Engine).Assembly.GetType("PZ3D.StartupHelp");Check(guide!=null,"startup guidance manager exists");string preference=Path.Combine(root,"hide-help.txt");Check((bool)guide.GetMethod("ShouldShow").Invoke(null,new object[]{preference}),"startup help shown for a new player");guide.GetMethod("SetHidden").Invoke(null,new object[]{preference,true});Check(!(bool)guide.GetMethod("ShouldShow").Invoke(null,new object[]{preference}),"do not show preference persists");
   File.AppendAllText(config,"keys.firstPerson=CTRL+P\nkeys.settings=F8\n");string body=(string)guide.GetMethod("Body").Invoke(null,new object[]{root});Check(body.Contains("CTRL+P")&&body.Contains("F8"),"startup guidance uses saved bindings instead of hardcoded defaults");
  }
  // Break caught: selection normalizes unrelated bytes, loses a BOM/newline, or writes/backups an already-matching file.
  static void PropertyBytes(){
   string root=Path.Combine(LauncherTestPaths.Root,"orange-shader-bytes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   var cases=new[]{
    new[]{"# keep\ngraphics.pack=vivid\ngraphics.mode=Modern\nunknown=原值\n","# keep\ngraphics.pack=normal\ngraphics.mode=Modern\nunknown=原值\n"},
    new[]{"# keep\r\ngraphics.mode=Modern\r\nunknown=keep\r\n","# keep\r\ngraphics.mode=Modern\r\nunknown=keep\r\ngraphics.pack=normal\r\n"},
    new[]{"# keep\ngraphics.pack=normal","# keep\ngraphics.pack=normal\r\ngraphics.mode=Modern\r\n"},
    new[]{"unknown=keep\r","unknown=keep\rgraphics.pack=normal\r\ngraphics.mode=Modern\r\n"},
    new[]{"","graphics.pack=normal\r\ngraphics.mode=Modern\r\n"},
    new[]{"graphics.mode=Modern\ngraphics.pack=normal\nunknown=keep\n","graphics.mode=Modern\ngraphics.pack=normal\nunknown=keep\n"}
   };
   for(int i=0;i<cases.Length;i++)foreach(bool bom in new[]{false,true}){string dir=Path.Combine(root,i+"-"+bom),backup=Path.Combine(dir,"backups"),file=Path.Combine(dir,"viewpoint-live.properties");Directory.CreateDirectory(dir);byte[] prefix=bom?new byte[]{0xef,0xbb,0xbf}:new byte[0],original=prefix.Concat(Encoding.UTF8.GetBytes(cases[i][0])).ToArray(),expected=prefix.Concat(Encoding.UTF8.GetBytes(i==4&&bom?"\r\ngraphics.pack=normal\r\ngraphics.mode=Modern\r\n":cases[i][1])).ToArray();File.WriteAllBytes(file,original);new ShaderSelection(dir,backup).Select("normal",false);Check(File.ReadAllBytes(file).SequenceEqual(expected),"shader literal property bytes case "+i+" BOM="+bom);if(i==5)Check(!Directory.Exists(backup),"matching shader creates no backup or write BOM="+bom);else{string saved=Directory.GetFiles(backup,"viewpoint-live.properties",SearchOption.AllDirectories).Single();Check(File.ReadAllBytes(saved).SequenceEqual(original),"shader backup retains exact original case "+i+" BOM="+bom);}new ShaderSelection(dir,backup).Select("normal",false);Check(File.ReadAllBytes(file).SequenceEqual(expected),"repeated shader selection retains exact bytes case "+i+" BOM="+bom);}
  }
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
