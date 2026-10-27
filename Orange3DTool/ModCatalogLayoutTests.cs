using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PZ3D {
 static class ModCatalogLayoutTests {
  static int assertions;
  static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception("FAIL: "+message);Console.WriteLine("PASS: "+message);}
  static void Put(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text,new UTF8Encoding(false));}
  static Engine Fixture(string version){string root=Path.Combine(Path.GetTempPath(),"ozl-layout-"+Guid.NewGuid().ToString("N"));Console.WriteLine("FIXTURE "+root);var e=new Engine(Path.Combine(root,"steam/steamapps/common/ProjectZomboid"),Path.Combine(root,"user"),Path.Combine(root,"data"),true);Put(Path.Combine(e.Game,"game-version.txt"),version);return e;}
  static string Mod(Engine e,string item,string folder){return Path.Combine(e.Workshop,item,"mods",folder);}
  static SaveProfiles.PreflightCatalog Read(Engine e,params string[] ids){var warnings=new List<string>();var budget=new LaunchReadBudget(67108864,100000);var result=SaveProfiles.SnapshotEntries(e,ids,warnings,budget);Check(!budget.Incomplete&&warnings.Count==0,"layout discovery completes without hiding a read failure");return result;}
  static void CommonOnly(){
   var e=Fixture("42.21.0");string root=Mod(e,"101","CommonOnly");Put(Path.Combine(root,"common/mod.info"),"id=CommonOnly\nname=Common metadata\nrequire=Dependency\n");
   var snapshot=Read(e,"CommonOnly");Check(snapshot.Candidates.Count(c=>c.Id=="CommonOnly")==1&&snapshot.Entries.Single(c=>c.id=="CommonOnly").requires.SequenceEqual(new[]{"Dependency"}),"B42 common-only mod is captured with its exact identity and dependencies");
   Check(SaveProfiles.Entries(e,new List<string>()).Count(c=>c.id=="CommonOnly")==1,"ordinary catalog also includes B42 common-only metadata");
  }
  static void LegacyDuplicate(){
   var e=Fixture("42.21.0");Put(Path.Combine(Mod(e,"201","Old"),"mod.info"),"id=Shared\nname=Legacy root\n");string current=Mod(e,"202","Current");Put(Path.Combine(current,"common/mod.info"),"id=Shared\nname=Current common\n");Directory.CreateDirectory(Path.Combine(current,"42"));Directory.CreateDirectory(Path.Combine(current,"42.13"));
   var snapshot=Read(e,"Shared");Check(snapshot.Candidates.Count(c=>c.Id=="Shared")==1&&snapshot.Candidates.Single(c=>c.Id=="Shared").Workshop=="202","B42 ignores a root-only legacy duplicate and retains the real B42 Workshop source");
   Check(SaveProfiles.Entries(e,new List<string>()).Count(c=>c.id=="Shared")==1,"ordinary catalog does not report a false cross-build source conflict");
  }
  static void GenuineDuplicate(){
   var e=Fixture("42.21.0");Put(Path.Combine(Mod(e,"301","First"),"42/mod.info"),"id=Duplicate\n");Put(Path.Combine(Mod(e,"302","Second"),"42.13/mod.info"),"id=Duplicate\n");
   Check(Read(e,"Duplicate").Candidates.Count(c=>c.Id=="Duplicate")==2,"two compatible B42 Workshop sources remain ambiguous instead of ranking different authors by directory version");
   Check(SaveProfiles.Entries(e,new List<string>()).Count(c=>c.id=="Duplicate"&&c.conflict)==2,"ordinary catalog preserves the genuine duplicate-source warning");
  }
  static void VersionChoice(){
   var e=Fixture("42.21.0");string root=Mod(e,"401","Versioned");Put(Path.Combine(root,"common/mod.info"),"id=BaseIdentity\n");Put(Path.Combine(root,"42.16/mod.info"),"id=OldIdentity\nversionMax=42.19\n");Put(Path.Combine(root,"42.20/mod.info"),"id=CurrentIdentity\n");Put(Path.Combine(root,"42.99/mod.info"),"id=FutureIdentity\n");
   var snapshot=Read(e,"CurrentIdentity","OldIdentity","FutureIdentity");Check(snapshot.Candidates.Count==1&&snapshot.Candidates[0].Id=="CurrentIdentity","highest compatible version wins within one root without resurrecting historical or future IDs");
   var legacy=Fixture("41.78.0");Put(Path.Combine(Mod(legacy,"402","Legacy"),"mod.info"),"id=LegacyIdentity\n");Check(Read(legacy,"LegacyIdentity").Candidates.Single().Id=="LegacyIdentity","B41 still supports its original root mod.info layout");
  }
  static string Hash(string file){using(var stream=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream));}
  static void InspectReal(string game,string user,string data,string saveMods){
   string before=Hash(saveMods);var ids=SaveProfiles.DefaultMods(File.ReadAllBytes(saveMods));Check(ids.Contains("AutoCook")&&ids.Contains("dustinguished_cleaning_wipes"),"actual selected save contains the two reported mod IDs");
   var e=new Engine(game,user,data,false);var snapshot=Read(e,ids);
   foreach(var expected in new[]{new[]{"AutoCook","3388721641"},new[]{"dustinguished_cleaning_wipes","3755185651"}}){var matches=snapshot.Candidates.Where(c=>c.Id==expected[0]).ToArray();Check(matches.Length==1&&matches[0].Workshop==expected[1],"actual save resolves "+expected[0]+" uniquely to Workshop "+expected[1]);Console.WriteLine("SOURCE "+matches[0].Id+" = "+matches[0].Root);}
   Check(Hash(saveMods)==before,"read-only real-save catalog check preserves the exact mods.txt bytes");
  }
  public static int Main(string[] args){try{LauncherRuntime.Initialize();if(args.Length==5&&args[0]=="--inspect-real")InspectReal(args[1],args[2],args[3],args[4]);else if(args.Length==0){int failed=0;foreach(Action test in new Action[]{CommonOnly,LegacyDuplicate,GenuineDuplicate,VersionChoice})try{test();}catch(Exception error){failed++;Console.WriteLine(error.Message);}if(failed>0)return 1;}else return 2;Console.WriteLine("MOD CATALOG LAYOUT TESTS PASSED: "+assertions+" assertions");return 0;}catch(Exception error){Console.WriteLine(error);return 1;}}
 }
}
