using System;
using System.IO;
using System.Linq;
namespace PZ3D {
 static class PresentationBridgeTests {
  static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  public static void Run(){var f=new AimBridgeRetirementTests.Fixture();f.OldAddon();byte[] config=File.ReadAllBytes(f.E.Config),author=File.ReadAllBytes(Path.Combine(f.Source,"mods",ShootClient.Id,"42/media/java/client/ISeeThem.jar"));var bridge=new PresentationBridge();string result=bridge.Enable(f.E,"missing source must never be inspected");Check(!bridge.Enabled(f.E)&&!File.Exists(f.Jar)&&!File.Exists(f.Journal)&&!File.ReadAllText(f.E.Mods).Contains("mod = OrangeShootPresentation,"),"obsolete Buddy bridge enable performs retirement only");Check(File.ReadAllBytes(f.E.Config).SequenceEqual(config)&&File.ReadAllBytes(Path.Combine(f.Source,"mods",ShootClient.Id,"42/media/java/client/ISeeThem.jar")).SequenceEqual(author)&&File.Exists(f.Unknown),"retirement keeps author bytes launcher arguments and unknown files");Check(result.Contains("退役")&&Directory.Exists(bridge.LastBackup),"retired bridge returns a recoverable cleanup receipt");}
 }
}
