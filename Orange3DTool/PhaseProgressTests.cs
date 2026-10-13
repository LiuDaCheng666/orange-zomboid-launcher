using System;using System.IO;
namespace PZ3D {static class PhaseProgressTests {
 public static void Run(){string root=Path.Combine(LauncherTestPaths.Root,"phase-"+Guid.NewGuid().ToString("N"));var e=new Engine(Path.Combine(root,"game"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Directory.CreateDirectory(e.Game);File.WriteAllText(e.Config,"{\"vmArgs\":[\"-Xmx4g\"]}");int fabricated=0,phase=0;e.Progress=(value,text)=>{phase++;if(value>0&&value<100)fabricated++;};e.Switch(false,false);if(phase==0||fabricated!=0)throw new Exception("FAIL: fixed phase milestones must not claim measured byte/count progress");Console.WriteLine("PASS: fixed install phases report unknown rather than fabricated measured percentage");}
 public static int Main(){try{Run();return 0;}catch(Exception x){Console.WriteLine(x);return 1;}}
}}
