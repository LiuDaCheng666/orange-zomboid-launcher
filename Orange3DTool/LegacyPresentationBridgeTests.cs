using System;
using System.IO;
namespace PZ3D {
 static class LegacyPresentationBridgeTests {
  static void Check(bool value,string label){if(!value)throw new Exception("FAIL: "+label);Console.WriteLine("PASS: "+label);}
  public static void Run(){var f=new AimBridgeRetirementTests.Fixture();string jar=Path.Combine(f.E.Game,PresentationBridge.Name),properties=Path.Combine(f.E.Game,PresentationBridge.ConfigName);AimBridgeRetirementTests.Put(jar,"legacy fixture");AimBridgeRetirementTests.Put(properties,"edited legacy settings");AimBridgeRetirementTests.Put(f.Journal,"{\"jarHash\":\"old\",\"propertiesHash\":\"old\"}");AimBridgeRetirementTests.Put(f.E.Config,"{\"vmArgs\":[\"-agentlib:zbNative\",\"-Xmx12345m\",\"-Dother=true\",\"-javaagent:OrangeShootPresentation.jar\"]}");var bridge=new LegacyPresentationBridge();string result=bridge.Enable(f.E,"no author dependency");Check(!File.Exists(jar)&&!File.Exists(properties)&&!File.Exists(f.Journal)&&!File.ReadAllText(f.E.Config).Contains("-javaagent:OrangeShootPresentation.jar"),"legacy compatibility enable retires owned files and only its argument");Check(File.ReadAllText(f.E.Config).Contains("-Xmx12345m")&&File.ReadAllText(f.E.Config).Contains("-agentlib:zbNative")&&File.ReadAllText(f.E.Mods).Contains(ShootClient.Id),"legacy retirement preserves Buddy memory and author shooting component");Check(Directory.Exists(bridge.LastBackup)&&result.Contains("备份"),"legacy edited settings retain recovery backup");}
 }
}
