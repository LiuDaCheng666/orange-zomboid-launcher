using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
namespace PZ3D {
 static class SteamSearchFilters {
  static int passed;
  static void Check(bool value,string message){if(!value)throw new Exception(message);passed++;}
  static void Reject(Action run,string message){try{run();}catch{passed++;return;}throw new Exception(message);}
  static Dictionary<string,object> Args(){return new Dictionary<string,object>{{"query"," 汽车 "},{"page",2},{"cursor","next-2"},{"version","b42"},{"sort","updated"},{"category","Vehicles"},{"password","never-forward"}};}
  static int Main(){try{Run();return 0;}catch(Exception error){Console.Error.WriteLine("FAIL "+error.Message);return 1;}}
  static void Run(){
   var clean=SteamLauncherServices.Normalize("search",Args());
   Check(clean.ContainsKey("version")&&Convert.ToString(clean["version"])=="b42","version filter must survive worker normalization");
   Check(Convert.ToString(clean["sort"])=="updated"&&Convert.ToString(clean["category"])=="Vehicles","sort and category survive normalization");
   Check(Convert.ToString(clean["query"])=="汽车"&&Convert.ToString(clean["cursor"])=="next-2"&&!clean.ContainsKey("password"),"query/cursor preserved without credentials");
   foreach(var item in new[]{new[]{"version","B43"},new[]{"sort","random"},new[]{"category","Invented"}}){var bad=Args();bad[item[0]]=item[1];Reject(()=>SteamLauncherServices.Normalize("search",bad),"invalid filter accepted: "+item[0]);}
   var wrong=Args();wrong["version"]=42;Reject(()=>SteamLauncherServices.Normalize("search",wrong),"non-text version accepted");
   var configure=typeof(SteamLauncherServices).GetMethod("ConfigureSearch",BindingFlags.Static|BindingFlags.NonPublic);
   Check(configure!=null,"native query configuration must apply real required tags");
   var calls=new List<string>();Func<string,bool> text=value=>{calls.Add("text:"+value);return true;};Func<string,bool> tag=value=>{calls.Add("tag:"+value);return true;};
   configure.Invoke(null,new object[]{clean,text,tag});
   Check(calls.SequenceEqual(new[]{"text:汽车","tag:Build 42","tag:Vehicles"}),"native-boundary search must combine unmodified query and both required tags");
   calls.Clear();var browse=SteamLauncherServices.Normalize("search",new Dictionary<string,object>{{"version","all"},{"sort","popular"},{"category","all"}});configure.Invoke(null,new object[]{browse,text,tag});Check(calls.Count==0,"cleared browse must remove search text and required tags");
   calls.Clear();var b41=SteamLauncherServices.Normalize("search",new Dictionary<string,object>{{"version","b41"},{"category","Map"}});configure.Invoke(null,new object[]{b41,text,tag});Check(calls.SequenceEqual(new[]{"tag:Build 41","tag:Map"}),"B41 maps to the official version tag without a search-text suffix");
   calls.Clear();Func<string,bool> rejected=value=>{calls.Add(value);return false;};Reject(()=>configure.Invoke(null,new object[]{clean,rejected,tag}),"rejected Steam text setter must fail");Check(calls.SequenceEqual(new[]{"汽车"}),"failed setter must stop further configuration");
   Reject(()=>configure.Invoke(null,new object[]{clean,text,rejected}),"rejected Steam required tag must fail instead of returning unfiltered results");
   var order=typeof(SteamLauncherServices).GetMethod("SearchOrder",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(string),typeof(string)},null);Check(order!=null,"explicit sort must reach native query creation");
   Check((int)order.Invoke(null,new object[]{"汽车","popular"})==0&&(int)order.Invoke(null,new object[]{"汽车","newest"})==1&&(int)order.Invoke(null,new object[]{"汽车","updated"})==19,"Steam documented order enums");
   Check(SteamLauncherServices.SearchOrder("cars")==11,"legacy callers retain relevance ordering");
   var json=new JavaScriptSerializer();Func<Dictionary<string,object>,string> request=p=>json.Serialize(new{id="filters",action="steamSearch",payload=p});ModernHostPolicy.Validate("https://orange.local/index.html",request(Args()));passed++;
   var invalid=Args();invalid["category"]="Invented";Reject(()=>ModernHostPolicy.Validate("https://orange.local/index.html",request(invalid)),"host must reject unsupported tags before native worker");
   var parse=typeof(ComponentEvidence).GetMethod("ReadPublishedDetails",BindingFlags.Static|BindingFlags.NonPublic);Check(parse!=null,"public Workshop metadata must retain validated preview_url before installation");var manifests=new Dictionary<ulong,string>();var previews=new Dictionary<ulong,string>();
   string details="{\"response\":{\"publishedfiledetails\":[{\"publishedfileid\":\"3815931973\",\"result\":1,\"consumer_app_id\":108600,\"hcontent_file\":\"22\",\"preview_url\":\"https://images.steamusercontent.com/ugc/fixture/poster.jpg\"},{\"publishedfileid\":\"3813908957\",\"result\":1,\"consumer_app_id\":108600,\"preview_url\":\"https://attacker.invalid/poster.jpg\"},{\"publishedfileid\":\"1\",\"result\":1,\"consumer_app_id\":108600,\"preview_url\":\"https://images.steamusercontent.com/ugc/unrequested.jpg\"}]}}";
   parse.Invoke(null,new object[]{details,new ulong[]{3815931973,3813908957},manifests,previews});Check(manifests[3815931973]=="22"&&previews[3815931973]=="https://images.steamusercontent.com/ugc/fixture/poster.jpg","official requested preview is returned independently of local installation");Check(previews.Count==1,"unrequested IDs and unsupported image hosts never become quick covers");
   Console.WriteLine("PASS "+passed+" Steam filter/preview checks (production normalization/query configuration/host validation/metadata parser; no live Steam SDK query)");
  }
 }
}
