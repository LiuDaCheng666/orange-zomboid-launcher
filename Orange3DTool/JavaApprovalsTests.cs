using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class JavaApprovalsTests {
  static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  static void Check(bool b,string reason){if(!b)throw new Exception("Java approval: "+reason);}
  static Type Api(){var t=typeof(Engine).Assembly.GetType("PZ3D.JavaApprovals");Check(t!=null,"individual approval backend exists");return t;}
  static Dictionary<string,object> Read(object o){return Json.Deserialize<Dictionary<string,object>>(Json.Serialize(o));}
  static Dictionary<string,object> List(Engine e){return Read(Api().GetMethod("List").Invoke(null,new object[]{e}));}
  static object Save(Engine e,Dictionary<string,object> list,string hash,bool decision){return Api().GetMethod("Save").Invoke(null,new object[]{e,(string)list["stamp"],hash,decision});}
  static Dictionary<string,object>[] Rows(Dictionary<string,object> list){return ((IEnumerable)list["jars"]).Cast<Dictionary<string,object>>().ToArray();}
  static void Reject(Action a,string reason){bool rejected=false;try{a();}catch(TargetInvocationException){rejected=true;}Check(rejected,reason);}
  sealed class Fixture {
   public string Root,Home,Approval,Jar;public Engine E;
   public Fixture(){Root=Path.Combine("E:/AppCaches/OrangeLauncherProfiles/TestFixtures","java-"+Guid.NewGuid().ToString("N"));Home=Path.Combine(Root,"home");E=new Engine(Path.Combine(Root,"steamapps/common/ProjectZomboid"),Path.Combine(Root,"user"),Path.Combine(Root,"data"),true);Directory.CreateDirectory(E.Game);Directory.CreateDirectory(Path.GetDirectoryName(E.Mods));Directory.CreateDirectory(Path.Combine(Home,".zombie_buddy"));Approval=Path.Combine(Home,".zombie_buddy/mod_approvals.json");Config();File.WriteAllText(E.Mods,"VERSION = 1,\nmods\n{\n mod = ZombieBuddy,\n mod = Viewpoint,\n}\nmaps\n{\n}\n");string folder=Path.Combine(E.Workshop,"3809306528/mods/Viewpoint");Directory.CreateDirectory(Path.Combine(folder,"common"));Directory.CreateDirectory(Path.Combine(folder,"42/media/java/client"));File.WriteAllText(Path.Combine(folder,"common/mod.info"),"name=Fixture View\nid=Viewpoint\njavaJarFile=media/java/client/Viewpoint.jar\njavaPkgName=viewpoint\n");Jar=Path.Combine(folder,"42/media/java/client/Viewpoint.jar");File.WriteAllText(Jar,"abc",new UTF8Encoding(false));}
   public void Config(string extra=""){File.WriteAllText(E.Config,Json.Serialize(new{vmArgs=new[]{"-agentlib:zbNative","-Duser.home="+Home}.Concat(extra==""?new string[0]:new[]{extra}).ToArray()}));}
  }
  // These tests catch consent applied to wrong bytes, broad writes, stale approval,
  // ambiguous game resolution, and bypassed schema/process guards.
  public static int Main(){try{Run();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
  public static void Run(){
   var f=new Fixture();var l=List(f.E);Check((bool)l["supported"],"isolated effective Java home supported");var r=Rows(l);Check(r.Length==1&&r[0]["id"].Equals("Viewpoint")&&r[0]["workshopId"].Equals("3809306528"),"enabled managed jar discovered from common metadata and version binary");Check(r[0]["sha256"].Equals("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"),"full original bytes hashed in lowercase SHA256");Check(r[0]["decision"]==null&&!File.Exists(f.Approval),"listing never creates approval or implies consent");
   File.WriteAllText(f.Approval,"{\"formatVersion\":2,\"extension\":{\"retain\":true},\"mods\":[{\"id\":\"Other\",\"jar_hash\":\""+new string('1',64)+"\",\"decision\":false,\"author_id\":76561198043849998,\"extra\":\"keep\"}]}");string trust=Path.Combine(Path.GetDirectoryName(f.Approval),"config.json");File.WriteAllText(trust,"{\"trusted_authors\":[76561198043849998],\"opaque\":true}");string beforeTrust=File.ReadAllText(trust);l=List(f.E);string h=(string)Rows(l)[0]["sha256"];Save(f.E,l,h,true);var stored=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(f.Approval));var mods=((IEnumerable)stored["mods"]).Cast<Dictionary<string,object>>().ToArray();Check(mods.Length==2&&(bool)mods[1]["decision"]&&mods[1]["jar_hash"].Equals(h),"explicit one-hash approval persisted");Check(mods[0]["author_id"].ToString()=="76561198043849998"&&mods[0]["extra"].Equals("keep")&&stored.ContainsKey("extension"),"unknown fields and 64-bit IDs preserved");Check(File.ReadAllText(trust)==beforeTrust&&!mods[1].ContainsKey("author_id"),"author trust untouched and signer never invented");Check(Rows(List(f.E))[0]["decision"].Equals(true),"persisted decision shown");l=List(f.E);Save(f.E,l,h,false);Check(Rows(List(f.E))[0]["decision"].Equals(false),"individual denial updates chosen hash");
   l=List(f.E);string saved=File.ReadAllText(f.Approval);File.WriteAllText(f.Jar,"changed");Reject(()=>Save(f.E,l,h,true),"changed bytes reject stale consent");Check(File.ReadAllText(f.Approval)==saved,"stale file leaves decisions untouched");l=List(f.E);File.AppendAllText(f.Approval," ");Reject(()=>Save(f.E,l,(string)Rows(l)[0]["sha256"],true),"external approval change invalidates snapshot");
   foreach(string bad in new[]{"{}","{\"formatVersion\":3,\"mods\":[]}","{\"formatVersion\":2,\"mods\":[{\"jar_hash\":\"bad\",\"decision\":true}]}","{\"formatVersion\":2,\"mods\":[{\"jar_hash\":\""+new string('a',64)+"\",\"decision\":true},{\"jar_hash\":\""+new string('a',64)+"\",\"decision\":false}]}","not-json"}){var b=new Fixture();File.WriteAllText(b.Approval,bad);Check(!(bool)List(b.E)["supported"],"malformed/unknown/conflicting approvals refused");Check(File.ReadAllText(b.Approval)==bad,"read-only schema failure");}
   var pending=new Fixture();Directory.CreateDirectory(Path.Combine(pending.E.Data,"launch-sessions"));File.WriteAllText(Path.Combine(pending.E.Data,"launch-sessions",LaunchProfiles.Key(pending.E)+".json"),"{}");Check(!(bool)List(pending.E)["supported"],"pending profile switch prevents consent");
   var overrideF=new Fixture();overrideF.Config("-Duser.home="+Path.Combine(overrideF.Root,"another"));Check(!(bool)List(overrideF.E)["supported"],"conflicting Java homes refused");var agent=new Fixture();string dir=Path.Combine(agent.Root,"config");File.WriteAllText(agent.E.Config,Json.Serialize(new{vmArgs=new[]{"-agentlib:zbNative=config_dir="+dir}}));var al=List(agent.E);Save(agent.E,al,(string)Rows(al)[0]["sha256"],true);Check(File.Exists(Path.Combine(dir,"mod_approvals.json"))&&!File.Exists(agent.Approval),"official agent config_dir receives isolated decision");
   var shadow=new Fixture();string shadowDir=Path.Combine(shadow.E.User,"mods/Shadow/common");Directory.CreateDirectory(shadowDir);File.WriteAllText(Path.Combine(shadowDir,"mod.info"),"id=Viewpoint\n");Check(!(bool)List(shadow.E)["supported"],"local duplicate identity refuses unknown game override");
   var escape=new Fixture();string info=Path.Combine(escape.E.Workshop,"3809306528/mods/Viewpoint/common/mod.info");File.WriteAllText(info,"id=Viewpoint\njavaJarFile=../../../../escape.jar\njavaPkgName=viewpoint\n");Check(!(bool)List(escape.E)["supported"],"escaped Java path refused");
   var inactive=new Fixture();File.WriteAllText(inactive.E.Mods,"mods\n{\n mod = Other,\n}\n");Check(Rows(List(inactive.E)).Length==0,"inactive managed component excluded");
   var mixed=new Fixture();File.WriteAllText(Path.Combine(mixed.E.Workshop,"3809306528/mods/Viewpoint/42/mod.info"),"id=OtherMod\njavaJarFile=media/java/client/Viewpoint.jar\njavaPkgName=other\n");Check(!(bool)List(mixed.E)["supported"],"conflicting version/common identity never authorizes another mod binary");
   var unknownHome=new Fixture();File.WriteAllText(unknownHome.E.Config,"{\"vmArgs\":[\"-agentlib:zbNative\"]}");Check(!(bool)List(unknownHome.E)["supported"],"default Java home is never guessed when bundled Java is absent");
   var external=Api().GetMethod("HasExternalOverrides",BindingFlags.Static|BindingFlags.NonPublic);Check(external!=null,"external Java directory overrides recognized");Check((bool)external.Invoke(null,new object[]{"\"LaunchOptions\" \"-Duser.home=E:\\\\other\""}),"Steam user.home override cannot silently approve wrong store");Check(!(bool)external.Invoke(null,new object[]{"\"LaunchOptions\" \"-nosteam\""}),"ordinary Steam launch options need no directory refusal");
   Console.WriteLine("JAVA APPROVAL TESTS PASSED; isolated fixtures retained under E:/AppCaches/OrangeLauncherProfiles/TestFixtures");
  }
 }
}
