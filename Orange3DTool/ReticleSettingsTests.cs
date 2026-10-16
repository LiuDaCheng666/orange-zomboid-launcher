using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
namespace PZ3D {
 static class ReticleSettingsTests {
  // Historical properties parsing stays covered; current launcher actions never deploy it.
  public static void Run(){var s=new ReticleSettings();string props=s.Properties("E:\\Steam\\ISeeThem.jar");Check(props.Contains("aim.mode=native")&&props.Contains("aim.key=0"),"historical bridge parser defaults to original aiming and unbound shortcut");s.KeyText="CTRL+F8";props=s.Properties("E:\\Steam\\ISeeThem.jar");Check(props.Contains("aim.key=66")&&props.Contains("aim.ctrl=true")&&props.Contains("shoot.jar=E:\\\\Steam\\\\ISeeThem.jar"),"historical parser converts scan code and escapes author path");s.KeyText="CTRL+UNKNOWN";bool rejected=false;try{s.Properties("source");}catch{rejected=true;}Check(rejected,"historical unsupported shortcut is rejected");var f=new AimBridgeRetirementTests.Fixture();f.OldAddon();using(var c=new ModernController(f.E.Game,f.E.User,f.E.Data,true)){c.Execute("reticleSave",new Dictionary<string,object>{{"keyText","INVALID"},{"startNative",false}});Check(!File.Exists(Path.Combine(f.E.Data,"aim-fallback.json"))&&!File.Exists(f.Jar),"obsolete aim action removes private settings without parsing or redeploying");}AimBridgeRetirementTests.Put(Path.Combine(f.E.User,"mods",PresentationBridge.Id,"42/mod.info"),"");new PresentationBridge().Enable(f.E,f.Source);Check(!File.Exists(Path.Combine(f.E.User,"mods",PresentationBridge.Id,"42/mod.info")),"obsolete enable retires corrupt owned metadata instead of repairing deployment");}
  static void Check(bool okay,string label){if(!okay)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
 }
}
